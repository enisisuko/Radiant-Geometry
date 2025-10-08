// BossC3_AttackSystem.cs
// 攻击系统 - 负责BOSS的各种攻击技能、子弹发射和攻击冷却管理
// Unity 2021+ / Unity 6.2 兼容（URP/HDRP均可）

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using FD.Bosses.C3;

namespace FD.Bosses.C3
{
    /// <summary>
    /// BossC3攻击系统 - 负责各种攻击技能、子弹发射和攻击冷却管理
    /// </summary>
    [DisallowMultipleComponent]
    public class BossC3_AttackSystem : MonoBehaviour
    {
        [Header("== Big Skill Scheduler ==")]
        public float bigSkillCooldownP1 = 22f;
        public float bigSkillCooldownP2 = 16f;

        [Header("== Micro Skill Concurrency ==")]
        public int p1MaxConcurrentMicros = 2;
        public int p2MaxConcurrentMicros = 3;

        [Header("== Boss Base Skills ==")]
        public float pulseCooldown = 7f;     // 指挥脉冲
        public float markCooldown = 9f;      // 点名延迟爆
        public float pulseRadius = 5.5f;
        public float pulseKnockSpeed = 3.2f;
        public float markDelay = 1.1f;
        public float markBlastRadius = 2.2f;
        public float markDamage = 12f;

        [Header("== Red Remote Fire ==")]
        public GameObject bulletPrefab;
        public Material bulletMaterial;
        public float bulletSpeed = 14f;
        public float bulletLifetime = 3.0f;
        public float bulletDamageMul = 1.0f;

        [Header("== Landing System ==")]
        public float landingRadiusAroundPlayer = 10f;
        public float minPlayerClearance = 1.0f;

        [Header("== Shockwave Bomb ==")]
        public GameObject shockwaveBombPrefab;
        public float shockwaveBombSpeed = 12f;
        public float shockwaveBombLifetime = 3f;

        [Header("== Debug ==")]
        public bool verboseLogs = true;

        // 组件引用
        private BossC3_PhaseManager phaseManager;
        private BossC3_OrbSystem orbSystem;
        private Transform player;

        // 攻击状态
        private bool _suppressMicros = false;
        private float _bigReadyAt = 0f;
        private int _nextBigIndex = 0;
        private int _concurrentMicros = 0;

        // 基础技能冷却
        private float _nextPulse = 0f;
        private float _nextMark = 0f;

        // 事件
        public event Action<BigIdP1> OnBigSkillP1Used;
        public event Action<BigIdP2> OnBigSkillP2Used;
        public event Action OnPulseUsed;
        public event Action OnMarkUsed;

        #region Unity Lifecycle

        private void Awake()
        {
            phaseManager = GetComponent<BossC3_PhaseManager>();
            orbSystem = GetComponent<BossC3_OrbSystem>();
        }

        private void Start()
        {
            // 查找玩家
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
            {
                player = playerObj.transform;
            }

            // 初始化攻击时间
            _bigReadyAt = Time.time;
        }

        private void Update()
        {
            TryPlayBaseSkills();
        }

        #endregion

        #region Base Skills

        /// <summary>
        /// 尝试使用基础技能
        /// </summary>
        private void TryPlayBaseSkills()
        {
            if (player == null) return;

            // 指挥脉冲
            if (Time.time >= _nextPulse)
            {
                StartCoroutine(DoCommandPulse());
                _nextPulse = Time.time + pulseCooldown;
            }

            // 点名延迟爆
            if (Time.time >= _nextMark)
            {
                StartCoroutine(DoMarkBeacon());
                _nextMark = Time.time + markCooldown;
            }
        }

        /// <summary>
        /// 执行指挥脉冲
        /// </summary>
        private IEnumerator DoCommandPulse()
        {
            if (verboseLogs)
                Debug.Log("[BossC3_AttackSystem] Using Command Pulse");

            // 创建脉冲效果
            Vector3 pulseCenter = transform.position;
            
            // 检测范围内的玩家
            Collider2D[] hits2d = Physics2D.OverlapCircleAll(pulseCenter, pulseRadius, LayerMask.GetMask("Player"));
            Collider[] hits3d = Physics.OverlapSphere(pulseCenter, pulseRadius, LayerMask.GetMask("Player"));

            List<Transform> affectedPlayers = new List<Transform>();
            
            foreach (var hit in hits2d)
            {
                if (hit.CompareTag("Player"))
                    affectedPlayers.Add(hit.transform);
            }
            
            foreach (var hit in hits3d)
            {
                if (hit.CompareTag("Player"))
                    affectedPlayers.Add(hit.transform);
            }

            // 对玩家施加击退效果
            foreach (Transform playerTransform in affectedPlayers)
            {
                Vector3 direction = (playerTransform.position - pulseCenter).normalized;
                Rigidbody2D rb2d = playerTransform.GetComponent<Rigidbody2D>();
                Rigidbody rb3d = playerTransform.GetComponent<Rigidbody>();

                if (rb2d != null)
                {
                    rb2d.AddForce(direction * pulseKnockSpeed, ForceMode2D.Impulse);
                }
                else if (rb3d != null)
                {
                    rb3d.AddForce(direction * pulseKnockSpeed, ForceMode.Impulse);
                }
            }

            OnPulseUsed?.Invoke();
            yield return null;
        }

        /// <summary>
        /// 执行点名延迟爆
        /// </summary>
        private IEnumerator DoMarkBeacon()
        {
            if (verboseLogs)
                Debug.Log("[BossC3_AttackSystem] Using Mark Beacon");

            // 在玩家位置创建标记
            Vector3 markPosition = player.position;
            
            // 等待延迟时间
            yield return new WaitForSeconds(markDelay);

            // 创建爆炸效果
            Collider2D[] hits2d = Physics2D.OverlapCircleAll(markPosition, markBlastRadius, LayerMask.GetMask("Player"));
            Collider[] hits3d = Physics.OverlapSphere(markPosition, markBlastRadius, LayerMask.GetMask("Player"));

            List<Transform> affectedPlayers = new List<Transform>();
            
            foreach (var hit in hits2d)
            {
                if (hit.CompareTag("Player"))
                    affectedPlayers.Add(hit.transform);
            }
            
            foreach (var hit in hits3d)
            {
                if (hit.CompareTag("Player"))
                    affectedPlayers.Add(hit.transform);
            }

            // 对玩家造成伤害
            foreach (Transform playerTransform in affectedPlayers)
            {
                // 这里应该调用玩家的受伤方法
                // playerTransform.GetComponent<PlayerHealth>()?.TakeDamage(markDamage);
            }

            OnMarkUsed?.Invoke();
        }

        #endregion

        #region Big Skills

        /// <summary>
        /// 播放大招
        /// </summary>
        public IEnumerator PlayBigSkill()
        {
            if (Time.time < _bigReadyAt) yield break;

            Phase currentPhase = phaseManager.GetCurrentPhase();
            
            if (currentPhase == Phase.P1)
            {
                BigIdP1 bigSkill = (BigIdP1)_nextBigIndex;
                yield return StartCoroutine(PlayBigP1(bigSkill));
                OnBigSkillP1Used?.Invoke(bigSkill);
                _nextBigIndex = (_nextBigIndex + 1) % Enum.GetValues(typeof(BigIdP1)).Length;
            }
            else if (currentPhase == Phase.P2)
            {
                BigIdP2 bigSkill = (BigIdP2)_nextBigIndex;
                yield return StartCoroutine(PlayBigP2(bigSkill));
                OnBigSkillP2Used?.Invoke(bigSkill);
                _nextBigIndex = (_nextBigIndex + 1) % Enum.GetValues(typeof(BigIdP2)).Length;
            }

            // 设置下次大招冷却时间
            float cooldown = (currentPhase == Phase.P1) ? bigSkillCooldownP1 : bigSkillCooldownP2;
            _bigReadyAt = Time.time + cooldown;
        }

        /// <summary>
        /// 播放P1阶段大招
        /// </summary>
        private IEnumerator PlayBigP1(BigIdP1 big)
        {
            if (verboseLogs)
                Debug.Log($"[BossC3_AttackSystem] Using P1 Big Skill: {big}");

            switch (big)
            {
                case BigIdP1.RingBurst:
                    yield return StartCoroutine(RingBurstAttack());
                    break;
                case BigIdP1.QuadrantMerge:
                    yield return StartCoroutine(QuadrantMergeAttack());
                    break;
            }
        }

        /// <summary>
        /// 播放P2阶段大招
        /// </summary>
        private IEnumerator PlayBigP2(BigIdP2 big)
        {
            if (verboseLogs)
                Debug.Log($"[BossC3_AttackSystem] Using P2 Big Skill: {big}");

            switch (big)
            {
                case BigIdP2.PrismSymphony:
                    yield return StartCoroutine(PrismSymphonyAttack());
                    break;
                case BigIdP2.FallingOrbit:
                    yield return StartCoroutine(FallingOrbitAttack());
                    break;
                case BigIdP2.ChromaReverse:
                    yield return StartCoroutine(ChromaReverseAttack());
                    break;
                case BigIdP2.FinalGeometry:
                    yield return StartCoroutine(FinalGeometryAttack());
                    break;
            }
        }

        #endregion

        #region P1 Big Skills Implementation

        /// <summary>
        /// 环爆攻击
        /// </summary>
        private IEnumerator RingBurstAttack()
        {
            // 聚集环绕体
            orbSystem.Gather(0.5f, 0.5f);
            yield return new WaitForSeconds(0.5f);

            // 爆发
            orbSystem.SetRadius(orbSystem.GetCurrentRadius() * 2f, 0.3f);
            yield return new WaitForSeconds(0.3f);

            // 发射所有环绕体
            orbSystem.FireAllOrbsAtPlayer(player);
            yield return new WaitForSeconds(1f);

            // 恢复
            orbSystem.SetRadius(orbSystem.baseRadius, 1f);
        }

        /// <summary>
        /// 象限合并攻击
        /// </summary>
        private IEnumerator QuadrantMergeAttack()
        {
            // 将环绕体分成四个象限
            List<Transform> orbs = orbSystem.GetAllOrbs();
            int quadrantSize = orbs.Count / 4;

            for (int i = 0; i < 4; i++)
            {
                Vector3 quadrantCenter = GetQuadrantCenter(i);
                
                // 移动该象限的环绕体到中心
                for (int j = 0; j < quadrantSize && i * quadrantSize + j < orbs.Count; j++)
                {
                    Transform orb = orbs[i * quadrantSize + j];
                    if (orb != null)
                    {
                        StartCoroutine(MoveOrbToPosition(orb, quadrantCenter, 0.5f));
                    }
                }
            }

            yield return new WaitForSeconds(1f);

            // 从象限中心发射子弹
            for (int i = 0; i < 4; i++)
            {
                Vector3 quadrantCenter = GetQuadrantCenter(i);
                Vector3 direction = (player.position - quadrantCenter).normalized;
                SpawnBulletFan(quadrantCenter, direction, 5, 30f);
            }
        }

        #endregion

        #region P2 Big Skills Implementation

        /// <summary>
        /// 棱镜交响攻击
        /// </summary>
        private IEnumerator PrismSymphonyAttack()
        {
            // 创建多个棱镜效果
            for (int i = 0; i < 6; i++)
            {
                float angle = i * 60f * Mathf.Deg2Rad;
                Vector3 position = transform.position + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * 5f;
                
                // 创建棱镜效果（这里简化实现）
                SpawnBulletFan(position, (player.position - position).normalized, 3, 20f);
                
                yield return new WaitForSeconds(0.2f);
            }
        }

        /// <summary>
        /// 坠落轨道攻击
        /// </summary>
        private IEnumerator FallingOrbitAttack()
        {
            // 环绕体向上移动
            orbSystem.SetRadius(orbSystem.GetCurrentRadius() * 1.5f, 1f);
            yield return new WaitForSeconds(1f);

            // 环绕体坠落
            orbSystem.SetRadius(orbSystem.baseRadius * 0.5f, 0.5f);
            yield return new WaitForSeconds(0.5f);

            // 爆发
            orbSystem.SetRadius(orbSystem.baseRadius * 2f, 0.3f);
        }

        /// <summary>
        /// 色彩反转攻击
        /// </summary>
        private IEnumerator ChromaReverseAttack()
        {
            // 反转颜色
            BossColor currentColor = phaseManager.GetCurrentColor();
            phaseManager.SetColor(phaseManager.GetOppositeColor());
            
            yield return new WaitForSeconds(1f);

            // 恢复颜色
            phaseManager.SetColor(currentColor);
        }

        /// <summary>
        /// 最终几何攻击
        /// </summary>
        private IEnumerator FinalGeometryAttack()
        {
            // 创建复杂的几何图案攻击
            for (int i = 0; i < 8; i++)
            {
                float angle = i * 45f * Mathf.Deg2Rad;
                Vector3 position = transform.position + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * 3f;
                
                SpawnBulletFan(position, (player.position - position).normalized, 2, 15f);
                
                yield return new WaitForSeconds(0.1f);
            }
        }

        #endregion

        #region Bullet System

        /// <summary>
        /// 生成子弹扇形
        /// </summary>
        public void SpawnBulletFan(Vector3 origin, Vector3 toward, int count, float spreadDeg, float speedScale = 1f)
        {
            if (bulletPrefab == null) return;

            float angleStep = spreadDeg / (count - 1);
            float startAngle = -spreadDeg / 2f;

            for (int i = 0; i < count; i++)
            {
                float angle = startAngle + i * angleStep;
                Vector3 direction = Quaternion.AngleAxis(angle, Vector3.forward) * toward;
                
                SpawnBullet(origin, direction, speedScale);
            }
        }

        /// <summary>
        /// 生成单个子弹
        /// </summary>
        private void SpawnBullet(Vector3 origin, Vector3 direction, float speedScale = 1f)
        {
            GameObject bullet = Instantiate(bulletPrefab, origin, Quaternion.LookRotation(direction));
            
            // 设置子弹属性
            BulletDamage bulletDamage = bullet.GetComponent<BulletDamage>();
            if (bulletDamage != null)
            {
                bulletDamage.Setup(this, LayerMask.GetMask("Player"), GetComponent<BossC3_Core>().defaultDamage * bulletDamageMul, true);
            }

            // 设置子弹物理
            Rigidbody2D rb2d = bullet.GetComponent<Rigidbody2D>();
            Rigidbody rb3d = bullet.GetComponent<Rigidbody>();

            Vector3 velocity = direction.normalized * bulletSpeed * speedScale;

            if (rb2d != null)
            {
                rb2d.linearVelocity = velocity;
            }
            else if (rb3d != null)
            {
                rb3d.linearVelocity = velocity;
            }

            // 设置子弹材质
            if (bulletMaterial != null)
            {
                Renderer bulletRenderer = bullet.GetComponent<Renderer>();
                if (bulletRenderer != null)
                {
                    bulletRenderer.material = bulletMaterial;
                }
            }

            // 销毁子弹
            Destroy(bullet, bulletLifetime);
        }

        #endregion

        #region Shockwave Bomb

        /// <summary>
        /// 设置冲击波炸弹
        /// </summary>
        public void SetShockwaveBomb(GameObject prefab, float speed = 12f, float lifetime = 3f)
        {
            shockwaveBombPrefab = prefab;
            shockwaveBombSpeed = speed;
            shockwaveBombLifetime = lifetime;
        }

        /// <summary>
        /// 生成冲击波炸弹
        /// </summary>
        public void SpawnShockwaveBomb(Vector3 origin, Vector3 dirNorm)
        {
            if (shockwaveBombPrefab == null) return;

            GameObject bomb = Instantiate(shockwaveBombPrefab, origin, Quaternion.LookRotation(dirNorm));
            
            // 设置炸弹物理
            Rigidbody2D rb2d = bomb.GetComponent<Rigidbody2D>();
            Rigidbody rb3d = bomb.GetComponent<Rigidbody>();

            Vector3 velocity = dirNorm * shockwaveBombSpeed;

            if (rb2d != null)
            {
                rb2d.linearVelocity = velocity;
            }
            else if (rb3d != null)
            {
                rb3d.linearVelocity = velocity;
            }

            // 销毁炸弹
            Destroy(bomb, shockwaveBombLifetime);
        }

        #endregion

        #region Utility Methods

        /// <summary>
        /// 获取象限中心
        /// </summary>
        private Vector3 GetQuadrantCenter(int quadrant)
        {
            float angle = quadrant * 90f * Mathf.Deg2Rad;
            return transform.position + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * 3f;
        }

        /// <summary>
        /// 移动环绕体到指定位置
        /// </summary>
        private IEnumerator MoveOrbToPosition(Transform orb, Vector3 targetPosition, float duration)
        {
            Vector3 startPosition = orb.position;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                float t = elapsed / duration;
                orb.position = Vector3.Lerp(startPosition, targetPosition, t);
                elapsed += Time.deltaTime;
                yield return null;
            }

            orb.position = targetPosition;
        }

        #endregion

        #region Public API

        /// <summary>
        /// 获取微技能并发限制
        /// </summary>
        public int GetMicroConcurrencyLimit()
        {
            return phaseManager.GetPhaseInt(p1MaxConcurrentMicros, p2MaxConcurrentMicros);
        }

        /// <summary>
        /// 获取当前微技能并发数
        /// </summary>
        public int GetMicroConcurrencyNow()
        {
            return _concurrentMicros;
        }

        /// <summary>
        /// 检查微技能是否被抑制
        /// </summary>
        public bool IsMicrosSuppressed()
        {
            return _suppressMicros;
        }

        /// <summary>
        /// 设置微技能抑制
        /// </summary>
        public void SetMicrosSuppressed(bool suppressed)
        {
            _suppressMicros = suppressed;
        }

        /// <summary>
        /// 检查大招是否准备就绪
        /// </summary>
        public bool IsBigSkillReady()
        {
            return Time.time >= _bigReadyAt;
        }

        /// <summary>
        /// 获取大招冷却时间
        /// </summary>
        public float GetBigSkillCooldown()
        {
            return Mathf.Max(0f, _bigReadyAt - Time.time);
        }

        #endregion
    }
}
