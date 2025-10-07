using UnityEngine;

namespace FadedDreams.Enemies
{
    /// <summary>
    /// 普通子弹：飞行→撞到玩家扣血；实现 IDamageable → 可被玩家近战打掉
    /// 建议放到 Layer: Bullet（并把近战的 hitMask 勾上 Bullet）
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
    public class BulletProjectile : MonoBehaviour, IDamageable
    {
        public float speed = 12f;
        public float damage = 12f;
        public float lifeTime = 4f;
        public LayerMask playerMask;
        public LayerMask obstacleMask;
        public bool destroyOnHit = true;

        private Rigidbody2D _rb;
        public bool IsDead { get; private set; }

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _rb.gravityScale = 0f;
            _rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        }

        public void Fire(Vector2 dir)
        {
            _rb.linearVelocity = dir.normalized * speed;
            Invoke(nameof(KillSelf), lifeTime);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            int layer = other.gameObject.layer;

            // 撞到玩家 → 扣血
            if (((1 << layer) & playerMask) != 0)
            {
                var ph = other.GetComponent<FadedDreams.Player.PlayerHealthLight>();
                if (ph) ph.TakeDamage(damage);
                if (destroyOnHit) KillSelf();
                return;
            }

            // 撞到障碍 → 销毁
            if (((1 << layer) & obstacleMask) != 0)
            {
                KillSelf();
            }
        }

        // 被近战击落：实现 IDamageable
        public void TakeDamage(float amount)
        {
            if (IsDead) return;
            KillSelf();
        }

        private void KillSelf()
        {
            if (IsDead) return;
            IsDead = true;
            Destroy(gameObject);
        }
    }
}
