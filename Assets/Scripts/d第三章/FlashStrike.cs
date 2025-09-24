using UnityEngine;
using System.Collections;

namespace FadedDreams.Player
{
    /// <summary>
    /// 在 Dash 中贴近敌人触发伤害。空中时按“绝闪”倍增。
    /// 用法：
    /// - 将本组件挂在 Player。
    /// - 你的 PlayerController2D 在开始/结束冲刺时调用 StartDash() / EndDash()。
    /// - 或者若已暴露 isDashing，可每帧 SetDashing(...).
    /// </summary>
    public class FlashStrike : MonoBehaviour
    {
        public LayerMask enemyMask;
        public float checkRadius = 1.0f;
        public float damage = 30f;
        public float aerialMultiplier = 1.5f;
        public float cooldown = 0.25f;

        private bool _isDashing;
        private float _lastProc = -99f;

        public void StartDash() => _isDashing = true;
        public void EndDash() => _isDashing = false;
        public void SetDashing(bool v) => _isDashing = v;

        private void Update()
        {
            if (!_isDashing) return;
            if (Time.time - _lastProc < cooldown) return;

            var cols = Physics2D.OverlapCircleAll(transform.position, checkRadius, enemyMask);
            if (cols.Length == 0) return;

            bool grounded = Physics2D.Raycast(transform.position, Vector2.down, 0.2f, LayerMask.GetMask("Ground"));
            float dmg = grounded ? damage : damage * aerialMultiplier;

            foreach (var c in cols)
            {
                var d = c.GetComponentInParent<FadedDreams.Enemies.IDamageable>();
                if (d != null && !d.IsDead) d.TakeDamage(dmg);
            }

            // 屏幕效果（简易）
            Camera.main.transform.position += (Vector3)(Random.insideUnitCircle * (grounded ? 0.05f : 0.1f));

            _lastProc = Time.time;
        }
    }
}
