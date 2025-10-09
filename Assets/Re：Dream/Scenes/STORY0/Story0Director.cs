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
        
        [Tooltip("抖动特效预制体（第4秒激活）")]
        public GameObject shakeEffectPrefab;
        
        [Tooltip("坠地爆炸特效预制体（第11秒激活）")]
        public GameObject explosionEffectPrefab;
        
        [Header("=== UI引用 ===")]
        public CanvasGroup titleGroup;
        public TextMeshProUGUI titleText;
        public CanvasGroup authorGroup;
        public TextMeshProUGUI authorText;
        public CanvasGroup fadeScreen;
        
        [Header("=== 正方形设置 ===")]
        public Vector2 startPosition = new Vector2(0, 8);
        public Vector2 fallDirection = new Vector2(0.5f, -1f);
        public float initialSpeed = 20f;
        public float acceleration = 10f;
        public float shakeIntensity = 0.2f;
        
        [Header("=== 坠地效果 ===")]
        [Tooltip("地面高度")]
        public float groundHeight = -10f;
        [Tooltip("地面Sprite")]
        public Sprite groundSprite;
        [Tooltip("地面颜色")]
        public Color groundColor = Color.white;
        
        [Header("=== 相机设置 ===")]
        public Vector2 cameraOffset = new Vector2(0, 2);
        public float cameraZoomStart = 5f;
        public float cameraZoomEnd = 8f;
        public float cameraZoomSpeed = 2f;
        
        [Header("=== 开场设置 ===")]
        [Tooltip("开场黑幕渐显时长")]
        public float openingFadeDuration = 2f;
        
        // 内部变量
        private float currentSpeed;
        private Vector2 squarePos;
        private bool isShaking;
        private float time;
        private GameObject shakeEffect;
        private GameObject explosionEffect;
        private GameObject ground;
        private bool hasLanded = false;
        
        void Start()
        {
            // 初始化
            if (fallingSquare) fallingSquare.position = startPosition;
            squarePos = startPosition;
            currentSpeed = initialSpeed;
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
            
            StartCoroutine(PlaySequence());
        }
        
        void Update()
        {
            time += Time.deltaTime;
            
            // 2-11秒：正方形下落（开场2秒黑幕后开始）
            if (time >= 2f && time < 11f && fallingSquare && !hasLanded)
            {
                // 加速下落
                currentSpeed += acceleration * Time.deltaTime;
                squarePos += fallDirection * currentSpeed * Time.deltaTime;
                
                // 检测是否到达地面
                if (squarePos.y <= groundHeight)
                {
                    squarePos.y = groundHeight;
                    hasLanded = true;
                    OnLanding();
                }
                
                // 应用抖动（4秒后）
                Vector2 finalPos = squarePos;
                if (isShaking && !hasLanded)
                {
                    float shake = shakeIntensity * Mathf.Clamp01((time - 4f) / 2f);
                    finalPos += (Vector2)Random.insideUnitCircle * shake;
                }
                
                fallingSquare.position = finalPos;
            }
            
            // 相机一直跟随正方形，8秒后开始后拉
            if (mainCamera && fallingSquare)
            {
                // 一直跟随正方形
                Vector3 target = (Vector2)fallingSquare.position + cameraOffset;
                target.z = -10;
                mainCamera.transform.position = Vector3.Lerp(
                    mainCamera.transform.position, target, Time.deltaTime * 5f);
                
                // 8秒后相机开始后拉
                if (time >= 8f && time < 12f)
                {
                    mainCamera.orthographicSize = Mathf.Lerp(
                        mainCamera.orthographicSize, cameraZoomEnd, Time.deltaTime * cameraZoomSpeed);
                }
            }
        }
        
        void OnLanding()
        {
            // 坠地时触发爆炸特效
            if (explosionEffectPrefab && fallingSquare)
            {
                explosionEffect = Instantiate(explosionEffectPrefab, 
                    new Vector3(squarePos.x, groundHeight, 0), 
                    Quaternion.identity);
                Debug.Log("💥 坠地爆炸！");
            }
            
            // 生成地面
            CreateGround();
        }
        
        void CreateGround()
        {
            ground = new GameObject("Ground");
            ground.transform.position = new Vector3(0, groundHeight - 0.5f, 1);
            ground.transform.localScale = new Vector3(30, 1, 1);
            
            var sr = ground.AddComponent<SpriteRenderer>();
            sr.sprite = groundSprite;
            sr.color = groundColor;
            sr.sortingOrder = -5;
            
            Debug.Log("🏔️ 地面生成！");
        }
        
        IEnumerator PlaySequence()
        {
            // 0-2秒：开场黑幕渐显
            yield return StartCoroutine(Fade(fadeScreen, 1, 0, openingFadeDuration));
            
            // 2-4秒：正方形快速下落（在Update中处理）
            yield return new WaitForSeconds(2f);
            
            // 4秒：抖动特效激活
            if (shakeEffectPrefab && fallingSquare)
            {
                shakeEffect = Instantiate(shakeEffectPrefab, fallingSquare.position, Quaternion.identity, fallingSquare);
                Debug.Log("✨ 抖动特效激活！");
            }
            isShaking = true;
            
            // 6秒：显示文字
            yield return new WaitForSeconds(2f);
            if (titleText) titleText.text = "Radiant Geometry";
            if (authorText) authorText.text = "EnishiEuko";
            StartCoroutine(Fade(titleGroup, 0, 1, 1f));
            StartCoroutine(Fade(authorGroup, 0, 1, 1f));
            
            // 8秒：相机开始后拉（在Update中处理）
            yield return new WaitForSeconds(2f);
            
            // 10秒：文字淡出
            yield return new WaitForSeconds(2f);
            StartCoroutine(Fade(titleGroup, 1, 0, 1f));
            StartCoroutine(Fade(authorGroup, 1, 0, 1f));
            
            // 11秒：等待坠地（坠地效果在Update中的OnLanding触发）
            // 等待到确保坠地发生
            yield return new WaitForSeconds(1f);
            
            // 12秒：黑屏
            yield return new WaitForSeconds(1f);
            yield return StartCoroutine(Fade(fadeScreen, 0, 1, 1f));
            
            // 14秒：跳转
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

