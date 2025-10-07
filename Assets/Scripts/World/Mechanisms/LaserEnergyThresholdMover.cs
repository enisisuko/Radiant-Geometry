// LaserEnergyThresholdMover.cs
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using FadedDreams.Player; // RedLightController
// 放在和 Torch 一样的命名空间，方便查找与配置
namespace FadedDreams.World.Light
{
    [DisallowMultipleComponent]
    public class LaserEnergyThresholdMover : MonoBehaviour
    {
        [Header("Mover")]
        [Tooltip("要被移动的目标（另一个方块）")]
        public Transform mover;
        [Tooltip("位置1（能量<阈值时回到这里）")]
        public Transform pos1;
        [Tooltip("位置2（能量>=阈值时移动到这里）")]
        public Transform pos2;
        [Tooltip("使用本地坐标移动（true）还是世界坐标（false）")]
        public bool useLocal = false;
        [Tooltip("移动速度（单位/秒）")]
        public float moveSpeed = 3f;

        [Header("Energy (Chapter 2)")]
        [Tooltip("玩家的红光控制器（第二章用红光/激光系统的话，拖玩家的 RedLightController）")]
        public RedLightController playerRed;
        [Tooltip("判断“足够能量”的阈值")]
        public float energyThreshold = 50f;
        [Tooltip("激光照射时，每秒从玩家扣除的能量")]
        public float drainPerSecond = 15f;
        [Tooltip("是否仅在激光照射时才扣能量")]
        public bool drainOnlyWhenLaser = true;

        [Header("Laser Gate")]
        [Tooltip("是否要求激光命中才生效")]
        public bool requireLaser = true;
        [Tooltip("激光命中后的维持判定时间（秒）。避免需要每帧都打中）")]
        public float laserHoldSeconds = 0.2f;

        [Header("Events")]
        [Tooltip("状态切换到 高能量（去往位置2） 时触发")]
        public UnityEvent onBecameHigh;
        [Tooltip("状态切换到 低能量（回到位置1） 时触发")]
        public UnityEvent onBecameLow;

        private float _laserUntil;   // 记录“被激光命中”的持续时间戳
        private bool _wasHigh;       // 上一帧是否为“高能量状态”

        private void Reset()
        {
            // 方便一键生成锚点
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
                Debug.LogWarning($"[{nameof(LaserEnergyThresholdMover)}] {name}: 请设置 Pos1 / Pos2 锚点。已自动创建。");
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
                currentEnergy = ReadPlayerEnergy(); // 扣完再读，保证判定及时
            }

            bool high = laserActive && currentEnergy >= energyThreshold;

            // 目标位置
            Vector3 target = useLocal
                ? (high ? pos2.localPosition : pos1.localPosition)
                : (high ? pos2.position : pos1.position);

            // 平滑移动
            if (mover)
            {
                if (useLocal)
                    mover.localPosition = Vector3.MoveTowards(mover.localPosition, target, moveSpeed * Time.deltaTime);
                else
                    mover.position = Vector3.MoveTowards(mover.position, target, moveSpeed * Time.deltaTime);
            }

            // 触发一次性事件
            if (high != _wasHigh)
            {
                if (high) onBecameHigh?.Invoke();
                else onBecameLow?.Invoke();
                _wasHigh = high;
            }
        }

        // ======== 激光系统接口（与 Torch.cs 保持一致，第二章激光命中会调用这些） ========
        public void OnLaserFirstHit() => MarkLaserHit();
        public void OnLaserHitAt(Vector2 _hitPoint) => MarkLaserHit();
        public void OnLaserHitAtLevel(Vector2 _hitPoint, float _incomingLight01to100) => MarkLaserHit();

        private void MarkLaserHit()
        {
            _laserUntil = Time.time + Mathf.Max(0.02f, laserHoldSeconds);
        }

        // ======== 能量读写（采用 RedLightController 的统一接口） ========
        private float ReadPlayerEnergy()
        {
            if (!playerRed) return 0f;
            return Mathf.Max(0f, playerRed.Current); // Torch.cs 中使用 Current / Max
        }

        private void ConsumeFromPlayer(float amount)
        {
            if (!playerRed || amount <= 0f) return;
            // Torch.cs 用 TryConsume；这里沿用
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
