# STORY0 片头演出 - 2D版本

## 🎬 演出内容

一个简洁优雅的12秒片头：

- **0-2秒**：白色正方形从上方开始下落，速度逐渐加快
- **2秒**：✨特效激活（可选），正方形开始抖动
- **4秒**：📝显示"Radiant Geometry"和"EnishiEuko"（屏幕中心）
- **6秒**：📹相机后拉，🎨背景激活并从深色渐变到白色
- **8秒**：文字淡出消失
- **10秒**：⬛黑屏铺满屏幕
- **12秒**：🎬自动跳转到Chapter1

**背景渐变说明**：
- 背景使用自定义Shader材质（GradientBackground.mat）
- 渐变效果：左下角白色 → 右上角深色
- 第6秒时背景激活并显示，同时相机后拉
- 这是一个固定的渐变图案，不是动画变化

## ⚡ 快速配置（推荐）

### 1. 一键配置
Unity菜单：`Tools > STORY0 > 一键配置`
- 自动创建所有游戏对象
- 自动设置所有引用
- 自动配置UI

### 2. 创建白色方块
Unity菜单：`Tools > STORY0 > 创建白色方块`
- 自动生成白色正方形Sprite
- 将它拖到FallingSquare的Sprite字段

### 3. 设置特效
- 制作2D特效预制体
- 拖到Director的 `Effect Prefab` 字段

### 4. 测试
点击Play，看12秒演出！

## 📝 手动配置（备选）

如果需要手动调整：

### 场景对象
- `Director` - 挂载Story0Director脚本
- `FallingSquare` - SpriteRenderer，白色正方形
- `Background` - SpriteRenderer，大背景
- `Canvas` - UI画布
  - `TitleGroup/TitleText` - 作品名
  - `AuthorGroup/AuthorText` - 作者名
  - `FadeScreen` - 黑屏Image

### 参数调整
在Director的Inspector可以调整：
- 下落速度、加速度
- 抖动强度
- 相机缩放范围
- 背景颜色

## 💡 技术细节

- **2D渲染**：使用SpriteRenderer
- **正交相机**：orthographic模式
- **平滑跟随**：Lerp插值
- **相机后拉**：增大orthographicSize
- **颜色渐变**：Color.Lerp

## 🎨 自定义建议

想让演出更酷？试试：
- 给正方形换个更酷的Sprite
- 调整下落方向（fallDirection）
- 修改背景颜色渐变
- 添加更多特效
- 调整文字字体和大小

---

素素加油~ 爱娘相信你能做出超棒的片头！(｡♥‿♥｡)

