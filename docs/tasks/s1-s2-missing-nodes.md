# Goal

S1 结尾留下 4 个始终无法解决的异常节点（叶子 / 经脉 / 果实 / 根系各一个）。玩家查看后它们发红光、浮出矩阵，不再退回。全部浮出后播 S1_002–004 + 询问，进入 S2。
S2 开局植株照常组织成回路形态并连线，但这 4 个节点的位置空着（连线断开），缺口有引导外发光，4 个节点散落在网格周围。玩家对比节点详情，把它们拖回正确的缺口：靠近正确缺口时自动伸线吸附，松手落位；放错时没有反应，松手后飘回原处，Agent 提示"此模块与当前区域系统不兼容。"。4 个都补齐后系统自动播放浇水动画（玩家不再自己拖线连接），之后接原来的 S2 后续剧情。

本 task 只做玩法，键鼠就能完整验收，**不碰 VR 代码**（`Interaction/XR/`、`XROriginBuilder`、XR 设置）。和 VR 迁移的衔接只靠下面"输入约定"里的接口。

# 输入约定（和 VR 迁移的唯一接口）

玩法代码只能用 `ctx.pointer` 上下面这些成员。迁移前的 `PointerInput` 和迁移后的 `InteractionSource` / `InteractionRouter` 都有，两边合并时不用改玩法：

| 用途 | 成员 | PC 手势 | VR 迁移后（不在本 task） |
|---|---|---|---|
| 授权 Agent 尝试一次 | `Tap(int)` | 左键点节点 | 扳机选中 + A |
| 查看详情 | `ctx.Inspected(int)`（由 `InspectStart` 触发） | 右键按住 | 扳机选中 + 握键 |
| 拖散落节点 | `DragStart(int)` / `DragEnd()`；拖动中每帧读 `CurrentRay` | 左键按住拖 | 扳机按住扫 |
| 点空白 | `TapEmpty()` | 左键空白 | A（无选中） |

- 不读 `Mouse`、`Keyboard`、屏幕坐标、`PointerPosition`，也不用 `Camera.ScreenPointToRay`。拖拽位置只由 `CurrentRay` 和植株本地空间决定。
- S2 拖拽要求 `allowNodeDrag = true`，和现在 S2 的做法一样，进关时保存、离关时恢复。
- "朝玩家的方向"统一通过 `StageContext` 上的一个新方法取得（例如 `ViewerPosition()`）。这个 task 里它返回 `PlantFit` 的构图视点（有 `viewPoint` 就用它，否则用 `viewCamera`）。迁移后它自动变成 ViewAnchor，不跟头显。玩法里不要直接用 `Camera.main`。
- 浮出、散落、落位全部走 `NodeMorpher.SetPoseOverride`，不加 Collider / GameObject。

# Design decisions (confirmed 2026-10-16)

S1

- 删掉 S1 的全部中等难度问题，只剩简单问题和困难问题。困难问题从"最多 4 个果实"改成 Leaf / Stem（= 经脉）/ Fruit / Root 各一个。
- 浮出条件：简单问题全部解决，并且这个困难节点至少尝试失败过 1 次。不满足时查看只显示详情，和现在一样。
- 满足条件后查看一次（`ctx.Inspected`）：节点永久浮出、不再退回，外圈发红光（复用 `NodeHaloRenderer.SetGlow`，和 S2 脉冲源同一套外发光）。
- 浮出位置：在植株本地空间，朝 `ViewerPosition()` 方向飘出 10–15 cm，停在矩阵外侧，轻微悬浮晃动。
- 通关：4 个全部浮出，播 S1_002–004，弹 S1_005 询问，选 Yes 后进 S2。原来"困难尝试 ≥ 3 次"的条件删掉。
- S1 到 S2 变形时，这 4 个节点不变成回路姿态，而是直接飘到 S2 的散落位置（姿态覆盖在变形中照常叠加，见 `NodeMorpher.Animate`）。

S2

- 其余节点照原来的方式变形成回路并连线。缺失节点自己那条连线和它子节点的连线都隐藏，看起来"缺了中间节点"。
- 缺口位置（缺失节点在 Circuit 形态下的槽位）显示和脉冲源一样的呼吸外发光（`sourceGlowColor` + 呼吸频率）。
- 节点详情不改：沿用现有 S2 Project 档详情。玩家靠对比散落节点和缺口两侧节点的详情来判断。散落节点也能查看详情。
- 拖拽：从散落节点按住拖动，节点跟着 `CurrentRay` 移动。移动平面过拖拽起点、垂直于拖拽开始时的射线方向（PC 上等于平行于屏幕）。
- 吸附半径：植株本地空间内约 1.5 个节点间距（Inspector 可调）。
  - 进入正确缺口的半径：缺口两侧节点向拖拽中的节点伸出连线（从 0 长到满），缺口外发光变强。拖出半径时连线收回。松手后节点缓动落回槽位，缺口外发光关闭，连线恢复正常显示。
  - 进入错误缺口的半径：没有任何反馈。松手后飘回散落位置，Agent 播固定一句"此模块与当前区域系统不兼容。"。
  - 不在任何缺口附近时松手：静默飘回，不播台词。
- 4 个补齐之前：点击或拖拽其他节点时，Agent 提示路径不存在（新台词）。对白正在播时不打断，同一句有冷却，避免刷屏。
- 4 个补齐后：先播"路径已重建"（新台词），然后自动浇水。脉冲从树根沿供给线路逐段推进（复用现有 `NodeHaloRenderer.Flash` 脉冲和连线变蓝），经过的缺水节点调用 `issues.Resolve`，连线的直角程度按进度降到 0（S3 变形仍从直线开始）。浇完播 S2_006–007，之后的流程不变：查看果实 → S2_008 → S3。
- 原来"玩家从脉冲源拖拽连线"的玩法删掉（`docs/tasks/S2_PULSE_ROUTE.md` 被本 task 取代，只保留它的脉冲视觉）。

# 缺失节点怎么选

- S1 和 S2 都要用这 4 个节点。跳关直接进 S2 时 S1 没跑过，所以挑选规则放在一个共用的纯函数里（新建 `Stages/MissingNodePicker.cs`），两个阶段各调一次，结果相同。
- 每个器官挑一个"中间节点"：有父节点，并且至少有一个子节点有连线（`NodeLinkRenderer.HasLink`），这样缺口两侧都有断线。找不到时退回到末端节点，并打 Warning。
- 不写死 id：Inspector 留一个可选的覆盖列表，运行时把挑出的 id 打 Log。不重新采样 `pepper_plant`。
- 散落位置：取其余节点 Circuit 布局的包围盒，向外留边距，在左 / 右 / 下方各放一个，位置固定、不随机。距离不超出 PlantFit 构图（拖拽时不会出画面）。

# 开源实现

- 缓动（浮出、飘回、落位、连线生长）：优先用 [PrimeTween](https://github.com/KyryloKuzyk/PrimeTween)（MIT，零 GC 分配，支持 IL2CPP）。节点不是 GameObject，用 `Tween.Custom(...)` 驱动姿态覆盖的权重或位置。
  - 安装时钉住具体 tag，确认 Unity 6000.3 编译通过。Quest 出包验证留给 VR 迁移，届时不要打开 `PRIME_TWEEN_SAFETY_CHECKS`（IL2CPP 下有已知问题，见 [issue #65](https://github.com/KyryloKuzyk/PrimeTween/issues/65)）。
  - 改了 `Packages/manifest.json` 时，提交前去掉本机的 `com.tripo3d.unitybridge` 一行。
  - 不兼容或安装受阻时，回退到项目现有的协程 + `SmoothStep` 写法（`TutorialStage.MergeIntoMatrix`），不另找其他库。
- 吸附：参考 XRI `XRSocketInteractor` 的"悬停 → 预览 → 松手吸入"状态划分和半径判定。只借思路、不直接用，因为它要求节点有 Collider。

# Existing reference

- 姿态覆盖和缓动：`Stages/TutorialStage.cs` `MergeIntoMatrix`。
- 外发光：`Morph/NodeHaloRenderer.cs`（`SetGlow`、`Flash`），用法见 `Stages/S2CircuitStage.cs` 的 `SetSource` / `Update`。
- 连线：`Morph/NodeLinkRenderer.cs`（`SetLinkColor`、`SetElbowScale`、`FindLink`、`HasLink`）。
- 拖拽和射线：S2 现在的 `HandleDragStart` / `UpdateDragLine`。
- 询问和通关：`S1MatrixStage.TryOfferQuery`。

# Files likely involved

- `Stages/MissingNodePicker.cs`（新建，带 `.meta`）。
- `Stages/S1MatrixStage.cs`：删掉 medium；困难节点改成 4 个器官；订阅 `ctx.Inspected` 处理浮出；红光；新的通关条件；任务面板去掉 medium。
- `Stages/S2CircuitStage.cs`：删掉玩家拖线；新增散落、拖拽、吸附、飘回、错误提示和补齐后自动浇水。保留 `NodeLinked`（浇水时发出），新增 `NodePlaced` / `PlaceRejected`。`TraceStarted/Ended` 改成拖散落节点时发出，并同步 `Audio/GameAudioCues.cs`。
- `Stages/StageContext.cs`：加 `ViewerPosition()`。
- `Morph/NodeLinkRenderer.cs`：按子节点隐藏 / 显示连线；连线生长进度（0–1）。
- `Morph/NodeHaloRenderer.cs`：在任意本地姿态画外发光（缺口处没有节点），例如 `SetGlowAt(key, pose, …)` / `ClearAt(key)`。
- `docs/script/02_dialogue.csv`：新增 3 行（通道 Agent弹窗_02，`待确认` 列标注）：
  - `S2_NOPATH`："【占位】供给路径不存在，无法执行资源调配。"
  - `S2_WRONG`："此模块与当前区域系统不兼容。"
  - `S2_PATCHED`："【占位】路径已重建，开始自动供给。"
- `Stages/Editor/StageAssets.cs`、`Core/Editor/MainSceneMenu.cs`：新段 key 和字段接线，改完重跑 Ghost → Core → Build Main Scene。
- 不改：`Interaction/` 下所有文件、`Gameplay/G3TestDriver.cs`、`NodeIssueSystem` 的 Medium 枚举。

# 实施步骤（建议每步一个对话）

1. 基础：PrimeTween（或决定回退）、`MissingNodePicker`、连线隐藏 / 生长、任意位置外发光、`ViewerPosition()`。验收：Console 打出 4 个 id，用临时调试开关能看到断线和缺口发光。
2. S1：全部 S1 条目，包括变形时飘到散落位置。
3. S2 补缺口：拖拽、吸附、飘回、两种提示。补齐后暂时直接把缺水节点全部解决，接上 S2_006。
4. 自动浇水 + 台词 + 场景重建 + 音效挂点 + 跳关测试 + 按 `CLOSEOUT.md` 更新文档。

# Acceptance criteria（全部用键鼠在 Play 里验）

S1

- 不再出现中等问题，任务面板和完成率不含中等问题。
- 4 个困难节点分别在叶子、茎、果实、根系上，Console 打出 id，每次一样。
- 简单问题没全部解决，或者某个困难节点还没尝试过时，右键它只显示详情，不浮出。
- 满足条件后右键一次：节点朝玩家方向飘出并发红光；松开右键、点别处都不会退回。
- 4 个都浮出后播 S1_002–004 和询问，选 Yes 进 S2。
- 变形到 S2 时，这 4 个节点直接飘到散落位置。

S2

- 其余节点组成回路并连线，4 个缺口处断线并有呼吸外发光。
- 补齐前点或拖其他节点：Agent 提示路径不存在，不刷屏。
- 拖到正确缺口附近：两侧伸出连线；拖开时收回；松手后落位，外发光熄灭，连线正常。
- 拖到错误缺口附近：无反馈；松手后飘回，Agent 播"此模块与当前区域系统不兼容。"。
- 空白处松手：静默飘回。
- 散落节点右键能看详情。
- 拖拽中途换缺口、快速连续拖放，节点不会卡在半路。
- 4 个补齐后自动浇水，缺水节点逐个恢复，连线变直；之后 S2_006–007 → 查看果实 → S2_008 → S3，和原流程一致。

通用

- 跳关：Shift + 数字直接进 S2，看到同样的 4 个缺口；S1 / S2 中途按 N 或跳到其他关，姿态覆盖、外发光、隐藏的连线、红光全部清掉。
- 玩法代码里搜不到 `Mouse.` / `Keyboard.` / `ScreenPointToRay` / `Camera.main`。
- 编译通过；`Main.unity` 已重建；Tutorial、S3、S4、Pick 的交互仍正常。

# Out of scope

- 所有 VR 相关改动：`Interaction/XR/`、手柄手势、选中突起和浮出 / 散落节点的配合、Quest 出包和实机验收。合并后在 `docs/tasks/vr-migration.md` 里补一条"S1 浮出 / S2 补缺口的 VR 验收"。
- 不改节点详情文案和 `NodeDetails.asset`。
- 不改 S3 及之后的流程。
- 不把 HUD 改成 World Space。
- 不重新采样 `pepper_plant`，不加节点 Collider。
- 新台词只写占位，定稿和配音由策划提供。
