# 开发指南

## 📋 代码总览
**重要**: 在开始任何开发工作前，请先阅读 `Assets/Scripts/CODE_OVERVIEW.md` 了解完整的项目结构和接口定义。

## 🚀 快速开始

### 1. 查看代码总览
```bash
# 在Unity编辑器中打开
Assets/Scripts/CODE_OVERVIEW.md
```

### 2. 核心系统概览
- **玩家系统**: `PlayerController2D.cs` + `PlayerColorModeController.cs`
- **Boss系统**: `BossC3_AllInOne.cs`
- **大阵系统**: `MatrixFormationManager.cs` + `MatrixVisualEffects.cs`
- **伤害系统**: `IDamageable` 接口

### 3. 常用接口速查
```csharp
// 玩家能量管理
PlayerColorModeController pcm = player.GetComponent<PlayerColorModeController>();
pcm.SpendEnergy(ColorMode.Red, 10f);

// Boss伤害处理
BossC3_AllInOne boss = GetComponent<BossC3_AllInOne>();
boss.TakeDamage(20f, BossColor.Red);

// 大阵控制
MatrixFormationManager matrix = GetComponent<MatrixFormationManager>();
matrix.StartMatrix();
```

## 🔧 开发流程

### 1. 修改前检查
- [ ] 阅读 `CODE_OVERVIEW.md`
- [ ] 确认要修改的系统和接口
- [ ] 检查相关依赖关系

### 2. 开发中注意
- [ ] 保持2D游戏兼容性
- [ ] 使用正确的命名空间
- [ ] 实现必要的接口
- [ ] 考虑性能影响

### 3. 完成后验证
- [ ] 检查编译错误
- [ ] 测试功能完整性
- [ ] 更新相关文档

## 📚 重要文件位置

```
Assets/Scripts/
├── CODE_OVERVIEW.md          # 📋 完整代码总览
├── DEVELOPMENT_GUIDE.md      # 🚀 本开发指南
├── Player/                   # 玩家系统
├── Enemies/3/               # 第三章Boss
├── d第三章/                 # 第三章相关脚本
└── Utilities/               # 工具类
```

## ⚠️ 重要提醒

1. **始终先读代码总览** - 避免重复造轮子
2. **保持接口一致性** - 使用标准化的接口
3. **注意2D适配** - 使用SpriteRenderer和Light2D
4. **性能优先** - 使用对象池和缓存
5. **文档同步** - 修改后更新相关文档

---
*此文档与 CODE_OVERVIEW.md 配合使用，确保开发效率和质量*
