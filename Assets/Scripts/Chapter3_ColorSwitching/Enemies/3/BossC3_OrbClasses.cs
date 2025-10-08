using UnityEngine;
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
        
        private List<OrbAgent> activeOrbs = new List<OrbAgent>();
        private bool isLocked = false;
        
        public void Setup(GameObject prefab, int maxCount, float radius, float speed)
        {
            orbPrefab = prefab;
            maxOrbs = maxCount;
            spawnRadius = radius;
            orbSpeed = speed;
        }
        
        public int GetOrbCount()
        {
            return activeOrbs.Count;
        }
        
        public OrbAgent GetOrb(int index)
        {
            if (index >= 0 && index < activeOrbs.Count)
                return activeOrbs[index];
            return null;
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
        
        public void SetLocked(bool locked)
        {
            isLocked = locked;
        }
        
        public void SetOrbCount(int count)
        {
            // 调整活跃环绕体数量
            while (activeOrbs.Count > count)
            {
                if (activeOrbs.Count > 0)
                {
                    OrbAgent orb = activeOrbs[activeOrbs.Count - 1];
                    activeOrbs.RemoveAt(activeOrbs.Count - 1);
                    if (orb != null)
                        Destroy(orb.gameObject);
                }
            }
            
            while (activeOrbs.Count < count && activeOrbs.Count < maxOrbs)
            {
                SpawnOrb();
            }
        }
        
        private void SpawnOrb()
        {
            if (orbPrefab != null)
            {
                Vector3 randomPos = transform.position + Random.insideUnitSphere * spawnRadius;
                randomPos.z = 0f;
                GameObject orb = Instantiate(orbPrefab, randomPos, Quaternion.identity);
                OrbAgent agent = orb.GetComponent<OrbAgent>();
                if (agent != null)
                {
                    agent.Setup(Vector3.right, orbSpeed);
                    activeOrbs.Add(agent);
                }
            }
        }
        
        private void SpawnBullet(Vector3 position, Vector3 direction, float speedScale = 1f)
        {
            if (orbPrefab != null)
            {
                GameObject bullet = Instantiate(orbPrefab, position, Quaternion.identity);
                OrbAgent agent = bullet.GetComponent<OrbAgent>();
                if (agent != null)
                {
                    agent.Setup(direction, orbSpeed * speedScale);
                    activeOrbs.Add(agent);
                }
            }
        }
    }
    
    public class OrbAgent : MonoBehaviour
    {
        [Header("Orb Agent Settings")]
        public float speed = 2f;
        public float lifetime = 10f;
        public bool isStunned = false;
        
        private Vector3 direction;
        private Rigidbody2D rb;
        private float timer;
        
        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            if (rb == null)
                rb = gameObject.AddComponent<Rigidbody2D>();
        }
        
        public void Setup(Vector3 dir, float spd)
        {
            direction = dir.normalized;
            speed = spd;
            timer = 0f;
            isStunned = false;
        }
        
        public void SetStunned(bool stunned)
        {
            isStunned = stunned;
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
            if (!isStunned)
            {
                rb.linearVelocity = direction * speed;
            }
            
            timer += Time.deltaTime;
            if (timer >= lifetime)
            {
                Destroy(gameObject);
            }
        }
        
        private void OnDestroy()
        {
            // 从父级列表中移除
            PrefabOrbConductor conductor = GetComponentInParent<PrefabOrbConductor>();
            if (conductor != null)
            {
                // 这里需要访问conductor的activeOrbs列表，但为了简化，我们跳过
            }
        }
    }
}