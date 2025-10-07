# 🚀 快速参考卡片

## 📋 必读文档
- **代码总览**: `Assets/Scripts/CODE_OVERVIEW.md` - 完整项目结构和接口
- **开发指南**: `Assets/Scripts/DEVELOPMENT_GUIDE.md` - 开发流程和注意事项

## 🎯 核心系统速查

### 玩家系统
```csharp
// 获取玩家控制器
PlayerController2D player = FindObjectOfType<PlayerController2D>();

// 获取能量控制器
PlayerColorModeController pcm = player.GetComponent<PlayerColorModeController>();

// 扣除能量
pcm.SpendEnergy(ColorMode.Red, 10f);

// 切换模式
pcm.TrySwitchMode();
```

### Boss系统
```csharp
// 获取Boss
BossC3_AllInOne boss = GetComponent<BossC3_AllInOne>();

// 造成伤害
boss.TakeDamage(20f, BossColor.Red);

// 获取Boss颜色
BossColor color = boss.GetColorMode();
```

### 大阵系统
```csharp
// 启动大阵
MatrixFormationManager matrix = gameObject.AddComponent<MatrixFormationManager>();
matrix.SetBossColor(BossColor.Red);
matrix.SetPlayerMode(ColorMode.Green);
matrix.StartMatrix();

// 停止大阵
matrix.StopMatrix();
```

### 伤害系统
```csharp
// 通用伤害接口
IDamageable target = GetComponent<IDamageable>();
target.TakeDamage(10f);
```

## 🔧 常用工具

### Unity MCP 命令
```bash
# 查看Unity日志
mcp_unity-mcp_console_getLogs

# 执行菜单命令
mcp_unity-mcp_menu_execute "GameObject/Create Empty"

# 查看连接状态
mcp_unity-mcp_unity_getActiveClient
```

### 调试技巧
1. 使用 `Debug.Log()` 输出调试信息
2. 检查控制台日志了解错误
3. 使用断点调试复杂逻辑
4. 查看 `CODE_OVERVIEW.md` 了解接口

## ⚠️ 重要提醒
- **2D游戏**: 使用 `SpriteRenderer` 和 `Light2D`
- **命名空间**: `FadedDreams.Player`, `FD.Bosses.C3`
- **性能**: 使用对象池和缓存
- **接口**: 实现 `IDamageable` 和 `IColorState`

---
*快速参考 - 详细内容请查看 CODE_OVERVIEW.md*
