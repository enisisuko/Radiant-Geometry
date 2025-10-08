using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace FadedDreams.UI
{
    /// <summary>
    /// 流体菜单场景设置脚本
    /// 负责自动创建和配置菜单场景的所有必要组件
    /// </summary>
    public class FluidMenuSceneSetup : MonoBehaviour
    {
        [Header("场景配置")]
        public string sceneName = "FluidMenuScene";
        public Vector2 canvasSize = new Vector2(1920, 1080);
        public float blockSize = 200f;
        public float blockSpacing = 300f;
        
        [Header("后期处理")]
        public bool enableBloom = true;
        public float bloomIntensity = 1.5f;
        public float bloomThreshold = 0.8f;
        public Color bloomTint = Color.white;
        
        [Header("背景")]
        public bool enableBackgroundGradient = true;
        public Color topColor = new Color(0.1f, 0.1f, 0.2f, 1f);
        public Color bottomColor = new Color(0.05f, 0.05f, 0.1f, 1f);
        
        [Header("音频")]
        public bool enableAudioListener = true;
        public float masterVolume = 0.8f;
        
        [Header("自动创建")]
        public bool autoCreateOnStart = true;
        public bool destroyAfterSetup = true;
        
        void Start()
        {
            if (autoCreateOnStart)
            {
                SetupFluidMenuScene();
            }
        }
        
        [ContextMenu("Setup Fluid Menu Scene")]
        public void SetupFluidMenuScene()
        {
            Debug.Log("开始设置流体菜单场景...");
            
            // 1. 设置相机
            SetupCamera();
            
            // 2. 创建Canvas和UI
            SetupCanvas();
            
            // 3. 创建色块
            CreateColorBlocks();
            
            // 4. 设置后期处理
            SetupPostProcessing();
            
            // 5. 设置背景
            SetupBackground();
            
            // 6. 设置音频
            SetupAudio();
            
            // 7. 设置菜单管理器
            SetupMenuManager();
            
            Debug.Log("流体菜单场景设置完成！");
            
            if (destroyAfterSetup)
            {
                if (Application.isPlaying)
                {
                    Destroy(gameObject);
                }
                else
                {
                    DestroyImmediate(gameObject);
                }
            }
        }
        
        void SetupCamera()
        {
            Camera mainCamera = Camera.main;
            if (mainCamera == null)
            {
                GameObject cameraGO = new GameObject("Main Camera");
                mainCamera = cameraGO.AddComponent<Camera>();
                cameraGO.tag = "MainCamera";
            }
            
            // 设置相机参数
            mainCamera.clearFlags = CameraClearFlags.SolidColor;
            mainCamera.backgroundColor = Color.black;
            mainCamera.orthographic = true;
            mainCamera.orthographicSize = 5f;
            mainCamera.nearClipPlane = 0.1f;
            mainCamera.farClipPlane = 100f;
            
            // 添加Universal Render Pipeline组件
            if (mainCamera.GetComponent<UniversalAdditionalCameraData>() == null)
            {
                mainCamera.gameObject.AddComponent<UniversalAdditionalCameraData>();
            }
        }
        
        void SetupCanvas()
        {
            // 查找或创建Canvas
            Canvas canvas = FindObjectOfType<Canvas>();
            if (canvas == null)
            {
                GameObject canvasGO = new GameObject("Canvas");
                canvas = canvasGO.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 0;
            }
            
            // 设置Canvas Scaler
            CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler == null)
            {
                scaler = canvas.gameObject.AddComponent<CanvasScaler>();
            }
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = canvasSize;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            
            // 添加GraphicRaycaster
            if (canvas.GetComponent<GraphicRaycaster>() == null)
            {
                canvas.gameObject.AddComponent<GraphicRaycaster>();
            }
            
            // 设置EventSystem
            SetupEventSystem();
        }
        
        void SetupEventSystem()
        {
            if (FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                GameObject eventSystemGO = new GameObject("EventSystem");
                eventSystemGO.AddComponent<UnityEngine.EventSystems.EventSystem>();
                eventSystemGO.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            }
        }
        
        void CreateColorBlocks()
        {
            Canvas canvas = FindObjectOfType<Canvas>();
            if (canvas == null) return;
            
            // 创建色块容器
            GameObject blocksContainer = new GameObject("ColorBlocks");
            blocksContainer.transform.SetParent(canvas.transform, false);
            
            RectTransform containerRect = blocksContainer.AddComponent<RectTransform>();
            containerRect.anchorMin = Vector2.zero;
            containerRect.anchorMax = Vector2.one;
            containerRect.offsetMin = Vector2.zero;
            containerRect.offsetMax = Vector2.zero;
            
            // 创建5个色块
            for (int i = 0; i < 5; i++)
            {
                CreateColorBlock(i, blocksContainer.transform);
            }
        }
        
        void CreateColorBlock(int index, Transform parent)
        {
            GameObject blockGO = new GameObject($"ColorBlock_{index}");
            blockGO.transform.SetParent(parent, false);
            
            // 添加RectTransform
            RectTransform rectTransform = blockGO.AddComponent<RectTransform>();
            rectTransform.sizeDelta = Vector2.one * blockSize;
            
            // 设置位置
            Vector2 position = GetBlockPosition(index);
            rectTransform.anchoredPosition = position;
            
            // 添加Image组件
            Image image = blockGO.AddComponent<Image>();
            image.color = GetBlockColor(index);
            image.raycastTarget = true;
            
            // 添加流体形状生成器
            FluidShapeGenerator shapeGenerator = blockGO.AddComponent<FluidShapeGenerator>();
            
            // 设置流体纹理
            Texture2D fluidTexture = shapeGenerator.GenerateFluidTexture();
            image.sprite = Sprite.Create(fluidTexture, new Rect(0, 0, fluidTexture.width, fluidTexture.height), new Vector2(0.5f, 0.5f));
            
            // 添加FluidColorBlock组件
            FluidColorBlock fluidBlock = blockGO.AddComponent<FluidColorBlock>();
            fluidBlock.image = image;
            
            // 设置材质（使用我们的流体Shader）
            Material fluidMaterial = CreateFluidMaterial(index);
            if (fluidMaterial != null)
            {
                image.material = fluidMaterial;
                fluidBlock.material = fluidMaterial;
            }
        }
        
        Vector2 GetBlockPosition(int index)
        {
            float halfSpacing = blockSpacing * 0.5f;
            
            switch (index)
            {
                case 0: return new Vector2(-halfSpacing, halfSpacing); // 左上：新游戏
                case 1: return new Vector2(0, 0); // 中心：继续游戏
                case 2: return new Vector2(halfSpacing, halfSpacing); // 右上：双人模式
                case 3: return new Vector2(halfSpacing, -halfSpacing); // 右下：退出游戏
                case 4: return new Vector2(-halfSpacing, -halfSpacing); // 左下：支持我
                default: return Vector2.zero;
            }
        }
        
        Color GetBlockColor(int index)
        {
            switch (index)
            {
                case 0: return new Color(0f, 0.85f, 1f, 0.8f); // 青蓝色
                case 1: return new Color(0.73f, 0.4f, 1f, 0.8f); // 柔和紫
                case 2: return new Color(1f, 0.6f, 0.34f, 0.8f); // 活力橙
                case 3: return new Color(1f, 0.42f, 0.42f, 0.8f); // 暗红色
                case 4: return new Color(1f, 0.85f, 0.24f, 0.8f); // 温暖金
                default: return Color.white;
            }
        }
        
        Material CreateFluidMaterial(int index)
        {
            // 创建使用我们自定义Shader的材质
            Shader fluidShader = Shader.Find("UI/FluidColorBlock");
            if (fluidShader == null)
            {
                Debug.LogWarning("找不到FluidColorBlock Shader，使用默认材质");
                return null;
            }
            
            Material material = new Material(fluidShader);
            
            // 设置材质属性
            Color blockColor = GetBlockColor(index);
            material.SetColor("_Color", blockColor);
            material.SetColor("_EmissionColor", blockColor);
            material.SetFloat("_EmissionIntensity", 2f);
            material.SetFloat("_DistortionStrength", 0.02f);
            material.SetFloat("_WaveSpeed", 2f);
            material.SetFloat("_WaveFrequency", 10f);
            material.SetFloat("_EdgeSoftness", 0.1f);
            material.SetFloat("_EdgeFade", 0.5f);
            material.SetFloat("_BreathScale", 0.05f);
            material.SetFloat("_BreathSpeed", 1f);
            
            return material;
        }
        
        void SetupPostProcessing()
        {
            if (!enableBloom) return;
            
            // 查找或创建Volume
            Volume volume = FindObjectOfType<Volume>();
            if (volume == null)
            {
                GameObject volumeGO = new GameObject("PostProcess Volume");
                volume = volumeGO.AddComponent<Volume>();
            }
            
            // 设置Volume为全局
            volume.isGlobal = true;
            volume.priority = 0;
            
            // 创建Volume Profile
            VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();
            volume.profile = profile;
            
            // 添加Bloom效果
            Bloom bloom = profile.Add<Bloom>();
            bloom.intensity.value = bloomIntensity;
            bloom.threshold.value = bloomThreshold;
            bloom.tint.value = bloomTint;
            bloom.scatter.value = 0.7f;
            bloom.clamp.value = 65472f;
            bloom.dirtTexture.value = null;
            bloom.dirtIntensity.value = 0f;
        }
        
        void SetupBackground()
        {
            if (!enableBackgroundGradient) return;
            
            Canvas canvas = FindObjectOfType<Canvas>();
            if (canvas == null) return;
            
            // 创建背景
            GameObject backgroundGO = new GameObject("Background");
            backgroundGO.transform.SetParent(canvas.transform, false);
            backgroundGO.transform.SetAsFirstSibling(); // 放在最底层
            
            RectTransform bgRect = backgroundGO.AddComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;
            
            // 添加渐变背景
            Image bgImage = backgroundGO.AddComponent<Image>();
            bgImage.color = Color.white;
            bgImage.raycastTarget = false;
            
            // 创建渐变材质
            Material gradientMaterial = CreateGradientMaterial();
            bgImage.material = gradientMaterial;
        }
        
        Material CreateGradientMaterial()
        {
            Shader gradientShader = Shader.Find("UI/Default");
            if (gradientShader == null)
            {
                gradientShader = Shader.Find("Sprites/Default");
            }
            
            Material material = new Material(gradientShader);
            material.color = Color.Lerp(topColor, bottomColor, 0.5f);
            
            return material;
        }
        
        void SetupAudio()
        {
            if (!enableAudioListener) return;
            
            // 查找或创建AudioListener
            if (FindObjectOfType<AudioListener>() == null)
            {
                Camera mainCamera = Camera.main;
                if (mainCamera != null)
                {
                    mainCamera.gameObject.AddComponent<AudioListener>();
                }
            }
            
            // 设置主音量
            AudioListener.volume = masterVolume;
        }
        
        void SetupMenuManager()
        {
            // 查找或创建FluidMenuManager
            FluidMenuManager menuManager = FindObjectOfType<FluidMenuManager>();
            if (menuManager == null)
            {
                GameObject managerGO = new GameObject("FluidMenuManager");
                menuManager = managerGO.AddComponent<FluidMenuManager>();
            }
            
            // 自动配置菜单管理器
            FluidColorBlock[] colorBlocks = FindObjectsOfType<FluidColorBlock>();
            menuManager.colorBlocks = colorBlocks;
            
            // 设置Transform引用
            Transform[] blockTransforms = new Transform[colorBlocks.Length];
            for (int i = 0; i < colorBlocks.Length; i++)
            {
                blockTransforms[i] = colorBlocks[i].transform;
            }
            menuManager.blockTransforms = blockTransforms;
            
            // 设置其他引用
            menuManager.menuCamera = Camera.main;
            menuManager.blockSpacing = blockSpacing;
            menuManager.centerBlockSize = 1.2f;
            menuManager.cornerBlockSize = 1.0f;
            
            // 添加输入处理组件
            if (menuManager.GetComponent<FluidMenuInput>() == null)
            {
                FluidMenuInput input = menuManager.gameObject.AddComponent<FluidMenuInput>();
                input.SetMenuManager(menuManager);
                input.SetMenuCamera(Camera.main);
            }
        }
        
        // 公共接口
        public void SetCanvasSize(Vector2 size)
        {
            canvasSize = size;
        }
        
        public void SetBlockSize(float size)
        {
            blockSize = size;
        }
        
        public void SetBlockSpacing(float spacing)
        {
            blockSpacing = spacing;
        }
        
        public void SetBloomSettings(bool enable, float intensity, float threshold, Color tint)
        {
            enableBloom = enable;
            bloomIntensity = intensity;
            bloomThreshold = threshold;
            bloomTint = tint;
        }
        
        public void SetBackgroundColors(Color top, Color bottom)
        {
            topColor = top;
            bottomColor = bottom;
        }
    }
}