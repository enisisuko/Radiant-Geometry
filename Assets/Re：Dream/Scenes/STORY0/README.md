# STORY0 片头演出 - 2D版本

## 🎬 演出内容（12秒完整片头）

震撼的坠落演出：

- **0秒**：⚡正方形以初速20f/s极速下落开始！第一特效激活！
- **0-4秒**：🌑黑幕渐显（不影响下落）
- **0-8秒**：震颤逐渐加强
- **5秒**：📝文字渐显，📹相机开始缓慢拉远
- **7秒**：⚡第二特效激活，加速度提升到20f/s²！
- **8秒**：相机拉远到最大值，震颤开始减弱
- **9秒**：文字渐隐
- **10秒**：🏔️地面提前生成（在正方形下方）
- **11秒**：💥撞地！速度归零+大爆炸+黑幕渐隐
- **12秒**：⬛完全黑屏，切换到Chapter1

**技术亮点**：
- 基于时间触发撞地，精确控制在11秒
- 10秒提前生成超大地面（100单位宽）
- 11秒时速度立即归零
- 相机初始更远（12），5-8秒渐变到18
- 震颤0-8秒增强，8-11秒减弱（平滑过渡）

## ⚡ 快速配置（推荐）

### 1. 一键配置
Unity菜单：`Tools > STORY0 > 一键配置`
- 自动创建所有游戏对象
- 自动设置所有引用
- 自动配置UI

### 2. 测试
点击▶️Play按钮，享受14秒震撼演出！

**手动设置特效**：
1. `Second Effect Prefab` - 第7秒播放的第二特效
2. `Explosion Effect Prefab` - 第11秒坠地爆炸特效

**自动完成**：
- ✓ 白色方块已自动设置
- ✓ "大气摩擦.prefab"会自动设为第一特效（0秒）
- ✓ 地面会在坠地时自动生成
- ✓ 初速20f/s，加速10f/s（7秒后变20f/s）

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
- 下落速度（initialSpeed: 20）、加速度（acceleration: 10）
- 抖动强度（shakeIntensity）
- 相机缩放范围（cameraZoomStart/End）
- 地面高度（groundHeight）
- 地面颜色（groundColor）
- 开场黑幕时长（openingFadeDuration）

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

