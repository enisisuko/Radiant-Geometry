// LaserEnergyThresholdMover.cs
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using FadedDreams.Player; // RedLightController
// ���ں� Torch һ���������ռ䣬�������������
namespace FadedDreams.World.Light
{
    [DisallowMultipleComponent]
    public class LaserEnergyThresholdMover : MonoBehaviour
    {
        [Header("Mover")]
        [Tooltip("Ҫ���ƶ���Ŀ�꣨��һ�����飩")]
        public Transform mover;
        [Tooltip("λ��1������<��ֵʱ�ص����")]
        public Transform pos1;
        [Tooltip("λ��2������>=��ֵʱ�ƶ������")]
        public Transform pos2;
        [Tooltip("ʹ�ñ��������ƶ���true�������������꣨false��")]
        public bool useLocal = false;
        [Tooltip("�ƶ��ٶȣ���λ/�룩")]
        public float moveSpeed = 3f;

        [Header("Energy (Chapter 2)")]
        [Tooltip("��ҵĺ����������ڶ����ú��/����ϵͳ�Ļ�������ҵ� RedLightController��")]
        public RedLightController playerRed;
        [Tooltip("�жϡ��㹻����������ֵ")]
        public float energyThreshold = 50f;
        [Tooltip("��������ʱ��ÿ�����ҿ۳�������")]
        public float drainPerSecond = 15f;
        [Tooltip("�Ƿ���ڼ�������ʱ�ſ�����")]
        public bool drainOnlyWhenLaser = true;

        [Header("Laser Gate")]
        [Tooltip("�Ƿ�Ҫ�󼤹����в���Ч")]
        public bool requireLaser = true;
        [Tooltip("�������к��ά���ж�ʱ�䣨�룩��������Ҫÿ֡�����У�")]
        public float laserHoldSeconds = 0.2f;

        [Header("Events")]
        [Tooltip("״̬�л��� ��������ȥ��λ��2�� ʱ����")]
        public UnityEvent onBecameHigh;
        [Tooltip("״̬�л��� ���������ص�λ��1�� ʱ����")]
        public UnityEvent onBecameLow;

        private float _laserUntil;   // ��¼�����������С��ĳ���ʱ���
        private bool _wasHigh;       // ��һ֡�Ƿ�Ϊ��������״̬��

        private void Reset()
        {
            // ����һ������ê��
            if (!mover) mover = transform;

            if (!pos1)
            {
                var a = new GameObject("Pos1").transform;
                a.SetParent(transform, false);
                a.localPosition = Vector3.zero;
                pos1 = a;
            }

            if (!pos2)
            {
                var b = new GameObject("Pos2").transform;
                b.SetParent(transform, false);
                b.localPosition = Vector3.right * 2f;
                pos2 = b;
            }
        }

        private void Awake()
        {
            if (!mover) mover = transform;
            if (!pos1 || !pos2)
            {
                Debug.LogWarning($"[{nameof(LaserEnergyThresholdMover)}] {name}: ������ Pos1 / Pos2 ê�㡣���Զ�������");
                Reset();
            }
        }

        private void Update()
        {
            bool laserActive = !requireLaser || Time.time < _laserUntil;

            float currentEnergy = ReadPlayerEnergy();
            if ((drainOnlyWhenLaser ? laserActive : true) && currentEnergy > 0f)
            {
                ConsumeFromPlayer(drainPerSecond * Time.deltaTime);
                currentEnergy = ReadPlayerEnergy(); // �����ٶ�����֤�ж���ʱ
            }

            bool high = laserActive && currentEnergy >= energyThreshold;

            // Ŀ��λ��
            Vector3 target = useLocal
                ? (high ? pos2.localPosition : pos1.localPosition)
                : (high ? pos2.position : pos1.position);

            // ƽ���ƶ�
            if (mover)
            {
                if (useLocal)
                    mover.localPosition = Vector3.MoveTowards(mover.localPosition, target, moveSpeed * Time.deltaTime);
                else
                    mover.position = Vector3.MoveTowards(mover.position, target, moveSpeed * Time.deltaTime);
            }

            // ����һ�����¼�
            if (high != _wasHigh)
            {
                if (high) onBecameHigh?.Invoke();
                else onBecameLow?.Invoke();
                _wasHigh = high;
            }
        }

        // ======== ����ϵͳ�ӿڣ��� Torch.cs ����һ�£��ڶ��¼������л������Щ�� ========
        public void OnLaserFirstHit() => MarkLaserHit();
        public void OnLaserHitAt(Vector2 _hitPoint) => MarkLaserHit();
        public void OnLaserHitAtLevel(Vector2 _hitPoint, float _incomingLight01to100) => MarkLaserHit();

        private void MarkLaserHit()
        {
            _laserUntil = Time.time + Mathf.Max(0.02f, laserHoldSeconds);
        }

        // ======== ������д������ RedLightController ��ͳһ�ӿڣ� ========
        private float ReadPlayerEnergy()
        {
            if (!playerRed) return 0f;
            return Mathf.Max(0f, playerRed.Current); // Torch.cs ��ʹ�� Current / Max
        }

        private void ConsumeFromPlayer(float amount)
        {
            if (!playerRed || amount <= 0f) return;
            // Torch.cs �� TryConsume����������
            playerRed.TryConsume(amount);
        }

        private void OnDrawGizmosSelected()
        {
            if (pos1 && pos2)
            {
                Gizmos.color = Color.white;
                Vector3 a = useLocal ? (transform.TransformPoint(pos1.localPosition)) : pos1.position;
                Vector3 b = useLocal ? (transform.TransformPoint(pos2.localPosition)) : pos2.position;
                Gizmos.DrawLine(a, b);
                Gizmos.DrawWireSphere(a, 0.1f);
                Gizmos.DrawWireSphere(b, 0.1f);
            }

            if (mover)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireCube(mover.position, Vector3.one * 0.2f);
            }
        }
    }
}
