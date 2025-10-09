using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using TMPro;

namespace FadedDreams.Story
{
    /// <summary>
    /// STORY0 片头演出控制器 - 2D版本
    /// 12秒完整演出序列
    /// </summary>
    public class Story0Director : MonoBehaviour
    {
        [Header("=== 游戏对象引用 ===")]
        [Tooltip("下落的正方形Sprite")]
        public Transform fallingSquare;
        
        [Tooltip("主相机")]
        public Camera mainCamera;
        
        [Tooltip("背景SpriteRenderer")]
        public SpriteRenderer background;
        
        [Tooltip("特效预制体（第2秒激活）")]
        public GameObject effectPrefab;
        
        [Header("=== UI引用 ===")]
        public CanvasGroup titleGroup;
        public TextMeshProUGUI titleText;
        public CanvasGroup authorGroup;
        public TextMeshProUGUI authorText;
        public CanvasGroup fadeScreen;
        
        [Header("=== 正方形设置 ===")]
        public Vector2 startPosition = new Vector2(0, 5);
        public Vector2 fallDirection = new Vector2(0.5f, -1f);
        public float initialSpeed = 2f;
        public float acceleration = 5f;
        public float shakeIntensity = 0.15f;
        
        [Header("=== 相机设置 ===")]
        public Vector2 cameraOffset = new Vector2(0, 2);
        public float cameraZoomStart = 5f;
        public float cameraZoomEnd = 8f;
        public float cameraZoomSpeed = 2f;
        
        [Header("=== 背景设置 ===")]
        [Tooltip("背景材质（左下方白色渐变效果）")]
        public Material backgroundGradientMaterial;
        
        // 内部变量
        private float currentSpeed;
        private Vector2 squarePos;
        private bool isShaking;
        private float time;
        private GameObject effect;
        
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
            if (fadeScreen) fadeScreen.alpha = 0;
            
            // 隐藏背景
            if (background) background.enabled = false;
            
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
            
            // 0-10秒：正方形下落
            if (time < 10f && fallingSquare)
            {
                // 加速下落
                currentSpeed += acceleration * Time.deltaTime;
                squarePos += fallDirection * currentSpeed * Time.deltaTime;
                
                // 应用抖动（2秒后）
                Vector2 finalPos = squarePos;
                if (isShaking)
                {
                    float shake = shakeIntensity * Mathf.Clamp01((time - 2f) / 2f);
                    finalPos += (Vector2)Random.insideUnitCircle * shake;
                }
                
                fallingSquare.position = finalPos;
            }
            
            // 相机一直跟随正方形，并在6秒后开始后拉
            if (mainCamera && fallingSquare)
            {
                // 一直跟随正方形
                Vector3 target = (Vector2)fallingSquare.position + cameraOffset;
                target.z = -10;
                mainCamera.transform.position = Vector3.Lerp(
                    mainCamera.transform.position, target, Time.deltaTime * 5f);
                
                // 6秒后相机开始后拉
                if (time >= 6f && time < 10f)
                {
                    mainCamera.orthographicSize = Mathf.Lerp(
                        mainCamera.orthographicSize, cameraZoomEnd, Time.deltaTime * cameraZoomSpeed);
                }
            }
        }
        
        IEnumerator PlaySequence()
        {
            // 0-2秒：下落开始
            yield return new WaitForSeconds(2f);
            
            // 2秒：特效和抖动
            if (effectPrefab && fallingSquare)
                effect = Instantiate(effectPrefab, fallingSquare.position, Quaternion.identity, fallingSquare);
            isShaking = true;
            
            // 4秒：显示文字
            yield return new WaitForSeconds(2f);
            if (titleText) titleText.text = "Radiant Geometry";
            if (authorText) authorText.text = "EnishiEuko";
            StartCoroutine(Fade(titleGroup, 0, 1, 1f));
            StartCoroutine(Fade(authorGroup, 0, 1, 1f));
            
            // 6秒：背景激活（显示渐变背景）
            yield return new WaitForSeconds(2f);
            if (background)
            {
                // 如果有渐变材质就使用，否则用纯白色
                if (backgroundGradientMaterial != null)
                {
                    background.material = backgroundGradientMaterial;
                }
                background.color = Color.white;
                background.enabled = true;
            }
            
            // 8秒：文字淡出
            yield return new WaitForSeconds(2f);
            StartCoroutine(Fade(titleGroup, 1, 0, 1f));
            StartCoroutine(Fade(authorGroup, 1, 0, 1f));
            
            // 10秒：黑屏
            yield return new WaitForSeconds(2f);
            yield return StartCoroutine(Fade(fadeScreen, 0, 1, 1f));
            
            // 12秒：跳转
            yield return new WaitForSeconds(2f);
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

