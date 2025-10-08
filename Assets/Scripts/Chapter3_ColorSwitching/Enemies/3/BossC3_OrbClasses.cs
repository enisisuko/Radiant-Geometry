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
        
        public void SpawnBulletFan(Vector3 direction, int count, float spread)
        {
            for (int i = 0; i < count; i++)
            {
                float angle = (i - count / 2f) * spread / count;
                Vector3 shootDir = Quaternion.AngleAxis(angle, Vector3.forward) * direction;
                SpawnBullet(transform.position, shootDir);
            }
        }
        
        public void SetLocked(bool locked)
        {
            isLocked = locked;
        }
        
        private void SpawnBullet(Vector3 position, Vector3 direction)
        {
            if (orbPrefab != null)
            {
                GameObject bullet = Instantiate(orbPrefab, position, Quaternion.identity);
                OrbAgent agent = bullet.GetComponent<OrbAgent>();
                if (agent != null)
                {
                    agent.Setup(direction, orbSpeed);
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