// Assets/Scripts/Bosses/Chapter2/BossC2Clone.cs
using System.Collections;
using UnityEngine;

namespace FadedDreams.Bosses
{
    public class BossC2Clone : MonoBehaviour
    {
        public SpriteRenderer model;
        public float trembleAmp = 0.08f;
        public float trembleFreq = 20f;

        private bool _alive;

        public void SpawnTo(Vector3 target, bool tremble, float lifeSeconds)
        {
            StartCoroutine(CoSpawnTo(target, tremble, lifeSeconds));
        }

        public void ForceVanish()
        {
            Destroy(gameObject);
        }

        private IEnumerator CoSpawnTo(Vector3 target, bool tremble, float lifeSeconds)
        {
            _alive = true;
            // ¿ìËÙÂäµã
            while ((transform.position - target).sqrMagnitude > 0.01f)
            {
                transform.position = Vector3.MoveTowards(transform.position, target, 20f * Time.deltaTime);
                yield return null;
            }

            float t = 0f;
            while (t < lifeSeconds && _alive)
            {
                t += Time.deltaTime;
                if (tremble)
                {
                    float k = Mathf.Sin(Time.time * trembleFreq);
                    transform.localPosition = target + (Vector3)(new Vector2(k, Mathf.Cos(Time.time * trembleFreq * 0.9f)) * trembleAmp);
                }
                yield return null;
            }
            Destroy(gameObject);
        }
    }
}
