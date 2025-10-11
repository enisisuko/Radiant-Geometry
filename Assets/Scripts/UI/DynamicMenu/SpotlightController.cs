// SpotlightController.cs
// 单个聚光灯控制器
// 功能：管理聚光灯位置、方向、平滑旋转、Shader参数更新

using UnityEngine;
using UnityEngine.UI;

namespace FadedDreams.UI
{
    /// <summary>
    /// 单个聚光灯控制器
    /// 管理聚光灯的位置、方向和视觉效果
    /// </summary>
    [RequireComponent(typeof(Image))]
    public class SpotlightController : MonoBehaviour
    {
        [Header("聚光灯设置")]
        [Tooltip("聚光灯颜色")]
        public Color spotlightColor = Color.white;

        [Tooltip("光照强度")]
        public float intensity = 3f;

        [Tooltip("光锥角度")]
        [Range(5f, 180f)]
        public float coneAngle = 30f;

        [Tooltip("最大距离")]
        public float maxDistance = 1000f;

        [Header("旋转设置")]
        [Tooltip("旋转速度（每秒度数）")]
        public float rotationSpeed = 120f;

        [Tooltip("是否使用缓动函数")]
        public bool useEasing = true;

        [Tooltip("缓动速度")]
        public float easingSpeed = 6f;

        [Header("起始位置")]
        [Tooltip("聚光灯起始位置（屏幕空间）")]
        public SpotlightPosition startPosition = SpotlightPosition.Top;

        // 私有状态
        private Image spotlightImage;
        private Material spotlightMaterial;
        private Vector2 currentDirection;
        private Vector2 targetDirection;
        private Vector2 spotlightScreenPos; // Canvas本地坐标
        private Vector2 spotlightUVPos; // UV坐标（0-1范围）
        private float currentMaxDistance; // 当前距离（用于平滑过渡）
        private float targetMaxDistance; // 目标距离
        private Canvas canvas;
        private RectTransform canvasRect;
        
        // Shader属性ID（性能优化）
        private static readonly int ColorID = Shader.PropertyToID("_SpotlightColor");
        private static readonly int IntensityID = Shader.PropertyToID("_Intensity");
        private static readonly int ConeAngleID = Shader.PropertyToID("_ConeAngle");
        private static readonly int DirectionID = Shader.PropertyToID("_Direction");
        private static readonly int PositionID = Shader.PropertyToID("_Position");
        private static readonly int MaxDistanceID = Shader.PropertyToID("_MaxDistance");

        public enum SpotlightPosition
        {
            Top,           // 顶部
            TopRight,      // 右上
            Right,         // 右侧
            BottomRight,   // 右下
            Bottom,        // 底部
            BottomLeft,    // 左下
            Left,          // 左侧
            TopLeft        // 左上
        }

        private void Awake()
        {
            // 获取组件
            spotlightImage = GetComponent<Image>();
            canvas = GetComponentInParent<Canvas>();
            canvasRect = canvas.GetComponent<RectTransform>();

            // 创建材质实例
            if (spotlightImage.material != null)
            {
                spotlightMaterial = new Material(spotlightImage.material);
                spotlightImage.material = spotlightMaterial;
            }

            // 初始化位置
            InitializePosition();
            
            // 将Canvas本地坐标转换为UV坐标
            spotlightUVPos = CanvasToUV(spotlightScreenPos);

            // 初始化方向（指向屏幕中心，UV坐标系）
            Vector2 centerUV = new Vector2(0.5f, 0.5f);
            currentDirection = (centerUV - spotlightUVPos).normalized;
            targetDirection = currentDirection;
        }

        private void Start()
        {
            // 初始化方向和距离（UV坐标系）
            if (canvasRect != null)
            {
                // UV坐标系中心点
                Vector2 centerUV = new Vector2(0.5f, 0.5f);
                currentDirection = (centerUV - spotlightUVPos).normalized;
                targetDirection = currentDirection;
                
                // 初始化maxDistance为UV坐标系中的距离
                // 计算从聚光灯位置到中心的UV距离
                float initialDistance = Vector2.Distance(spotlightUVPos, centerUV);
                // 增加一些余量确保光束能完全到达目标
                initialDistance = Mathf.Max(initialDistance * 1.2f, 0.5f);
                
                // 初始化当前距离和目标距离
                currentMaxDistance = initialDistance;
                targetMaxDistance = initialDistance;
                maxDistance = currentMaxDistance;
            }
            
            UpdateShaderProperties();
        }

        private void Update()
        {
            // 更新方向
            UpdateDirection();
            
            // 平滑过渡距离（防止频闪）
            UpdateDistance();

            // 更新Shader参数
            UpdateShaderProperties();
        }

        /// <summary>
        /// 初始化聚光灯位置
        /// </summary>
        private void InitializePosition()
        {
            if (canvasRect == null) return;

            Vector2 canvasSize = canvasRect.sizeDelta;
            float halfWidth = canvasSize.x * 0.5f;
            float halfHeight = canvasSize.y * 0.5f;

            // 统一发射点：所有聚光灯从顶部中心稍高的位置发出
            // 这样看起来像一个聚光灯阵列从上方照射
            spotlightScreenPos = new Vector2(0f, halfHeight + 150f);
            
            // 注释掉原来的多点发射逻辑
            /*
            // 根据起始位置设置屏幕坐标
            switch (startPosition)
            {
                case SpotlightPosition.Top:
                    spotlightScreenPos = new Vector2(0f, halfHeight + 100f);
                    break;
                case SpotlightPosition.TopRight:
                    spotlightScreenPos = new Vector2(halfWidth + 100f, halfHeight + 100f);
                    break;
                case SpotlightPosition.Right:
                    spotlightScreenPos = new Vector2(halfWidth + 100f, 0f);
                    break;
                case SpotlightPosition.BottomRight:
                    spotlightScreenPos = new Vector2(halfWidth + 100f, -halfHeight - 100f);
                    break;
                case SpotlightPosition.Bottom:
                    spotlightScreenPos = new Vector2(0f, -halfHeight - 100f);
                    break;
                case SpotlightPosition.BottomLeft:
                    spotlightScreenPos = new Vector2(-halfWidth - 100f, -halfHeight - 100f);
                    break;
                case SpotlightPosition.Left:
                    spotlightScreenPos = new Vector2(-halfWidth - 100f, 0f);
                    break;
                case SpotlightPosition.TopLeft:
                    spotlightScreenPos = new Vector2(-halfWidth - 100f, halfHeight + 100f);
                    break;
            }
            */
        }

        /// <summary>
        /// 更新方向（平滑旋转）
        /// </summary>
        private void UpdateDirection()
        {
            if (useEasing)
            {
                // 使用缓动插值
                currentDirection = Vector2.Lerp(currentDirection, targetDirection, Time.deltaTime * easingSpeed);
            }
            else
            {
                // 使用固定速度旋转
                float maxRotation = rotationSpeed * Time.deltaTime * Mathf.Deg2Rad;
                currentDirection = Vector2.MoveTowards(currentDirection, targetDirection, maxRotation);
            }

            // 确保方向归一化
            currentDirection = currentDirection.normalized;
        }
        
        /// <summary>
        /// 更新距离（平滑过渡，防止频闪）
        /// </summary>
        private void UpdateDistance()
        {
            // 使用缓动插值平滑过渡距离
            // 使用与方向相同的缓动速度确保视觉一致性
            currentMaxDistance = Mathf.Lerp(currentMaxDistance, targetMaxDistance, Time.deltaTime * easingSpeed);
            
            // 更新maxDistance供Shader使用
            maxDistance = currentMaxDistance;
        }

        /// <summary>
        /// 更新Shader属性
        /// </summary>
        private void UpdateShaderProperties()
        {
            if (spotlightMaterial == null || canvasRect == null) return;

            // 直接使用UV坐标（0-1范围）
            // 不需要转换，spotlightUVPos已经是UV坐标

            // 更新Shader参数
            spotlightMaterial.SetColor(ColorID, spotlightColor * intensity);
            spotlightMaterial.SetFloat(IntensityID, intensity);
            spotlightMaterial.SetFloat(ConeAngleID, coneAngle);
            spotlightMaterial.SetVector(DirectionID, new Vector4(currentDirection.x, currentDirection.y, 0, 0));
            spotlightMaterial.SetVector(PositionID, new Vector4(spotlightUVPos.x, spotlightUVPos.y, 0, 0));
            spotlightMaterial.SetFloat(MaxDistanceID, maxDistance);
        }

        /// <summary>
        /// 设置目标按键（聚光灯将转向该按键）
        /// </summary>
        /// <param name="targetPosition">目标位置（Canvas本地坐标）</param>
        public void SetTarget(Vector2 targetPosition)
        {
            if (canvasRect == null) return;
            
            // 将目标位置从Canvas本地坐标转换为UV坐标
            Vector2 targetUV = CanvasToUV(targetPosition);
            
            // 在UV坐标系中计算方向
            Vector2 direction = (targetUV - spotlightUVPos).normalized;
            targetDirection = direction;
            
            // 在UV坐标系中计算到目标的精确距离
            // 这样光束末端就会正好到达目标位置
            float distanceToTarget = Vector2.Distance(spotlightUVPos, targetUV);
            
            // 设置目标距离（通过平滑过渡更新，防止频闪）
            // 不直接修改maxDistance，而是通过targetMaxDistance平滑过渡
            targetMaxDistance = distanceToTarget;
        }

        /// <summary>
        /// 设置目标方向
        /// </summary>
        /// <param name="direction">目标方向（归一化向量）</param>
        public void SetTargetDirection(Vector2 direction)
        {
            targetDirection = direction.normalized;
        }

        /// <summary>
        /// 立即对准目标（无动画）
        /// </summary>
        /// <param name="targetPosition">目标位置</param>
        public void SnapToTarget(Vector2 targetPosition)
        {
            Vector2 direction = (targetPosition - spotlightScreenPos).normalized;
            currentDirection = direction;
            targetDirection = direction;
        }

        /// <summary>
        /// 获取当前方向
        /// </summary>
        public Vector2 GetCurrentDirection()
        {
            return currentDirection;
        }

        /// <summary>
        /// 获取聚光灯位置（Canvas本地坐标）
        /// </summary>
        public Vector2 GetPosition()
        {
            return spotlightScreenPos;
        }
        
        /// <summary>
        /// 将Canvas本地坐标转换为UV坐标（0-1范围）
        /// Canvas本地坐标：中心为原点，范围 ±halfWidth, ±halfHeight
        /// UV坐标：左下角为原点，范围 0-1
        /// </summary>
        private Vector2 CanvasToUV(Vector2 canvasPos)
        {
            if (canvasRect == null) return Vector2.zero;
            
            Vector2 canvasSize = canvasRect.sizeDelta;
            
            // 转换公式：将Canvas中心坐标系转换为UV坐标系
            // UV = (Canvas / CanvasSize) + 0.5
            Vector2 uv = new Vector2(
                (canvasPos.x / canvasSize.x) + 0.5f,
                (canvasPos.y / canvasSize.y) + 0.5f
            );
            
            return uv;
        }

        /// <summary>
        /// 设置颜色
        /// </summary>
        public void SetColor(Color color)
        {
            spotlightColor = color;
        }

        /// <summary>
        /// 设置强度
        /// </summary>
        public void SetIntensity(float value)
        {
            intensity = value;
        }

        private void OnDestroy()
        {
            // 清理材质实例
            if (spotlightMaterial != null)
            {
                Destroy(spotlightMaterial);
            }
        }

#if UNITY_EDITOR
        // 编辑器调试
        private void OnDrawGizmos()
        {
            if (!Application.isPlaying) return;

            // 绘制聚光灯位置和方向
            Gizmos.color = spotlightColor;
            Vector3 worldPos = new Vector3(spotlightScreenPos.x * 0.01f, spotlightScreenPos.y * 0.01f, 0f);
            Vector3 directionEnd = worldPos + new Vector3(currentDirection.x, currentDirection.y, 0f) * 2f;
            
            Gizmos.DrawSphere(worldPos, 0.1f);
            Gizmos.DrawLine(worldPos, directionEnd);
        }
#endif
    }
}

