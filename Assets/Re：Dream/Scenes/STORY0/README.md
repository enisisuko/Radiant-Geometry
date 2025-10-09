# STORY0 场景配置说明

## 已完成的工作

爱娘已经为素素完成了以下内容：

✅ 创建了STORY0场景文件
✅ 创建了Story0Director.cs控制脚本
✅ 创建了渐变背景Shader和材质
✅ 创建了所有必要的GameObject：
   - Director（控制脚本挂载点）
   - FallingCube（下落的正方体）
   - Main Camera（主相机）
   - Canvas（UI画布）
   - TitleGroup / TitleText（作品名）
   - AuthorGroup / AuthorText（作者信息）
   - FadeScreen（黑屏用）
   - BackgroundPlane（背景渐变）

## 需要素素手动完成的配置

### 1. Director 脚本引用设置

打开 STORY0 场景，选中 `Director` 对象，在Inspector中设置 `Story0Director` 组件的以下字段：

**游戏对象引用：**
- `Falling Cube` → 拖拽场景中的 `FallingCube` 对象
- `Main Camera` → 拖拽场景中的 `Main Camera` 对象
- `Effect Prefab` → 拖拽你制作的特效预制体（在第2秒时激活）
- `Background Plane` → 拖拽场景中的 `BackgroundPlane` 对象

**UI引用：**
- `Title Group` → 拖拽 `Canvas/TitleGroup` 对象
- `Title Text` → 拖拽 `Canvas/TitleGroup/TitleText` 对象
- `Author Group` → 拖拽 `Canvas/AuthorGroup` 对象
- `Author Text` → 拖拽 `Canvas/AuthorGroup/AuthorText` 对象
- `Fade Screen` → 拖拽 `Canvas/FadeScreen` 对象

**材质：**
- `Gradient Material` → 拖拽 `Assets/Re：Dream/Scenes/STORY0/GradientBackground.mat`

### 2. BackgroundPlane 材质设置

选中 `BackgroundPlane` 对象，在 Inspector 的 `Mesh Renderer` 组件中：
- 将 `Materials[0]` 设置为 `GradientBackground.mat`

### 3. UI文本样式调整（可选）

如果需要调整文本样式：
- 选中 `TitleText`，在 `TextMeshProUGUI` 组件中调整字体、大小、颜色等
- 选中 `AuthorText`，同样可以调整样式
- 当前文本：
  - 作品名：Radiant Geometry
  - 作者：EnishiEuko

### 4. 特效预制体

爱娘在脚本中预留了特效的引用位置，素素制作好特效预制体后：
1. 将特效预制体拖拽到 `Director` 的 `Effect Prefab` 字段
2. 特效会在第2秒时自动激活，并作为FallingCube的子对象

## 演出时间轴

脚本已经实现了完整的12秒演出：

- **0-2秒**：正方体开始下落，相机跟随，速度逐渐加快
- **第2秒**：激活特效，正方体开始抖动
- **第4秒**：屏幕中央显示"Radiant Geometry"和"EnishiEuko"
- **第6秒**：相机开始往后拉，背景渐变平面激活（左下方白色渐变）
- **第8秒**：文字开始淡出
- **第10秒**：屏幕黑屏
- **第12秒**：自动跳转到 Chapter1 场景

## 脚本参数说明

可以在Inspector中调整 `Story0Director` 的参数来微调演出效果：

**正方体下落设置：**
- `Cube Start Position`: 正方体初始位置 (0, 10, 0)
- `Fall Direction`: 下落方向（斜下方）
- `Initial Speed`: 初始速度
- `Acceleration`: 加速度
- `Shake Intensity`: 抖动强度

**相机设置：**
- `Camera Offset`: 相机跟随偏移量
- `Camera Pull Back Distance`: 相机后拉距离
- `Camera Pull Back Speed`: 相机后拉速度

**其他：**
- `Auto Start`: 是否场景加载时自动开始（默认开启）

## 测试方法

1. 在Unity中打开STORY0场景
2. 完成上述配置后，点击Play按钮
3. 观察完整的12秒演出序列
4. 确认在12秒后自动跳转到Chapter1场景

## 注意事项

- 确保Chapter1场景已经添加到Build Settings中，否则场景跳转会失败
- 如果需要调整演出的时间节点，可以直接修改 `Story0Director.cs` 中的协程代码
- 背景渐变效果可以通过调整 `GradientBackground.mat` 的参数来微调颜色和渐变强度

---

爱娘已经尽力为素素准备好了所有内容~ 如果有任何问题随时告诉爱娘哦！(｡♥‿♥｡)

