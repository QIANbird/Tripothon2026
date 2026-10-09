# Architecture

## Major systems

| 系统 | 目录 | 职责 |
|---|---|---|
| Flow | `Scripts/Core` | `GameFlow` 按顺序运行 `Stage`；进关时触发变形 |
| Stages | `Scripts/Stages` | 每关的玩法和通关判定；`StageContext` 提供共用引用 |
| Morph | `Scripts/Morph` | 同一组节点在 5 种形态之间插值渲染（GPU instancing）；逐节点的显示状态；连线；节点外发光圈（`NodeHaloRenderer`）；PlantFit 构图；写实模型交接 |
| Gameplay | `Scripts/Gameplay` | `NodeIssueSystem`：问题的难度、重试和解决状态 |
| Interaction | `Scripts/Interaction` | `PointerInput`（交互发起方）、`DialogueAdvanceInput`（PC 端对白推进）、`NodePicker`（射线测节点）、`TargetRotator`、`IInteractable` |
| Agent UI | `Scripts/Agent` | 任务面板、详情弹窗、Yes 询问框（HUD） |
| Player | `Scripts/Player` | Pick 阶段的第一人称玩家：`FirstPersonMotor`（CharacterController 移动、转头、蹲下）、`PlayerRig`（开关玩家、接管 / 归还主相机）；`Player/PC/` 下是 PC 专用的 `PcCursorLock`、`PcCrosshair` |
| Narrative | `Scripts/Narrative` | 对白播放、字幕和 Agent 弹窗、黑屏、节点详情文本表 |

## Ownership of state

- 当前关卡：`GameFlow`。
- 当前形态和逐节点的显示（闪烁、高亮、着色、脉冲、隐藏、写实度）：`NodeMorpher`。其他系统只调它的接口，不缓存颜色。
- 问题状态（剩余点击次数、尝试次数、是否解决）：`NodeIssueSystem`。
- 关卡内的玩法状态（已找到的虫子、S2 的路径、是否已查看果实）：各 Stage 自己，Exit 时清掉。
- 主相机的父物体：平时在场景根（固定机位）；Pick 阶段由 `PlayerRig` 挂到 `PlayerRoot/CameraPivot` 下，离开时还原。
- 植株的世界缩放和位置：`PlantFit`（Pick 阶段 `Hold` 住真实尺寸，自动适配暂停）。旋转：`TargetRotator`，在 PlantFit 的父空间里转。
- 静态数据：`PlantNodeSet`（节点和布局）、`DialogueSequence`、`DialogueLibrary`（台词表索引）、`NodeDetailTable`，都是 ScriptableObject，运行时只读。

## Communication between systems

- 全部用 C# 事件，单向调用：
  - 上层订阅下层：Stage 订阅 `PointerInput`、`NodeIssueSystem`、`DialoguePlayer`。
  - 下层不认识上层：`NodeMorpher` 不知道有 Stage 存在。
- `GameFlow` → `Stage.Enter/Exit` + `NodeMorpher.MorphTo`；`Stage.Completed` → `GameFlow.Next`。
- `NodeMorpher.MorphStarted` → `PlantFit`（构图过渡和变形同步）。
- `StageContext` 统一转发两类输入：左键 Tap 触发脉冲反馈，右键 Inspect 弹出详情。阶段只提供详情内容。
- Agent UI 和 Narrative 只负责显示，数据由 Stage 推送进去。

## Key interfaces and events

命名空间：`Ghost.Core`、`Ghost.Stages`、`Ghost.Morph`、`Ghost.Gameplay`、`Ghost.Interaction`、`Ghost.Agent`、`Ghost.Narrative`。节点一律用 `int id`（`PlantNode.id`）引用。

| 类型 | 事件 | 主要方法 / 属性 |
|---|---|---|
| `Stage`（抽象） | `Completed(Stage)` | `Enter()`、`Exit()`（子类重写要调 base）、`protected Complete()`、`form`、`changesForm`、`PanelText` |
| `IssueStage : Stage`（抽象） | — | 钩子：`OnNodeTapped(id)`、`OnIssueAttempted(issue, solved)`、`OnIssueResolved(issue)`、`PopupTitle/PopupBody(id)`、`Describe(id, …)` |
| `GameFlow` | `StageEntered(Stage)`、`StageExited(Stage)` | `Next()`、`JumpTo(index)`、`CurrentIndex`、`FormAt(index)` |
| `StageContext` | `Inspected(id)` | 共用引用（morpher、links、issues、pointer、picker、rotator、taskPanel、detailPopup、query、dialogue、detailTable）；`SetInspectProvider`、`RefreshInspect`、`SetPickFilter`、`DefaultFilter`、`IsNodeVisible`、`Node(id)`、`Play(sequence, onComplete)`、`ShowTaskPanel`、`ResetShared()`、`NearestNodeInLayout` |
| `PointerInput` | `Tap(id)`、`DragStart(id)`、`DragOver(id)`、`DragEnd`、`DragEmpty(Vector2)`、`DragEmptyEnd`、`HoverChanged(id)`、`InteractableTapped(IInteractable)`、`TapEmpty`、`InspectStart(id)`、`InspectEnd` | — |
| `IInteractable` | — | `OnTap()`、`OnHoverEnter()`、`OnHoverExit()` |
| `NodePicker` | — | `Pick(ray[, filter], out distance)`（-1 = 未命中）、`Filter`、`OrganOf(id)`、`TryGetNodeWorldPosition` |
| `TargetRotator` | — | `Enable/Disable`、`AddRotation(yaw, pitch)`、`ResetRotation(smooth)`、`CaptureRestPose` |
| `NodeMorpher`（partial，含 `NodeMorpherStates`） | `MorphStarted(MorphForm)`、`MorphCompleted(MorphForm)` | 形态：`MorphTo`、`SnapTo`、`CurrentForm`、`IsMorphing`。逐节点显示：`SetBlink`、`SetHighlight`、`SetTint`、`Pulse`、`Restore`、`RestoreAll`、`GetState`、`Hide/Show/SetVisible`、`IsHidden`、`GetNodeVisibility`、`TryGetNodeWorldSphere`。写实度：`Realness`、`ClearRealness`、`SetRealnessImmediate`、`RealReveal`（由 `RealModelHandoff` 写） |
| `PlantFit` | `Fitted` | `TransitionTo(form, seconds)`、`SnapTo(form)`、`Hold(localPos, scale, seconds)`、`Release(seconds)`、`IsHeld`、`GetFormBounds`、`ClearCache` |
| `PlayerRig` | — | `Activate()`（对齐当前相机姿态、接管相机、打开玩家）、`Deactivate()`、`IsActive` |
| `FirstPersonMotor` | — | `PlaceAt(eyePos, viewRot, crouched)`、`IsCrouching`、`EyeHeight`、`Pitch` |
| `NodeIssueSystem` | `IssueCreated`、`IssueAttempted(issue, solved)`、`IssueResolved` | `AddIssue(s)`、`GenerateRandom`、`TryAttempt(id)`、`Resolve`、`RemoveIssue`、`ClearAll`、`GetIssue/HasIssue`、`AllResolved`、`CountUnresolved`、`TotalAttempts` |
| `DialoguePlayer` | `LineStarted`、`LineFinished`、`SequenceFinished`、`Stopped` | `Play/Enqueue(sequence, onComplete)`、`Skip`、`Stop`、`CurrentLine` |
| `SubtitlePanel` | — | `Show(line)`、`Hide()`、`Advance()`（打字中补全，否则 `DialoguePlayer.Skip`）；按 `Speaker` 分到 `Channel.Subtitle` / `AgentPopup` |
| `ScreenBlackout` | — | `SetImmediate(black)`、`FadeTo(black, fade)` |
| `AgentQueryDialog` | `Answered(string)` | `Ask(question, onYes)`、`Hide`、`ConfirmYes` |
| `AgentTaskPanel` | `TaskChanged(index, TaskState)` | `SetTasks/AddTask/UpdateTask`、`SetMetric`、`Show/Hide`（当前 `showTaskPanel = false`，不显示） |
| `NodeDetailPopup` | — | `Show(id, title, body)`、`SetText`、`Release`、`Hide` |
| `DialogueLibrary`（SO） | — | `Get(id)`（段 key 或段内任意台词 ID）、`GetSection(section)`；由 `DialogueCsvImporter` 从 `docs/script/02_dialogue.csv` 生成 |
| `NodeDetailTable`（SO） | — | `Get(id, organ, depth)`、`GetName(organ, NameKind, fallback)`、`Format`、`DepthForStage(stageName)` |

关键枚举：`MorphForm { Matrix, Circuit, Network, Geometric, Real }`、`Organ { Soil, Root, Stem, Leaf, Bud, Fruit, Bug }`、`IssueDifficulty { Easy, Medium, Hard }`、`NodeVisualState { Normal, Blink, Highlight, Tint }`、`DetailDepth`、`Speaker`。

## Data flow (typical interactions)

- **左键点节点**：Input Action `Select` → `PointerInput` 用 `NodePicker.Pick`（经 `StageContext` 的筛选）→ `Tap(id)` → `StageContext` 调 `NodeMorpher.Pulse(id)` 做反馈 → 当前 Stage 处理（`IssueStage` 里调 `NodeIssueSystem.TryAttempt(id)` → `IssueAttempted` / `IssueResolved` → Stage 调 `NodeMorpher.Restore/SetTint` 并检查通关）。
- **右键查看**：`InspectStart(id)` → `StageContext` 调当前 Stage 设置的 `InspectProvider` 取标题/正文（通常来自 `NodeDetailTable`，深度由 `DepthForStage` 决定）→ `NodeDetailPopup.Show` → 触发 `Inspected(id)`；`InspectEnd` → `Release`，2.5 s 后淡出。
- **空白处拖动**：`DragEmpty(delta)` → `TargetRotator.AddRotation`（S3/S4 才 `Enable`）。
- **推进对白**：`PointerInput.TapEmpty`（点空白处；点中节点不触发）或 Input Action `Advance`（空格 / 回车）→ `DialogueAdvanceInput` → `SubtitlePanel.Advance()`。
- **换关**：`Stage.Completed`（需要时先 `AgentQueryDialog.Ask`，Yes 后再 `Complete`）→ `GameFlow.Next` → `Exit` 旧关 → `NodeMorpher.MorphTo(form)` → `MorphStarted` → `PlantFit.TransitionTo` → `Enter` 新关 → `StageContext.Play(开场对白)` → `DialoguePlayer.LineStarted` → `SubtitlePanel.Show`。
- **S4 写实化**：每摘一只虫子 → Stage 设 `NodeMorpher.Realness` 提高一档；进入 `Real` 时 `RealModelHandoff` 写 `RealReveal`，节点缩没、写实模型显现。

## Interface stability

**稳定（扩展点，可以依赖；改签名前先和团队确认）**

- `Stage` 生命周期契约：`Enter/Exit/Complete/Completed`，Enter 订阅、Exit 退订并 `ResetShared()`。
- `IssueStage` 的虚方法钩子。
- `PointerInput` 的事件集合与签名：VR 迁移时用新的发起方替换它，**签名必须保持不变**。
- `IInteractable`。
- `NodeMorpher` 的逐节点显示接口和 `MorphTo/MorphStarted`；`NodeIssueSystem` 的事件和查询方法。
- `DialoguePlayer.Play/Enqueue` + 事件；`NodeDetailTable.Get/Format`。
- 数据契约：节点 id 在 `pepper_plant.asset` 内固定；`MorphForm` 的顺序和取值。
- Input Action 名称：`Gameplay`（Point / Select / Inspect）、`Debug`（NextStage / JumpStage1–9）。

**不稳定（内部实现，可能随时改，不要从外部依赖）**

- `PlantFit` 的构图算法和参数；`LayoutGenerator` 的具体布局算法。
- Agent UI / 字幕的样式、布局和 HUD 实现（比赛期为 Screen Space Overlay，赛后改 World Space）。
- `AgentTaskPanel`（当前不显示）。
- 各具体 Stage（S1–S4、Tutorial、Intro）内部逻辑和写死的节点 id 列表。
- `MainSceneMenu` 的场景构建细节（场景层级结构以它为准，但不要在别处依赖具体层级路径）。
- 测试/调试用脚本：`G3TestDriver`、`AgentUITestDriver`、`PointerDebugLogger`、`MorphDebugSwitcher`、`GameFlowDebug`、`FakePlantGenerator`。

## Input abstraction

- `InputSystem_Actions.inputactions`：
  - `Gameplay` 表：Point、Select（左键）、Inspect（右键）。
  - `Debug` 表：NextStage、JumpStage1–9。
  - `Player` 表（Pick 阶段）：Move（WASD）、Look（鼠标 delta）、Crouch（C / 左 Ctrl）、Grab（左键）、Eat（E）。Grab / Eat 还没有使用方。
- `PointerInput` 是 PC 端唯一的交互发起方，把输入翻译成手势事件：
  - `Tap`
  - `DragStart`、`DragOver`、`DragEnd`
  - `DragEmpty`（在空白处拖动，用于旋转）
  - `InspectStart`、`InspectEnd`
  - 指针在 HUD 上时不往节点派发任何事件。
- 被交互物不读输入：节点通过 `NodePicker` 被选中；普通物体实现 `IInteractable`；HUD 按钮走 UGUI 的 EventSystem 和 InputSystemUIInputModule。
- VR 迁移：给同一组 Action 加 XR 绑定，用手柄射线发起方替换 `PointerInput`，保持事件签名不变。

## Scene/stage lifecycle

1. 菜单 Build Main Scene 生成 `Main.unity`：
   - 相机
   - PlantFit / Plant（NodeMorpher、Links、Issues、Rotator、Handoff）
   - Pointer
   - HUD
   - Dialogue
   - GameFlow（挂所有 Stage 和 StageContext）
2. Play 时 `GameFlow.Start` 进入第 0 关。
3. 进关：`Exit(旧关)` → `MorphTo(form)` + `Enter(新关)`，两者在同一帧。
4. Stage.Enter：调 `ctx.ResetShared()`，订阅事件，生成问题，播放开场对白。
5. Stage 判定通关后调 `Complete()`，有需要时先弹 AI 询问，等玩家选 Yes。
6. Stage.Exit：退订事件，恢复临时改过的参数（拾取半径、拖拽开关、旋转），调 `ctx.ResetShared()`。

## Important extension points

- **新增关卡**：写一个 `Stage` 子类（处理点击问题的参考 `IssueStage`，自定义手势的参考 `S2CircuitStage`），然后在 `MainSceneMenu.Stages` 加一行，并在构建分支里挂上组件。
- **新增节点表现**：在 `NodeMorpherStates` 里加状态或叠加效果（参考 `Pulse`）。
- **新增手势或输入**：先在 `.inputactions` 里加 Action，再给 `PointerInput` 加事件。
- **新增对白或文本**：在 `StageAssets` / `NarrativeAssets` 里加 `Ensure*`，生成 ScriptableObject。
- **新增布局形态**：修改 `LayoutGenerator`，只重算 poses，保持节点 id 不变。
- **构图规则**：调 `PlantFit` 的参数，或改它按形态计算包围体的逻辑。
