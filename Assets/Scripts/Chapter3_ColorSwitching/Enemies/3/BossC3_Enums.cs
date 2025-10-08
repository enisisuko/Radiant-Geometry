// BossC3_Enums.cs
// BossC3相关的枚举定义
// Unity 2021+ / Unity 6.2 兼容（URP/HDRP均可）

namespace FD.Bosses.C3
{
    /// <summary>
    /// Boss阶段
    /// </summary>
    public enum Phase 
    { 
        P1, 
        P2 
    }

    /// <summary>
    /// Boss颜色状态
    /// </summary>
    public enum BossColor 
    { 
        Red, 
        Green, 
        None 
    }

    /// <summary>
    /// 技能阶段
    /// </summary>
    public enum Stage 
    { 
        TELL,       // 预告
        WINDUP,     // 蓄力
        ACTIVE,     // 激活
        RECOVER     // 恢复
    }

    /// <summary>
    /// BossC3一阶段大招类型
    /// </summary>
    public enum BigIdP1 
    { 
        RingBurst, 
        QuadrantMerge 
    }

    /// <summary>
    /// BossC3二阶段大招类型
    /// </summary>
    public enum BigIdP2 
    { 
        PrismSymphony, 
        FallingOrbit, 
        ChromaReverse, 
        FinalGeometry 
    }

    /// <summary>
    /// P1阶段小技类型
    /// </summary>
    public enum MicroIdP1
    {
        SimpleFan,          // 简单扇形
        TrackingBurst,      // 追踪爆发
        CrossPattern        // 十字图案
    }

    /// <summary>
    /// P2阶段小技类型
    /// </summary>
    public enum MicroIdP2
    {
        SpiralShot,         // 螺旋射击
        PrismReflect,       // 棱镜反射
        WavePattern,        // 波浪图案
        FocusBeam           // 聚焦光束
    }
}
