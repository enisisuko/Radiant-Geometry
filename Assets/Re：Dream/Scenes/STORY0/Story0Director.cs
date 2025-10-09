using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using TMPro;

namespace FadedDreams.Story
{
    /// <summary>
    /// STORY0 片头演出控制器
    /// 控制12秒的完整演出序列
    /// </summary>
    public class Story0Director : MonoBehaviour
    {
        [Header("游戏对象引用")]
        [Tooltip("下落的正方体")]
        public Transform fallingCube;
        
        [Tooltip("主相机")]
        public Camera mainCamera;
        
        [Tooltip("特效预制体（手动拖拽赋值）")]
        public GameObject effectPrefab;
        
        [Tooltip("背景平面")]
        public Renderer backgroundPlane;
        
        [Header("UI引用")]
        [Tooltip("作品名文字")]
        public CanvasGroup titleGroup;
        public TextMeshProUGUI titleText;
        
        [Tooltip("作者信息文字")]
        public CanvasGroup authorGroup;
        public TextMeshProUGUI authorText;
        
        [Tooltip("黑屏用的Image")]
        public CanvasGroup fadeScreen;
        
        [Header("正方体下落设置")]
        [Tooltip("初始位置")]
        public Vector3 cubeStartPosition = new Vector3(0, 10, 0);
        
        [Tooltip("下落方向（单位向量）")]
        public Vector3 fallDirection = new Vector3(0.5f, -1f, 0).normalized;
        
        [Tooltip("初始速度")]
        public float initialSpeed = 1f;
        
        [Tooltip("加速度")]
        public float acceleration = 2f;
        
        [Tooltip("第2秒开始的抖动强度")]
        public float shakeIntensity = 0.1f;
        
        [Header("相机设置")]
        [Tooltip("相机跟随偏移")]
        public Vector3 cameraOffset = new Vector3(0, 2, -5);
        
        [Tooltip("第6秒开始相机后拉距离")]
        public float cameraPullBackDistance = 10f;
        
        [Tooltip("相机后拉速度")]
        public float cameraPullBackSpeed = 2f;
        
        [Header("背景渐变设置")]
        [Tooltip("渐变材质")]
        public Material gradientMaterial;
        
        [Header("时间控制")]
        public bool autoStart = true;
        
        // 内部状态
        private float currentSpeed;
        private Vector3 cubeBasePosition;
        private GameObject activeEffect;
        private bool isShaking = false;
        private float elapsedTime = 0f;
        
        void Start()
        {
            // 初始化
            if (fallingCube != null)
            {
                fallingCube.position = cubeStartPosition;
                cubeBasePosition = cubeStartPosition;
            }
            
            currentSpeed = initialSpeed;
            
            // 隐藏UI元素
            if (titleGroup != null) titleGroup.alpha = 0;
            if (authorGroup != null) authorGroup.alpha = 0;
            if (fadeScreen != null) fadeScreen.alpha = 0;
            
            // 隐藏背景
            if (backgroundPlane != null)
            {
                backgroundPlane.enabled = false;
            }
            
            if (autoStart)
            {
                StartCoroutine(PlayOpeningSequence());
            }
        }
        
        void Update()
        {
            elapsedTime += Time.deltaTime;
            
            // 0-10秒：正方体下落
            if (elapsedTime < 10f && fallingCube != null)
            {
                // 更新速度（加速）
                currentSpeed += acceleration * Time.deltaTime;
                
                // 更新基础位置
                cubeBasePosition += fallDirection * currentSpeed * Time.deltaTime;
                
                // 应用抖动效果（2秒后）
                Vector3 finalPosition = cubeBasePosition;
                if (isShaking)
                {
                    float shakeAmount = shakeIntensity * Mathf.Clamp01((elapsedTime - 2f) / 2f);
                    finalPosition += Random.insideUnitSphere * shakeAmount;
                }
                
                fallingCube.position = finalPosition;
            }
            
            // 0-6秒：相机跟随正方体
            if (elapsedTime < 6f && fallingCube != null && mainCamera != null)
            {
                Vector3 targetCameraPos = fallingCube.position + cameraOffset;
                mainCamera.transform.position = Vector3.Lerp(
                    mainCamera.transform.position,
                    targetCameraPos,
                    Time.deltaTime * 5f
                );
                
                // 相机看向正方体
                mainCamera.transform.LookAt(fallingCube);
            }
            // 6-10秒：相机往后拉
            else if (elapsedTime >= 6f && elapsedTime < 10f && mainCamera != null)
            {
                Vector3 pullBackDirection = (mainCamera.transform.position - fallingCube.position).normalized;
                mainCamera.transform.position += pullBackDirection * cameraPullBackSpeed * Time.deltaTime;
            }
        }
        
        IEnumerator PlayOpeningSequence()
        {
            // 0-2秒：正方体开始下落，相机跟随
            yield return new WaitForSeconds(2f);
            
            // 第2秒：激活特效和抖动
            if (effectPrefab != null && fallingCube != null)
            {
                activeEffect = Instantiate(effectPrefab, fallingCube.position, Quaternion.identity, fallingCube);
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
            if (backgroundPlane != null && gradientMaterial != null)
            {
                backgroundPlane.material = gradientMaterial;
                backgroundPlane.enabled = true;
                
                // 将背景放置在合适的位置
                backgroundPlane.transform.position = fallingCube.position + new Vector3(0, -5, 5);
                backgroundPlane.transform.localScale = new Vector3(20, 20, 1);
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

