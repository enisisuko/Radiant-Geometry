# STORY0 场景配置说明 - 2D版本

## 已完成的工作

爱娘已经为素素完成了以下内容：

✅ 创建了STORY0场景文件（2D模式）
✅ 创建了Story0Director2D.cs控制脚本
✅ 创建了渐变背景Shader和材质
✅ 创建了所有必要的GameObject：
   - Director（控制脚本挂载点）
   - FallingSquare（下落的2D正方形）
   - Main Camera（正交相机）
   - Canvas（UI画布）
   - TitleGroup / TitleText（作品名）
   - AuthorGroup / AuthorText（作者信息）
   - FadeScreen（黑屏用）
   - Background（2D背景）

## 需要素素手动完成的配置

### 🎯 方法一：自动配置（推荐）

爱娘为素素准备了2D专用的一键配置工具！

1. **运行自动配置**：
   - 在Unity菜单栏点击：`Tools > STORY0 > Auto Setup Director 2D`
   - 工具会自动设置所有引用（除了特效预制体和Sprite）
   - 相机会自动设为正交模式

2. **创建白色正方形Sprite**：
   - 在Unity菜单栏点击：`Tools > STORY0 > Create White Square Sprite`
   - 会自动生成一个白色正方形贴图
   - 将生成的Sprite拖拽到FallingSquare的SpriteRenderer组件上

3. **设置特效预制体**：
   - 制作2D特效预制体
   - 将它拖拽到Director的 `Effect Prefab` 字段

4. **（可选）设置背景Sprite**：
   - 如果想要自定义背景图，拖拽到Background的SpriteRenderer
   - 或者留空，使用脚本控制的颜色渐变

### 方法二：手动配置

如果需要手动配置，请按以下步骤：

#### 1. Director 脚本引用设置

打开 STORY0 场景，选中 `Director` 对象，在Inspector中设置 `Story0Director2D` 组件的以下字段：

**游戏对象引用：**
- `Falling Square` → 拖拽场景中的 `FallingSquare` 对象
- `Main Camera` → 拖拽场景中的 `Main Camera` 对象
- `Effect Prefab` → 拖拽你制作的2D特效预制体
- `Background Sprite` → 拖拽场景中的 `Background` 对象的SpriteRenderer

**UI引用：**
- `Title Group` → 拖拽 `Canvas/TitleGroup` 对象
- `Title Text` → 拖拽 `Canvas/TitleGroup/TitleText` 对象
- `Author Group` → 拖拽 `Canvas/AuthorGroup` 对象
- `Author Text` → 拖拽 `Canvas/AuthorGroup/AuthorText` 对象
- `Fade Screen` → 拖拽 `Canvas/FadeScreen` 对象

**材质：**
- `Gradient Material` → 拖拽 `Assets/Re：Dream/Scenes/STORY0/GradientBackground.mat`

#### 2. 设置正方形的Sprite

- 选中 `FallingSquare` 对象
- 在 `SpriteRenderer` 组件中设置Sprite（可以用Unity内置的白色方块，或自己创建）
- 调整颜色和大小

#### 3. 设置相机为正交模式

- 选中 `Main Camera`
- 在 `Camera` 组件中：
  - 设置 `Projection` 为 `Orthographic`
  - 设置 `Size` 为 `5`（或根据需要调整）

#### 4. 背景设置

- 选中 `Background` 对象
- 可以设置Sprite，或者留空让脚本控制颜色渐变

## 演出时间轴

脚本已经实现了完整的12秒演出：

- **0-2秒**：正方形开始下落，速度逐渐加快，相机跟随
- **第2秒**：激活特效，正方形开始抖动
- **第4秒**：屏幕中央显示"Radiant Geometry"和"EnishiEuko"
- **第6秒**：相机往后拉（增大orthographicSize），背景渐变激活
- **第8秒**：文字开始淡出
- **第10秒**：屏幕黑屏
- **第12秒**：自动跳转到 Chapter1 场景

## 2D版本特点

与3D版本的主要区别：

1. **使用SpriteRenderer**而不是MeshRenderer
2. **相机是正交模式**（orthographic）
3. **物体在2D平面上移动**（只改变x和y坐标）
4. **相机后拉通过增大orthographicSize实现**，而不是移动位置
5. **背景是2D Sprite**，可以用颜色渐变或贴图

## 脚本参数说明

可以在Inspector中调整 `Story0Director2D` 的参数来微调演出效果：

**正方形下落设置：**
- `Square Start Position`: 正方形初始位置 (0, 5)
- `Fall Direction`: 下落方向（斜下方）
- `Initial Speed`: 初始速度
- `Acceleration`: 加速度
- `Shake Intensity`: 抖动强度

**相机设置：**
- `Camera Offset`: 相机跟随偏移量
- `Camera Zoom Out Amount`: 相机后拉的正交大小增量
- `Camera Zoom Speed`: 相机后拉速度

**背景设置：**
- `Background Start Color`: 背景初始颜色
- `Background End Color`: 背景目标颜色（白色）

**其他：**
- `Auto Start`: 是否场景加载时自动开始（默认开启）

## 测试方法

1. 在Unity中打开STORY0场景
2. 完成上述配置后，点击Play按钮
3. 观察完整的12秒演出序列
4. 确认在12秒后自动跳转到Chapter1场景

## 注意事项

- 确保Chapter1场景已经添加到Build Settings中，否则场景跳转会失败
- 2D游戏需要注意Sprite的Pixels Per Unit设置
- 建议FallingSquare的大小约为1x1单位
- 可以通过调整相机的Size来控制视野大小

---

爱娘已经尽力为素素准备好了所有2D版本的内容~ 如果有任何问题随时告诉爱娘哦！(｡♥‿♥｡)

