// Assets/Scripts/Bosses/Chapter2/BossRedDrainBullet.cs
using UnityEngine;
using FadedDreams.Player; // RedLightController
using FadedDreams.Bosses; // ADD: 使用 C2RespawnHelper


namespace FadedDreams.Bosses
{
    [RequireComponent(typeof(Collider2D))]
    public class BossRedDrainBullet : MonoBehaviour
    {
        [Header("Motion")]
        public float speed = 10f;
        public float lifeTime = 6f;
        public float gravity = 0f; // >0 时做简单下坠
        public bool locked = false; // 轨道期用

        [Header("Damage")]
        public float redDamage = 15f;
        public LayerMask playerMask;

        private Vector2 _vel;
        private float _t;

        private void Awake()
        {
            var col = GetComponent<Collider2D>();
            col.isTrigger = true;
        }

        public void Fire(Vector2 dir)
        {
            _vel = dir.normalized * speed;
        }

        private void Update()
        {
            if (locked) return;

            _t += Time.deltaTime;
            if (_t >= lifeTime) { Destroy(gameObject); return; }

            if (gravity > 0f) _vel += Vector2.down * gravity * Time.deltaTime;

            transform.position += (Vector3)(_vel * Time.deltaTime);
            float ang = Mathf.Atan2(_vel.y, _vel.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0, 0, ang);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (((1 << other.gameObject.layer) & playerMask.value) != 0 || other.CompareTag("Player"))
            {
                var r = other.GetComponentInParent<RedLightController>();
                if (r)
                {
                    bool consumed = r.TryConsume(redDamage);
                    if (!consumed)
                    {
                        // ADD: 无红光被击中 → 回到最后 checkpoint（安全反射）
                        C2RespawnHelper.TryReloadLastCheckpointSafe();
                    }
                }
                Destroy(gameObject);
            }
            else if (other.isTrigger == false)
            {
                Destroy(gameObject);
            }
        }
    }
}
