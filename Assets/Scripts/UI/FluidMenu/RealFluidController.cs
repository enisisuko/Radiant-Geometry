using UnityEngine;
using UnityEngine.UI;
using System.Collections;

namespace FadedDreams.UI
{
    /// <summary>
    /// 真实流体控制器
    /// 控制真实物理流体的交互和渲染
    /// </summary>
    public class RealFluidController : MonoBehaviour
    {
        [Header("流体设置")]
        public Color fluidColor = Color.cyan;
        public float fluidOpacity = 0.8f;
        public float emissionIntensity = 2.0f;
        
        [Header("交互设置")]
        public float mouseForceStrength = 5.0f;
        public float mouseForceRadius = 20.0f;
        public float mouseAttractionStrength = 3.0f;
        public float mouseAttractionRadius = 15.0f;
        
        [Header("物理参数")]
        public float gravity = 9.81f;
        public float viscosity = 0.01f;
        public float surfaceTension = 0.5f;
        public float density = 1.0f;
        
        // 组件引用
        private RealFluidPhysics fluidPhysics;
        private Image fluidImage;
        private RectTransform rectTransform;
        private Material fluidMaterial;
        
        // 状态管理
        private bool isMouseOver = false;
        private Vector2 lastMousePosition;
        private Vector2 mouseVelocity;
        
        void Start()
        {
            InitializeComponents();
            SetupFluidMaterial();
        }
        
        void InitializeComponents()
        {
            // 获取或创建真实流体物理组件
            fluidPhysics = GetComponent<RealFluidPhysics>();
            if (fluidPhysics == null)
            {
                fluidPhysics = gameObject.AddComponent<RealFluidPhysics>();
            }
            
            // 设置物理参数
            fluidPhysics.gravity = gravity;
            fluidPhysics.viscosity = viscosity;
            fluidPhysics.surfaceTension = surfaceTension;
            fluidPhysics.density = density;
            
            // 获取UI组件
            fluidImage = GetComponent<Image>();
            rectTransform = GetComponent<RectTransform>();
            
            // 设置Image颜色
            if (fluidImage != null)
            {
                fluidImage.color = new Color(fluidColor.r, fluidColor.g, fluidColor.b, fluidOpacity);
            }
        }
        
        void SetupFluidMaterial()
        {
            if (fluidImage != null)
            {
                // 创建流体材质
                Shader fluidShader = Shader.Find("UI/FluidColorBlock");
                if (fluidShader != null)
                {
                    fluidMaterial = new Material(fluidShader);
                    fluidImage.material = fluidMaterial;
                    
                    // 设置材质属性
                    fluidMaterial.SetColor("_Color", fluidColor);
                    fluidMaterial.SetFloat("_EmissionIntensity", emissionIntensity);
                    fluidMaterial.SetFloat("_DistortionStrength", 0.2f);
                    fluidMaterial.SetFloat("_WaveSpeed", 2.0f);
                    fluidMaterial.SetFloat("_WaveFrequency", 8.0f);
                }
                else
                {
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
            if (fluidPhysics != null && fluidImage != null)
            {
                Texture2D fluidTexture = fluidPhysics.GetFluidTexture();
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
                    bool isMouseOverNow = distance <= 0.5f;
                    
                    if (isMouseOverNow)
                    {
                        if (!isMouseOver)
                        {
                            OnMouseEnter();
                        }
                        
                        // 计算鼠标速度
                        mouseVelocity = (normalizedMousePos - lastMousePosition) / Time.deltaTime;
                        
                        // 应用鼠标力
                        ApplyMouseForce(normalizedMousePos, mouseVelocity);
                        
                        lastMousePosition = normalizedMousePos;
                    }
                    else
                    {
                        if (isMouseOver)
                        {
                            OnMouseExit();
                        }
                    }
                    
                    isMouseOver = isMouseOverNow;
                }
            }
        }
        
        void ApplyMouseForce(Vector2 mousePos, Vector2 mouseVel)
        {
            if (fluidPhysics == null) return;
            
            // 应用鼠标移动产生的力
            Vector2 force = mouseVel * mouseForceStrength;
            fluidPhysics.AddForce(mousePos, force);
            
            // 应用鼠标吸引力
            Vector2 center = Vector2.one * 0.5f;
            Vector2 attractionDir = (mousePos - center).normalized;
            float attractionDistance = Vector2.Distance(mousePos, center);
            
            if (attractionDistance > 0.1f)
            {
                Vector2 attractionForce = -attractionDir * mouseAttractionStrength * (1.0f - attractionDistance);
                fluidPhysics.AddForce(mousePos, attractionForce);
            }
        }
        
        void OnMouseEnter()
        {
            Debug.Log("Mouse entered fluid area");
            // 可以在这里添加进入效果
        }
        
        void OnMouseExit()
        {
            Debug.Log("Mouse exited fluid area");
            // 可以在这里添加离开效果
        }
        
        void UpdateFluidMaterial()
        {
            if (fluidMaterial != null)
            {
                // 根据鼠标交互调整材质参数
                float interactionIntensity = isMouseOver ? 1.5f : 1.0f;
                fluidMaterial.SetFloat("_EmissionIntensity", emissionIntensity * interactionIntensity);
                
                // 根据鼠标速度调整扭曲强度
                float distortionStrength = 0.2f + mouseVelocity.magnitude * 0.1f;
                fluidMaterial.SetFloat("_DistortionStrength", distortionStrength);
            }
        }
        
        public void SetFluidColor(Color color)
        {
            fluidColor = color;
            if (fluidImage != null)
            {
                fluidImage.color = new Color(color.r, color.g, color.b, fluidOpacity);
            }
            if (fluidMaterial != null)
            {
                fluidMaterial.SetColor("_Color", color);
            }
        }
        
        public void AddFluidAt(Vector2 position, float amount)
        {
            if (fluidPhysics != null)
            {
                fluidPhysics.AddFluid(position, amount);
            }
        }
        
        public void AddForceAt(Vector2 position, Vector2 force)
        {
            if (fluidPhysics != null)
            {
                fluidPhysics.AddForce(position, force);
            }
        }
        
        public void SetGravity(float newGravity)
        {
            gravity = newGravity;
            if (fluidPhysics != null)
            {
                fluidPhysics.gravity = newGravity;
            }
        }
        
        public void SetViscosity(float newViscosity)
        {
            viscosity = newViscosity;
            if (fluidPhysics != null)
            {
                fluidPhysics.viscosity = newViscosity;
            }
        }
        
        public void SetSurfaceTension(float newSurfaceTension)
        {
            surfaceTension = newSurfaceTension;
            if (fluidPhysics != null)
            {
                fluidPhysics.surfaceTension = newSurfaceTension;
            }
        }
        
        public bool IsMouseOver()
        {
            return isMouseOver;
        }
        
        public Vector2 GetMouseVelocity()
        {
            return mouseVelocity;
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
