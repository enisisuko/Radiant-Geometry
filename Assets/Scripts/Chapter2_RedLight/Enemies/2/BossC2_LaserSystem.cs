// BossC2_LaserSystem.cs
// 激光系统 - 负责追踪激光、旋镰扫射和激光地面碰撞检测
// Unity 2021+ / Unity 6.2 兼容（URP/HDRP均可）

using System;
using System.Collections;
using UnityEngine;

namespace FadedDreams.Bosses
{
    /// <summary>
    /// BossC2激光系统 - 负责追踪激光、旋镰扫射和激光地面碰撞检测
    /// </summary>
    [DisallowMultipleComponent]
    public class BossC2_LaserSystem : MonoBehaviour
    {
        [Header("== Laser Settings ==")]
        public LayerMask groundMask = -1;
        public LayerMask playerLayer = 1 << 8;
        public Material laserMaterial;
        public GameObject laserHitGroundVfx;

        [Header("== Phase 1 Lasers ==")]
        public Color laserColorP1 = new Color(1f, 0.3f, 0.2f, 1f);
        public float laserWidthP1 = 0.12f;
        public float homingLaserDurationP1 = 2.6f;
        public float homingLaserDrainPerSecP1 = 15f;
        public float homingLaserTurnRateDegP1 = 45f;
        public float scytheSweepSpanDegP1 = 120f;
        public float scytheSweepSecondsP1 = 1.8f;
        public float scytheSweepDrainPerSecP1 = 12f;

        [Header("== Phase 2 Lasers ==")]
        public Color laserColorP2 = new Color(1f, 0.1f, 0.1f, 1f);
        public float laserWidthP2 = 0.15f;
        public float homingLaserDurationP2 = 3.5f;
        public float homingLaserDrainPerSecP2 = 20f;
        public float homingLaserTurnRateDegP2 = 60f;
        public float scytheSweepSpanDegP2 = 150f;
        public float scytheSweepSecondsP2 = 1.2f;
        public float scytheSweepDrainPerSecP2 = 18f;

        [Header("== Laser Physics ==")]
        public float laserRange = 50f;
        public float laserDamage = 25f;
        public float laserKnockback = 5f;

        [Header("== Debug ==")]
        public bool verboseLogs = true;
        public bool drawGizmos = true;

        // 组件引用
        private BossC2_Core core;
        private BossC2_PhaseSystem phaseSystem;
        private BossC2_TorchSystem torchSystem;

        // 激光状态
        private LineRenderer _currentLaser;
        private bool _isLaserActive = false;
        private Coroutine _laserCoroutine;

        // 事件
        public event Action OnLaserStarted;
        public event Action OnLaserEnded;
        public event Action<Vector3> OnLaserHitGround;
        public event Action<Transform> OnLaserHitPlayer;

        #region Unity Lifecycle

        private void Awake()
        {
            core = GetComponent<BossC2_Core>();
            phaseSystem = GetComponent<BossC2_PhaseSystem>();
            torchSystem = GetComponent<BossC2_TorchSystem>();
        }

        private void Start()
        {
            // 创建激光材质（如果没有提供）
            if (laserMaterial == null)
            {
                CreateLaserMaterial();
            }
        }

        #endregion

        #region Laser Creation

        /// <summary>
        /// 创建激光材质
        /// </summary>
        private void CreateLaserMaterial()
        {
            laserMaterial = new Material(Shader.Find("Sprites/Default"));
            laserMaterial.color = Color.red;
            laserMaterial.SetFloat("_Mode", 3); // Transparent
            laserMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            laserMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            laserMaterial.SetInt("_ZWrite", 0);
            laserMaterial.DisableKeyword("_ALPHATEST_ON");
            laserMaterial.EnableKeyword("_ALPHABLEND_ON");
            laserMaterial.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            laserMaterial.renderQueue = 3000;

            if (verboseLogs)
                Debug.Log("[BossC2_LaserSystem] Created default laser material");
        }

        /// <summary>
        /// 创建激光LineRenderer
        /// </summary>
        private LineRenderer CreateLaserRenderer()
        {
            GameObject laserObj = new GameObject("LaserBeam");
            laserObj.transform.SetParent(transform);
            laserObj.transform.localPosition = Vector3.zero;

            LineRenderer lr = laserObj.AddComponent<LineRenderer>();
            lr.material = laserMaterial;
            lr.startWidth = GetLaserWidth();
            lr.endWidth = GetLaserWidth();
            lr.positionCount = 2;
            lr.useWorldSpace = true;
            lr.sortingOrder = 10;

            return lr;
        }

        #endregion

        #region Homing Laser

        /// <summary>
        /// 开始追踪激光
        /// </summary>
        public void StartHomingLaser()
        {
            if (_isLaserActive) return;

            if (verboseLogs)
                Debug.Log("[BossC2_LaserSystem] Starting homing laser");

            _laserCoroutine = StartCoroutine(CoHomingLaser());
        }

        /// <summary>
        /// 追踪激光协程
        /// </summary>
        private IEnumerator CoHomingLaser()
        {
            _isLaserActive = true;
            OnLaserStarted?.Invoke();

            // 获取激光参数
            float duration = GetHomingLaserDuration();
            float drainPerSec = GetHomingLaserDrainPerSec();
            float turnRateDeg = GetHomingLaserTurnRateDeg();
            Color color = GetLaserColor();
            float width = GetLaserWidth();

            // 创建激光
            _currentLaser = CreateLaserRenderer();
            _currentLaser.startColor = color;
            _currentLaser.endColor = color;
            _currentLaser.startWidth = width;
            _currentLaser.endWidth = width;

            // 设置初始方向
            Vector2 direction = Vector2.right;
            Transform player = core.GetPlayer();
            if (player != null)
            {
                direction = (player.position - transform.position).normalized;
            }

            float elapsed = 0f;
            while (elapsed < duration && _isLaserActive)
            {
                // 更新激光方向（追踪玩家）
                if (player != null)
                {
                    Vector2 targetDirection = (player.position - transform.position).normalized;
                    direction = Vector2.Lerp(direction, targetDirection, turnRateDeg * Time.deltaTime).normalized;
                }

                // 更新激光位置
                UpdateLaserBeam(direction);

                // 消耗能量
                if (torchSystem != null)
                {
                    torchSystem.ConsumeEnergy(drainPerSec * Time.deltaTime);
                }

                elapsed += Time.deltaTime;
                yield return null;
            }

            // 结束激光
            EndLaser();
        }

        #endregion

        #region Scythe Sweep

        /// <summary>
        /// 开始旋镰扫射
        /// </summary>
        public void StartScytheSweep()
        {
            if (_isLaserActive) return;

            if (verboseLogs)
                Debug.Log("[BossC2_LaserSystem] Starting scythe sweep");

            _laserCoroutine = StartCoroutine(CoScytheSweep());
        }

        /// <summary>
        /// 旋镰扫射协程
        /// </summary>
        private IEnumerator CoScytheSweep()
        {
            _isLaserActive = true;
            OnLaserStarted?.Invoke();

            // 获取扫射参数
            float spanDeg = GetScytheSweepSpanDeg();
            float seconds = GetScytheSweepSeconds();
            float drainPerSec = GetScytheSweepDrainPerSec();
            Color color = GetLaserColor();
            float width = GetLaserWidth();

            // 创建激光
            _currentLaser = CreateLaserRenderer();
            _currentLaser.startColor = color;
            _currentLaser.endColor = color;
            _currentLaser.startWidth = width;
            _currentLaser.endWidth = width;

            // 计算扫射角度
            float startAngle = -spanDeg / 2f;
            float endAngle = spanDeg / 2f;
            float angleStep = spanDeg / (seconds * 60f); // 假设60FPS

            float currentAngle = startAngle;
            float elapsed = 0f;

            while (elapsed < seconds && _isLaserActive)
            {
                // 计算当前方向
                Vector2 direction = new Vector2(Mathf.Cos(currentAngle * Mathf.Deg2Rad), Mathf.Sin(currentAngle * Mathf.Deg2Rad));

                // 更新激光位置
                UpdateLaserBeam(direction);

                // 更新角度
                currentAngle += angleStep * Time.deltaTime;

                // 消耗能量
                if (torchSystem != null)
                {
                    torchSystem.ConsumeEnergy(drainPerSec * Time.deltaTime);
                }

                elapsed += Time.deltaTime;
                yield return null;
            }

            // 结束激光
            EndLaser();
        }

        #endregion

        #region Laser Physics

        /// <summary>
        /// 更新激光束
        /// </summary>
        private void UpdateLaserBeam(Vector2 direction)
        {
            if (_currentLaser == null) return;

            Vector3 startPos = transform.position;
            Vector3 endPos = startPos + (Vector3)direction * laserRange;

            // 检测地面碰撞
            RaycastHit2D groundHit = Physics2D.Raycast(startPos, direction, laserRange, groundMask);
            if (groundHit.collider != null)
            {
                endPos = groundHit.point;
                OnLaserHitGround?.Invoke(endPos);

                // 播放地面击中特效
                if (laserHitGroundVfx != null)
                {
                    GameObject vfx = Instantiate(laserHitGroundVfx, endPos, Quaternion.identity);
                    Destroy(vfx, 2f);
                }
            }

            // 检测玩家碰撞
            RaycastHit2D playerHit = Physics2D.Raycast(startPos, direction, Vector3.Distance(startPos, endPos), playerLayer);
            if (playerHit.collider != null && playerHit.collider.CompareTag("Player"))
            {
                // 对玩家造成伤害
                OnLaserHitPlayer?.Invoke(playerHit.transform);

                // 这里可以添加玩家受伤逻辑
                // playerHit.collider.GetComponent<PlayerHealth>()?.TakeDamage(laserDamage);
            }

            // 更新激光位置
            _currentLaser.SetPosition(0, startPos);
            _currentLaser.SetPosition(1, endPos);
        }

        /// <summary>
        /// 结束激光
        /// </summary>
        private void EndLaser()
        {
            if (_currentLaser != null)
            {
                // 淡出激光
                StartCoroutine(FadeOutLaser());
            }

            _isLaserActive = false;
            OnLaserEnded?.Invoke();

            if (verboseLogs)
                Debug.Log("[BossC2_LaserSystem] Laser ended");
        }

        /// <summary>
        /// 激光淡出协程
        /// </summary>
        private IEnumerator FadeOutLaser()
        {
            if (_currentLaser == null) yield break;

            float fadeDuration = 0.5f;
            float elapsed = 0f;
            Color originalColor = _currentLaser.startColor;

            while (elapsed < fadeDuration)
            {
                float alpha = Mathf.Lerp(1f, 0f, elapsed / fadeDuration);
                Color fadeColor = originalColor;
                fadeColor.a = alpha;
                _currentLaser.startColor = fadeColor;
                _currentLaser.endColor = fadeColor;

                elapsed += Time.deltaTime;
                yield return null;
            }

            // 销毁激光
            if (_currentLaser != null)
            {
                Destroy(_currentLaser.gameObject);
                _currentLaser = null;
            }
        }

        #endregion

        #region Phase Parameters

        /// <summary>
        /// 获取激光颜色
        /// </summary>
        private Color GetLaserColor()
        {
            int currentPhase = phaseSystem != null ? phaseSystem.GetCurrentPhase() : 1;
            return (currentPhase == 1) ? laserColorP1 : laserColorP2;
        }

        /// <summary>
        /// 获取激光宽度
        /// </summary>
        private float GetLaserWidth()
        {
            int currentPhase = phaseSystem != null ? phaseSystem.GetCurrentPhase() : 1;
            return (currentPhase == 1) ? laserWidthP1 : laserWidthP2;
        }

        /// <summary>
        /// 获取追踪激光持续时间
        /// </summary>
        private float GetHomingLaserDuration()
        {
            int currentPhase = phaseSystem != null ? phaseSystem.GetCurrentPhase() : 1;
            return (currentPhase == 1) ? homingLaserDurationP1 : homingLaserDurationP2;
        }

        /// <summary>
        /// 获取追踪激光每秒消耗
        /// </summary>
        private float GetHomingLaserDrainPerSec()
        {
            int currentPhase = phaseSystem != null ? phaseSystem.GetCurrentPhase() : 1;
            return (currentPhase == 1) ? homingLaserDrainPerSecP1 : homingLaserDrainPerSecP2;
        }

        /// <summary>
        /// 获取追踪激光转向速率
        /// </summary>
        private float GetHomingLaserTurnRateDeg()
        {
            int currentPhase = phaseSystem != null ? phaseSystem.GetCurrentPhase() : 1;
            return (currentPhase == 1) ? homingLaserTurnRateDegP1 : homingLaserTurnRateDegP2;
        }

        /// <summary>
        /// 获取旋镰扫射角度
        /// </summary>
        private float GetScytheSweepSpanDeg()
        {
            int currentPhase = phaseSystem != null ? phaseSystem.GetCurrentPhase() : 1;
            return (currentPhase == 1) ? scytheSweepSpanDegP1 : scytheSweepSpanDegP2;
        }

        /// <summary>
        /// 获取旋镰扫射时间
        /// </summary>
        private float GetScytheSweepSeconds()
        {
            int currentPhase = phaseSystem != null ? phaseSystem.GetCurrentPhase() : 1;
            return (currentPhase == 1) ? scytheSweepSecondsP1 : scytheSweepSecondsP2;
        }

        /// <summary>
        /// 获取旋镰扫射每秒消耗
        /// </summary>
        private float GetScytheSweepDrainPerSec()
        {
            int currentPhase = phaseSystem != null ? phaseSystem.GetCurrentPhase() : 1;
            return (currentPhase == 1) ? scytheSweepDrainPerSecP1 : scytheSweepDrainPerSecP2;
        }

        #endregion

        #region Public API

        /// <summary>
        /// 检查激光是否激活
        /// </summary>
        public bool IsLaserActive() => _isLaserActive;

        /// <summary>
        /// 停止激光
        /// </summary>
        public void StopLaser()
        {
            if (_laserCoroutine != null)
            {
                StopCoroutine(_laserCoroutine);
                _laserCoroutine = null;
            }

            EndLaser();
        }

        /// <summary>
        /// 设置激光材质
        /// </summary>
        public void SetLaserMaterial(Material material)
        {
            laserMaterial = material;
        }

        /// <summary>
        /// 设置激光击中地面特效
        /// </summary>
        public void SetLaserHitGroundVfx(GameObject vfx)
        {
            laserHitGroundVfx = vfx;
        }

        /// <summary>
        /// 重置激光系统
        /// </summary>
        public void ResetLaserSystem()
        {
            StopLaser();

            if (verboseLogs)
                Debug.Log("[BossC2_LaserSystem] Laser system reset");
        }

        #endregion

        #region Debug

        /// <summary>
        /// 获取调试信息
        /// </summary>
        public string GetDebugInfo()
        {
            return $"Laser Active: {_isLaserActive}, Phase: {phaseSystem?.GetCurrentPhase() ?? 1}";
        }

        #endregion

        #region Gizmos

        private void OnDrawGizmos()
        {
            if (!drawGizmos) return;

            // 绘制激光范围
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, laserRange);

            // 绘制当前激光方向
            if (_isLaserActive && _currentLaser != null)
            {
                Gizmos.color = Color.yellow;
                Vector3 startPos = _currentLaser.GetPosition(0);
                Vector3 endPos = _currentLaser.GetPosition(1);
                Gizmos.DrawLine(startPos, endPos);
            }
        }

        #endregion
    }
}
