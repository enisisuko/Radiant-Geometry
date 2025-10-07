// LaserHitRelay.cs
using UnityEngine;

namespace FadedDreams.World.Light
{
    public class LaserHitRelay : MonoBehaviour
    {
        [Tooltip("接收激光命中事件的目标（通常是 MoverBlock 上的 LaserEnergyThresholdMover）")]
        public LaserEnergyThresholdMover target;

        public void OnLaserFirstHit() => target?.OnLaserFirstHit();
        public void OnLaserHitAt(Vector2 hitPoint) => target?.OnLaserHitAt(hitPoint);
        public void OnLaserHitAtLevel(Vector2 p, float lv) => target?.OnLaserHitAtLevel(p, lv);
    }
}
