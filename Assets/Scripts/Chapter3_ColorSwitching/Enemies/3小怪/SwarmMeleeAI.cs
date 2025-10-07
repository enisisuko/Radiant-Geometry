using System.Collections;
using UnityEngine;
using FadedDreams.VFX;

namespace FadedDreams.Enemies
{
    [RequireComponent(typeof(LineRenderer), typeof(Rigidbody2D), typeof(Collider2D))]
    public class SwarmMeleeAI : MonoBehaviour
    {
        [Header("Common")]
        public LayerMask playerMask;
        public LayerMask groundMask;
        public float damage = 20f;

        [Header("Vision")]
        public float seeDistance = 10f;
        public float attackDistance = 1.6f;

        [Header("Ground Move")]
        public float moveSpeed = 2.8f;
        public float gravityScale = 2.5f;
        public float groundCheckDistance = 0.2f;
        public float stepProbeDistance = 0.4f;
        public float smallHopImpulse = 3.5f;
        public float headClearance = 0.35f;

        [Header("Wander (看不到玩家时)")]
        public float wanderRadius = 5f;
        public float wanderStaySeconds = 1.5f;
        public float wanderPickInterval = 2.2f;

        [Header("Sweep (地面横扫-原有)")]
        public float sweepWindup = 0.25f;
        public float sweepDuration = 0.45f;
        public float sweepCooldown = 0.8f;
        public float laserLen = 1.2f;

        [Header("Jump-Cleave (新增跳劈)")]
        public float jumpImpulse = 6.2f;
        public float fallCleaveLen = 1.45f;
        public float fallCleaveHitRadius = 0.25f;

        [Header("Dash (新增冲撞)")]
        public float dashDistance = 4.0f;
        public float dashTime = 0.18f;
        public float dashCooldown = 1.1f;
        public AfterimageTrail2D dashAfterimage; // 挂同物体或子物体上

        private Transform _player;
        private Rigidbody2D _rb;
        private Collider2D _col;
        private LineRenderer _lr;

        private Vector2 _spawn;
        private Vector2 _wanderTarget;
        private float _nextWanderPick;

        private bool _busy;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _col = GetComponent<Collider2D>();
            _lr = GetComponent<LineRenderer>();
            _lr.enabled = false;

            _rb.gravityScale = gravityScale;
            _rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            _rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            _spawn = transform.position;
            _wanderTarget = _spawn;
        }

        private void Update()
        {
            if (!_player) _player = FindObjectOfType<FadedDreams.Player.PlayerColorModeController>()?.transform;
            if (_busy) return;

            bool canSee = _player && Vector2.Distance(transform.position, _player.position) <= seeDistance && HasLOS();

            if (!canSee)
            {
                Wander();
                return;
            }

            float d = Vector2.Distance(transform.position, _player.position);

            if (d > attackDistance + 1.0f)
            {
                GroundChase();
            }
            else
            {
                // 近身后，轮换几种动作：跳劈 / 冲撞 / 横扫
                float r = Random.value;
                if (r < 0.34f) StartCoroutine(CoJumpCleave());
                else if (r < 0.67f) StartCoroutine(CoDashAttack());
                else StartCoroutine(CoSweep());
            }
        }

        private bool HasLOS()
        {
            if (!_player) return false;
            Vector2 dir = (_player.position - transform.position);
            var hit = Physics2D.Raycast(transform.position, dir.normalized, dir.magnitude, groundMask);
            return !hit.collider;
        }

        private void Wander()
        {
            if (Time.time >= _nextWanderPick)
            {
                _nextWanderPick = Time.time + wanderPickInterval;
                Vector2 rand = Random.insideUnitCircle * wanderRadius;
                _wanderTarget = _spawn + rand;
            }

            // 超出出生半径时强制拉回方向
            Vector2 toTarget = (_wanderTarget - (Vector2)transform.position);
            if (toTarget.magnitude < 0.25f) { _rb.linearVelocity = new Vector2(0, _rb.linearVelocity.y); return; }

            float dirX = Mathf.Sign(toTarget.x);
            Vector2 vel = _rb.linearVelocity; vel.x = dirX * moveSpeed;

            // 地面/台阶小跳
            bool grounded = Physics2D.Raycast(transform.position, Vector2.down, groundCheckDistance + 0.05f, groundMask);
            Vector2 probeStart = (Vector2)transform.position + new Vector2(dirX * (_col.bounds.extents.x + 0.02f), _col.bounds.extents.y * 0.25f);
            var wall = Physics2D.Raycast(probeStart, new Vector2(dirX, 0f), stepProbeDistance, groundMask);
            if (wall.collider && grounded)
            {
                Vector2 upStart = (Vector2)transform.position + new Vector2(dirX * (_col.bounds.extents.x * 0.5f), _col.bounds.extents.y);
                bool headBlocked = Physics2D.Raycast(upStart, Vector2.up, headClearance, groundMask);
                bool forwardUpBlocked = Physics2D.Raycast(upStart, new Vector2(dirX, 1f).normalized, headClearance, groundMask);

                if (!headBlocked && !forwardUpBlocked)
                    _rb.AddForce(Vector2.up * smallHopImpulse, ForceMode2D.Impulse);
                else
                    vel.x *= 0.3f;
            }

            _rb.linearVelocity = vel;
        }

        private void GroundChase()
        {
            Vector2 pos = transform.position;
            float dirX = Mathf.Sign(_player.position.x - pos.x);
            Vector2 vel = _rb.linearVelocity;
            vel.x = dirX * moveSpeed;

            bool grounded = Physics2D.Raycast(pos, Vector2.down, groundCheckDistance + 0.05f, groundMask);
            Vector2 probeStart = pos + new Vector2(dirX * (_col.bounds.extents.x + 0.02f), _col.bounds.extents.y * 0.25f);
            var wall = Physics2D.Raycast(probeStart, new Vector2(dirX, 0f), stepProbeDistance, groundMask);

            if (wall.collider && grounded)
            {
                Vector2 upStart = pos + new Vector2(dirX * (_col.bounds.extents.x * 0.5f), _col.bounds.extents.y);
                bool headBlocked = Physics2D.Raycast(upStart, Vector2.up, headClearance, groundMask);
                bool forwardUpBlocked = Physics2D.Raycast(upStart, new Vector2(dirX, 1f).normalized, headClearance, groundMask);
                if (!headBlocked && !forwardUpBlocked)
                    _rb.AddForce(Vector2.up * smallHopImpulse, ForceMode2D.Impulse);
                else
                    vel.x *= 0.3f;
            }

            _rb.linearVelocity = vel;
        }

        // ===== 横扫（保留原近战） =====
        private IEnumerator CoSweep()
        {
            _busy = true;

            float t0 = 0f;
            _lr.enabled = true;
            Vector2 toPlayer = (_player.position - transform.position).normalized;
            Vector2 startDir = Quaternion.Euler(0, 0, 45f) * toPlayer;

            // windup
            while (t0 < sweepWindup)
            {
                float jitter = Mathf.Sin(Time.time * 40f) * 0.05f;
                ApplyLR(startDir, 0.08f + Mathf.Abs(jitter));
                t0 += Time.deltaTime;
                yield return null;
            }

            // sweep
            float t = 0f;
            while (t < sweepDuration)
            {
                float a = Mathf.Lerp(-45f, 45f, t / sweepDuration);
                Vector2 dir = Quaternion.Euler(0, 0, a) * startDir;
                ApplyLR(dir, 0.12f);
                DoPointHit(dir, laserLen, 0.25f);
                t += Time.deltaTime;
                yield return null;
            }

            _lr.enabled = false;
            yield return new WaitForSeconds(sweepCooldown);
            _busy = false;
        }

        private void ApplyLR(Vector2 dir, float width)
        {
            Vector3 p0 = transform.position;
            Vector3 p1 = p0 + (Vector3)(dir.normalized * laserLen);
            _lr.positionCount = 2;
            _lr.SetPosition(0, p0);
            _lr.SetPosition(1, p1);
            _lr.startWidth = width;
            _lr.endWidth = width * 0.95f;
        }

        private void DoPointHit(Vector2 dir, float len, float radius)
        {
            Vector3 end = transform.position + (Vector3)(dir.normalized * len);
            var cols = Physics2D.OverlapCircleAll(end, radius, playerMask);
            foreach (var c in cols)
            {
                var light = c.GetComponent<FadedDreams.Player.PlayerHealthLight>();
                if (light) light.TakeDamage(damage);
            }
        }

        // ===== 跳劈：起跳→下落时向前下斩 =====
        private IEnumerator CoJumpCleave()
        {
            _busy = true;

            // 起跳
            _rb.AddForce(Vector2.up * jumpImpulse, ForceMode2D.Impulse);
            yield return new WaitForSeconds(0.08f);

            // 下落过程做“下劈判定”：每帧在前下方打一段短线
            while (_rb.linearVelocity.y < 0f) // 开始下落
            {
                if (_player)
                {
                    Vector2 dir = ((_player.position - transform.position).normalized + Vector3.down).normalized;
                    Vector3 p0 = transform.position;
                    Vector3 p1 = p0 + (Vector3)(dir * fallCleaveLen);

                    var cols = Physics2D.OverlapCircleAll(p1, fallCleaveHitRadius, playerMask);
                    foreach (var c in cols)
                    {
                        var light = c.GetComponent<FadedDreams.Player.PlayerHealthLight>();
                        if (light) light.TakeDamage(damage);
                    }
                }
                yield return null;
            }

            yield return new WaitForSeconds(0.15f);
            _busy = false;
        }

        // ===== 冲撞（带残影） =====
        private IEnumerator CoDashAttack()
        {
            _busy = true;

            // 朝玩家方向 dashDistance，在 dashTime 内线性插值（禁用X摩擦）
            Vector3 start = transform.position;
            Vector3 end = _player ? _player.position : start + transform.right * dashDistance;
            Vector2 dir = ((Vector2)(end - start)).normalized;
            end = start + (Vector3)(dir * dashDistance);

            // 残影
            if (dashAfterimage) dashAfterimage.BurstOnce();

            float t = 0f;
            Vector2 origVel = _rb.linearVelocity;
            _rb.gravityScale = 0f;

            while (t < dashTime)
            {
                t += Time.deltaTime;
                float u = Mathf.Clamp01(t / dashTime);
                Vector3 p = Vector3.Lerp(start, end, u);
                _rb.MovePosition(p);

                // 碰撞判定（沿着身前的胶囊/圆）
                var cols = Physics2D.OverlapCircleAll(p, Mathf.Max(_col.bounds.extents.x, _col.bounds.extents.y), playerMask);
                foreach (var c in cols)
                {
                    var light = c.GetComponent<FadedDreams.Player.PlayerHealthLight>();
                    if (light) light.TakeDamage(damage);
                }
                yield return null;
            }

            _rb.gravityScale = gravityScale;
            _rb.linearVelocity = origVel;
            yield return new WaitForSeconds(dashCooldown);
            _busy = false;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (!_col) _col = GetComponent<Collider2D>();
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(_spawn == Vector2.zero ? transform.position : (Vector3)_spawn, wanderRadius);
        }
#endif
    }
}
