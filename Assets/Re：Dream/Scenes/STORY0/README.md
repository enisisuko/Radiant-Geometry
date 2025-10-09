# STORY0 片头演出 - 2D版本

## 🎬 演出内容

一个震撼的14秒片头：

- **0-2秒**：🌑开场黑幕渐显
- **2-4秒**：⚡白色正方形极速下落（初速20，加速10）
- **4秒**：✨抖动特效激活，正方形开始剧烈抖动
- **6秒**：📝显示"Radiant Geometry"和"EnishiEuko"（屏幕中心）
- **8秒**：📹相机开始后拉，保持跟随
- **10秒**：文字淡出消失
- **11秒**：💥坠地！大爆炸特效+地面生成
- **12秒**：⬛黑屏铺满屏幕
- **14秒**：🎬自动跳转到Chapter1

**新增坠地效果**：
- 正方形以极高速度坠落
- 触地瞬间触发大爆炸特效
- 自动生成白色地面
- 相机全程跟随，完整捕捉坠落到爆炸的过程

## ⚡ 快速配置（推荐）

### 1. 一键配置
Unity菜单：`Tools > STORY0 > 一键配置`
- 自动创建所有游戏对象
- 自动设置所有引用
- 自动配置UI

### 2. 测试
点击▶️Play按钮，享受14秒震撼演出！

**手动设置**：
- 在Director的Inspector中设置 `Explosion Effect Prefab` 字段
- 拖入你的坠地爆炸特效预制体

**自动完成**：
- ✓ 白色方块已自动设置
- ✓ "大气摩擦.prefab"会自动设为抖动特效
- ✓ 地面会在坠地时自动生成

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

