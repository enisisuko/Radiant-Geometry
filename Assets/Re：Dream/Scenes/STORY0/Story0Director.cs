using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using TMPro;

namespace FadedDreams.Story
{
    /// <summary>
    /// STORY0 片头演出控制器 - 2D版本
    /// 14秒完整演出序列
    /// </summary>
    public class Story0Director : MonoBehaviour
    {
        [Header("=== 游戏对象引用 ===")]
        [Tooltip("下落的正方形Sprite")]
        public Transform fallingSquare;
        
        [Tooltip("主相机")]
        public Camera mainCamera;
        
        [Tooltip("第一特效预制体（0秒激活）")]
        public GameObject firstEffectPrefab;
        
        [Tooltip("第二特效预制体（7秒激活）")]
        public GameObject secondEffectPrefab;
        
        [Tooltip("坠地爆炸特效预制体（11秒激活）")]
        public GameObject explosionEffectPrefab;
        
        [Header("=== UI引用 ===")]
        public CanvasGroup titleGroup;
        public TextMeshProUGUI titleText;
        public CanvasGroup authorGroup;
        public TextMeshProUGUI authorText;
        public CanvasGroup fadeScreen;
        
        [Header("=== 正方形设置 ===")]
        public Vector2 startPosition = new Vector2(0, 100);
        public Vector2 fallDirection = new Vector2(0.5f, -1f);
        public float initialSpeed = 20f;
        public float acceleration = 10f;
        public float shakeIntensity = 0.2f;
        
        [Header("=== 坠地效果 ===")]
        [Tooltip("坠地时间点")]
        public float landingTime = 11f;
        [Tooltip("提前生成地面的时间")]
        public float groundSpawnTime = 10f;
        [Tooltip("地面Sprite")]
        public Sprite groundSprite;
        [Tooltip("地面颜色")]
        public Color groundColor = Color.white;
        [Tooltip("地面大小")]
        public float groundSize = 100f;
        
        [Header("=== 相机设置 ===")]
        public Vector2 cameraOffset = new Vector2(0, 2);
        [Tooltip("初始相机距离（更远）")]
        public float cameraZoomStart = 12f;
        [Tooltip("最终相机距离")]
        public float cameraZoomEnd = 18f;
        [Tooltip("相机拉远开始时间")]
        public float cameraZoomStartTime = 5f;
        [Tooltip("相机拉远结束时间")]
        public float cameraZoomEndTime = 8f;
        
        [Header("=== 开场设置 ===")]
        [Tooltip("开场黑幕渐显时长")]
        public float openingFadeDuration = 4f;
        
        [Header("=== 加速阶段 ===")]
        [Tooltip("第7秒提升后的加速度")]
        public float boostAcceleration = 20f;
        
        // 内部变量
        private float currentSpeed;
        private Vector2 squarePos;
        private bool isShaking;
        private float time;
        private GameObject firstEffect;
        private GameObject secondEffect;
        private GameObject explosionEffect;
        private GameObject ground;
        private bool hasLanded = false;
        private float currentAcceleration;
        private bool groundSpawned = false;
        
        void Start()
        {
            // 初始化
            if (fallingSquare) fallingSquare.position = startPosition;
            squarePos = startPosition;
            currentSpeed = initialSpeed;
            currentAcceleration = acceleration;
            fallDirection.Normalize();
            
            // 隐藏UI
            if (titleGroup) titleGroup.alpha = 0;
            if (authorGroup) authorGroup.alpha = 0;
            
            // 开场黑幕铺满屏幕
            if (fadeScreen) fadeScreen.alpha = 1;
            
            // 设置相机
            if (mainCamera)
            {
                mainCamera.orthographic = true;
                mainCamera.orthographicSize = cameraZoomStart;
            }
            
            // 0秒立即激活第一特效
            if (firstEffectPrefab && fallingSquare)
            {
                firstEffect = Instantiate(firstEffectPrefab, fallingSquare.position, Quaternion.identity, fallingSquare);
                Debug.Log("✨ 第一特效激活（0秒）");
            }
            
            // 0秒开始震颤
            isShaking = true;
            
            StartCoroutine(PlaySequence());
        }
        
        void Update()
        {
            time += Time.deltaTime;
            
            // 0-11秒：正方形下落（从0秒就开始！）
            if (time < landingTime && fallingSquare && !hasLanded)
            {
                // 先用当前速度移动（保证第一帧就有初速度20）
                squarePos += fallDirection * currentSpeed * Time.deltaTime;
                
                // 然后加速（下一帧速度会更快）
                currentSpeed += currentAcceleration * Time.deltaTime;
                
                // 应用抖动（0-8秒逐渐加强，8秒后逐渐减弱）
                Vector2 finalPos = squarePos;
                if (isShaking && !hasLanded)
                {
                    float shake;
                    if (time <= 8f)
                    {
                        // 0-8秒逐渐增强
                        shake = shakeIntensity * Mathf.Clamp01(time / 8f);
                    }
                    else
                    {
                        // 8-11秒逐渐减弱
                        shake = shakeIntensity * Mathf.Clamp01((landingTime - time) / (landingTime - 8f));
                    }
                    finalPos += (Vector2)Random.insideUnitCircle * shake;
                }
                
                fallingSquare.position = finalPos;
            }
            
            // 10秒：提前生成地面
            if (time >= groundSpawnTime && !groundSpawned)
            {
                CreateGround();
                groundSpawned = true;
            }
            
            // 11秒：撞地时刻
            if (time >= landingTime && !hasLanded)
            {
                hasLanded = true;
                currentSpeed = 0;  // 速度归零
                OnLanding();
            }
            
            // 相机一直跟随正方形
            if (mainCamera && fallingSquare)
            {
                // 一直跟随正方形
                Vector3 target = (Vector2)fallingSquare.position + cameraOffset;
                target.z = -10;
                mainCamera.transform.position = Vector3.Lerp(
                    mainCamera.transform.position, target, Time.deltaTime * 5f);
                
                // 5-8秒：相机缓慢拉远
                if (time >= cameraZoomStartTime && time <= cameraZoomEndTime)
                {
                    float t = (time - cameraZoomStartTime) / (cameraZoomEndTime - cameraZoomStartTime);
                    mainCamera.orthographicSize = Mathf.Lerp(cameraZoomStart, cameraZoomEnd, t);
                }
                else if (time > cameraZoomEndTime)
                {
                    mainCamera.orthographicSize = cameraZoomEnd;
                }
            }
        }
        
        void OnLanding()
        {
            // 坠地时触发爆炸特效（在正方形当前位置）
            if (explosionEffectPrefab && fallingSquare)
            {
                explosionEffect = Instantiate(explosionEffectPrefab, 
                    fallingSquare.position, 
                    Quaternion.identity);
                Debug.Log("💥 坠地爆炸！");
            }
            
            // 开始黑幕渐隐（11秒开始，12秒完成）
            StartCoroutine(Fade(fadeScreen, 0, 1, 1f));
        }
        
        void CreateGround()
        {
            // 在正方形当前位置下方生成超大地面
            ground = new GameObject("Ground");
            ground.transform.position = new Vector3(squarePos.x, squarePos.y - 1f, 1);
            ground.transform.localScale = new Vector3(groundSize, 2, 1);  // 使用可调节的大小
            
            var sr = ground.AddComponent<SpriteRenderer>();
            sr.sprite = groundSprite;
            sr.color = groundColor;
            sr.sortingOrder = -5;
            
            Debug.Log($"🏔️ 地面生成！位置：({squarePos.x}, {squarePos.y - 1})，大小：{groundSize}");
        }
        
        IEnumerator PlaySequence()
        {
            // 0-4秒：黑幕渐显（同时0秒就开始下落和特效）
            StartCoroutine(Fade(fadeScreen, 1, 0, 4f));
            
            // 5秒：文字渐显
            yield return new WaitForSeconds(5f);
            if (titleText) titleText.text = "Radiant Geometry";
            if (authorText) authorText.text = "EnishiEuko";
            StartCoroutine(Fade(titleGroup, 0, 1, 1f));
            StartCoroutine(Fade(authorGroup, 0, 1, 1f));
            
            // 7秒：第二特效，加速度提升
            yield return new WaitForSeconds(2f);
            if (secondEffectPrefab && fallingSquare)
            {
                secondEffect = Instantiate(secondEffectPrefab, fallingSquare.position, Quaternion.identity, fallingSquare);
                Debug.Log("⚡ 第二特效激活！加速度提升！");
            }
            currentAcceleration = boostAcceleration;  // 提升到20
            
            // 9秒：文字渐隐
            yield return new WaitForSeconds(2f);
            StartCoroutine(Fade(titleGroup, 1, 0, 1f));
            StartCoroutine(Fade(authorGroup, 1, 0, 1f));
            
            // 11秒：等待撞地（在Update的OnLanding中触发爆炸和黑幕）
            yield return new WaitForSeconds(2f);
            
            // 12秒：确保完全黑屏后切换
            yield return new WaitForSeconds(1f);
            SceneManager.LoadScene("Chapter1");
        }
        
        IEnumerator Fade(CanvasGroup group, float from, float to, float duration)
        {
            if (!group) yield break;
            float t = 0;
            while (t < duration)
            {
                t += Time.deltaTime;
                group.alpha = Mathf.Lerp(from, to, t / duration);
                yield return null;
            }
            group.alpha = to;
        }
        
    }
}

