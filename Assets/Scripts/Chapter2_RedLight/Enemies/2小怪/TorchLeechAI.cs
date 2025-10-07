using UnityEngine;
using UnityEngine.Rendering.Universal;
using System.Collections;
using FadedDreams.Enemies;
using FadedDreams.Player;
using FadedDreams.World;

namespace FadedDreams.Enemies
{
    /// <summary>
    /// 火吸蚀虫 - 会抢火：靠近火把、吸取强度，把火焰"拖移"到身侧当移动电源
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    [RequireComponent(typeof(EnemyHealth))]
    public class TorchLeechAI : MonoBehaviour, IDamageable
    {
        [Header("Refs")]
        public Transform player;
        public Rigidbody2D rb;
        public SpriteRenderer bodyRenderer;
        public Light2D auraLight;

        [Header("Torch Seeking")]
        public float seekRadius = 12f;
        public float siphonRadius = 2f;
        public LayerMask torchLayers = ~0;

        [Header("Siphon")]
        [Tooltip("抽取速率 (每秒)")]
        public float siphonRate = 15f;
        [Tooltip("牵引距离上限")]
        public float maxLeashDistance = 1f;
        [Tooltip("牵引阻尼 (回位速度)")]
        public float leashDamping = 2f;

        [Header("Heat Wave")]
        [Tooltip("热浪吐射间隔")]
        public float heatWaveInterval = 2f;
        [Tooltip("热浪弹能量")]
        public float heatWaveEnergy = 20f;
        [Tooltip("热浪弹预制体")]
        public GameObject heatWavePrefab;

        [Header("Interrupt")]
        [Tooltip("打断门槛 (被激光多少能量)")]
        public float interruptThreshold = 50f;
        [Tooltip("硬直时间")]
        public float stunDuration = 1.5f;
        [Tooltip("烬火预制体")]
        public GameObject emberPrefab;

        [Header("Overload")]
        [Tooltip("过载阈值")]
        public float overloadThreshold = 100f;
        [Tooltip("火洼半径")]
        public float firePitRadius = 3f;
        [Tooltip("火洼持续时间")]
        public float firePitDuration = 8f;
        [Tooltip("火洼强度")]
        public float firePitIntensity = 1.5f;
        [Tooltip("火洼预制体")]
        public GameObject firePitPrefab;

        [Header("Visual")]
        [Tooltip("牵引光带预制体")]
        public GameObject leashLinePrefab;
        [Tooltip("体色脉冲速度")]
        public float pulseSpeed = 3f;

        [Header("Combat")]
        public float maxHp = 80f;
        public float currentCharge = 0f;

        // 内部状态
        private LightSource2D _targetTorch;
        private bool _isSiphoning = false;
        private bool _isStunned = false;
        private bool _isOverloaded = false;
        private Vector3 _originalTorchPosition;
        private GameObject _leashLine;
        private float _lastHeatWaveTime = 0f;
        private float _interruptAccumulator = 0f;

        // 组件引用
        private EnemyHealth _health;
        private Collider2D _collider;

        private void Awake()
        {
            _health = GetComponent<EnemyHealth>();
            _collider = GetComponent<Collider2D>();
            _health.maxHp = maxHp;
        }

        private void Start()
        {
            if (!player)
                player = FindFirstObjectByType<PlayerController2D>()?.transform;
        }

        private void Update()
        {
            if (_isStunned || _isOverloaded) return;

            // 寻找火把
            if (!_isSiphoning)
            {
                SeekTorch();
            }
            else
            {
                SiphonTorch();
            }

            // 更新视觉效果
            UpdateVisuals();

            // 检查过载
            if (currentCharge >= overloadThreshold && !_isOverloaded)
            {
                Overload();
            }
        }

        private void SeekTorch()
        {
            // 寻找最近的火把
            Collider2D[] torches = Physics2D.OverlapCircleAll(transform.position, seekRadius, torchLayers);
            LightSource2D closestTorch = null;
            float closestDistance = float.MaxValue;

            foreach (var torch in torches)
            {
                var lightSource = torch.GetComponent<LightSource2D>();
                if (lightSource && lightSource.ProvidesLight(0.1f))
                {
                    float distance = Vector2.Distance(transform.position, torch.transform.position);
                    if (distance < closestDistance)
                    {
                        closestDistance = distance;
                        closestTorch = lightSource;
                    }
                }
            }

            if (closestTorch)
            {
                // 移动到火把附近
                Vector2 direction = (closestTorch.transform.position - transform.position).normalized;
                rb.linearVelocity = direction * 3f;

                // 进入吸取范围
                if (closestDistance <= siphonRadius)
                {
                    StartSiphon(closestTorch);
                }
            }
        }

        private void StartSiphon(LightSource2D torch)
        {
            _targetTorch = torch;
            _isSiphoning = true;
            _originalTorchPosition = torch.transform.position;

            // 创建牵引光带
            if (leashLinePrefab)
            {
                _leashLine = Instantiate(leashLinePrefab);
                _leashLine.transform.SetParent(transform);
            }
        }

        private void SiphonTorch()
        {
            if (!_targetTorch || !_targetTorch.ProvidesLight(0.1f))
            {
                StopSiphon();
                return;
            }

            // 抽取能量
            float siphonAmount = siphonRate * Time.deltaTime;
            _targetTorch.currentEnergy = Mathf.Max(0, _targetTorch.currentEnergy - siphonAmount);
            currentCharge += siphonAmount;

            // 牵引火焰
            Vector2 leashDirection = (transform.position - _targetTorch.transform.position).normalized;
            float leashDistance = Vector2.Distance(transform.position, _targetTorch.transform.position);
            
            if (leashDistance > maxLeashDistance)
            {
                Vector2 targetPos = (Vector2)transform.position - leashDirection * maxLeashDistance;
                _targetTorch.transform.position = Vector2.Lerp(_targetTorch.transform.position, targetPos, leashDamping * Time.deltaTime);
            }

            // 更新牵引光带
            UpdateLeashLine();

            // 吐射热浪
            if (Time.time - _lastHeatWaveTime >= heatWaveInterval)
            {
                SpawnHeatWave();
                _lastHeatWaveTime = Time.time;
            }
        }

        private void StopSiphon()
        {
            _isSiphoning = false;
            
            if (_targetTorch)
            {
                // 火焰回弹
                StartCoroutine(ReturnTorchToOriginalPosition());
            }

            // 销毁牵引光带
            if (_leashLine)
            {
                Destroy(_leashLine);
                _leashLine = null;
            }

            _targetTorch = null;
        }

        private IEnumerator ReturnTorchToOriginalPosition()
        {
            if (!_targetTorch) yield break;

            Vector3 startPos = _targetTorch.transform.position;
            float duration = 0.5f;
            float elapsed = 0f;

            while (elapsed < duration && _targetTorch)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                _targetTorch.transform.position = Vector3.Lerp(startPos, _originalTorchPosition, t);
                yield return null;
            }

            if (_targetTorch)
            {
                _targetTorch.transform.position = _originalTorchPosition;
            }
        }

        private void UpdateLeashLine()
        {
            if (_leashLine && _targetTorch)
            {
                // 更新光带位置和方向
                Vector3 midPoint = Vector3.Lerp(transform.position, _targetTorch.transform.position, 0.5f);
                _leashLine.transform.position = midPoint;
                
                Vector3 direction = _targetTorch.transform.position - transform.position;
                _leashLine.transform.rotation = Quaternion.LookRotation(Vector3.forward, direction);
                _leashLine.transform.localScale = new Vector3(1f, direction.magnitude, 1f);
            }
        }

        private void SpawnHeatWave()
        {
            if (heatWavePrefab && player)
            {
                Vector2 direction = (player.position - transform.position).normalized;
                Vector3 spawnPos = transform.position + (Vector3)direction * 1f;
                
                var heatWave = Instantiate(heatWavePrefab, spawnPos, Quaternion.identity);
                
                // 设置热浪弹属性
                var heatWaveScript = heatWave.GetComponent<HeatWaveProjectile>();
                if (heatWaveScript)
                {
                    heatWaveScript.Initialize(direction, heatWaveEnergy);
                }
            }
        }

        private void UpdateVisuals()
        {
            // 体色脉冲
            if (bodyRenderer)
            {
                float pulse = Mathf.Sin(Time.time * pulseSpeed) * 0.5f + 0.5f;
                Color baseColor = Color.Lerp(Color.gray, Color.red, currentCharge / overloadThreshold);
                bodyRenderer.color = Color.Lerp(baseColor, Color.white, pulse * 0.3f);
            }

            // 光环效果
            if (auraLight)
            {
                auraLight.intensity = Mathf.Lerp(0.5f, 2f, currentCharge / overloadThreshold);
                auraLight.color = Color.Lerp(Color.red, Color.orange, currentCharge / overloadThreshold);
            }
        }

        public void OnLaserHit(float energy)
        {
            if (_isStunned) return;

            _interruptAccumulator += energy;
            
            if (_interruptAccumulator >= interruptThreshold)
            {
                Interrupt();
            }
        }

        private void Interrupt()
        {
            _isStunned = true;
            _interruptAccumulator = 0f;
            
            // 停止吸取
            StopSiphon();
            
            // 洒落烬火
            SpawnEmbers();
            
            // 硬直恢复
            StartCoroutine(RecoverFromStun());
        }

        private IEnumerator RecoverFromStun()
        {
            yield return new WaitForSeconds(stunDuration);
            _isStunned = false;
        }

        private void SpawnEmbers()
        {
            if (emberPrefab)
            {
                for (int i = 0; i < 3; i++)
                {
                    Vector2 randomPos = (Vector2)transform.position + Random.insideUnitCircle * 2f;
                    var ember = Instantiate(emberPrefab, randomPos, Quaternion.identity);
                    
                    // 设置烬火属性
                    var light2D = ember.GetComponent<Light2D>();
                    if (light2D)
                    {
                        light2D.intensity = 1f;
                        light2D.color = Color.orange;
                    }
                    
                    // 自动销毁
                    Destroy(ember, 3f);
                }
            }
        }

        private void Overload()
        {
            _isOverloaded = true;
            
            // 停止吸取
            StopSiphon();
            
            // 创建火洼
            CreateFirePit();
            
            // 死亡
            _health.TakeDamage(_health.maxHp);
        }

        private void CreateFirePit()
        {
            if (firePitPrefab)
            {
                var firePit = Instantiate(firePitPrefab, transform.position, Quaternion.identity);
                
                // 设置火洼属性
                var light2D = firePit.GetComponent<Light2D>();
                if (light2D)
                {
                    light2D.intensity = firePitIntensity;
                    light2D.pointLightOuterRadius = firePitRadius;
                    light2D.color = Color.red;
                }
                
                // 自动销毁
                Destroy(firePit, firePitDuration);
            }
        }

        public void TakeDamage(float amount)
        {
            _health.TakeDamage(amount);
        }

        public bool IsDead => _health.IsDead;

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(transform.position, seekRadius);
            
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, siphonRadius);
            
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, firePitRadius);
        }
    }

    /// <summary>
    /// 热浪弹组件
    /// </summary>
    public class HeatWaveProjectile : MonoBehaviour
    {
        public float speed = 5f;
        public float lifetime = 1f;
        public float energy = 20f;
        
        private Vector2 _direction;
        private float _elapsedTime = 0f;

        public void Initialize(Vector2 direction, float energy)
        {
            _direction = direction.normalized;
            this.energy = energy;
        }

        private void Update()
        {
            transform.position += (Vector3)(_direction * speed * Time.deltaTime);
            
            _elapsedTime += Time.deltaTime;
            if (_elapsedTime >= lifetime)
            {
                Destroy(gameObject);
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            // 对玩家造成伤害或给敌人充能
            if (other.CompareTag("Player"))
            {
                // 对玩家造成轻微伤害
                var playerHealth = other.GetComponent<IDamageable>();
                if (playerHealth != null)
                {
                    playerHealth.TakeDamage(5f);
                }
            }
            else if (other.CompareTag("Enemy"))
            {
                // 给敌人充能
                var enemy = other.GetComponent<TorchLeechAI>();
                if (enemy)
                {
                    enemy.currentCharge += energy * 0.5f;
                }
            }
            
            Destroy(gameObject);
        }
    }
}