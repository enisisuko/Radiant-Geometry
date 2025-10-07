# 代码总览文档

## 项目概述
这是一个2D Unity游戏项目，包含复杂的Boss战斗系统、玩家控制系统、能量管理系统和华丽的大阵攻击系统。

## 核心系统架构

### 1. 玩家系统 (Player System)

#### PlayerController2D.cs
**位置**: `Assets/Scripts/Player/PlayerController2D.cs`
**功能**: 2D玩家控制器，处理移动、跳跃、冲刺等基础操作
**主要接口**:
- `public bool isDashing { get; }` - 是否正在冲刺
- `public void StartDash()` - 开始冲刺
- `public void EndDash()` - 结束冲刺

#### PlayerColorModeController.cs
**位置**: `Assets/Scripts/d第三章/PlayerColorModeController.cs`
**功能**: 双能量条系统，管理红绿两种能量模式
**主要接口**:
- `public enum ColorMode { Red, Green }` - 颜色模式枚举
- `public ColorMode Mode { get; set; }` - 当前模式
- `public float Red01 { get; }` - 红色能量百分比
- `public float Green01 { get; }` - 绿色能量百分比
- `public bool SpendEnergy(ColorMode m, float amount)` - 扣除能量
- `public void AddEnergy(ColorMode m, float amount)` - 增加能量
- `public bool TrySwitchMode()` - 尝试切换模式
- `public bool TrySpendAttackCost()` - 尝试扣除攻击消耗

#### PlayerLightController.cs
**位置**: `Assets/Scripts/Player/PlayerLightController.cs`
**功能**: 玩家光源控制器
**主要接口**:
- `public void AddEnergy(float amount)` - 增加能量
- `public float GetEnergy()` - 获取当前能量

### 2. Boss系统 (Boss System)

#### BossC3_AllInOne.cs
**位置**: `Assets/Scripts/Enemies/3/BossC3_AllInOne.cs`
**功能**: 第三章Boss的完整实现，包含多阶段战斗系统
**主要接口**:
- `public enum BossColor { Red, Green }` - Boss颜色枚举
- `public enum BigIdP1 { RingBurst, QuadrantMerge }` - 一阶段大招
- `public enum BigIdP2 { PrismSymphony, FallingOrbit, ChromaReverse, FinalGeometry }` - 二阶段大招
- `public void TakeDamage(float damage, BossColor? sourcePlayerColor = null)` - 受到伤害
- `public interface IColorState` - 颜色状态接口
  - `BossColor GetColorMode()` - 获取当前颜色模式

#### 内部类 (Internal Classes):
- `PrefabOrbConductor` - 环绕体管理器
- `OrbAgent` - 环绕体代理
- `OrbUnit` - 环绕体单元
- `BulletDamage` - 子弹伤害处理

### 3. 大阵系统 (Matrix Formation System)

#### MatrixFormationManager.cs
**位置**: `Assets/Scripts/d第三章/MatrixFormationManager.cs`
**功能**: 三圈七层大阵管理器，实现华丽的几何攻击系统
**主要接口**:
- `public void StartMatrix()` - 启动大阵
- `public void StopMatrix()` - 停止大阵
- `public void SetBossColor(BossColor color)` - 设置Boss颜色
- `public void SetPlayerMode(ColorMode mode)` - 设置玩家模式
- `public int GetCurrentBeat()` - 获取当前节拍
- `public bool IsMatrixActive()` - 大阵是否激活

**核心功能**:
- 12拍节拍系统
- 七层几何结构（母体、花瓣、星曜、拱弧、外轮刻、远天星、地纹）
- 自动攻击系统
- 复制体生成

#### MatrixVisualEffects.cs
**位置**: `Assets/Scripts/d第三章/MatrixVisualEffects.cs`
**功能**: 大阵视觉效果管理器
**主要接口**:
- `public void UpdateMotherColor(Transform mother, int index, BossColor bossColor, float intensity = 1f)` - 更新母体颜色
- `public void UpdatePetalColor(Transform petal, int motherIndex, int petalIndex, BossColor bossColor, ColorMode playerMode, float intensity = 1f)` - 更新花瓣颜色
- `public void UpdateStarColor(Transform star, int motherIndex, int starIndex, float intensity = 1f)` - 更新星曜颜色
- `public void UpdateArcBrightness(Transform arc, float brightness)` - 更新拱弧亮度
- `public void UpdateMarkerGlow(Transform marker, bool glow)` - 更新标记发光
- `public void UpdateGroundGlyphIntensity(Transform glyph, float intensity)` - 更新地纹强度

### 4. 伤害系统 (Damage System)

#### IDamageable接口
**位置**: 分布在多个文件中
**功能**: 统一的伤害接口
**主要接口**:
- `void TakeDamage(float amount)` - 受到伤害
- `bool IsDead { get; }` - 是否死亡

#### 实现类:
- `EnemyHealth` - 敌人生命值
- `PlayerHealth` - 玩家生命值（如果存在）
- `BossC3_AllInOne` - Boss伤害处理

### 5. 武器系统 (Weapon System)

#### LaserEmitter.cs
**位置**: `Assets/Scripts/Player/Weapons/LaserEmitter.cs`
**功能**: 激光发射器
**主要接口**:
- `public void Fire(Vector2 direction)` - 发射激光
- `public void SetTarget(Transform target)` - 设置目标

#### PlayerMeleeLaser.cs
**位置**: `Assets/Scripts/d第三章/PlayerMeleeLaser.cs`
**功能**: 玩家近战激光
**主要接口**:
- `public void Activate()` - 激活激光
- `public void Deactivate()` - 停用激光

#### PlayerRangedCharger.cs
**位置**: `Assets/Scripts/d第三章/PlayerRangedCharger.cs`
**功能**: 玩家远程充能武器
**主要接口**:
- `public void StartCharge()` - 开始充能
- `public void ReleaseCharge()` - 释放充能

### 6. 敌人系统 (Enemy System)

#### EnemyHealth.cs
**位置**: 分布在多个敌人文件中
**功能**: 敌人生命值管理
**主要接口**:
- `public void TakeDamage(float amount)` - 受到伤害
- `public bool IsDead { get; }` - 是否死亡
- `public void Die()` - 死亡处理

#### 具体敌人类型:
- `EnemyCharger2D` - 冲锋敌人
- `EnemyRangedShooter2D` - 远程射击敌人
- `EnemyGraviton2D` - 重力敌人
- `DarkSpriteAI` - 暗精灵AI

### 7. UI系统 (UI System)

#### RedLightHUD.cs
**位置**: `Assets/Scripts/UI/RedLightHUD.cs`
**功能**: 红色光效HUD
**主要接口**:
- `public void UpdateIntensity(float intensity)` - 更新强度

#### ProjectedMenuController.cs
**位置**: `Assets/Scripts/UI/MENU/ProjectedMenuController.cs`
**功能**: 投影菜单控制器
**主要接口**:
- `public void ShowMenu()` - 显示菜单
- `public void HideMenu()` - 隐藏菜单

### 8. 工具系统 (Utility System)

#### Singleton.cs
**位置**: `Assets/Scripts/Utilities/Singleton.cs`
**功能**: 单例模式基类
**主要接口**:
- `public static T Instance { get; }` - 获取单例实例

## 核心数据结构

### 枚举类型 (Enums)
```csharp
// 颜色模式
public enum ColorMode { Red, Green }
public enum BossColor { Red, Green }

// Boss阶段
public enum BigIdP1 { RingBurst, QuadrantMerge }
public enum BigIdP2 { PrismSymphony, FallingOrbit, ChromaReverse, FinalGeometry }

// 环绕体状态
public enum State { Passive, Telegraph, Attack }
```

### 接口定义 (Interfaces)
```csharp
// 伤害接口
public interface IDamageable
{
    void TakeDamage(float amount);
    bool IsDead { get; }
}

// 颜色状态接口
public interface IColorState
{
    BossColor GetColorMode();
}
```

## 重要配置参数

### 大阵系统参数
```csharp
[Header("Matrix Configuration")]
public float baseRadius = 8f;                    // 基础半径
public float[] layerRadii = { 1f, 1.4f, 1.8f, 2.2f, 2.6f }; // 各层半径比例
public int motherCount = 6;                      // 母体数量
public int petalsPerMother = 5;                  // 每母体花瓣数
public int starsPerFlower = 8;                   // 每花星曜数
public int outerMarkerCount = 60;                // 外轮刻数量

[Header("Rhythm System")]
public float beatDuration = 0.5f;                // 每拍持续时间
public int totalBeats = 12;                      // 总拍数
```

### 能量系统参数
```csharp
[Header("Energies")]
public float redMax = 100f;                      // 红色能量最大值
public float greenMax = 100f;                    // 绿色能量最大值
public float switchCost = 5f;                    // 切换消耗
public float redAttackCost = 8f;                 // 红色攻击消耗
public float greenAttackCost = 6f;               // 绿色攻击消耗
```

## 关键设计模式

### 1. 单例模式 (Singleton Pattern)
- `Singleton<T>` - 用于全局管理器

### 2. 状态模式 (State Pattern)
- `OrbAgent.State` - 环绕体状态管理
- `PlayerColorModeController.Mode` - 玩家模式状态

### 3. 观察者模式 (Observer Pattern)
- `UnityEvent` - 事件系统
- `System.Action` - 委托回调

### 4. 工厂模式 (Factory Pattern)
- `MatrixFormationManager` - 大阵对象创建
- `PrefabOrbConductor` - 环绕体创建

## 性能优化要点

### 1. 对象池 (Object Pooling)
- 子弹系统使用对象池避免频繁创建销毁
- 大阵系统使用缓存减少组件查找

### 2. 缓存系统 (Caching)
- `MatrixVisualEffects` 使用字典缓存组件引用
- `MatrixFormationManager` 缓存相位数组

### 3. 协程管理 (Coroutine Management)
- 使用协程处理复杂动画序列
- 避免在Update中执行重复计算

## 调试和测试

### 日志系统
- 使用 `UnityEngine.Debug.Log()` 输出调试信息
- 控制台日志可通过 `mcp_unity-mcp_console_getLogs` 查看

### 关键测试点
1. 大阵是否正确跟随BOSS移动
2. 能量扣除是否正确触发
3. 攻击是否在正确节拍发射
4. 颜色系统是否正确响应玩家模式

## 扩展指南

### 添加新攻击类型
1. 在 `BigIdP1` 或 `BigIdP2` 枚举中添加新类型
2. 在 `BossC3_AllInOne` 的 switch 语句中添加处理逻辑
3. 实现对应的协程方法

### 添加新视觉效果
1. 在 `MatrixVisualEffects` 中添加新的更新方法
2. 在 `MatrixFormationManager` 中调用相应方法
3. 确保2D渲染兼容性

### 修改节拍系统
1. 调整 `beatDuration` 和 `totalBeats` 参数
2. 在 `HandleSpecialBeats()` 中添加新的节拍处理
3. 更新 `UpdateAllLayerPositions()` 中的位置计算

## 注意事项

1. **2D游戏适配**: 所有渲染使用 `SpriteRenderer` 和 `Light2D`
2. **命名空间**: 使用 `FadedDreams.Player` 和 `FD.Bosses.C3`
3. **内存管理**: 大阵结束后自动清理所有生成的对象
4. **性能考虑**: 避免在Update中执行复杂计算，使用协程和缓存
5. **兼容性**: 确保所有接口都实现了 `IDamageable` 和 `IColorState`

---

**最后更新**: 2024年12月
**维护者**: AI Assistant
**版本**: 1.0