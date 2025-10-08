# 流体色块菜单系统 - 使用说明

## 🎯 系统概述

流体色块菜单系统是一个创新的Unity UI系统，采用真实的流体挤压效果和发光色块设计，为《小天使大冒险》游戏提供沉浸式的主菜单体验。

## ✨ 核心特性

- **流体挤压效果**：基于Shader的真实流体变形
- **发光色块**：5个不同颜色的发光色块
- **四角+中心布局**：优化的空间利用和视觉平衡
- **流畅动画**：悬停、点击、过渡动画
- **多输入支持**：鼠标、键盘、触摸屏
- **系统集成**：与SaveSystem、SceneLoader无缝集成

## 🎨 视觉设计

### 布局
```
┌─────────────────────────────────────┐
│  新游戏     │         │    双人模式   │
│  (左上)     │ 继续游戏 │     (右上)   │
│             │  (中心)  │             │
├─────────────┼─────────┼─────────────┤
│             │         │             │
│  支持我     │         │    退出游戏   │
│  (左下)     │         │     (右下)   │
└─────────────────────────────────────┘
```

### 配色方案
- **新游戏**：青蓝色光 (#00D9FF → #0077FF)
- **继续游戏**：柔和紫光 (#B967FF → #7B2FFF) - 中心，最亮
- **双人模式**：活力橙光 (#FF9A56 → #FF6B35)
- **退出游戏**：暗红光 (#FF6B6B → #C92A2A)
- **支持我**：温暖金光 (#FFD93D → #FFB319)

## 🚀 快速开始

### 1. 自动设置（推荐）

1. 在场景中创建一个空的GameObject
2. 添加 `FluidMenuSceneSetup` 组件
3. 在Inspector中点击 "Setup Fluid Menu Scene" 按钮
4. 系统会自动创建所有必要的组件和UI

### 2. 手动设置

如果需要自定义设置，可以手动创建：

1. **创建Canvas**
   - 添加Canvas组件
   - 设置Render Mode为Screen Space - Overlay
   - 添加CanvasScaler和GraphicRaycaster

2. **创建色块**
   - 创建5个Image对象
   - 应用对应的FluidColorBlock材质
   - 设置位置和大小

3. **添加脚本**
   - 添加FluidMenuManager到场景
   - 配置色块引用和参数

## 📁 文件结构

```
Assets/Scripts/UI/FluidMenu/
├── Shaders/
│   └── FluidColorBlock.shader          # 流体色块Shader
├── Materials/
│   ├── FluidColorBlock_NewGame.mat     # 新游戏材质
│   ├── FluidColorBlock_Continue.mat    # 继续游戏材质
│   ├── FluidColorBlock_Coop.mat        # 双人模式材质
│   ├── FluidColorBlock_Quit.mat        # 退出游戏材质
│   └── FluidColorBlock_Support.mat     # 支持我材质
├── FluidMenuManager.cs                 # 主控制器
├── FluidColorBlock.cs                  # 单个色块组件
├── FluidAnimationController.cs         # 动画控制器
├── FluidMenuInput.cs                   # 输入处理
├── FluidMenuSceneSetup.cs              # 场景自动设置
├── FluidMenuIntegration.cs             # 系统集成
└── README.md                           # 使用说明
```

## 🎮 交互方式

### 鼠标操作
- **悬停**：鼠标悬停在色块上，色块变亮并扩大，挤压其他色块
- **点击**：点击色块执行对应功能，其他色块被挤出屏幕

### 键盘操作
- **数字键1-5**：快速选择对应色块
- **方向键**：在色块间导航
- **ESC键**：快速退出游戏

### 触摸操作
- **触摸悬停**：触摸色块产生悬停效果
- **点击**：触摸点击执行功能

## ⚙️ 配置参数

### FluidMenuManager
- `blockSpacing`：色块间距
- `centerBlockSize`：中心色块大小
- `cornerBlockSize`：角落色块大小
- `hoverScale`：悬停时放大倍数
- `squeezeScale`：挤压时缩小倍数
- `animationSpeed`：动画速度

### FluidColorBlock
- `breathScale`：呼吸动画幅度
- `breathSpeed`：呼吸动画速度
- `hoverIntensity`：悬停时发光强度
- `baseIntensity`：基础发光强度

### Shader参数
- `_EmissionIntensity`：发光强度
- `_DistortionStrength`：变形强度
- `_WaveSpeed`：波纹速度
- `_WaveFrequency`：波纹频率
- `_EdgeSoftness`：边缘柔和度

## 🔧 系统集成

### SaveSystem集成
```csharp
// 检查存档状态
bool hasSave = SaveSystem.Instance.HasSaveData();

// 继续游戏
string lastScene = SaveSystem.Instance.LoadLastScene();
string checkpoint = SaveSystem.Instance.LoadCheckpoint();
```

### SceneLoader集成
```csharp
// 加载场景
SceneLoader.LoadScene(sceneName, checkpointId);
```

### 自定义功能
```csharp
// 在FluidMenuIntegration中自定义菜单行为
public void OnCustomActionSelected()
{
    // 自定义逻辑
}
```

## 🎵 音频集成

系统支持音频反馈：

```csharp
// 在FluidMenuManager中配置音频
public AudioClip hoverSound;    // 悬停音效
public AudioClip clickSound;    // 点击音效
public AudioClip backgroundMusic; // 背景音乐
```

## 🎨 后期处理

系统自动配置Bloom效果增强发光感：

```csharp
// 在FluidMenuSceneSetup中配置
public bool enableBloom = true;
public float bloomIntensity = 1.5f;
public float bloomThreshold = 0.8f;
```

## 🐛 故障排除

### 常见问题

1. **Shader不显示**
   - 确保使用URP渲染管线
   - 检查Shader是否正确导入
   - 验证材质是否应用了正确的Shader

2. **色块不响应**
   - 检查Collider是否正确设置
   - 验证LayerMask配置
   - 确保EventSystem存在

3. **动画不流畅**
   - 调整animationSpeed参数
   - 检查帧率设置
   - 优化Shader复杂度

4. **存档功能异常**
   - 验证SaveSystem是否正确初始化
   - 检查存档文件路径
   - 确认权限设置

### 性能优化

1. **Shader优化**
   - 使用MaterialPropertyBlock避免材质实例化
   - 合理设置LOD级别
   - 优化纹理分辨率

2. **动画优化**
   - 使用对象池管理UI元素
   - 避免频繁的GC分配
   - 合理设置更新频率

## 📝 开发建议

1. **测试不同分辨率**
   - 确保在各种屏幕尺寸下正常显示
   - 测试不同宽高比

2. **性能测试**
   - 在目标设备上测试帧率
   - 监控内存使用情况

3. **用户体验**
   - 测试各种输入方式
   - 确保反馈及时准确

## 🔮 扩展功能

系统设计为可扩展的，可以轻松添加：

- 更多菜单选项
- 自定义动画效果
- 不同的配色方案
- 额外的交互方式

## 📞 技术支持

如有问题，请检查：
1. Unity版本兼容性（推荐2022.3+）
2. URP版本（推荐14.0+）
3. 系统要求

---

**版本**：v1.0  
**更新日期**：2024年12月  
**兼容性**：Unity 2022.3+, URP 14.0+