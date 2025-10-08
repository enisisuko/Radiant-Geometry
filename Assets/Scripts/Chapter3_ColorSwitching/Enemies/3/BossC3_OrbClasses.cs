using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace FD.Bosses.C3
{
    public class PrefabOrbConductor : MonoBehaviour
    {
        [Header("Orb Conductor Settings")]
        public GameObject orbPrefab;
        public int maxOrbs = 10;
        public float spawnRadius = 5f;
        public float orbSpeed = 2f;
        public float baseRadius = 2.8f;
        
        private List<Transform> orbs = new List<Transform>();
        private List<OrbAgent> agents = new List<OrbAgent>();
        private List<bool> detached = new List<bool>();
        private bool isLocked = false;
        private float currentRadius = 2.8f;
        private float idleSpinDegPerSec = 32f;
        private float spinDegPerSec = 0f;
        private float spinAngle = 0f;
        private bool lineMode = false;
        private float lineAngle = 0f;
        
        // 调度队列
        private List<LaunchSchedule> schedule = new List<LaunchSchedule>();
        
        struct LaunchSchedule 
        { 
            public int idx; 
            public float delay; 
            public float fly; 
            public float arc; 
            public Transform target; 
        }
        
        public void Setup(GameObject prefab, int maxCount, float radius, float speed)
        {
            orbPrefab = prefab;
            maxOrbs = maxCount;
            baseRadius = radius;
            currentRadius = radius;
            orbSpeed = speed;
        }
        
        // === All-In-One 兼容接口 ===
        
        public int OrbCount => orbs.Count;
        
        public Transform GetOrb(int index)
        {
            if (index >= 0 && index < orbs.Count)
                return orbs[index];
            return null;
        }
        
        public void SetLocked(bool locked)
        {
            isLocked = locked;
        }
        
        public void SetOrbCount(int count)
        {
            count = Mathf.Max(0, count);
            
            // 减少环绕体
            while (orbs.Count > count)
            {
                if (orbs.Count > 0)
                {
                    Transform orb = orbs[orbs.Count - 1];
                    orbs.RemoveAt(orbs.Count - 1);
                    agents.RemoveAt(agents.Count - 1);
                    detached.RemoveAt(detached.Count - 1);
                    
                    if (orb != null)
                        Destroy(orb.gameObject);
                }
            }
            
            // 增加环绕体
            while (orbs.Count < count && orbs.Count < maxOrbs)
            {
                SpawnOrb();
            }
            
            // 重新布局
            LayoutRingInstant();
        }
        
        public void SetRadius(float r, float dur = 0.25f)
        {
            currentRadius = Mathf.Max(0.1f, r);
            LayoutRingInstant();
        }
        
        public void Gather(float scale, float dur = 0.25f)
        {
            SetRadius(baseRadius * Mathf.Clamp(scale, 0.2f, 2.0f), dur);
        }
        
        public void Spread(float scale, float dur = 0.25f)
        {
            SetRadius(baseRadius * Mathf.Clamp(scale, 0.2f, 3.0f), dur);
        }
        
        public void Spin(float degPerSec)
        {
            spinDegPerSec = degPerSec;
        }
        
        public void SetIdleSpin(float degPerSec)
        {
            idleSpinDegPerSec = degPerSec;
        }
        
        public void Pulse(float time, float scale = 1.1f)
        {
            float to = currentRadius * scale;
            SetRadius(to, time * 0.5f);
        }
        
        public void AimAt(Transform t)
        {
            // 占位实现
        }
        
        public void Telegraph(float intensity, float dur)
        {
            foreach (var agent in agents)
            {
                if (agent != null)
                    agent.SetState(OrbAgent.State.Telegraph, intensity);
            }
        }
        
        public void AttackOn(float dmgMul = 1f, bool ignoreColorGate = false)
        {
            foreach (var agent in agents)
            {
                if (agent != null)
                    agent.SetState(OrbAgent.State.Attack, dmgMul);
            }
            
            spinDegPerSec = Mathf.Max(spinDegPerSec, idleSpinDegPerSec + 15f);
        }
        
        public void AttackOff()
        {
            foreach (var agent in agents)
            {
                if (agent != null)
                    agent.SetState(OrbAgent.State.Idle, 1f);
            }
            
            ClearPending();
        }
        
        public void DashToward(Transform t, float speed, float time)
        {
            // 占位实现
        }
        
        public void PreBlendColor(BossColor color, float dur)
        {
            Color c = (color == BossColor.Red) ? new Color(1, 0.25f, 0.25f) : new Color(0.25f, 1, 0.25f);
            foreach (var agent in agents)
            {
                if (agent != null)
                    agent.SetTint(c, 2.2f);
            }
        }
        
        public void FormLine(float angleDeg, float length, float spacing, float dur)
        {
            lineMode = true;
            lineAngle = angleDeg;
        }
        
        public void ExitLineMode(float relayoutDur = 0.35f)
        {
            lineMode = false;
            SetRadius(currentRadius, relayoutDur);
        }
        
        public void SweepLine(float degPerSec)
        {
            // 占位实现
        }
        
        public void LaunchFractionAtTarget(Transform target, float fraction, float flyTime, float arc, float stagger, System.Func<float, float> ease)
        {
            if (target == null) return;
            
            int count = Mathf.CeilToInt(orbs.Count * fraction);
            for (int i = 0; i < count && i < orbs.Count; i++)
            {
                schedule.Add(new LaunchSchedule
                {
                    idx = i,
                    delay = i * stagger,
                    fly = flyTime,
                    arc = arc,
                    target = target
                });
            }
        }
        
        public bool IsDetached(int idx)
        {
            return (idx >= 0 && idx < detached.Count) ? detached[idx] : false;
        }
        
        public int DetachedCount()
        {
            int count = 0;
            for (int i = 0; i < detached.Count; i++)
            {
                if (detached[i]) count++;
            }
            return count;
        }
        
        public void LaunchSingle(int idx, Transform target, float flyTime, float arc, System.Func<float, float> ease = null)
        {
            if (idx >= 0 && idx < orbs.Count && target != null)
            {
                detached[idx] = true;
                // 这里应该实现真正的发射逻辑
            }
        }
        
        public void RecallSingle(int idx, float recallTime = 0.5f)
        {
            if (idx >= 0 && idx < orbs.Count)
            {
                detached[idx] = false;
                // 这里应该实现真正的召回逻辑
            }
        }
        
        public void RecallAll(float recallTime = 0.6f, System.Func<float, float> ease = null)
        {
            for (int i = 0; i < orbs.Count; i++)
            {
                detached[i] = false;
            }
        }
        
        public void ClearPending()
        {
            schedule.Clear();
        }
        
        public void SetOrbTint(int idx, Color color, float emissionIntensity = 0f)
        {
            if (idx >= 0 && idx < agents.Count && agents[idx] != null)
            {
                agents[idx].SetTint(color, emissionIntensity);
            }
        }
        
        public void FireAllOrbsAtPlayerOnce()
        {
            // 占位实现 - 每个环绕体朝玩家发射一枚子弹
            foreach (var orb in orbs)
            {
                if (orb != null)
                {
                    // 这里需要实现发射逻辑
                }
            }
        }
        
        public IEnumerator RedAssaultBurst(Transform player, float duration, float fractionPerVolley, float flyTime, float arc, float gap, float recallTime)
        {
            // 占位实现 - 红色突击爆发
            float elapsed = 0f;
            while (elapsed < duration)
            {
                LaunchFractionAtTarget(player, fractionPerVolley, flyTime, arc, 0.1f, null);
                yield return new WaitForSeconds(gap);
                elapsed += gap;
            }
        }
        
        public void Tick(float dt)
        {
            // 处理旋转
            if (!isLocked)
            {
                float degToAdd = (spinDegPerSec != 0f ? spinDegPerSec : idleSpinDegPerSec) * dt;
                spinAngle += degToAdd;
                
                if (!lineMode)
                {
                    LayoutRingWithSpin(spinAngle);
                }
            }
            
            // 处理调度队列
            for (int i = schedule.Count - 1; i >= 0; i--)
            {
                var s = schedule[i];
                s.delay -= dt;
                
                if (s.delay <= 0f)
                {
                    if (s.idx >= 0 && s.idx < orbs.Count)
                    {
                        LaunchSingle(s.idx, s.target, s.fly, s.arc);
                    }
                    schedule.RemoveAt(i);
                }
                else
                {
                    schedule[i] = s;
                }
            }
        }
        
        public void SpawnBulletFan(Vector3 origin, Vector3 toward, int count, float spreadDeg, float speedScale = 1f)
        {
            for (int i = 0; i < count; i++)
            {
                float angle = (i - count / 2f) * spreadDeg / count;
                Vector3 shootDir = Quaternion.AngleAxis(angle, Vector3.forward) * toward.normalized;
                SpawnBullet(origin, shootDir, speedScale);
            }
        }
        
        // === 内部方法 ===
        
        private void SpawnOrb()
        {
            if (orbPrefab != null)
            {
                GameObject orb = Instantiate(orbPrefab, transform.position, Quaternion.identity, transform);
                Transform tr = orb.transform;
                
                OrbAgent agent = tr.GetComponent<OrbAgent>();
                if (agent == null)
                    agent = orb.AddComponent<OrbAgent>();
                
                agent.Setup(Vector3.right, orbSpeed);
                
                orbs.Add(tr);
                agents.Add(agent);
                detached.Add(false);
            }
        }
        
        private void SpawnBullet(Vector3 position, Vector3 direction, float speedScale = 1f)
        {
            if (orbPrefab != null)
            {
                GameObject bullet = Instantiate(orbPrefab, position, Quaternion.LookRotation(Vector3.forward, direction));
                
                Rigidbody2D rb2d = bullet.GetComponent<Rigidbody2D>();
                Rigidbody rb3d = bullet.GetComponent<Rigidbody>();
                
                Vector3 velocity = direction.normalized * orbSpeed * speedScale;
                
                if (rb2d != null)
                {
                    rb2d.linearVelocity = velocity;
                }
                else if (rb3d != null)
                {
                    rb3d.linearVelocity = velocity;
                }
                
                Destroy(bullet, 3f);
            }
        }
        
        private void LayoutRingInstant()
        {
            for (int i = 0; i < orbs.Count; i++)
            {
                if (orbs[i] != null && !detached[i])
                {
                    float ang = (360f / Mathf.Max(1, orbs.Count)) * i * Mathf.Deg2Rad;
                    Vector3 pos = new Vector3(Mathf.Cos(ang), Mathf.Sin(ang), 0) * currentRadius;
                    orbs[i].localPosition = pos;
                }
            }
        }
        
        private void LayoutRingWithSpin(float angleOffset)
        {
            for (int i = 0; i < orbs.Count; i++)
            {
                if (orbs[i] != null && !detached[i])
                {
                    float ang = ((360f / Mathf.Max(1, orbs.Count)) * i + angleOffset) * Mathf.Deg2Rad;
                    Vector3 pos = new Vector3(Mathf.Cos(ang), Mathf.Sin(ang), 0) * currentRadius;
                    orbs[i].localPosition = pos;
                }
            }
        }
        
        private void Update()
        {
            Tick(Time.deltaTime);
        }
    }
    
    public class OrbAgent : MonoBehaviour
    {
        public enum State
        {
            Idle,
            Telegraph,
            Attack,
            Returning
        }
        
        [Header("Orb Agent Settings")]
        public float speed = 2f;
        public float lifetime = 10f;
        public bool isStunned = false;
        
        private Vector3 direction;
        private Rigidbody2D rb;
        private float timer;
        private State currentState = State.Idle;
        private Color tintColor = Color.white;
        private float emissionIntensity = 0f;
        private Renderer rend;
        
        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            if (rb == null)
                rb = gameObject.AddComponent<Rigidbody2D>();
            
            rend = GetComponent<Renderer>();
        }
        
        public void Setup(Vector3 dir, float spd)
        {
            direction = dir.normalized;
            speed = spd;
            timer = 0f;
            isStunned = false;
        }
        
        public void SetState(State state, float intensity = 1f, float dmgMul = 1f)
        {
            currentState = state;
        }
        
        public void SetStunned(bool stunned)
        {
            isStunned = stunned;
        }
        
        public void SetTint(Color color, float emission = 0f)
        {
            tintColor = color;
            emissionIntensity = emission;
            
            if (rend != null)
            {
                rend.material.color = color;
                
                if (emission > 0f && rend.material.HasProperty("_EmissionColor"))
                {
                    rend.material.SetColor("_EmissionColor", color * emission);
                    rend.material.EnableKeyword("_EMISSION");
                }
            }
        }
        
        public void FireAtTarget(Transform target)
        {
            if (target != null)
            {
                direction = (target.position - transform.position).normalized;
            }
        }
        
        private void Update()
        {
            if (!isStunned && currentState != State.Idle)
            {
                rb.linearVelocity = direction * speed;
            }
            else
            {
                rb.linearVelocity = Vector2.zero;
            }
            
            timer += Time.deltaTime;
            if (timer >= lifetime)
            {
                Destroy(gameObject);
            }
        }
    }
    
    /// <summary>
    /// OrbUnit - 环绕体单体逻辑（轻量版）
    /// 管理充能→发射→执行→回收的小状态机
    /// </summary>
    [DisallowMultipleComponent]
    public class OrbUnit : MonoBehaviour
    {
        public bool IsBusy { get; private set; }
        
        private int index;
        private object boss;  // 这里先用object，实际使用时需要转型
        private PrefabOrbConductor conductor;
        private OrbAgent agent;
        
        private float cdRemain = 0f;
        private bool stopRequested = false;
        
        private float chargeU = 0f;
        private bool chargeToRed = true;
        
        private float spinPeak = 0f;
        private float spinNow = 0f;
        private float spinBonusMul = 1f;
        private float spinBonusRemain = 0f;
        
        public void Initialize(int idx, object bossRef, PrefabOrbConductor con)
        {
            index = idx;
            boss = bossRef;
            conductor = con;
            agent = GetComponent<OrbAgent>();
            
            IsBusy = false;
            
            // 启动主循环
            StartCoroutine(RunLoop());
        }
        
        public void StopNow()
        {
            stopRequested = true;
            IsBusy = false;
        }
        
        public void RequestStopAndReturn()
        {
            stopRequested = true;
        }
        
        public void AddSpinBonus(float mul, float time)
        {
            spinBonusMul = Mathf.Max(spinBonusMul, 1f + Mathf.Abs(mul));
            spinBonusRemain = Mathf.Max(spinBonusRemain, time);
        }
        
        private IEnumerator RunLoop()
        {
            while (!stopRequested)
            {
                // 等待冷却
                while (cdRemain > 0f)
                {
                    cdRemain -= Time.deltaTime;
                    yield return null;
                }
                
                // 标记为忙碌
                IsBusy = true;
                
                // 执行小技能（占位实现）
                yield return new WaitForSeconds(UnityEngine.Random.Range(1f, 3f));
                
                // 设置冷却
                cdRemain = UnityEngine.Random.Range(2f, 5f);
                IsBusy = false;
                
                yield return null;
            }
        }
        
        private void Update()
        {
            // 更新旋转加成
            if (spinBonusRemain > 0f)
            {
                spinBonusRemain -= Time.deltaTime;
                if (spinBonusRemain <= 0f)
                {
                    spinBonusMul = 1f;
                }
            }
        }
    }
}