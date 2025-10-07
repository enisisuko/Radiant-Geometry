using UnityEngine;
using FadedDreams.VFX;


namespace FadedDreams.Player
{
    /// <summary>
    /// 玩家冲刺特效：在玩家周围生成一个“虚影”，并立刻拉出一串残影。
    /// 不缩放，只透明度随时间衰减。
    /// </summary>
    [RequireComponent(typeof(AfterimageTrail2D))]
    public class PlayerDashAfterimage : MonoBehaviour
    {
        public bool autoEmitDuringDash = false; // 如果你 dash 有持续段，可在 dash 期间 BeginEmit/StopEmit
        public float ghostHoldSeconds = 0.08f;  // 冲刺起点“环绕虚影”的停留时长

        private AfterimageTrail2D _trail;

        private void Awake()
        {
            _trail = GetComponent<AfterimageTrail2D>();
        }

        /// <summary> 从你的 Dash 代码里调用 </summary>
        public void PlayDashVFX(Vector2 dashDir)
        {
            if (!_trail) return;
            // 1) 残影串（立刻打一个 Burst）
            _trail.BurstOnce();
            // 2) 玩家“环绕虚影”= 当前位置打一张快照并延时销毁（用 Afterimage 的第一张即可）
            //    （为了简单，仍然复用 Afterimage，一帧后它就开始淡出）
            // 如果 dash 有一段持续，你也可以：
            // if (autoEmitDuringDash) _trail.BeginEmit();  在 dash 结束时 StopEmit();
        }

        // 可选：暴露这俩给你的 Dash 协程用
        public void BeginContinuous() => _trail?.BeginEmit();
        public void EndContinuous() => _trail?.StopEmit();
    }
}
