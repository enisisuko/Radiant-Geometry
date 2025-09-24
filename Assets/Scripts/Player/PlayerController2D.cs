using UnityEngine;
using UnityEngine.Events;
using FadedDreams.Core;
using FadedDreams.Player;  // 确保能访问 FlashStrike

namespace FadedDreams.Player
{
    [RequireComponent(typeof(Rigidbody2D))]
    public class PlayerController2D : MonoBehaviour
    {
        // === 新增：剧情限制 ===
        [Header("Story Mode Restriction")]
        [Tooltip("剧情模式下禁止跳跃/冲刺/长按判定，但保留移动")]
        public bool disableJumpAndDashInStory = false;

        [Header("Move")]
        public float moveSpeed = 6f;
        public float dashSpeed = 12f;
        public float dashDuration = .15f;
        public float coyoteTime = .1f;

        [Header("Jump")]
        public float jumpForce = 12f;
        public int maxJumps = 2;

        [Header("Ground Check")]
        public Transform groundCheck;
        public float groundRadius = 0.1f;
        public LayerMask groundMask;

        [Header("Dash Reset Rules")]
        [Tooltip("冲刺冷却：未通过“跳跃”重置时，过 X 秒自动恢复一次冲刺资格")]
        public float dashCooldown = 1f;

        [Header("Events")]
        public UnityEvent onJump;
        public UnityEvent onDoubleJump;
        public UnityEvent onLanded;
        public UnityEvent onDashStart;
        public UnityEvent onDashEnd;

        [Header("Chapter 3 — Dash Enhancements")]
        public bool chapter3Dash = false;
        public float airDashDistanceMultiplier = 1.6f;
        public float airDashSpeedMultiplier = 1.25f;
        public GameObject vfxGroundDash;
        public GameObject vfxAirDash;
        public float vfxAutoDestroyDelay = 1.5f;

        private Rigidbody2D rb;
        private int jumpsLeft;
        private float coyoteCounter = 0f;
        private bool isDashing;
        private FlashStrike flashStrike;

        private float inputX;
        private bool queuedJump;

        private bool canDash = true;
        private float lastDashTime = -999f;

        private bool wasGrounded = false;

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            if (rb.gravityScale <= 0f) rb.gravityScale = 2.5f;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            flashStrike = GetComponent<FlashStrike>();
        }

        private void Start()
        {
            jumpsLeft = maxJumps;
        }

        private void Update()
        {
            inputX = Input.GetAxisRaw("Horizontal");

            // —— 公共的落地与冷却 —— 
            HandleGroundAndCooldown();

            if (disableJumpAndDashInStory)
            {
                // 禁止跳跃/冲刺：清除队列，不响应按键
                queuedJump = false;
                return;
            }

            // 跳跃排队
            if (Input.GetKeyDown(KeyCode.Space) && (coyoteCounter > 0f || jumpsLeft > 0))
            {
                queuedJump = true;
            }

            // 冲刺
            if (Input.GetKeyDown(KeyCode.LeftShift) && !isDashing && canDash)
            {
                StartCoroutine(Dash(inputX));
            }
        }

        private void HandleGroundAndCooldown()
        {
            bool grounded = false;
            if (groundCheck != null)
                grounded = Physics2D.OverlapCircle(groundCheck.position, groundRadius, groundMask);

            if (grounded)
            {
                coyoteCounter = coyoteTime;
                jumpsLeft = maxJumps;
            }
            else
            {
                coyoteCounter -= Time.deltaTime;
            }

            if (grounded && !wasGrounded && rb.linearVelocity.y <= 0.1f)
            {
                onLanded?.Invoke();
            }
            wasGrounded = grounded;

            if (!canDash && (Time.time - lastDashTime) >= dashCooldown)
            {
                canDash = true;
            }
        }

        private void FixedUpdate()
        {
            if (!isDashing)
            {
                Vector2 vel = rb.linearVelocity;
                vel.x = inputX * moveSpeed;
                rb.linearVelocity = vel;
            }

            if (disableJumpAndDashInStory) return;

            if (queuedJump)
            {
                bool isDouble = coyoteCounter <= 0f && jumpsLeft > 0;
                Vector2 vel = rb.linearVelocity;
                vel.y = jumpForce;
                rb.linearVelocity = vel;

                jumpsLeft = Mathf.Max(0, jumpsLeft - 1);
                queuedJump = false;

                canDash = true;
                onJump?.Invoke();
                if (isDouble) onDoubleJump?.Invoke();
            }
        }

        private System.Collections.IEnumerator Dash(float mx)
        {
            canDash = false;
            lastDashTime = Time.time;

            isDashing = true;
            onDashStart?.Invoke();
            if (flashStrike) flashStrike.StartDash();

            bool groundedAtStart = false;
            if (groundCheck != null)
                groundedAtStart = Physics2D.OverlapCircle(groundCheck.position, groundRadius, groundMask);

            float useDashSpeed = dashSpeed;
            float useDashDuration = dashDuration;

            float dir = Mathf.Sign(mx == 0 ? (transform.localScale.x == 0 ? 1 : transform.localScale.x) : mx);
            if (dir == 0f) dir = 1f;

            if (chapter3Dash)
            {
                if (!groundedAtStart)
                {
                    useDashSpeed *= Mathf.Max(1f, airDashSpeedMultiplier);
                    useDashDuration *= Mathf.Max(1f, airDashDistanceMultiplier);
                    SpawnDashVFX(vfxAirDash, dir);
                }
                else
                {
                    SpawnDashVFX(vfxGroundDash, dir);
                }
            }

            float t = 0f;
            while (t < useDashDuration)
            {
                if (disableJumpAndDashInStory)
                {
                    ForceEndDash();
                    yield break;
                }

                var vel = rb.linearVelocity;
                vel.x = dir * useDashSpeed;
                rb.linearVelocity = vel;
                t += Time.deltaTime;
                yield return null;
            }

            isDashing = false;
            onDashEnd?.Invoke();
            if (flashStrike) flashStrike.EndDash();
        }

        private void SpawnDashVFX(GameObject prefab, float dir)
        {
            if (!prefab) return;

            Vector3 pos = groundCheck ? groundCheck.position : transform.position;
            Quaternion rot = (dir > 0f) ? Quaternion.Euler(0f, 0f, -90f) : Quaternion.Euler(0f, 0f, 90f);

            var go = Instantiate(prefab, pos, rot);
            go.transform.SetParent(transform, worldPositionStays: true);
            if (vfxAutoDestroyDelay > 0f) Destroy(go, vfxAutoDestroyDelay);
        }

        public void EnableFlight(bool enabled)
        {
            rb.gravityScale = enabled ? 0f : 2.5f;
            if (enabled)
            {
                var vel = rb.linearVelocity;
                vel.y = 0f;
                rb.linearVelocity = vel;
            }
        }

        public void SetStoryRestriction(bool disabled)
        {
            disableJumpAndDashInStory = disabled;
            if (disabled)
            {
                queuedJump = false;
                if (isDashing) ForceEndDash();
            }
        }

        private void ForceEndDash()
        {
            if (!isDashing) return;
            isDashing = false;
            onDashEnd?.Invoke();
            if (flashStrike) flashStrike.EndDash();
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (groundCheck != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(groundCheck.position, groundRadius);
            }
        }
#endif
    }
}
