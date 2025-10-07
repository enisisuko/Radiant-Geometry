// Assets/Scripts/Bosses/Chapter2/ShockwaveGrenade.cs
using System.Collections;
using UnityEngine;
using FadedDreams.Player; // RedLightController
using FadedDreams.Bosses; // ADD

namespace FadedDreams.Bosses
{
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(Collider2D))] // ���� CircleCollider2D
    public class ShockwaveGrenade : MonoBehaviour
    {
        [Header("Motion")]
        public float speed = 7f;
        public float gravityScale = 1.2f;
        public int maxBounces = 0;          // ��������������0=������
        public bool explodeOnCollision = true;
        public float fuseSeconds = 1.2f;    // ����ʱ����<=0 ��ʾֻ����ײ������

        [Header("Explosion")]
        public float radius = 3.6f;
        public float maxForce = 18f;        // �������ȣ�Խ��Խ��
        public float redDamage = 40f;       // ������ҿ۳�����
        public LayerMask playerMask;        // ������ڲ㣨Ҳ�ɲ��裬���� Tag=Player��
        public LayerMask obstacleMask;      // �ϰ��㣨�ɿգ�

        [Header("VFX & SFX")]
        public GameObject shockwaveRingPrefab; // ��ѡ����ɢ����Ч
        public GameObject explosionVfx;        // ��ѡ����ը����
        public AudioClip explosionSfx;         // ��ѡ
        public float sfxVolume = 0.8f;

        private Rigidbody2D _rb;
        private Collider2D _col;
        private int _bounces;
        private bool _exploded;
        private Coroutine _fuseCo;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _col = GetComponent<Collider2D>();
            _rb.gravityScale = gravityScale;
            _col.isTrigger = false; // ��Ҫʵ����ײ
        }

        private void OnEnable()
        {
            if (fuseSeconds > 0f)
            {
                _fuseCo = StartCoroutine(CoFuse(fuseSeconds));
            }
        }

        public void Fire(Vector2 dir)
        {
            // ��Boss���ã����ó���
            _rb.linearVelocity = dir.normalized * speed;
        }

        private IEnumerator CoFuse(float t)
        {
            yield return new WaitForSeconds(t);
            Explode();
        }

        private void OnCollisionEnter2D(Collision2D c)
        {
            if (_exploded) return;

            // �ϰ�/���棺��������
            if (((1 << c.gameObject.layer) & obstacleMask.value) != 0 || !c.collider.isTrigger)
            {
                if (explodeOnCollision && _bounces >= maxBounces)
                {
                    Explode();
                    return;
                }
                _bounces++;

                // ��΢˥�� & �����ȶ���
                _rb.linearVelocity *= 0.85f;
            }

            // ײ�����Ҳ��ֱ�ӱ�
            if (((1 << c.gameObject.layer) & playerMask.value) != 0 || c.collider.CompareTag("Player"))
            {
                Explode();
            }
        }

        private void Explode()
        {
            if (_exploded) return;
            _exploded = true;
            if (_fuseCo != null) StopCoroutine(_fuseCo);

            // ������Ч/��Ч
            if (explosionVfx) Instantiate(explosionVfx, transform.position, Quaternion.identity);
            if (shockwaveRingPrefab) Instantiate(shockwaveRingPrefab, transform.position, Quaternion.identity);
            if (explosionSfx) AudioSource.PlayClipAtPoint(explosionSfx, transform.position, sfxVolume);

            // �����ж�
            var hits = Physics2D.OverlapCircleAll(transform.position, radius);
            foreach (var h in hits)
            {
                // ��ң��ۺ� & ����
                if (((1 << h.gameObject.layer) & playerMask.value) != 0 || h.CompareTag("Player"))
                {
                    var red = h.GetComponentInParent<RedLightController>();
                    if (red)
                    {
                        bool consumed = red.TryConsume(redDamage);
                        if (!consumed)
                        {
                            // ADD: 无红光被击中 → 回到最后 checkpoint（安全反射）
                            C2RespawnHelper.TryReloadLastCheckpointSafe();
                        }
                    }

                    var prb = h.attachedRigidbody;
                    if (prb)
                    {
                        Vector2 dir = ((Vector2)h.transform.position - (Vector2)transform.position);
                        float dist01 = Mathf.Clamp01(dir.magnitude / Mathf.Max(0.001f, radius));
                        float force = Mathf.Lerp(maxForce, maxForce * 0.25f, dist01); // Խ������Խ��
                        prb.AddForce(dir.normalized * force, ForceMode2D.Impulse);
                    }
                }
                // �������壨��ѡ����������
                else if (h.attachedRigidbody && !h.isTrigger)
                {
                    Vector2 dir = ((Vector2)h.transform.position - (Vector2)transform.position);
                    float dist01 = Mathf.Clamp01(dir.magnitude / Mathf.Max(0.001f, radius));
                    float force = Mathf.Lerp(maxForce * 0.7f, maxForce * 0.2f, dist01);
                    h.attachedRigidbody.AddForce(dir.normalized * force, ForceMode2D.Impulse);
                }
            }

            Destroy(gameObject);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.3f, 1f, 0.3f, 0.35f);
            Gizmos.DrawSphere(transform.position, radius);
        }
    }
}
