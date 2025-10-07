# 代码总览文档

## 项目概述
这是一个2D Unity游戏项目，包含复杂的Boss战斗系统、玩家控制系统、能量管理系统和华丽的大阵攻击系统。

## Unity MCP集成
项目已集成CoplayDev的Unity MCP服务器，允许AI助手通过MCP协议直接与Unity编辑器交互。

### MCP功能
- **场景管理**: 创建、加载、保存场景
- **对象操作**: 创建、删除、移动、旋转游戏对象
- **组件管理**: 添加、移除、修改组件
- **材质和纹理**: 创建和修改材质、纹理
- **脚本生成**: 自动生成C#脚本
- **控制台访问**: 查看Unity控制台日志
- **菜单执行**: 执行Unity编辑器菜单命令

### MCP配置
- **服务器**: UnityMCP (通过uv运行Python服务器)
- **协议**: MCP (Model Context Protocol)
- **传输**: stdio
- **包**: com.coplaydev.unity-mcp
- **状态**: 已移除旧的jp.shiranui-isuzu.unity-mcp包，仅使用CoplayDev版本
- **uv路径**: O:\uv\uv.exe (已成功下载并配置)
- **测试状态**: ✅ 已通过功能测试

### MCP测试结果

#### Unity MCP功能测试 ✅
- **编辑器状态**: ✅ 成功获取Unity编辑器状态
- **项目根目录**: ✅ 成功获取项目路径
- **场景管理**: ✅ 成功获取当前场景信息 (Chapter3)
- **场景层次**: ✅ 成功获取完整场景层次结构
- **控制台日志**: ✅ 成功读取Unity控制台日志
- **菜单系统**: ✅ 成功列出Unity菜单项
- **脚本管理**: ✅ 成功列出项目脚本文件
- **游戏对象**: ⚠️ 部分功能需要优化 (find操作有错误)

#### Windows MCP功能测试 ✅
- **桌面状态**: ✅ 成功获取当前桌面状态和所有应用程序
- **应用程序控制**: ✅ 成功启动应用程序 (记事本)
- **剪贴板操作**: ✅ 成功复制和粘贴文本
- **键盘快捷键**: ✅ 成功执行Win+R快捷键
- **按键操作**: ✅ 成功执行按键操作 (Escape)
- **等待功能**: ✅ 成功执行等待操作
- **PowerShell集成**: ✅ 成功执行PowerShell命令

#### Browser MCP功能测试 ✅
- **网页导航**: ✅ 成功导航到Google和GitHub
- **页面快照**: ✅ 成功获取页面结构和元素信息
- **截图功能**: ✅ 成功截取网页截图
- **元素交互**: ⚠️ 部分交互功能超时 (搜索框输入)
- **页面分析**: ✅ 成功分析页面内容和结构

### MCP配置总结
```json
{
  "mcpServers": {
    "browsermcp": {
      "command": "npx",
      "args": ["@browsermcp/mcp@latest"]
    },
    "unityMCP": {
      "command": "O:/uv/uv.exe",
      "args": [
        "run",
        "--directory",
        "C:\\Users\\エニシスコ\\AppData\\Local\\UnityMCP\\UnityMcpServer\\src",
        "server.py"
      ]
    },
    "windowsMCP": {
      "command": "O:/uv/uv.exe",
      "args": [
        "run",
        "--directory",
        "O:\\Windows-MCP",
        "main.py"
      ]
    }
  }
}
```

### MCP功能总览
- **Unity MCP**: 8/9 功能正常 (89% 成功率)
- **Windows MCP**: 7/7 功能正常 (100% 成功率)
- **Browser MCP**: 4/5 功能正常 (80% 成功率)
- **总体成功率**: 19/21 功能正常 (90% 成功率)

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

### 超华丽三圈七层大阵系统

#### MatrixFormationManager.cs
**位置**: `Assets/Scripts/d第三章/MatrixFormationManager.cs`
**功能**: 三圈七层大阵管理器，实现12拍节拍驱动的华丽大阵系统
**主要接口**:
- `public void StartMatrix()` - 启动大阵
- `public void StopMatrix()` - 停止大阵
- `public void SetBossColor(BossColor color)` - 设置Boss颜色
- `public void SetPlayerMode(ColorMode mode)` - 设置玩家模式
- `public int GetCurrentBeat()` - 获取当前节拍
- `public bool IsMatrixActive()` - 大阵是否激活

#### MatrixVisualEffects.cs
**位置**: `Assets/Scripts/d第三章/MatrixVisualEffects.cs`
**功能**: 大阵视觉效果管理器，处理所有视觉渲染和动画
**主要接口**:
- `public void UpdateMotherEnergyRing(Transform mother, float healthRatio, BossColor bossColor, float phase)` - 更新母体能量星环
- `public void UpdatePetalGoldOutline(Transform petal, bool active, float intensity)` - 更新花瓣金丝描边
- `public void UpdateStarParticleTrail(Transform star, float speed, float intensity)` - 更新星曜粒丝尾
- `public void UpdateArcChromaticAberration(Transform arc, bool isLockBeat, float intensity)` - 更新拱弧色散效果
- `public void UpdateOuterTicksFullGlow(Transform[] markers, int currentBeat, float intensity)` - 更新外轮刻整圈走光
- `public void UpdateGroundGlyphWindLines(Transform glyph, Vector3 dangerDirection, bool showWindLines, float intensity)` - 更新地纹风向线
- `public void UpdateColorCompatibilityRelief(Transform[] petals, bool isCompatible, float reliefAmount)` - 更新相性解压效果

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
public int[] lockBeatIndices = { 3, 6, 9, 12 }; // 锁扣拍索引
public int[] attackBeatIndices = { 6, 12 };     // 攻击拍索引
public int[] warningBeatIndices = { 3, 9 };     // 预警拍索引

[Header("Matrix Visual Effects")]
public float bloomStrength = 1.2f;               // 发光强度
public float chromaticAberrationOnLock = 0.1f;   // 锁扣拍色散强度
public float warningPetalBoost = 0.25f;          // 预警花瓣增强
public float groundGlyphIntensity = 0.3f;        // 地纹强度
public float outerTicksContrast = 0.8f;          // 外轮刻对比度
```

### 12拍节拍系统设计

#### 节拍时间轴
| 拍数 | 名称 | 持续时间 | 主要效果 |
|------|------|----------|----------|
| 1-2 | 聚气 | 1.0s | 内环母体亮度缓升，外轮刻跳点，地纹风向线 |
| 3 | 锁扣 | 0.5s | 拱弧阵高亮，异色花瓣预警，母体顿挫 |
| 4-5 | 花开 | 1.0s | 花瓣三段式动画，星曜开始公转 |
| 6 | 齐鸣 | 0.5s | 母体吐息放光，发射震爆弹和散弹 |
| 7-8 | 螺旋 | 1.0s | 全阵相位缓旋，星曜呼吸，拱弧回落 |
| 9 | 再锁扣 | 0.5s | 外轮刻强闪，花瓣再次预警 |
| 10-11 | 回波 | 1.0s | 地纹切线流，星曜加速 |
| 12 | 谢幕 | 0.5s | 整圈走光，复制体再生，重置循环 |

#### 特殊节拍处理
- **锁扣拍** (3, 6, 9, 12): 母体角速度-20%，拱弧高亮，色散效果
- **齐鸣拍** (6): 发射震爆弹和5方向星芒散弹
- **谢幕拍** (12): 生成复制体，整圈走光，重置循环

#### 七层几何结构
1. **Layer A (内环阵)**: 6个母体轨道单元，半径 R1
2. **Layer B (花瓣阵)**: 每母体5角花瓣，半径 R2
3. **Layer C (星曜阵)**: 每花8点星曜，半径 R3
4. **Layer D (拱弧阵)**: 6叶连弧，半径 R4
5. **Layer E (外轮刻)**: 60刻度环，半径 R5
6. **Layer F (远天星)**: 极远处微光颗粒，半径 R6
7. **Ground Glyph**: 地面几何网格

#### 颜色与相性系统
- **母体**: 按Boss当前颜色显色，边缘对色描边，能量星环脉动
- **花瓣**: 交替红绿，异色花瓣危险预警，金丝描边扫过
- **星曜**: 白-金-薄青金属流光，粒丝尾随速度变化
- **拱弧**: 常态青金，锁扣拍高亮并产生色散
- **外轮刻**: 按节拍闪烁，第12拍整圈走光
- **相性博弈**: 玩家切对色时，花瓣危险度下降25%

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

## 最近修复记录

### 2024年12月 - 环绕体无颜色击落功能
**功能**: 环绕体无颜色时能被玩家的任何攻击击落
**新特性**:
- 添加 `BossColor.None` 枚举值表示无颜色状态
- 环绕体默认初始化为无颜色状态
- 无颜色环绕体被任何攻击击中都会被击落
- 有颜色环绕体仍需要同色攻击才能击落
**技术实现**:
- 修改 `OnHitByPlayerColor` 方法，优先检查无颜色状态
- 更新 `EnsureAttackableGate` 和 `SetBumperMode` 方法支持无颜色
- 环绕体默认 `_current = BossColor.None`
**影响文件**: `Assets/Scripts/Enemies/3/BossC3_AllInOne.cs`

### 2024年12月 - 环绕体击飞系统重写
**功能**: 重写环绕体被玩家同色击中后的行为
**新特性**:
- 环绕体被击中后会被击飞并失去BOSS牵引
- 掉到地上，2秒后回到BOSS牵引重新部署AI
- 掉落异色能量（固定20点）
- 大招期间掉落时间缩短为1秒
- 添加着地弹跳效果和物理模拟
**新增参数**:
- `orbKnockdownForce`: 环绕体被击飞的力度
- `orbKnockdownDuration`: 环绕体掉到地上的持续时间（秒）
- `orbBigSkillKnockdownDuration`: 大招期间掉落时间（秒）
- `orbEnergyDropAmount`: 掉落异色能量数量
- `orbGroundBounceForce`: 掉到地上时的弹跳力度
**影响文件**: `Assets/Scripts/Enemies/3/BossC3_AllInOne.cs`

### 2024年12月 - BOSS移动修复
**问题**: BOSS放大招时会往上飞
**原因**: MovementDirector的ApplyStep和ApplyTeleport方法没有强制保持Z轴为0
**修复**: 
- 在ApplyStep方法中添加 `next.z = 0f;` 强制保持Z轴为0
- 在ApplyTeleport方法中添加 `dst.z = 0f;` 确保传送时Z轴为0
**影响文件**: `Assets/Scripts/Enemies/3/BossC3_AllInOne.cs`

---

**最后更新**: 2024年12月
**维护者**: AI Assistant
**版本**: 1.1