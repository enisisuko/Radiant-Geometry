using UnityEngine;

namespace FadedDreams.Enemies
{
    /// <summary>
    /// �𱬵������ٷ��У�ײ����������������ը���԰뾶���������˺���
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
    public class ShockwaveGrenade : MonoBehaviour
    {
        public float speed = 6f;                // ��ͨ�ӵ���һ������
        public float damage = 26f;
        public float radius = 2.8f;
        public float lifeTime = 6f;
        public LayerMask playerMask;
        public LayerMask obstacleMask;
        public GameObject explosionVfx;

        private Rigidbody2D _rb;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _rb.gravityScale = 0f;
            _rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        }

        public void Fire(Vector2 dir)
        {
            _rb.linearVelocity = dir.normalized * speed;
            Invoke(nameof(Explode), lifeTime);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            int layer = other.gameObject.layer;
            if (((1 << layer) & obstacleMask) != 0 || ((1 << layer) & playerMask) != 0)
            {
                Explode();
            }
        }

        private void Explode()
        {
            // VFX
            if (explosionVfx)
            {
                var fx = Instantiate(explosionVfx, transform.position, Quaternion.identity);
                Destroy(fx, 2.5f);
            }

            // �˺� + ��΢��������ѡ��
            var cols = Physics2D.OverlapCircleAll(transform.position, radius, playerMask);
            foreach (var c in cols)
            {
                var ph = c.GetComponent<FadedDreams.Player.PlayerHealthLight>();
                if (ph) ph.TakeDamage(damage);
            }
            // ��΢�����������ѷ��� CameraShake2D ������
            var type = System.Type.GetType("FadedDreams.Enemies.CameraShake2D, Assembly-CSharp");
            if (type != null)
            {
                var m = type.GetMethod("Shake", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                if (m != null) m.Invoke(null, new object[] { 0.12f, 0.25f, 22f });
            }

            Destroy(gameObject);
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, .6f, .1f, .35f);
            Gizmos.DrawWireSphere(transform.position, radius);
        }
#endif
    }
}
