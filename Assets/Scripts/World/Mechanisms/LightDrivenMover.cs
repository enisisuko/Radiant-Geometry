// Scripts/World/LightDrivenMover.cs (upgraded)
using UnityEngine;

namespace FadedDreams.World
{
    [DisallowMultipleComponent]
    public class LightDrivenMover : MonoBehaviour
    {
        public enum MoveMode { Linear, PathPingPong }

        [Header("Sensor")]
        public LightIrradianceSensor sensor;
        public float holdSecondsBeforeMove = 0.2f;

        [Header("Movement")]
        public MoveMode mode = MoveMode.Linear;
        public Vector2 linearDirection = Vector2.right;
        public float linearDistance = 3f;

        [Tooltip("目标速度（单位/秒）")]
        public float targetSpeed = 4f;
        [Tooltip("加速度（单位/秒²）")]
        public float acceleration = 20f;
        [Tooltip("是否在光照不足时暂停推进")]
        public bool pauseWhenUnsaturated = true;

        [Header("Physics")]
        [Tooltip("（可选）使用刚体移动以更好地承载玩家")]
        public Rigidbody2D rb; // 设为 Kinematic
        public bool slideOnSurface = true;

        [Header("Path (for PathPingPong)")]
        public Transform[] waypoints;

        Vector3 _startPos;
        float _held;
        bool _moving;
        int _pathIndex = 0;
        int _pathDir = 1;
        Vector3 _vel; // 仅内部记录（用于平滑），rb 时由 MovePosition 驱动

        void Awake() { _startPos = transform.position; }

        void FixedUpdate()
        {
            if (!sensor) return;

            // 满格计时
            if (sensor.IsSaturated) _held += Time.fixedDeltaTime;
            else _held = 0f;

            if (!_moving && _held >= holdSecondsBeforeMove)
                _moving = true;

            if (!_moving) return;
            if (pauseWhenUnsaturated && !sensor.IsSaturated) return;

            // 计算期望速度方向
            Vector3 desiredDir = Vector3.zero;
            Vector3 pos = transform.position;
            switch (mode)
            {
                case MoveMode.Linear:
                    {
                        Vector3 a = _startPos;
                        Vector3 b = _startPos + (Vector3)(linearDirection.normalized * Mathf.Abs(linearDistance));
                        // 朝当前“往返目标”方向推进
                        float t = Vector3.Dot(pos - a, (b - a).normalized);
                        bool forward = (Mathf.FloorToInt(Time.time * (targetSpeed / Mathf.Max(0.01f, (b - a).magnitude))) % 2 == 0);
                        Vector3 target = forward ? b : a;
                        desiredDir = (target - pos).normalized;
                    }
                    break;
                case MoveMode.PathPingPong:
                    {
                        if (waypoints == null || waypoints.Length < 2) return;
                        Vector3 target = waypoints[_pathIndex].position;
                        if (Vector3.Distance(pos, target) <= 0.01f)
                        {
                            _pathIndex += _pathDir;
                            if (_pathIndex >= waypoints.Length) { _pathIndex = waypoints.Length - 2; _pathDir = -1; }
                            else if (_pathIndex < 0) { _pathIndex = 1; _pathDir = 1; }
                            target = waypoints[_pathIndex].position;
                        }
                        desiredDir = (target - pos).normalized;
                    }
                    break;
            }

            // 速度平滑（加速度限制）
            Vector3 desiredVel = desiredDir * Mathf.Max(0f, targetSpeed);
            _vel = Vector3.MoveTowards(_vel, desiredVel, acceleration * Time.fixedDeltaTime);

            // 位移
            Vector3 next = pos + _vel * Time.fixedDeltaTime;
            if (rb)
            {
                // 用刚体移动：更好地与玩家接触，避免穿透
                rb.MovePosition(next);
                if (slideOnSurface && _vel.sqrMagnitude > 0.0001f)
                    rb.linearVelocity = _vel; // 给点表面速度（可选）
            }
            else
            {
                transform.position = next;
            }
        }
    }
}
