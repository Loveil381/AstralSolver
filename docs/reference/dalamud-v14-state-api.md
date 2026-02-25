# Dalamud v14 战斗状态相关 API 速查笔记

本笔记汇总了在 Dalamud v14 环境下，通过 `StateTracker` 读取游戏状态、技能冷却及 Buffer 信息所需的关键服务与数据结构。

---

## 🚀 核心服务 (Dalamud Services)

### IObjectTable
- **注入方式**: `[PluginService] public static IObjectTable ObjectTable { get; private set; } = null!;`
- **关键属性**:
  - `LocalPlayer (IPlayerCharacter?)`: 获取当前玩家对象。v14 中 **必须** 从此处获取，而非 `IClientState`。
  - `Length (int)`: 当前场景中的对象数量。
- **示例**:
  ```csharp
  var player = ObjectTable.LocalPlayer;
  if (player != null) {
      var hp = player.CurrentHp;
  }
  ```

### IPlayerState (v14 新增)
- **注入方式**: `[PluginService] public static IPlayerState PlayerState { get; private set; } = null!;`
- **关键属性**:
  - `ContentId (ulong)`: 稳定的玩家角色 ID（跨场景不变量）。
  - `ClassJob (RowRef<ClassJob>)`: 当前职业。
  - `Level (uint)`: 当前等级。
  - `IsLevelSynced (bool)`: 是否处于同步状态。
- **笔记**: 相比 `IClientState`，该服务在登录后即有效，且不依赖于 GameObject 的存活周期。

### IPartyList
- **注入方式**: `[PluginService] public static IPartyList PartyList { get; private set; } = null!;`
- **关键属性**:
  - `Length (int)`: 小队人数。
  - `Index [int]`: 获取 `IPartyMember` 实例。
- **性能**: 遍历小队成员时无需使用 `SearchById`，直接索引访问效率最高。

### ICondition
- **注入方式**: `[PluginService] public static ICondition Condition { get; private set; } = null!;`
- **关键属性**:
  - `this[ConditionFlag.BoundByDuty]`: 是否在副本中。
  - `this[ConditionFlag.InCombat]`: 是否在战斗状态。
- **性能**: 每帧读取开销极低。

---

## ⚡ 内存交互 (FFXIVClientStructs)

### ActionManager
- **获取方式**: `ActionManager.Instance()`
- **关键字段/方法**:
  - `AnimationLock (float)`: 当前硬直剩余时间（秒）。
  - `CastTimeElapsed (float)` / `CastTimeTotal (float)`: 读条进度。
  - `Combo (struct)`: 连击状态（`ActionId`, `Timer`）。
  - `GetRecastTimeElapsed(ActionType, uint actionId)`: 已过冷却时间。
  - `GetRecastTime(ActionType, uint actionId)`: 总冷却时间。
- **示例**:
  ```csharp
  uint actionId = 3596; // 凶星
  float elapsed = ActionManager.Instance()->GetRecastTimeElapsed(ActionType.Action, actionId);
  ```

### StatusManager
- **获取方式**: `GameObject.Character.Character*->StatusManager`
- **关键成员**:
  - `HasStatus(uint statusId)`: 判断是否有特定 Buff/Debuff。
  - `_status (FixedSizeArray60<Status>)`: 存储所有状态的数组，每项包含 `StatusId`, `RemainingTime`, `SourceObject`。
- **性能**: 检查自身 Buff 建议使用 `HasStatus`，遍历小队 Buff 时建议使用 `NumValidStatuses` 限制循环。

---

## 💡 性能与最佳实践

1.  **热路径优化**: `StateTracker` 的 `Update` 方法每帧运行。避免在循环中进行堆分配（例如拼凑临时 List），改用 `Span<T>` 或数组缓存。
2.  **空检查**: 游戏对象（尤其是目标和队友）可能随时消失，访问任何 `IntPtr` 或 `GameObject*` 之前必须核验。
3.  **时间缩放**: 内存中的 `float` 时间通常以秒为单位，且随游戏帧律更新。
4.  **同步性**: 部分 Lumina Excel 数据载入是异步或延迟的（如 `RowRef`），在插件启动初期需检查 `.IsLoaded`。

---

## 🛠️ StateTracker 最小服务清单 (StateTracker Service Set)

为实现完整的战斗快照，`StateTracker` 需要以下服务支持：
1.  `IObjectTable` (本地球员/目标)
2.  `IPartyList` (队友血量/位置)
3.  `IPlayerState` (等级/经验)
4.  `ICondition` (战斗标记/副本环境)
5.  `IFramework` (驱动 Update 事件)
6.  `ITargetManager` (获取当前选中的敌人)
7.  `IDataManager` (获取 Lumina 表格数据，如技能威力和属性)

---
*Created on 2026-02-23 for AstralSolver Project*
