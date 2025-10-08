# Unity MCP 使用指南

## 概述
本文档记录了Unity MCP (Model Context Protocol) 工具的标准使用语法和最佳实践，确保正确使用所有Unity相关工具。

## 1. 资源管理工具 (Resource Tools)

### 1.1 list_resources - 列出项目资源
```python
await list_resources(
    pattern: str = "*.cs",           # 文件模式，默认*.cs
    under: str = "Assets",           # 搜索目录，默认Assets
    limit: int = 200,                # 结果限制
    project_root: str = None         # 项目根目录
)
```

### 1.2 read_resource - 读取资源内容
```python
await read_resource(
    uri: str,                        # 资源URI (unity://path/...)
    start_line: int = None,          # 起始行号(0-based)
    line_count: int = None,          # 读取行数
    head_bytes: int = None,          # 读取字节数
    tail_lines: int = None,          # 末尾行数
    project_root: str = None,        # 项目根目录
    request: str = None              # 请求ID
)
```

### 1.3 find_in_file - 文件内搜索
```python
await find_in_file(
    uri: str,                        # 资源URI
    pattern: str,                    # 正则表达式
    ignore_case: bool = True,        # 忽略大小写
    project_root: str = None,        # 项目根目录
    max_results: int = 200           # 最大结果数
)
```

## 2. 脚本管理工具 (Script Management)

### 2.1 apply_text_edits - 应用文本编辑
```python
apply_text_edits(
    uri: str,                        # 脚本URI
    edits: list[dict],               # 编辑列表
    precondition_sha256: str = None, # 前置条件SHA256
    strict: bool = None,             # 严格模式
    options: dict = None             # 选项
)
```

**编辑格式：**
```python
edits = [
    {
        "startLine": 1,              # 起始行(1-based)
        "startCol": 1,               # 起始列(1-based)
        "endLine": 5,                # 结束行(1-based)
        "endCol": 10,                # 结束列(1-based)
        "newText": "新的代码内容"      # 新文本
    }
]
```

### 2.2 create_script - 创建脚本
```python
create_script(
    path: str,                       # 脚本路径
    contents: str,                   # 脚本内容
    script_type: str = None,         # 脚本类型
    namespace: str = None            # 命名空间
)
```

### 2.3 delete_script - 删除脚本
```python
delete_script(
    uri: str                         # 脚本URI
)
```

### 2.4 validate_script - 验证脚本
```python
validate_script(
    uri: str,                        # 脚本URI
    level: str = "basic",            # 验证级别: basic/standard
    include_diagnostics: bool = False # 包含诊断信息
)
```

### 2.5 script_apply_edits - 结构化脚本编辑
```python
script_apply_edits(
    name: str,                       # 脚本名称
    path: str,                       # 脚本路径
    edits: list[dict],               # 编辑列表
    options: dict = None,            # 选项
    script_type: str = "MonoBehaviour", # 脚本类型
    namespace: str = None            # 命名空间
)
```

**结构化编辑格式：**
```python
edits = [
    {
        "op": "replace_method",      # 操作类型
        "className": "MyClass",      # 类名
        "methodName": "MyMethod",    # 方法名
        "replacement": "public void MyMethod() { ... }"  # 替换内容
    },
    {
        "op": "insert_method",       # 插入方法
        "className": "MyClass",      # 类名
        "replacement": "public void NewMethod() { ... }", # 新方法
        "position": "after",         # 位置
        "afterMethodName": "MyMethod" # 参考方法
    }
]
```

## 3. 资源管理工具 (Asset Management)

### 3.1 manage_asset - 资源管理
```python
manage_asset(
    action: str,                     # 操作类型
    path: str,                       # 资源路径
    asset_type: str = None,          # 资源类型
    properties: dict = None,         # 属性字典
    destination: str = None,         # 目标路径
    generate_preview: bool = False,  # 生成预览
    search_pattern: str = None,      # 搜索模式
    filter_type: str = None,         # 过滤类型
    filter_date_after: str = None,   # 日期过滤
    page_size: int = None,           # 页面大小
    page_number: int = None          # 页码
)
```

**支持的操作：**
- `import` - 导入资源
- `create` - 创建资源
- `modify` - 修改资源
- `delete` - 删除资源
- `duplicate` - 复制资源
- `move` - 移动资源
- `rename` - 重命名资源
- `search` - 搜索资源
- `get_info` - 获取信息
- `create_folder` - 创建文件夹
- `get_components` - 获取组件

## 4. 编辑器管理工具 (Editor Management)

### 4.1 manage_editor - 编辑器管理
```python
manage_editor(
    action: str,                     # 操作类型
    wait_for_completion: bool = None, # 等待完成
    tool_name: str = None,           # 工具名称
    tag_name: str = None,            # 标签名称
    layer_name: str = None           # 层名称
)
```

**支持的操作：**
- `telemetry_status` - 遥测状态
- `telemetry_ping` - 遥测ping
- `play` - 播放
- `pause` - 暂停
- `stop` - 停止
- `get_state` - 获取状态
- `get_project_root` - 获取项目根目录
- `get_windows` - 获取窗口
- `get_active_tool` - 获取活动工具
- `get_selection` - 获取选择
- `get_prefab_stage` - 获取预制体阶段
- `set_active_tool` - 设置活动工具
- `add_tag` - 添加标签
- `remove_tag` - 移除标签
- `get_tags` - 获取标签
- `add_layer` - 添加层
- `remove_layer` - 移除层
- `get_layers` - 获取层

## 5. 游戏对象管理工具 (GameObject Management)

### 5.1 manage_gameobject - 游戏对象管理
```python
manage_gameobject(
    action: str,                     # 操作类型
    target: str = None,              # 目标对象
    search_method: str = None,       # 搜索方法
    name: str = None,                # 对象名称
    tag: str = None,                 # 标签
    parent: str = None,              # 父对象
    position: list[float] = None,    # 位置 [x, y, z]
    rotation: list[float] = None,    # 旋转 [x, y, z]
    scale: list[float] = None,       # 缩放 [x, y, z]
    components_to_add: list[str] = None, # 要添加的组件
    primitive_type: str = None,      # 原始类型
    save_as_prefab: bool = None,     # 保存为预制体
    prefab_path: str = None,         # 预制体路径
    prefab_folder: str = None,       # 预制体文件夹
    set_active: bool = None,         # 设置激活
    layer: str = None,               # 层
    components_to_remove: list[str] = None, # 要移除的组件
    component_properties: dict = None, # 组件属性
    search_term: str = None,         # 搜索词
    find_all: bool = None,           # 查找所有
    search_in_children: bool = None, # 在子对象中搜索
    search_inactive: bool = None,    # 搜索非激活对象
    component_name: str = None,      # 组件名称
    includeNonPublicSerialized: bool = None # 包含非公共序列化字段
)
```

**搜索方法：**
- `by_id` - 按ID
- `by_name` - 按名称
- `by_path` - 按路径
- `by_tag` - 按标签
- `by_layer` - 按层
- `by_component` - 按组件

## 6. 菜单项管理工具 (Menu Item Management)

### 6.1 manage_menu_item - 菜单项管理
```python
manage_menu_item(
    action: str,                     # 操作类型
    menu_path: str = None,           # 菜单路径
    search: str = None,              # 搜索词
    refresh: bool = None             # 刷新缓存
)
```

**支持的操作：**
- `execute` - 执行菜单项
- `list` - 列出菜单项
- `exists` - 检查菜单项是否存在

## 7. 预制体管理工具 (Prefab Management)

### 7.1 manage_prefabs - 预制体管理
```python
manage_prefabs(
    action: str,                     # 操作类型
    prefab_path: str = None,         # 预制体路径
    mode: str = None,                # 模式
    save_before_close: bool = None,  # 关闭前保存
    target: str = None,              # 目标对象
    allow_overwrite: bool = None,    # 允许覆盖
    search_inactive: bool = None     # 搜索非激活对象
)
```

**支持的操作：**
- `open_stage` - 打开阶段
- `close_stage` - 关闭阶段
- `save_open_stage` - 保存打开的阶段
- `create_from_gameobject` - 从游戏对象创建

## 8. 场景管理工具 (Scene Management)

### 8.1 manage_scene - 场景管理
```python
manage_scene(
    action: str,                     # 操作类型
    name: str = None,                # 场景名称
    path: str = None,                # 场景路径
    build_index: int = None          # 构建索引
)
```

**支持的操作：**
- `create` - 创建场景
- `load` - 加载场景
- `save` - 保存场景
- `get_hierarchy` - 获取层次结构
- `get_active` - 获取活动场景
- `get_build_settings` - 获取构建设置

## 9. 着色器管理工具 (Shader Management)

### 9.1 manage_shader - 着色器管理
```python
manage_shader(
    action: str,                     # 操作类型
    name: str,                       # 着色器名称
    path: str,                       # 着色器路径
    contents: str = None             # 着色器内容
)
```

**支持的操作：**
- `create` - 创建着色器
- `read` - 读取着色器
- `update` - 更新着色器
- `delete` - 删除着色器

## 10. 控制台管理工具 (Console Management)

### 10.1 read_console - 读取控制台
```python
read_console(
    action: str,                     # 操作类型
    types: list[str] = None,         # 消息类型
    count: int = None,               # 最大消息数
    filter_text: str = None,         # 文本过滤
    since_timestamp: str = None,     # 时间戳过滤
    format: str = None,              # 输出格式
    include_stacktrace: bool = None  # 包含堆栈跟踪
)
```

**支持的操作：**
- `get` - 获取控制台消息
- `clear` - 清除控制台

**消息类型：**
- `error` - 错误
- `warning` - 警告
- `log` - 日志
- `all` - 所有

**输出格式：**
- `plain` - 纯文本
- `detailed` - 详细
- `json` - JSON格式

## 关键语法要点

### URI格式
- `unity://path/Assets/...` - Unity资源路径
- `file://...` - 文件路径
- `Assets/...` - 相对路径

### 坐标系统
- 行号和列号都是1-based（从1开始）
- 位置、旋转、缩放使用Vector3格式：[x, y, z]

### 异步操作
- 资源相关工具使用 `await` 关键字
- 脚本管理工具是同步的

### 错误处理
- 所有工具返回包含 `success` 字段的字典
- 失败时返回错误信息和错误代码

### 内容编码
- 大内容使用Base64编码传输
- 自动处理编码/解码

### 选项配置
- 通过 `options` 字典传递额外配置
- 支持验证、刷新、应用模式等选项

## 🚨 重要提醒 - 使用前必读

### 控制台工具正确用法
```python
# ❌ 错误用法 - 缺少action参数
mcp_unityMCP_read_console()

# ✅ 正确用法
mcp_unityMCP_read_console(action="get")
mcp_unityMCP_read_console(action="clear")
```

### 资源工具异步调用
```python
# ❌ 错误用法 - 忘记await
list_resources(pattern="*.cs")

# ✅ 正确用法
await list_resources(pattern="*.cs")
```

### 脚本编辑工具参数
```python
# ❌ 错误用法 - 坐标从0开始
{"startLine": 0, "startCol": 0}

# ✅ 正确用法 - 坐标从1开始
{"startLine": 1, "startCol": 1}
```

### 常用工具快速参考
```python
# 读取控制台
mcp_unityMCP_read_console(action="get", types=["error", "warning"])

# 列出资源
await list_resources(pattern="*.cs", under="Assets/Scripts")

# 读取资源
await read_resource(uri="unity://path/Assets/Scripts/MyScript.cs")

# 创建脚本
create_script(path="Assets/Scripts/NewScript.cs", contents="// 代码内容")

# 应用文本编辑
apply_text_edits(uri="unity://path/Assets/Scripts/MyScript.cs", edits=[...])

# 管理游戏对象
manage_gameobject(action="create", name="NewObject", primitive_type="Cube")
```

## 最佳实践

### 1. 脚本编辑
- 优先使用 `script_apply_edits` 进行结构化编辑
- 使用 `apply_text_edits` 进行精确的文本编辑
- 编辑前先读取文件内容确认

### 2. 资源管理
- 使用相对路径 `Assets/...` 格式
- 创建资源时指定正确的类型
- 批量操作时使用分页

### 3. 游戏对象操作
- 使用合适的搜索方法
- 设置组件属性时使用正确的格式
- 批量操作时考虑性能

### 4. 错误处理
- 始终检查返回的 `success` 字段
- 处理错误时提供有意义的错误信息
- 使用适当的重试机制

### 5. 性能优化
- 避免频繁的GetComponent调用
- 使用缓存引用
- 批量操作时使用适当的限制

## 常见错误和解决方案

### 1. URI格式错误
**错误：** `Invalid URI format`
**解决：** 使用正确的URI格式，如 `unity://path/Assets/Scripts/MyScript.cs`

### 2. 坐标越界
**错误：** `Index out of bounds`
**解决：** 确保行号和列号在有效范围内，使用1-based坐标

### 3. 组件引用丢失
**错误：** `Component not found`
**解决：** 确保组件存在，使用正确的搜索方法

### 4. 权限错误
**错误：** `Access denied`
**解决：** 确保有足够的权限访问资源

### 5. 异步操作错误
**错误：** `Await not allowed`
**解决：** 确保在异步函数中使用await

---

**文档版本：** v1.0  
**创建日期：** 2025年1月8日  
**最后更新：** 2025年1月8日  
**维护者：** AI Assistant
