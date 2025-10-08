using UnityEngine;
using UnityEngine.UI;
using System.Collections;

namespace FadedDreams.UI
{
    /// <summary>
    /// 流体蔓延控制器
    /// 控制色块的流体蔓延效果和交互
    /// </summary>
    public class FluidSpreadController : MonoBehaviour
    {
        [Header("流体蔓延设置")]
        public float spreadIntensity = 1.0f;
        public float spreadSpeed = 2.0f;
        public float mouseAttractionStrength = 3.0f;
        public float mouseAttractionRadius = 50.0f;
        public float surfaceTensionStrength = 0.8f;
        
        [Header("视觉效果")]
        public float emissionIntensity = 2.0f;
        public Color fluidColor = Color.cyan;
        public float fluidOpacity = 0.8f;
        
        [Header("动画曲线")]
        public AnimationCurve spreadCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        public AnimationCurve attractionCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        
        // 组件引用
        private FluidPhysicsSimulator physicsSimulator;
        private Image fluidImage;
        private RectTransform rectTransform;
        private Material fluidMaterial;
        
        // 状态管理
        private bool isSpreading = false;
        private bool isMouseAttracted = false;
        private Vector2 mousePosition;
        private Vector2 originalSize;
        private Vector2 targetSize;
        private float spreadProgress = 0f;
        
        // 动画参数
        private Coroutine spreadCoroutine;
        private Coroutine attractionCoroutine;
        
        void Start()
        {
            InitializeComponents();
            SetupFluidMaterial();
        }
        
        void InitializeComponents()
        {
            // 获取或创建物理模拟器
            physicsSimulator = GetComponent<FluidPhysicsSimulator>();
            if (physicsSimulator == null)
            {
                physicsSimulator = gameObject.AddComponent<FluidPhysicsSimulator>();
            }
            
            // 获取UI组件
            fluidImage = GetComponent<Image>();
            rectTransform = GetComponent<RectTransform>();
            
            // 记录原始尺寸
            originalSize = rectTransform.sizeDelta;
            targetSize = originalSize;
        }
        
        void SetupFluidMaterial()
        {
            if (fluidImage != null)
            {
                // 设置Image的颜色
                fluidImage.color = fluidColor;
                
                // 创建流体材质
                Shader fluidShader = Shader.Find("UI/FluidColorBlock");
                if (fluidShader != null)
                {
                    fluidMaterial = new Material(fluidShader);
                    fluidImage.material = fluidMaterial;
                    
                    // 设置材质属性
                    fluidMaterial.SetColor("_Color", fluidColor);
                    fluidMaterial.SetFloat("_EmissionIntensity", emissionIntensity);
                    fluidMaterial.SetFloat("_DistortionStrength", 0.1f);
                    fluidMaterial.SetFloat("_WaveSpeed", spreadSpeed);
                    fluidMaterial.SetFloat("_WaveFrequency", 5.0f);
                }
                else
                {
                    // 如果没有找到自定义Shader，使用默认材质
                    Debug.LogWarning("FluidColorBlock Shader not found, using default material");
                }
            }
        }
        
        void Update()
        {
            UpdateFluidTexture();
            UpdateMouseInteraction();
            UpdateFluidMaterial();
        }
        
        void UpdateFluidTexture()
        {
            if (physicsSimulator != null && fluidImage != null)
            {
                Texture2D fluidTexture = physicsSimulator.GetFluidTexture();
                if (fluidTexture != null)
                {
                    // 创建Sprite并应用到Image
                    Sprite fluidSprite = Sprite.Create(
                        fluidTexture,
                        new Rect(0, 0, fluidTexture.width, fluidTexture.height),
                        new Vector2(0.5f, 0.5f)
                    );
                    
                    fluidImage.sprite = fluidSprite;
                }
            }
        }
        
        void UpdateMouseInteraction()
        {
            if (Input.mousePresent)
            {
                Vector2 mousePos = Input.mousePosition;
                Vector2 localMousePos;
                
                // 将鼠标位置转换为本地坐标
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    rectTransform, mousePos, null, out localMousePos))
                {
                    // 转换为0-1范围
                    Vector2 normalizedMousePos = new Vector2(
                        (localMousePos.x + rectTransform.sizeDelta.x * 0.5f) / rectTransform.sizeDelta.x,
                        (localMousePos.y + rectTransform.sizeDelta.y * 0.5f) / rectTransform.sizeDelta.y
                    );
                    
                    // 检查是否在色块范围内
                    float distance = Vector2.Distance(normalizedMousePos, Vector2.one * 0.5f);
                    bool isMouseOver = distance <= 0.5f;
                    
                    if (isMouseOver && !isMouseAttracted)
                    {
                        StartMouseAttraction(normalizedMousePos);
                    }
                    else if (!isMouseOver && isMouseAttracted)
                    {
                        StopMouseAttraction();
                    }
                    
                    if (isMouseAttracted)
                    {
                        physicsSimulator.SetMouseInfluence(normalizedMousePos);
                    }
                }
            }
        }
        
        void UpdateFluidMaterial()
        {
            if (fluidMaterial != null)
            {
                // 更新材质参数
                fluidMaterial.SetFloat("_EmissionIntensity", emissionIntensity * (1f + spreadProgress * 0.5f));
                fluidMaterial.SetFloat("_DistortionStrength", 0.1f + spreadProgress * 0.2f);
                fluidMaterial.SetFloat("_WaveSpeed", spreadSpeed * (1f + spreadProgress * 0.3f));
                
                // 根据蔓延进度调整颜色
                Color currentColor = Color.Lerp(fluidColor, fluidColor * 1.2f, spreadProgress);
                fluidMaterial.SetColor("_Color", currentColor);
            }
        }
        
        public void StartFluidSpread()
        {
            if (!isSpreading)
            {
                isSpreading = true;
                spreadCoroutine = StartCoroutine(FluidSpreadAnimation());
            }
        }
        
        public void StopFluidSpread()
        {
            if (isSpreading)
            {
                isSpreading = false;
                if (spreadCoroutine != null)
                {
                    StopCoroutine(spreadCoroutine);
                }
                spreadCoroutine = StartCoroutine(FluidRetractAnimation());
            }
        }
        
        void StartMouseAttraction(Vector2 mousePos)
        {
            isMouseAttracted = true;
            mousePosition = mousePos;
            attractionCoroutine = StartCoroutine(MouseAttractionAnimation());
        }
        
        void StopMouseAttraction()
        {
            isMouseAttracted = false;
            physicsSimulator.ClearMouseInfluence();
            if (attractionCoroutine != null)
            {
                StopCoroutine(attractionCoroutine);
            }
        }
        
        IEnumerator FluidSpreadAnimation()
        {
            float duration = 1.0f / spreadSpeed;
            float elapsedTime = 0f;
            
            Vector2 startSize = rectTransform.sizeDelta;
            Vector2 endSize = startSize * (1f + spreadIntensity);
            
            while (elapsedTime < duration)
            {
                elapsedTime += Time.deltaTime;
                spreadProgress = spreadCurve.Evaluate(elapsedTime / duration);
                
                // 更新尺寸
                rectTransform.sizeDelta = Vector2.Lerp(startSize, endSize, spreadProgress);
                
                // 更新物理模拟参数
                if (physicsSimulator != null)
                {
                    physicsSimulator.spreadRate = spreadIntensity * spreadProgress;
                    physicsSimulator.surfaceTension = surfaceTensionStrength * (1f - spreadProgress * 0.5f);
                }
                
                yield return null;
            }
            
            spreadProgress = 1f;
            rectTransform.sizeDelta = endSize;
        }
        
        IEnumerator FluidRetractAnimation()
        {
            float duration = 1.0f / spreadSpeed;
            float elapsedTime = 0f;
            
            Vector2 startSize = rectTransform.sizeDelta;
            Vector2 endSize = originalSize;
            
            while (elapsedTime < duration)
            {
                elapsedTime += Time.deltaTime;
                spreadProgress = 1f - spreadCurve.Evaluate(elapsedTime / duration);
                
                // 更新尺寸
                rectTransform.sizeDelta = Vector2.Lerp(startSize, endSize, elapsedTime / duration);
                
                // 更新物理模拟参数
                if (physicsSimulator != null)
                {
                    physicsSimulator.spreadRate = spreadIntensity * spreadProgress;
                    physicsSimulator.surfaceTension = surfaceTensionStrength * (1f - spreadProgress * 0.5f);
                }
                
                yield return null;
            }
            
            spreadProgress = 0f;
            rectTransform.sizeDelta = endSize;
        }
        
        IEnumerator MouseAttractionAnimation()
        {
            while (isMouseAttracted)
            {
                // 创建向鼠标位置的流动效果
                Vector2 attractionForce = (mousePosition - Vector2.one * 0.5f) * mouseAttractionStrength;
                
                // 更新物理模拟器的鼠标影响
                physicsSimulator.SetMouseInfluence(mousePosition);
                
                // 轻微调整色块位置朝向鼠标
                Vector2 currentPos = rectTransform.anchoredPosition;
                Vector2 targetPos = currentPos + attractionForce * 0.1f;
                rectTransform.anchoredPosition = Vector2.Lerp(currentPos, targetPos, Time.deltaTime * 2f);
                
                yield return null;
            }
        }
        
        public void SetFluidColor(Color color)
        {
            fluidColor = color;
            if (fluidImage != null)
            {
                fluidImage.color = color;
            }
            if (fluidMaterial != null)
            {
                fluidMaterial.SetColor("_Color", color);
            }
        }
        
        public void SetSpreadIntensity(float intensity)
        {
            spreadIntensity = Mathf.Clamp01(intensity);
        }
        
        public bool IsSpreading()
        {
            return isSpreading;
        }
        
        public bool IsMouseAttracted()
        {
            return isMouseAttracted;
        }
        
        public float GetSpreadProgress()
        {
            return spreadProgress;
        }
        
        void OnDestroy()
        {
            if (fluidMaterial != null)
            {
                DestroyImmediate(fluidMaterial);
            }
        }
    }
}
