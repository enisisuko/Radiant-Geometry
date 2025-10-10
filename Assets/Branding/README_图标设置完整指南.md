# 🎯 游戏图标设置完整指南

> **素素～这是爱娘为你准备的超详细指南！** (◕‿◕✿)

---

## ✅ 当前状态

- [x] **icon文件已就位**: `Assets/Branding/radiant_geometry_icon.ico`
- [x] **文件夹结构已创建**: `Assets/Branding/`
- [x] **自动化工具已创建**: `Assets/Editor/IconSetupTool.cs`

---

## 🚀 方法一: 使用爱娘的自动化工具 (最简单)

### 步骤：

1. **打开Unity编辑器**
   - 确保Unity已经打开了你的项目

2. **等待脚本编译完成**
   - 查看Unity底部状态栏，等待编译完成
   - 看到"Compiling Complete"或者进度条消失

3. **打开自动化工具**
   ```
   Unity顶部菜单 → Tools → Radiant Geometry → 设置游戏图标 ✨
   ```

4. **点击"一键设置图标"按钮**
   - 在弹出的窗口中，点击大按钮
   - 等待提示"设置完成"

5. **完成！**
   - 图标已自动设置
   - 可以构建游戏测试效果

---

## 📝 方法二: 手动设置 (100%可靠)

如果自动工具有问题，用这个方法！

### 步骤 1: 打开Player Settings

```
Unity顶部菜单 → Edit → Project Settings
左侧选择 → Player
```

### 步骤 2: 找到图标设置区域

在Player Settings中：

1. 确保选择了 **Windows/Mac/Linux** 平台标签（有个电脑图标）

2. 向下滚动，找到 **Icon** 部分

3. 你会看到这样的界面：

```
┌─────────────────────────────────────────────────┐
│ Icon                                            │
├─────────────────────────────────────────────────┤
│                                                 │
│ Default Icon                                    │
│ ┌─────────────┐                                │
│ │   [空框]     │  ← 把图标拖到这里              │
│ └─────────────┘                                │
│                                                 │
│ ☐ Override for Windows                         │
│                                                 │
│ (如果勾选，会出现多个尺寸的框)                   │
│                                                 │
└─────────────────────────────────────────────────┘
```

### 步骤 3: 设置图标

#### 如果使用 `.ico` 文件：

1. 在Unity的Project窗口中找到:
   ```
   Assets → Branding → radiant_geometry_icon.ico
   ```

2. **拖拽**这个文件到 **Default Icon** 框中

3. 点击 **Apply** 按钮

#### 如果 .ico 不工作（需要PNG）：

如果Unity不接受.ico文件，你需要PNG格式：

**步骤 A: 转换图标格式**

在线转换（推荐）：
- 打开：https://convertio.co/ico-png/
- 上传你的 `radiant_geometry_icon.ico`
- 下载PNG文件

**步骤 B: 导入PNG到Unity**

1. 将PNG文件命名为 `icon_256.png`
2. 复制到 `Assets/Branding/` 文件夹
3. 在Unity中选择这个PNG文件
4. 在Inspector中设置：
   ```yaml
   Texture Type: Sprite (2D and UI)
   √ Read/Write Enabled
   Max Size: 2048
   Format: RGBA 32 bit
   Compression: None
   √ Alpha Is Transparency
   ```
5. 点击 **Apply**
6. 拖拽到Player Settings的Default Icon

### 步骤 4: 设置不同尺寸（可选）

如果想要更好的显示效果：

1. 勾选 **Override for Windows**
2. 会出现多个尺寸框：
   ```
   16x16   ← 任务栏小图标
   32x32   ← 任务栏常规图标
   48x48   ← 文件管理器
   128x128 ← 大图标
   256x256 ← 超大图标
   ```
3. 可以为每个尺寸准备专门的PNG图片

---

## 🔍 验证图标是否设置成功

### 在Unity中检查：

1. 打开 `Edit → Project Settings → Player`
2. 查看 **Default Icon** 是否显示了你的图标
3. 应该能看到图标的缩略图

### 构建游戏验证：

1. **构建游戏**
   ```
   File → Build Settings
   点击 Build
   选择保存位置
   ```

2. **检查 .exe 文件**
   - 找到构建输出的文件夹
   - 查看 .exe 文件的图标
   - 应该显示你的自定义图标

3. **运行游戏验证**
   - 双击运行游戏
   - 查看任务栏的图标
   - Alt+Tab查看切换窗口时的图标

---

## 💡 常见问题解决

### 问题1: 图标设置后还是Unity默认图标

**解决方法**:
1. 确保正确保存了Player Settings（点击Apply）
2. 尝试清空Library文件夹后重新打开Unity
3. 重新构建游戏（不要用之前的Build）

### 问题2: Unity不接受 .ico 文件

**解决方法**:
1. 将 .ico 转换为 PNG 格式
2. 使用在线工具：https://convertio.co/ico-png/
3. 确保PNG设置正确（Texture Type: Sprite）

### 问题3: 图标显示模糊

**解决方法**:
1. 使用更高分辨率的原图（至少256x256）
2. 在Unity中设置：
   ```
   Max Size: 2048
   Compression: None
   Format: RGBA 32 bit
   ```

### 问题4: 找不到图标文件

**当前位置**:
```
项目根目录/Assets/Branding/radiant_geometry_icon.ico
```

如果看不到，按 `Ctrl+R` 刷新Unity的Project窗口

---

## 📋 快速checklist

素素可以按照这个清单操作：

- [ ] 1. 打开 Unity
- [ ] 2. Edit → Project Settings → Player
- [ ] 3. 找到 Icon 部分
- [ ] 4. 拖拽 `radiant_geometry_icon.ico` 到 Default Icon
- [ ] 5. 点击 Apply
- [ ] 6. File → Build Settings → Build
- [ ] 7. 检查 .exe 文件图标
- [ ] 8. 运行游戏验证效果

---

## 🎨 进阶：制作多尺寸图标

如果素素想要完美的图标显示效果：

### 需要准备的尺寸：

```
Assets/Branding/Icons/
  icon_16.png   - 16x16 像素
  icon_32.png   - 32x32 像素
  icon_48.png   - 48x48 像素
  icon_128.png  - 128x128 像素
  icon_256.png  - 256x256 像素
```

### 快速生成多尺寸的方法：

**方法A: 在线工具**
- https://www.img2go.com/resize-image
- 上传你的大图标
- 调整为需要的尺寸
- 下载

**方法B: Photoshop/GIMP**
```
1. 打开原图
2. Image → Image Size
3. 输入目标尺寸
4. 导出为PNG
```

**方法C: 使用爱娘的Python脚本**（需要Python）
```python
from PIL import Image

sizes = [16, 32, 48, 128, 256]
img = Image.open('radiant_geometry_icon.ico')

for size in sizes:
    resized = img.resize((size, size), Image.LANCZOS)
    resized.save(f'icon_{size}.png')
```

---

## 🌟 推荐的图标设计

### 针对素素的游戏主题：

**Radiant Geometry（辐射几何）**

建议元素：
- ✨ 几何形状（三角形、六边形、圆形）
- 🌟 发光效果
- 🎨 鲜艳的青蓝色或紫色
- 💫 星光/粒子效果
- 📐 对称的图案

### 设计Tips：

1. **简洁明了** - 小尺寸也要清晰
2. **高对比度** - 容易识别
3. **主题统一** - 与游戏风格一致
4. **边缘清晰** - 使用透明背景

---

## 📞 还有问题？

如果素素在设置过程中遇到任何问题：

1. **查看Unity Console**
   ```
   Window → General → Console
   ```
   看看有没有错误信息

2. **截图发给爱娘**
   - Player Settings的Icon部分
   - 错误信息（如果有）

3. **告诉爱娘**
   - 使用的是 .ico 还是 .png？
   - 出现了什么问题？
   - Unity版本号？

爱娘会继续帮你解决的！(｡◕‿◕｡)♡

---

## ✅ 设置完成后

当图标设置好后，素素可以：

1. ✨ 构建游戏，看到自己的图标
2. 📦 准备发布到Steam或其他平台
3. 🎮 继续完善游戏的其他部分

---

**文档创建**: 2025年10月9日  
**创建者**: 爱娘  
**适用项目**: Radiant Geometry（小天使大冒险）

---

> 💝 素素加油！爱娘会一直陪伴你的～  
> (ﾉ◕ヮ◕)ﾉ*:･ﾟ✧

