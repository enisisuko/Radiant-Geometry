// BossC3_OrbSystem.cs
// 环绕体系统 - 负责环绕体的管理、旋转、动画和碰撞处理
// Unity 2021+ / Unity 6.2 兼容（URP/HDRP均可）

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using FD.Bosses.C3;

namespace FD.Bosses.C3
{
    /// <summary>
    /// BossC3环绕体系统 - 负责环绕体的管理、旋转、动画和碰撞处理
    /// </summary>
    [DisallowMultipleComponent]
    public class BossC3_OrbSystem : MonoBehaviour
    {
        [Header("== Orbs ==")]
        public List<GameObject> orbPrefabs = new List<GameObject>();
        public GameObject orbPrefab;
        public int maxOrbs = 10;
        public float orbSpeed = 2f;
        public float orbRadius = 2.8f;
        public bool autoBindOrbs = true;
        public float baseRadius = 2.8f;

        [Header("== Orb Animation ==")]
        public float rotationSpeed = 30f;
        public AnimationCurve radiusAnimationCurve = AnimationCurve.EaseInOut(0, 1, 1, 1);
        public float animationDuration = 2f;
        public bool autoRotate = true;

        [Header("== Orb Physics ==")]
        public float orbMass = 1f;
        public float orbDrag = 2f;
        public LayerMask orbCollisionMask = -1;

        [Header("== Orb Hit System ==")]
        [SerializeField] private GameObject orbHitExplosionVfx;
        [SerializeField] private float orbGhostAlpha = 0.35f;
        [SerializeField] private float orbRecallSpeed = 12f;
        [SerializeField] private float orbStunDuration = 2f;

        [Header("== Orb Knockdown System ==")]
        [SerializeField] private float orbKnockdownForce = 15f;
        [SerializeField] private float orbKnockdownDuration = 2f;

        [Header("== Terrain Clearance / Orbs ==")]
        [Tooltip("每个仍在环上的环绕体，半径内不希望出现地形的清场半径")]
        public float orbClearanceRadius = 3.5f;
        [Tooltip("清场推力的整体权重（越大越积极躲避）")]
        public float orbClearanceGain = 0.5f;
        [Tooltip("清场推力的上限（米/秒），避免过猛")]
        public float orbClearanceMax = 3f;
        [Tooltip("只对仍在环上的环绕体生效；乱飞/脱离时不计入")]
        public bool clearanceOnlyForAttached = true;

        [Header("== Debug ==")]
        public bool verboseLogs = true;
        public bool drawGizmos = true;

        // 环绕体管理器
        private PrefabOrbConductor _conductor;
        private BossC3_PhaseManager _phaseManager;
        private List<GameObject> _activeOrbs = new List<GameObject>();
        private List<OrbAgent> _orbAgents = new List<OrbAgent>();

        // 动画状态
        private float _currentRadius = 2.8f;
        private float _targetRadius = 2.8f;
        private float _radiusAnimationTime = 0f;
        private bool _isAnimatingRadius = false;

        // 旋转状态
        private float _currentRotation = 0f;
        private Coroutine _rotationCR;

        // 事件
        public event Action<int> OnOrbCountChanged;
        public event Action<GameObject> OnOrbHit;
        public event Action<GameObject> OnOrbKnockedDown;
        public event Action<GameObject> OnOrbRecalled;

        #region Unity Lifecycle

        private void Awake()
        {
            _conductor = GetComponent<PrefabOrbConductor>();
            if (_conductor == null)
            {
                _conductor = gameObject.AddComponent<PrefabOrbConductor>();
            }

            _phaseManager = GetComponent<BossC3_PhaseManager>();

            _currentRadius = baseRadius;
            _targetRadius = baseRadius;
        }

        private void Start()
        {
            if (autoBindOrbs)
            {
                AutoBindOrbPrefabs();
            }

            InitializeOrbs();
        }

        private void Update()
        {
            UpdateOrbRotation();
            UpdateRadiusAnimation();
            UpdateOrbClearance();
        }

        #endregion

        #region Orb Management

        /// <summary>
        /// 初始化环绕体
        /// </summary>
        private void InitializeOrbs()
        {
            if (verboseLogs)
                Debug.Log("[BossC3_OrbSystem] InitializeOrbs START");
                
            if (_conductor == null)
            {
                Debug.LogWarning("[BossC3_OrbSystem] Conductor is null, skipping initialization");
                return;
            }

            if (verboseLogs)
                Debug.Log($"[BossC3_OrbSystem] Setting up conductor with maxOrbs={maxOrbs}");
                
            try
            {
                _conductor.Setup(orbPrefab, maxOrbs, orbSpeed, orbRadius);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[BossC3_OrbSystem] Error in conductor setup: {e.Message}");
                return;
            }
            
            // 根据PhaseManager的当前阶段设置初始环绕体数量
            int initialOrbCount = 4; // 默认P1阶段4个
            if (_phaseManager != null)
            {
                try
                {
                    Phase currentPhase = _phaseManager.GetCurrentPhase();
                    initialOrbCount = currentPhase == Phase.P1 ? _phaseManager.p1Orbs : _phaseManager.p2Orbs;
                    
                    if (verboseLogs)
                        Debug.Log($"[BossC3_OrbSystem] Initializing with phase {currentPhase}: {initialOrbCount} orbs");
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[BossC3_OrbSystem] Error getting phase: {e.Message}");
                }
            }
            else
            {
                if (verboseLogs)
                    Debug.LogWarning("[BossC3_OrbSystem] PhaseManager is null, using default count");
            }
            
            if (verboseLogs)
                Debug.Log($"[BossC3_OrbSystem] Calling SetOrbCount({initialOrbCount})");
                
            SetOrbCount(initialOrbCount);
            
            if (verboseLogs)
                Debug.Log("[BossC3_OrbSystem] InitializeOrbs COMPLETE");
        }

        /// <summary>
        /// 设置环绕体数量
        /// </summary>
        public void SetOrbCount(int count)
        {
            if (_conductor == null) return;

            _conductor.SetOrbCount(count);
            OnOrbCountChanged?.Invoke(count);

            if (verboseLogs)
                Debug.Log($"[BossC3_OrbSystem] Set orb count to {count}");
        }

        /// <summary>
        /// 获取当前环绕体数量
        /// </summary>
        public int GetCurrentOrbCount()
        {
            return _conductor != null ? _conductor.OrbCount : 0;
        }

        /// <summary>
        /// 获取指定索引的环绕体
        /// </summary>
        public Transform GetOrb(int index)
        {
            if (_conductor != null && index >= 0 && index < _conductor.OrbCount)
            {
                return _conductor.GetOrb(index);
            }
            return null;
        }

        /// <summary>
        /// 获取所有活跃的环绕体
        /// </summary>
        public List<Transform> GetAllOrbs()
        {
            List<Transform> orbs = new List<Transform>();
            if (_conductor == null) return orbs;

            int count = GetCurrentOrbCount();
            for (int i = 0; i < count; i++)
            {
                Transform orb = GetOrb(i);
                if (orb != null)
                {
                    orbs.Add(orb);
                }
            }

            return orbs;
        }

        /// <summary>
        /// 自动绑定环绕体预制体
        /// </summary>
        private void AutoBindOrbPrefabs()
        {
            if (orbPrefabs.Count == 0)
            {
                Debug.LogWarning("[BossC3_OrbSystem] No orb prefabs assigned!");
                return;
            }

            if (verboseLogs)
                Debug.Log($"[BossC3_OrbSystem] Auto-binding {orbPrefabs.Count} orb prefabs");
        }

        #endregion

        #region Orb Animation

        /// <summary>
        /// 更新环绕体旋转
        /// </summary>
        private void UpdateOrbRotation()
        {
            if (!autoRotate) return;

            _currentRotation += rotationSpeed * Time.deltaTime;
            if (_currentRotation >= 360f)
            {
                _currentRotation -= 360f;
            }

            // 应用旋转到环绕体
            ApplyRotationToOrbs();
        }

        /// <summary>
        /// 应用旋转到环绕体
        /// </summary>
        private void ApplyRotationToOrbs()
        {
            List<Transform> orbs = GetAllOrbs();
            int count = orbs.Count;

            for (int i = 0; i < count; i++)
            {
                if (orbs[i] == null) continue;

                float angle = (_currentRotation + (360f / count) * i) * Mathf.Deg2Rad;
                Vector3 offset = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * _currentRadius;
                orbs[i].position = transform.position + offset;
            }
        }

        /// <summary>
        /// 设置旋转速度
        /// </summary>
        public void SetRotationSpeed(float speed)
        {
            rotationSpeed = speed;
        }

        /// <summary>
        /// 停止旋转
        /// </summary>
        public void StopRotation()
        {
            autoRotate = false;
        }

        /// <summary>
        /// 开始旋转
        /// </summary>
        public void StartRotation()
        {
            autoRotate = true;
        }

        #endregion

        #region Radius Animation

        /// <summary>
        /// 更新半径动画
        /// </summary>
        private void UpdateRadiusAnimation()
        {
            if (!_isAnimatingRadius) return;

            _radiusAnimationTime += Time.deltaTime;
            float t = _radiusAnimationTime / animationDuration;

            if (t >= 1f)
            {
                _currentRadius = _targetRadius;
                _isAnimatingRadius = false;
                _radiusAnimationTime = 0f;
            }
            else
            {
                float curveValue = radiusAnimationCurve.Evaluate(t);
                _currentRadius = Mathf.Lerp(_currentRadius, _targetRadius, curveValue);
            }

            // 更新环绕体位置
            ApplyRotationToOrbs();
        }

        /// <summary>
        /// 设置半径
        /// </summary>
        public void SetRadius(float radius, float duration = 0.25f)
        {
            _targetRadius = radius;
            animationDuration = duration;
            _radiusAnimationTime = 0f;
            _isAnimatingRadius = true;

            if (verboseLogs)
                Debug.Log($"[BossC3_OrbSystem] Setting radius to {radius} over {duration}s");
        }

        /// <summary>
        /// 聚集环绕体
        /// </summary>
        public void Gather(float scale, float duration = 0.25f)
        {
            float targetRadius = baseRadius * Mathf.Clamp(scale, 0.2f, 2.0f);
            SetRadius(targetRadius, duration);
        }

        /// <summary>
        /// 获取当前半径
        /// </summary>
        public float GetCurrentRadius()
        {
            return _currentRadius;
        }

        /// <summary>
        /// 获取目标半径
        /// </summary>
        public float GetTargetRadius()
        {
            return _targetRadius;
        }

        #endregion

        #region Orb Hit System

        /// <summary>
        /// 处理环绕体被击中
        /// </summary>
        public void HandleOrbHit(GameObject orb, BossColor hitColor, BossColor orbColor)
        {
            if (orb == null) return;

            // 检查颜色匹配
            if (hitColor == orbColor)
            {
                // 同色击中 - 造成伤害
                OnOrbHit?.Invoke(orb);
                PlayOrbHitEffect(orb);
                RecallOrb(orb);
            }
            else
            {
                // 异色击中 - 击飞环绕体
                KnockdownOrb(orb);
            }
        }

        /// <summary>
        /// 播放环绕体击中效果
        /// </summary>
        private void PlayOrbHitEffect(GameObject orb)
        {
            if (orbHitExplosionVfx != null)
            {
                GameObject effect = Instantiate(orbHitExplosionVfx, orb.transform.position, Quaternion.identity);
                Destroy(effect, 3f);
            }
        }

        /// <summary>
        /// 召回环绕体
        /// </summary>
        private void RecallOrb(GameObject orb)
        {
            if (orb == null) return;

            StartCoroutine(RecallOrbCoroutine(orb));
        }

        /// <summary>
        /// 召回环绕体协程
        /// </summary>
        private IEnumerator RecallOrbCoroutine(GameObject orb)
        {
            Vector3 startPos = orb.transform.position;
            Vector3 targetPos = transform.position;
            float distance = Vector3.Distance(startPos, targetPos);
            float duration = distance / orbRecallSpeed;

            float elapsed = 0f;
            while (elapsed < duration)
            {
                float t = elapsed / duration;
                orb.transform.position = Vector3.Lerp(startPos, targetPos, t);
                elapsed += Time.deltaTime;
                yield return null;
            }

            orb.transform.position = targetPos;
            OnOrbRecalled?.Invoke(orb);

            // 禁用AI一段时间
            OrbAgent agent = orb.GetComponent<OrbAgent>();
            if (agent != null)
            {
                agent.SetStunned(true);
                yield return new WaitForSeconds(orbStunDuration);
                agent.SetStunned(false);
            }
        }

        /// <summary>
        /// 击倒环绕体
        /// </summary>
        private void KnockdownOrb(GameObject orb)
        {
            if (orb == null) return;

            StartCoroutine(KnockdownOrbCoroutine(orb));
        }

        /// <summary>
        /// 击倒环绕体协程
        /// </summary>
        private IEnumerator KnockdownOrbCoroutine(GameObject orb)
        {
            // 应用击飞力
            Rigidbody2D rb2d = orb.GetComponent<Rigidbody2D>();
            Rigidbody rb3d = orb.GetComponent<Rigidbody>();

            Vector3 knockDirection = (orb.transform.position - transform.position).normalized;
            Vector3 knockForce = knockDirection * orbKnockdownForce;

            if (rb2d != null)
            {
                rb2d.AddForce(knockForce, ForceMode2D.Impulse);
            }
            else if (rb3d != null)
            {
                rb3d.AddForce(knockForce, ForceMode.Impulse);
            }

            OnOrbKnockedDown?.Invoke(orb);

            // 等待击倒时间
            yield return new WaitForSeconds(orbKnockdownDuration);

            // 召回环绕体
            RecallOrb(orb);
        }

        #endregion

        #region Orb Clearance

        /// <summary>
        /// 更新环绕体清场
        /// </summary>
        private void UpdateOrbClearance()
        {
            if (!clearanceOnlyForAttached) return;

            List<Transform> orbs = GetAllOrbs();
            foreach (Transform orb in orbs)
            {
                if (orb == null) continue;

                // 检查环绕体周围是否有地形
                Vector3 orbPos = orb.position;
                Collider2D hit2d = Physics2D.OverlapCircle(orbPos, orbClearanceRadius, orbCollisionMask);
                bool hit3d = Physics.CheckSphere(orbPos, orbClearanceRadius, orbCollisionMask);

                if (hit2d != null || hit3d)
                {
                    // 计算清场推力
                    Vector3 clearanceDirection = (orbPos - transform.position).normalized;
                    Vector3 clearanceForce = clearanceDirection * orbClearanceGain;

                    // 应用推力
                    Rigidbody2D rb2d = orb.GetComponent<Rigidbody2D>();
                    Rigidbody rb3d = orb.GetComponent<Rigidbody>();

                    if (rb2d != null)
                    {
                        Vector2 force2d = Vector2.ClampMagnitude(clearanceForce, orbClearanceMax);
                        rb2d.AddForce(force2d, ForceMode2D.Force);
                    }
                    else if (rb3d != null)
                    {
                        Vector3 force3d = Vector3.ClampMagnitude(clearanceForce, orbClearanceMax);
                        rb3d.AddForce(force3d, ForceMode.Force);
                    }
                }
            }
        }

        #endregion

        #region Orb Actions

        /// <summary>
        /// 向玩家发射所有环绕体
        /// </summary>
        public void FireAllOrbsAtPlayer(Transform player)
        {
            if (player == null) return;

            List<Transform> orbs = GetAllOrbs();
            foreach (Transform orb in orbs)
            {
                if (orb == null) continue;

                OrbAgent agent = orb.GetComponent<OrbAgent>();
                if (agent != null)
                {
                    agent.FireAtTarget(player);
                }
            }
        }

        /// <summary>
        /// 生成子弹扇形
        /// </summary>
        public void SpawnBulletFan(Vector3 origin, Vector3 toward, int count, float spreadDeg, float speedScale = 1f)
        {
            if (_conductor != null)
            {
                _conductor.SpawnBulletFan(origin, toward, count, spreadDeg, speedScale);
            }
        }

        /// <summary>
        /// 锁定环绕体
        /// </summary>
        public void SetOrbsLocked(bool locked)
        {
            if (_conductor != null)
            {
                _conductor.SetLocked(locked);
            }
        }

        #endregion

        #region Public API

        /// <summary>
        /// 获取环绕体系统状态
        /// </summary>
        public bool IsSystemActive()
        {
            return _conductor != null && GetCurrentOrbCount() > 0;
        }

        /// <summary>
        /// 重置环绕体系统
        /// </summary>
        public void ResetSystem()
        {
            _currentRadius = baseRadius;
            _targetRadius = baseRadius;
            _isAnimatingRadius = false;
            _radiusAnimationTime = 0f;
            _currentRotation = 0f;

            if (_conductor != null)
            {
                _conductor.SetOrbCount(0);
            }
        }

        /// <summary>
        /// 获取调试信息
        /// </summary>
        public string GetDebugInfo()
        {
            return $"Orbs: {GetCurrentOrbCount()}, Radius: {_currentRadius:F2}, Rotation: {_currentRotation:F1}°";
        }

        #endregion

        #region Gizmos

        private void OnDrawGizmos()
        {
            if (!drawGizmos) return;

            // 绘制环绕体轨道
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, _currentRadius);

            // 绘制清场半径
            Gizmos.color = Color.red;
            List<Transform> orbs = GetAllOrbs();
            foreach (Transform orb in orbs)
            {
                if (orb != null)
                {
                    Gizmos.DrawWireSphere(orb.position, orbClearanceRadius);
                }
            }
        }

        #endregion
    }
}
