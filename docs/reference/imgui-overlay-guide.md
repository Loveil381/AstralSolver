# ImGui Overlay 绘制速查指南
> 用途: Navigator UI 开发参考

## 一、Dalamud 窗口系统
Dalamud 建议使用其提供的 **Windowing System** 来管理窗口，而不是手动在 `IUiBuilder.Draw` 中调用 `ImGui.Begin`。

### 1. Window 基类 vs 直接 IUiBuilder.Draw
- **Window 基类 (推荐)**: 继承自 `Dalamud.Interface.Windowing.Window`。自动处理配置持久化、聚焦逻辑和 Z-Order。
- **直接绘图**: 仅在极简单的全局覆盖（如图标浮窗）时使用，在 `pluginInterface.UiBuilder.Draw` 事件中调用 `ImGui.Begin`。

### 2. ImGuiWindowFlags 常用组合
- `ImGuiWindowFlags.NoDecoration`: 移除标题栏、调整大小手柄、滚动条等。相当于 `NoTitleBar | NoResize | NoScrollbar | NoCollapse`。
- `ImGuiWindowFlags.NoInputs`: **穿透鼠标点击**。这对悬浮层至关重要，用户可以点击悬浮层背后的游戏。
- `ImGuiWindowFlags.NoBackground`: 透明背景。
- `ImGuiWindowFlags.AlwaysAutoResize`: 窗口大小随内容自动调整。
- `ImGuiWindowFlags.NoFocusOnAppearing`: 出现时不抢占输入焦点。

### 3. 悬浮层推荐 Flags 组合
```csharp
// 典型的战斗引导图标 Flags
var flags = ImGuiWindowFlags.NoDecoration | 
            ImGuiWindowFlags.NoInputs | 
            ImGuiWindowFlags.NoBackground | 
            ImGuiWindowFlags.AlwaysAutoResize;
```

## 二、绘制基础
### 1. 获取 DrawList
- `ImGui.GetWindowDrawList()`: 在当前窗口坐标系内绘制，受窗口剪裁。
- `ImGui.GetForegroundDrawList()`: 在**所有**窗口最上层绘制。适合跨窗口的连线或全局警告。
- `ImGui.GetBackgroundDrawList()`: 在 UI 背景层绘制，通常在游戏背后。

### 2. 核心 API
- `DrawList.AddRectFilled(pMin, pMax, color, rounding)`: 填充矩形（用于底色或进度条）。
- `DrawList.AddText(pos, color, text)`: 绘制文字。
- `DrawList.AddCircleFilled(center, radius, color)`: 填充圆形。
- `DrawList.AddLine(p1, p2, color, thickness)`: 绘制线条。

### 3. 颜色与坐标
- **颜色**: 使用 `uint` 格式（ABGR）。推荐 `ImGui.ColorConvertFloat4ToU32(new Vector4(r, g, b, a))`。
- **坐标系**:
    - `ImGui.GetCursorScreenPos()`: 获取当前绘图组件的屏幕绝对坐标。
    - `AddRect` 等方法默认接受屏幕绝对坐标。

## 三、技能图标绘制
### 1. 加载纹理
使用 Dalamud 的 `ITextureProvider`：
```csharp
// 注入 ITextureProvider
var iconTexture = textureProvider.GetFromGameIcon(iconId).GetWrapOrDefault();
var handle = iconTexture.ImGuiHandle;
```

### 2. 绘制到 DrawList
```csharp
// 绘制 40x40 的技能图标
Vector2 pMin = ImGui.GetCursorScreenPos();
Vector2 pMax = new Vector2(pMin.X + 40, pMin.Y + 40);
drawList.AddImage(handle, pMin, pMax);
```

### 3. 图标 ID 查找
- 可通过 Lumina 访问 `Action` 表获取 `Icon` 字段。
- 高级玩法：使用 `GetFromGameIcon(iconId).GetWrapOrDefault()` 并结合 `IDalamudTextureWrap` 进行缓存。

## 四、动画与过渡
- **时间源**: 使用 `ImGui.GetTime()` 获取自系统启动以来的秒数。
- **渐变**: `float alpha = 0.5f + 0.5f * (float)Math.Sin(ImGui.GetTime() * 2.0);`
- **平滑插值**: 记录上一帧的位置/透明度，计算当前帧应达到的目标，每帧步进。

## 五、性能注意
1. **避免每帧内存分配**:
    - 用于描述技能理由的文字应尽量预存，不要每帧 `string.Format`。
    - 列表容器复用 `List.Clear()` 而非 `new`。
2. **纹理缓存**:
    - `ITextureProvider` 获取的 `IDalamudTextureWrap` 应该缓存 handle，否则高频查询会有开销。
3. **绘制耗时**:
    - 简单的 UI 系统应控制在 **0.1ms - 0.3ms**。
    - 使用 `Stopwatch` 监控 `UI.Draw` 方法。

## 六、参考项目中的有用模式
### 1. RotationSolver 的图标模式
- **双轨显示**: 左侧显示当前正在连击或可用的核心 GCD，右侧显示建议穿插的 oGCD。
- **光圈提示**: 为推荐最高的技能图标添加一个闪烁的 `AddCircle` 边框。
- **队列预览**: 绘制多个图标并按透明度递减，模拟"即将到来"的视觉感。

### 2. BossMod 的时间轴模式
- **固定锚点**: 轴心（当前时间点）固定在屏幕某处，事件块随时间上升/下降。
- **预警分级**: 根据事件优先级改变 `AddRectFilled` 的颜色（警告=橙色，致命=红色）。
- **空间投影**: 将 3D 世界坐标转换到 2D 屏幕坐标（`gameGui.WorldToScreen`）在玩家脚下画圈。
