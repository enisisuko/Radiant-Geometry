using UnityEngine;
using FadedDreams.Enemies;

namespace FadedDreams.Enemies
{
    public class Projectile : MonoBehaviour
    {
        public float damage = 20f;
        public float lifespan = 2f;

        private void Start()
        {
            Destroy(gameObject, lifespan);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            var enemy = other.GetComponent<DarkSpriteAI>();
            if (enemy != null)
            {
                enemy.TakeDamage(damage);
                Destroy(gameObject);
            }
        }
    }
}
