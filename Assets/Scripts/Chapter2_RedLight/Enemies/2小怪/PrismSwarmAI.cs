using UnityEngine;
using UnityEngine.Rendering.Universal;
using System.Collections.Generic;
using FadedDreams.Enemies;
using FadedDreams.Player;

namespace FadedDreams.Enemies
{
    /// <summary>
    /// 棱镜群体 - 将玩家的光分束/分色，既能帮玩家"绕射"触达盲区，也会"放大误差"
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    [RequireComponent(typeof(RedLightController))]
    public class PrismSwarmAI : MonoBehaviour, IDamageable
    {
        [Header("Refs")]
        public Transform player;
        public Rigidbody2D rb;
        public SpriteRenderer prismRenderer;
        public Light2D auraLight;

        [Header("Detection")]
        public float detectRadius = 8f;
        public LayerMask lightLayers = ~0;

        [Header("Light Processing")]
        [Tooltip("吸收比例 (0-1)")]
        [Range(0f, 1f)]
        public float absorptionRatio = 0.3f;
        [Tooltip("激光命中时的束数")]
        public int laserBeamCount = 8;
        [Tooltip("散射光命中时的束数")]
        public int scatterBeamCount = 4;
        [Tooltip("扇形宽度 (度)")]
        public float fanWidth = 60f;
        [Tooltip("角度随机抖动")]
        public float angleJitter = 10f;

        [Header("Rotation")]
        [Tooltip("外壳旋转速度 (度/秒)")]
        public float rotationSpeed = 45f;
        [Tooltip("影响再发射扇形的相位")]
        public float phaseOffset = 0f;

        [Header("Overload")]
        [Tooltip("过载阈值")]
        public float overloadThreshold = 80f;
        [Tooltip("临时火点数量")]
        public int tempFirePointCount = 3;
        [Tooltip("临时火点半径")]
        public float tempFirePointRadius = 2f;
        [Tooltip("临时火点持续时间")]
        public float tempFirePointDuration = 5f;
        [Tooltip("临时火点充能强度")]
        public float tempFirePointIntensity = 2f;

        [Header("Visual Feedback")]
        [Tooltip("光谱刻度条纹预制体")]
        public GameObject spectrumStripePrefab;
        [Tooltip("过载碎裂特效")]
        public GameObject shatterEffectPrefab;
        [Tooltip("临时火点预制体")]
        public GameObject tempFirePointPrefab;

        [Header("Combat")]
        public float maxHp = 60f;
        public float currentCharge = 0f;
        
        // 红光控制器（用于生命值系统）
        private RedLightController redLightController;

        // 内部状态
        private bool _isOverloaded = false;
        private List<GameObject> _activeStripes = new List<GameObject>();
        private List<GameObject> _tempFirePoints = new List<GameObject>();
        private float _lastLightHitTime = 0f;
        private int _lastBeamCount = 0;

        // 组件引用
        private RedLightController _redLightController;
        private Collider2D _collider;

        private void Awake()
        {
            _redLightController = GetComponent<RedLightController>();
            _collider = GetComponent<Collider2D>();
            _redLightController.maxRed = maxHp;
            _redLightController.startRed = maxHp;
        }

        private void Start()
        {
            if (!player)
                player = FindFirstObjectByType<PlayerController2D>()?.transform;
        }

        private void Update()
        {
            // 外壳旋转
            transform.Rotate(0, 0, rotationSpeed * Time.deltaTime);
            phaseOffset = transform.eulerAngles.z;

            // 检测光命中
            CheckLightHit();

            // 更新充能提示
            UpdateChargeVisual();

            // 检查过载
            if (currentCharge >= overloadThreshold && !_isOverloaded)
            {
                Overload();
            }
        }

        private void CheckLightHit()
        {
            // 检测范围内的光源
            Collider2D[] lightSources = Physics2D.OverlapCircleAll(transform.position, detectRadius, lightLayers);
            
            foreach (var source in lightSources)
            {
                var light2D = source.GetComponent<Light2D>();
                if (light2D && light2D.intensity > 0.1f)
                {
                    ProcessLightHit(light2D);
                }
            }
        }

        private void ProcessLightHit(Light2D light)
        {
            _lastLightHitTime = Time.time;
            
            // 计算吸收的能量
            float absorbedEnergy = light.intensity * absorptionRatio;
            currentCharge += absorbedEnergy;

            // 计算再发射束数
            _lastBeamCount = light.intensity > 2f ? laserBeamCount : scatterBeamCount;

            // 生成再发射光束
            GenerateRefractedBeams(light.intensity * (1f - absorptionRatio));

            // 更新光谱刻度
            UpdateSpectrumStripes();
        }

        private void GenerateRefractedBeams(float intensity)
        {
            // 计算扇形角度范围
            float halfFan = fanWidth * 0.5f;
            float startAngle = phaseOffset - halfFan;
            float angleStep = fanWidth / (_lastBeamCount - 1);

            for (int i = 0; i < _lastBeamCount; i++)
            {
                float angle = startAngle + (angleStep * i) + Random.Range(-angleJitter, angleJitter);
                Vector2 direction = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));

                // 创建光束
                CreateBeam(transform.position, direction, intensity / _lastBeamCount);
            }
        }

        private void CreateBeam(Vector3 origin, Vector2 direction, float intensity)
        {
            // 这里可以创建实际的光束效果
            // 暂时用Debug.DrawRay来显示
            Debug.DrawRay(origin, direction * 5f, Color.cyan, 0.1f);
        }

        private void UpdateSpectrumStripes()
        {
            // 清除旧的条纹
            foreach (var stripe in _activeStripes)
            {
                if (stripe) Destroy(stripe);
            }
            _activeStripes.Clear();

            // 创建新的条纹
            if (spectrumStripePrefab)
            {
                for (int i = 0; i < _lastBeamCount; i++)
                {
                    float angle = (360f / _lastBeamCount) * i + phaseOffset;
                    Vector3 pos = transform.position + new Vector3(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad), 0) * 1.5f;
                    
                    var stripe = Instantiate(spectrumStripePrefab, pos, Quaternion.Euler(0, 0, angle), transform);
                    _activeStripes.Add(stripe);
                }
            }
        }

        private void UpdateChargeVisual()
        {
            if (auraLight)
            {
                float chargeRatio = currentCharge / overloadThreshold;
                auraLight.intensity = Mathf.Lerp(0.5f, 3f, chargeRatio);
                
                // 色温变化：淡到亮，色温逐步偏冷→中性
                Color baseColor = Color.Lerp(Color.red, Color.white, chargeRatio);
                auraLight.color = baseColor;

                // 接近过载时闪烁
                if (chargeRatio > 0.8f)
                {
                    float flash = Mathf.Sin(Time.time * 10f) * 0.5f + 0.5f;
                    auraLight.intensity *= (1f + flash * 0.5f);
                }
            }
        }

        private void Overload()
        {
            _isOverloaded = true;

            // 六瓣碎裂特效
            if (shatterEffectPrefab)
            {
                Instantiate(shatterEffectPrefab, transform.position, Quaternion.identity);
            }

            // 创建临时火点
            CreateTempFirePoints();

            // 死亡
            _redLightController.Set(0f);
        }

        private void CreateTempFirePoints()
        {
            for (int i = 0; i < tempFirePointCount; i++)
            {
                Vector2 randomPos = (Vector2)transform.position + Random.insideUnitCircle * tempFirePointRadius;
                
                if (tempFirePointPrefab)
                {
                    var firePoint = Instantiate(tempFirePointPrefab, randomPos, Quaternion.identity);
                    _tempFirePoints.Add(firePoint);

                    // 设置临时火点的属性
                    var light2D = firePoint.GetComponent<Light2D>();
                    if (light2D)
                    {
                        light2D.intensity = tempFirePointIntensity;
                        light2D.color = Color.orange;
                    }

                    // 设置自动销毁
                    Destroy(firePoint, tempFirePointDuration);
                }
            }
        }

        public void TakeDamage(float amount)
        {
            if (_redLightController.IsEmpty) return;
            _redLightController.TryConsume(amount);
        }

        public bool IsDead => _redLightController.IsEmpty;

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, detectRadius);
            
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, tempFirePointRadius);
        }
    }
}