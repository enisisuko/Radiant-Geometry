# 三圈七层大阵系统使用说明

## 概述

这是一个华丽的三圈七层大阵系统，实现了你设计的复杂几何攻击模式。系统包含完整的节拍驱动、视觉效果和攻击逻辑。

## 系统架构

### 核心组件

1. **MatrixFormationManager** - 大阵管理器
   - 负责整体大阵的创建、更新和销毁
   - 管理12拍节拍系统
   - 协调各层级的行为

2. **MatrixVisualEffects** - 视觉效果管理器
   - 处理所有视觉渲染和动画
   - 管理颜色变化和发光效果
   - 实现危险预警和特殊动画

### 大阵结构

#### 七层设计
- **Layer A (内环阵)**: 6个母体轨道单元，半径 R1
- **Layer B (花瓣阵)**: 每母体5角花瓣，半径 R2
- **Layer C (星曜阵)**: 每花8点星曜，半径 R3
- **Layer D (拱弧阵)**: 6叶连弧，半径 R4
- **Layer E (外轮刻)**: 60刻度环，半径 R5
- **Layer F (远天星)**: 极远处微光颗粒，半径 R6
- **Ground Glyph**: 地面几何网格

#### 半径比例
```
R1 : R2 : R3 : R4 : R5 ≈ 1 : 1.4 : 1.8 : 2.2 : 2.6
```

## 12拍节拍系统

### 节拍时间轴

| 拍数 | 名称 | 持续时间 | 主要效果 |
|------|------|----------|----------|
| 1-2 | 聚气 | 1.0s | 内环母体亮度缓升，外轮刻跳点 |
| 3 | 锁扣 | 0.5s | 拱弧阵高亮，异色花瓣预警 |
| 4-5 | 花开 | 1.0s | 花瓣依次伸展，星曜开始公转 |
| 6 | 齐鸣 | 0.5s | 母体放光，发射攻击 |
| 7-8 | 螺旋 | 1.0s | 全阵相位缓旋，星曜呼吸 |
| 9 | 再锁扣 | 0.5s | 外轮刻强闪，花瓣再次预警 |
| 10-11 | 回波 | 1.0s | 地纹切线流，星曜加速 |
| 12 | 谢幕 | 0.5s | 整圈走光，复制体再生 |

### 特殊节拍处理

- **锁扣拍** (3, 6, 9, 12): 母体角速度-20%，拱弧高亮
- **齐鸣拍** (6): 发射震爆弹和散弹
- **谢幕拍** (12): 生成复制体，重置循环

## 颜色与相性系统

### 颜色规则
- **母体**: 按Boss当前颜色显色，边缘对色描边
- **花瓣**: 交替红绿，异色花瓣危险预警
- **星曜**: 白-金-薄青金属流光
- **拱弧**: 常态青金，锁扣拍高亮
- **外轮刻**: 按节拍闪烁，第12拍整圈走光

### 相性博弈
- 玩家切对色时，花瓣危险度下降25%
- 异色花瓣在发招前0.4s打亮预警
- 危险花瓣有特殊的闪烁和缩放动画

## 使用方法

### 在Boss中使用

```csharp
// 在BossC3的FinalGeometry大招中
case BigIdP2.FinalGeometry:
{
    // 创建大阵管理器
    var matrixManager = gameObject.AddComponent<MatrixFormationManager>();
    matrixManager.SetBossColor(color);
    matrixManager.SetPlayerMode(playerMode);
    
    // 启动大阵
    matrixManager.StartMatrix();
    
    // 大阵运行期间自动处理所有逻辑
    // ...
    
    // 停止大阵
    matrixManager.StopMatrix();
}
```

### 自定义配置

```csharp
// 在Inspector中配置
[Header("Matrix Configuration")]
public float baseRadius = 8f;
public float[] layerRadii = { 1f, 1.4f, 1.8f, 2.2f, 2.6f };
public int motherCount = 6;
public int petalsPerMother = 5;
public int starsPerFlower = 8;

[Header("Rhythm System")]
public float beatDuration = 0.5f;
public int totalBeats = 12;
```

## 预制体设置（2D游戏适配）

### 推荐预制体配置

1. **Mother Prefab**
   - 包含SpriteRenderer组件
   - 使用圆形Sprite（程序生成）
   - 包含Light2D组件用于发光
   - 支持颜色变化

2. **Petal Prefab**
   - 包含SpriteRenderer组件
   - 使用花瓣形Sprite（程序生成）
   - 包含Light2D组件
   - 支持缩放动画

3. **Star Prefab**
   - 包含SpriteRenderer组件
   - 使用星形Sprite（程序生成）
   - 包含Light2D组件
   - 支持旋转动画

4. **Arc Prefab**
   - 包含LineRenderer组件
   - 使用贝塞尔曲线
   - 支持光流动画
   - 设置sortingOrder用于2D排序

5. **Marker Prefab**
   - 包含SpriteRenderer组件
   - 使用圆形Sprite（程序生成）
   - 包含Light2D组件
   - 支持闪烁效果

6. **Ground Glyph Prefab**
   - 包含SpriteRenderer组件
   - 使用网格Sprite（程序生成）
   - 设置sortingOrder为-1（地面层）
   - 支持流向动画

### 程序生成的Sprite

系统会自动生成以下Sprite：
- **圆形Sprite**: 用于母体和标记
- **花瓣Sprite**: 椭圆形，用于花瓣
- **星形Sprite**: 小圆形，用于星曜
- **网格Sprite**: 用于地纹

## 扩展功能

### 添加新攻击类型

```csharp
// 在MatrixFormationManager中添加
private void FireShockwaveFromMother(Transform mother)
{
    // 实现震爆弹发射
}

private void FireBulletFromPetal(Transform petal)
{
    // 实现散弹发射
}

private void SpawnClonesAroundMother(Transform mother)
{
    // 实现复制体生成
}
```

### 自定义视觉效果

```csharp
// 在MatrixVisualEffects中添加
public void CustomEffect(Transform target, float intensity)
{
    // 实现自定义视觉效果
}
```

## 性能优化

### 建议设置
- 使用对象池管理频繁创建/销毁的对象
- 限制同时存在的复制体数量
- 使用LOD系统优化远距离渲染
- 合理设置更新频率

### 内存管理
- 大阵结束时自动清理所有生成的对象
- 使用缓存减少重复的组件查找
- 及时销毁不再使用的协程

## 调试功能

### 可视化调试
- 在Scene视图中显示大阵层级
- 实时显示当前节拍和相位
- 颜色变化和动画状态可视化

### 日志输出
```csharp
// 启用调试日志
matrixManager.debugLogs = true;
```

## 注意事项

1. **坐标系**: 系统使用2D坐标系，Z轴用于层级排序
2. **时间缩放**: 节拍系统使用unscaledTime，不受游戏时间缩放影响
3. **内存管理**: 大阵结束后会自动清理，无需手动管理
4. **性能**: 建议在性能较低的设备上减少星曜和复制体数量

## 故障排除

### 常见问题

1. **大阵不显示**: 检查预制体是否正确设置
2. **颜色不正确**: 检查材质和Light2D组件
3. **节拍不同步**: 检查beatDuration设置
4. **性能问题**: 减少层级数量或降低更新频率

### 调试步骤

1. 检查Console是否有错误信息
2. 验证预制体组件是否完整
3. 确认材质和着色器设置
4. 测试在简单场景中的表现
