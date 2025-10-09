using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using TMPro;

namespace FadedDreams.Story
{
    /// <summary>
    /// STORY0 片头演出控制器 - 2D版本
    /// 控制12秒的完整演出序列
    /// </summary>
    public class Story0Director2D : MonoBehaviour
    {
        [Header("游戏对象引用")]
        [Tooltip("下落的正方形 (2D Sprite)")]
        public Transform fallingSquare;
        
        [Tooltip("主相机")]
        public Camera mainCamera;
        
        [Tooltip("特效预制体（手动拖拽赋值）")]
        public GameObject effectPrefab;
        
        [Tooltip("背景SpriteRenderer")]
        public SpriteRenderer backgroundSprite;
        
        [Header("UI引用")]
        [Tooltip("作品名文字")]
        public CanvasGroup titleGroup;
        public TextMeshProUGUI titleText;
        
        [Tooltip("作者信息文字")]
        public CanvasGroup authorGroup;
        public TextMeshProUGUI authorText;
        
        [Tooltip("黑屏用的Image")]
        public CanvasGroup fadeScreen;
        
        [Header("正方形下落设置")]
        [Tooltip("初始位置")]
        public Vector2 squareStartPosition = new Vector2(0, 5);
        
        [Tooltip("下落方向（单位向量）")]
        public Vector2 fallDirection = new Vector2(0.5f, -1f);
        
        [Tooltip("初始速度")]
        public float initialSpeed = 1f;
        
        [Tooltip("加速度")]
        public float acceleration = 2f;
        
        [Tooltip("第2秒开始的抖动强度")]
        public float shakeIntensity = 0.1f;
        
        [Header("相机设置")]
        [Tooltip("相机跟随偏移")]
        public Vector2 cameraOffset = new Vector2(0, 2);
        
        [Tooltip("第6秒开始相机后拉距离（改变正交大小）")]
        public float cameraZoomOutAmount = 3f;
        
        [Tooltip("相机后拉速度")]
        public float cameraZoomSpeed = 2f;
        
        [Header("背景渐变设置")]
        [Tooltip("渐变材质")]
        public Material gradientMaterial;
        
        [Tooltip("背景初始颜色")]
        public Color backgroundStartColor = new Color(0.1f, 0.1f, 0.15f, 1f);
        
        [Tooltip("背景目标颜色（左下白色）")]
        public Color backgroundEndColor = Color.white;
        
        [Header("时间控制")]
        public bool autoStart = true;
        
        // 内部状态
        private float currentSpeed;
        private Vector2 squareBasePosition;
        private GameObject activeEffect;
        private bool isShaking = false;
        private float elapsedTime = 0f;
        private float initialCameraSize;
        
        void Start()
        {
            // 初始化
            if (fallingSquare != null)
            {
                fallingSquare.position = new Vector3(squareStartPosition.x, squareStartPosition.y, 0);
                squareBasePosition = squareStartPosition;
            }
            
            currentSpeed = initialSpeed;
            fallDirection = fallDirection.normalized;
            
            // 保存相机初始大小
            if (mainCamera != null)
            {
                initialCameraSize = mainCamera.orthographicSize;
            }
            
            // 隐藏UI元素
            if (titleGroup != null) titleGroup.alpha = 0;
            if (authorGroup != null) authorGroup.alpha = 0;
            if (fadeScreen != null) fadeScreen.alpha = 0;
            
            // 隐藏背景
            if (backgroundSprite != null)
            {
                backgroundSprite.enabled = false;
            }
            
            if (autoStart)
            {
                StartCoroutine(PlayOpeningSequence());
            }
        }
        
        void Update()
        {
            elapsedTime += Time.deltaTime;
            
            // 0-10秒：正方形下落
            if (elapsedTime < 10f && fallingSquare != null)
            {
                // 更新速度（加速）
                currentSpeed += acceleration * Time.deltaTime;
                
                // 更新基础位置
                squareBasePosition += fallDirection * currentSpeed * Time.deltaTime;
                
                // 应用抖动效果（2秒后）
                Vector2 finalPosition = squareBasePosition;
                if (isShaking)
                {
                    float shakeAmount = shakeIntensity * Mathf.Clamp01((elapsedTime - 2f) / 2f);
                    finalPosition += (Vector2)Random.insideUnitCircle * shakeAmount;
                }
                
                fallingSquare.position = new Vector3(finalPosition.x, finalPosition.y, 0);
            }
            
            // 0-6秒：相机跟随正方形
            if (elapsedTime < 6f && fallingSquare != null && mainCamera != null)
            {
                Vector2 squarePos = fallingSquare.position;
                Vector3 targetCameraPos = new Vector3(
                    squarePos.x + cameraOffset.x,
                    squarePos.y + cameraOffset.y,
                    mainCamera.transform.position.z
                );
                mainCamera.transform.position = Vector3.Lerp(
                    mainCamera.transform.position,
                    targetCameraPos,
                    Time.deltaTime * 5f
                );
            }
            // 6-10秒：相机往后拉（增大orthographicSize）
            else if (elapsedTime >= 6f && elapsedTime < 10f && mainCamera != null)
            {
                float targetSize = initialCameraSize + cameraZoomOutAmount;
                mainCamera.orthographicSize = Mathf.Lerp(
                    mainCamera.orthographicSize,
                    targetSize,
                    Time.deltaTime * cameraZoomSpeed
                );
            }
        }
        
        IEnumerator PlayOpeningSequence()
        {
            // 0-2秒：正方形开始下落，相机跟随
            yield return new WaitForSeconds(2f);
            
            // 第2秒：激活特效和抖动
            if (effectPrefab != null && fallingSquare != null)
            {
                activeEffect = Instantiate(effectPrefab, fallingSquare.position, Quaternion.identity, fallingSquare);
            }
            isShaking = true;
            
            // 等待到第4秒
            yield return new WaitForSeconds(2f);
            
            // 第4秒：显示作品名和作者信息
            if (titleText != null)
            {
                titleText.text = "Radiant Geometry";
                StartCoroutine(FadeInCanvasGroup(titleGroup, 1f));
            }
            
            if (authorText != null)
            {
                authorText.text = "EnishiEuko";
                StartCoroutine(FadeInCanvasGroup(authorGroup, 1f));
            }
            
            // 等待到第6秒
            yield return new WaitForSeconds(2f);
            
            // 第6秒：相机开始往后拉，激活背景渐变
            if (backgroundSprite != null)
            {
                if (gradientMaterial != null)
                {
                    backgroundSprite.material = gradientMaterial;
                }
                backgroundSprite.enabled = true;
                backgroundSprite.color = backgroundStartColor;
                
                // 背景渐变动画
                StartCoroutine(FadeBackground());
            }
            
            // 等待到第8秒
            yield return new WaitForSeconds(2f);
            
            // 第8秒：文字淡出
            if (titleGroup != null)
            {
                StartCoroutine(FadeOutCanvasGroup(titleGroup, 1f));
            }
            if (authorGroup != null)
            {
                StartCoroutine(FadeOutCanvasGroup(authorGroup, 1f));
            }
            
            // 等待到第10秒
            yield return new WaitForSeconds(2f);
            
            // 第10秒：黑屏
            if (fadeScreen != null)
            {
                yield return StartCoroutine(FadeInCanvasGroup(fadeScreen, 1f));
            }
            
            // 等待2秒
            yield return new WaitForSeconds(2f);
            
            // 第12秒：跳转到Chapter1
            SceneManager.LoadScene("Chapter1");
        }
        
        IEnumerator FadeBackground()
        {
            if (backgroundSprite == null) yield break;
            
            float elapsed = 0f;
            float duration = 2f;
            
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                backgroundSprite.color = Color.Lerp(backgroundStartColor, backgroundEndColor, t);
                yield return null;
            }
            
            backgroundSprite.color = backgroundEndColor;
        }
        
        IEnumerator FadeInCanvasGroup(CanvasGroup group, float duration)
        {
            if (group == null) yield break;
            
            float elapsed = 0f;
            float startAlpha = group.alpha;
            
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                group.alpha = Mathf.Lerp(startAlpha, 1f, elapsed / duration);
                yield return null;
            }
            
            group.alpha = 1f;
        }
        
        IEnumerator FadeOutCanvasGroup(CanvasGroup group, float duration)
        {
            if (group == null) yield break;
            
            float elapsed = 0f;
            float startAlpha = group.alpha;
            
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                group.alpha = Mathf.Lerp(startAlpha, 0f, elapsed / duration);
                yield return null;
            }
            
            group.alpha = 0f;
        }
    }
}

