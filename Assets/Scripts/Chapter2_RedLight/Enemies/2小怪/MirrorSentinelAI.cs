using UnityEngine;
using UnityEngine.Rendering.Universal;
using System.Collections;
using System.Collections.Generic;
using FadedDreams.Enemies;
using FadedDreams.Player;

namespace FadedDreams.Enemies
{
    /// <summary>
    /// 镜卫 - 反射散射光、窗口式强吸收，玩家要么等窗强灼、要么用火把侧打反射进行间接击破
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    [RequireComponent(typeof(RedLightController))]
    public class MirrorSentinelAI : MonoBehaviour, IDamageable
    {
        [Header("Refs")]
        public Transform player;
        public Rigidbody2D rb;
        public SpriteRenderer mirrorRenderer;
        public SpriteRenderer shutterRenderer;
        public Light2D mirrorLight;

        [Header("State Cycle")]
        [Tooltip("反射态持续时间")]
        public float reflectDuration = 3f;
        [Tooltip("吸收窗持续时间")]
        public float apertureDuration = 1.2f;
        [Tooltip("状态切换提示时间")]
        public float warningTime = 0.5f;

        [Header("Reflection")]
        [Tooltip("反射损耗率")]
        [Range(0f, 1f)]
        public float reflectionLossRate = 0.2f;
        [Tooltip("激光反射比例")]
        [Range(0f, 1f)]
        public float laserReflectionRatio = 0.7f;
        [Tooltip("反射检测半径")]
        public float reflectionRadius = 5f;
        [Tooltip("反射层")]
        public LayerMask lightLayers = ~0;

        [Header("Aperture")]
        [Tooltip("吸收窗充能加速倍率")]
        public float apertureChargeMultiplier = 3f;
        [Tooltip("吸收窗最大充能")]
        public float maxApertureCharge = 100f;

        [Header("Body Aim")]
        [Tooltip("旋转速度")]
        public float rotationSpeed = 30f;
        [Tooltip("旋转范围")]
        public float rotationRange = 45f;
        [Tooltip("瞄准玩家的最小距离")]
        public float aimMinDistance = 3f;

        [Header("Overload")]
        [Tooltip("过载阈值")]
        public float overloadThreshold = 80f;
        [Tooltip("镜片陷阱数量")]
        public int trapShardCount = 3;
        [Tooltip("镜片陷阱持续时间")]
        public float trapShardDuration = 2f;
        [Tooltip("镜片陷阱预制体")]
        public GameObject trapShardPrefab;

        [Header("Visual")]
        [Tooltip("镜面方向亮带")]
        public GameObject directionStripePrefab;
        [Tooltip("百叶窗张开特效")]
        public GameObject shutterOpenEffectPrefab;
        [Tooltip("镜碎特效")]
        public GameObject shatterEffectPrefab;

        [Header("Combat")]
        public float maxHp = 100f;
        public float currentCharge = 0f;

        // 状态枚举
        public enum MirrorState
        {
            Reflect,
            Aperture
        }

        // 内部状态
        private MirrorState _currentState = MirrorState.Reflect;
        private float _stateTimer = 0f;
        private bool _isOverloaded = false;
        private bool _isWarning = false;
        private float _baseRotation;
        private List<GameObject> _activeTrapShards = new List<GameObject>();
        private GameObject _directionStripe;

        // 组件引用
        private RedLightController _redLightController;
        private Collider2D _collider;

        private void Awake()
        {
            _redLightController = GetComponent<RedLightController>();
            _collider = GetComponent<Collider2D>();
            _redLightController.maxRed = maxHp;
            _redLightController.startRed = maxHp;
            _baseRotation = transform.eulerAngles.z;
        }

        private void Start()
        {
            if (!player)
                player = FindFirstObjectByType<PlayerController2D>()?.transform;
        }

        private void Update()
        {
            if (_isOverloaded) return;

            // 更新状态循环
            UpdateStateCycle();

            // 根据状态执行不同行为
            switch (_currentState)
            {
                case MirrorState.Reflect:
                    HandleReflectState();
                    break;
                case MirrorState.Aperture:
                    HandleApertureState();
                    break;
            }

            // 更新瞄准
            UpdateBodyAim();

            // 更新视觉效果
            UpdateVisuals();

            // 检查过载
            if (currentCharge >= overloadThreshold && !_isOverloaded)
            {
                Overload();
            }
        }

        private void UpdateStateCycle()
        {
            _stateTimer += Time.deltaTime;
            float currentDuration = _currentState == MirrorState.Reflect ? reflectDuration : apertureDuration;

            // 检查是否需要切换状态
            if (_stateTimer >= currentDuration)
            {
                SwitchState();
            }
            else if (_stateTimer >= currentDuration - warningTime)
            {
                // 进入警告状态
                if (!_isWarning)
                {
                    _isWarning = true;
                    OnWarningStart();
                }
            }
        }

        private void SwitchState()
        {
            _currentState = _currentState == MirrorState.Reflect ? MirrorState.Aperture : MirrorState.Reflect;
            _stateTimer = 0f;
            _isWarning = false;
            OnStateChanged();
        }

        private void HandleReflectState()
        {
            // 检测并反射光线
            Collider2D[] lightSources = Physics2D.OverlapCircleAll(transform.position, reflectionRadius, lightLayers);
            
            foreach (var source in lightSources)
            {
                var light2D = source.GetComponent<Light2D>();
                if (light2D && light2D.intensity > 0.1f)
                {
                    ReflectLight(light2D);
                }
            }
        }

        private void HandleApertureState()
        {
            // 吸收所有光线
            Collider2D[] lightSources = Physics2D.OverlapCircleAll(transform.position, reflectionRadius, lightLayers);
            
            foreach (var source in lightSources)
            {
                var light2D = source.GetComponent<Light2D>();
                if (light2D && light2D.intensity > 0.1f)
                {
                    AbsorbLight(light2D);
                }
            }
        }

        private void ReflectLight(Light2D light)
        {
            // 计算反射方向
            Vector2 lightDirection = (light.transform.position - transform.position).normalized;
            Vector2 mirrorNormal = transform.up;
            Vector2 reflectedDirection = Vector2.Reflect(lightDirection, mirrorNormal);

            // 计算反射强度
            float reflectedIntensity = light.intensity * (1f - reflectionLossRate);
            
            // 判断是否为激光（高强度）
            bool isLaser = light.intensity > 2f;
            if (isLaser)
            {
                reflectedIntensity *= laserReflectionRatio;
            }

            // 创建反射光束
            CreateReflectedBeam(transform.position, reflectedDirection, reflectedIntensity);

            // 自身充能
            currentCharge += light.intensity * 0.1f;
        }

        private void AbsorbLight(Light2D light)
        {
            // 完全吸收光线
            float absorbedEnergy = light.intensity * apertureChargeMultiplier;
            currentCharge += absorbedEnergy;

            // 限制最大充能
            currentCharge = Mathf.Min(currentCharge, maxApertureCharge);
        }

        private void CreateReflectedBeam(Vector3 origin, Vector2 direction, float intensity)
        {
            // 这里可以创建实际的反射光束效果
            // 暂时用Debug.DrawRay来显示
            Debug.DrawRay(origin, direction * 5f, Color.cyan, 0.1f);
        }

        private void UpdateBodyAim()
        {
            if (!player) return;

            float distanceToPlayer = Vector2.Distance(transform.position, player.position);
            if (distanceToPlayer < aimMinDistance) return;

            // 计算目标角度
            Vector2 directionToPlayer = (player.position - transform.position).normalized;
            float targetAngle = Mathf.Atan2(directionToPlayer.y, directionToPlayer.x) * Mathf.Rad2Deg - 90f;

            // 限制旋转范围
            float angleDifference = Mathf.DeltaAngle(_baseRotation, targetAngle);
            float clampedAngle = Mathf.Clamp(angleDifference, -rotationRange, rotationRange);
            float finalAngle = _baseRotation + clampedAngle;

            // 平滑旋转
            float currentAngle = transform.eulerAngles.z;
            float newAngle = Mathf.MoveTowardsAngle(currentAngle, finalAngle, rotationSpeed * Time.deltaTime);
            transform.rotation = Quaternion.Euler(0, 0, newAngle);
        }

        private void UpdateVisuals()
        {
            // 更新镜面方向亮带
            UpdateDirectionStripe();

            // 更新百叶窗状态
            UpdateShutterVisual();

            // 更新镜面光效
            UpdateMirrorLight();
        }

        private void UpdateDirectionStripe()
        {
            if (directionStripePrefab)
            {
                if (!_directionStripe && _currentState == MirrorState.Reflect)
                {
                    _directionStripe = Instantiate(directionStripePrefab, transform);
                }
                else if (_directionStripe && _currentState == MirrorState.Aperture)
                {
                    Destroy(_directionStripe);
                    _directionStripe = null;
                }
            }
        }

        private void UpdateShutterVisual()
        {
            if (shutterRenderer)
            {
                float shutterAlpha = _currentState == MirrorState.Aperture ? 1f : 0.3f;
                Color color = shutterRenderer.color;
                color.a = shutterAlpha;
                shutterRenderer.color = color;
            }
        }

        private void UpdateMirrorLight()
        {
            if (mirrorLight)
            {
                if (_currentState == MirrorState.Reflect)
                {
                    mirrorLight.intensity = 0.5f;
                    mirrorLight.color = Color.cyan;
                }
                else if (_currentState == MirrorState.Aperture)
                {
                    mirrorLight.intensity = 2f;
                    mirrorLight.color = Color.red;
                }

                // 警告闪烁
                if (_isWarning)
                {
                    float flash = Mathf.Sin(Time.time * 10f) * 0.5f + 0.5f;
                    mirrorLight.intensity *= (1f + flash * 0.5f);
                }
            }
        }

        private void OnWarningStart()
        {
            // 播放警告音效或特效
            if (shutterOpenEffectPrefab)
            {
                Instantiate(shutterOpenEffectPrefab, transform.position, Quaternion.identity);
            }
        }

        private void OnStateChanged()
        {
            // 状态切换时的特效
            if (_currentState == MirrorState.Aperture && shutterOpenEffectPrefab)
            {
                Instantiate(shutterOpenEffectPrefab, transform.position, Quaternion.identity);
            }
        }

        private void Overload()
        {
            _isOverloaded = true;

            // 镜碎特效
            if (shatterEffectPrefab)
            {
                Instantiate(shatterEffectPrefab, transform.position, Quaternion.identity);
            }

            // 创建镜片陷阱
            CreateTrapShards();

            // 死亡
            _redLightController.Set(0f);
        }

        private void CreateTrapShards()
        {
            if (trapShardPrefab)
            {
                for (int i = 0; i < trapShardCount; i++)
                {
                    float angle = (360f / trapShardCount) * i;
                    Vector3 position = transform.position + new Vector3(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad), 0) * 2f;
                    
                    var shard = Instantiate(trapShardPrefab, position, Quaternion.Euler(0, 0, angle));
                    _activeTrapShards.Add(shard);

                    // 设置陷阱属性
                    var trapScript = shard.GetComponent<MirrorTrapShard>();
                    if (trapScript)
                    {
                        trapScript.Initialize(trapShardDuration);
                    }
                    else
                    {
                        // 自动销毁
                        Destroy(shard, trapShardDuration);
                    }
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
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, reflectionRadius);
            
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, aimMinDistance);
        }
    }

    /// <summary>
    /// 镜片陷阱组件
    /// </summary>
    public class MirrorTrapShard : MonoBehaviour
    {
        public float duration = 2f;
        public float reflectionIntensity = 3f;
        public LayerMask lightLayers = ~0;
        
        private bool _hasReflected = false;
        private float _elapsedTime = 0f;

        public void Initialize(float duration)
        {
            this.duration = duration;
        }

        private void Update()
        {
            _elapsedTime += Time.deltaTime;
            if (_elapsedTime >= duration)
            {
                Destroy(gameObject);
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (_hasReflected) return;

            var light2D = other.GetComponent<Light2D>();
            if (light2D && light2D.intensity > 0.1f)
            {
                // 一次性强反射
                Vector2 lightDirection = (light2D.transform.position - transform.position).normalized;
                Vector2 mirrorNormal = transform.up;
                Vector2 reflectedDirection = Vector2.Reflect(lightDirection, mirrorNormal);

                // 创建强反射光束
                CreateStrongReflection(transform.position, reflectedDirection, reflectionIntensity);
                
                _hasReflected = true;
                
                // 立即销毁
                Destroy(gameObject);
            }
        }

        private void CreateStrongReflection(Vector3 origin, Vector2 direction, float intensity)
        {
            // 创建强反射光束
            Debug.DrawRay(origin, direction * 8f, Color.yellow, 0.2f);
        }
    }
}