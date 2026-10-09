# Goal

S2 改成"脉冲引路 + 分段拖拽"：系统在当前脉冲源上沿固定线路发脉冲（指示箭头），玩家从脉冲源按住，沿脉冲方向拖过这一段（最多 5 个节点）连上；连完后段终点成为新的脉冲源，继续指向下一个问题节点。玩家不需要选方向。

# Existing reference

- `Assets/Scripts/Stages/S2CircuitStage.cs`（旧的自由拖拽版，通关条件、详情、任务面板沿用）
- `NodeMorpher.SetBlink / SetTint`、`NodeLinkRenderer.SetLinkColor`
- 发光圈参考 `NodeLinkRenderer` 的动态网格 + `Ghost/MorphLine` 材质

# Files likely involved

- `Assets/Scripts/Stages/S2CircuitStage.cs`
- `Assets/Scripts/Morph/NodeHaloRenderer.cs`（新增：节点外发光圈）
- `Assets/Scripts/Morph/NodeLinkRenderer.cs`（`elbowScale`：直角程度倍数）
- `Assets/Scripts/Stages/StageContext.cs`（`halos` 引用）、`Assets/Scripts/Core/Editor/MainSceneMenu.cs`（重建场景时挂上 NodeHaloRenderer）

# Acceptance criteria

- 进关后只有脉冲源（树根节点 0）是蓝色：节点本身蓝色闪烁 + 外圈蓝色呼吸发光，和问题节点的白色闪烁分得开；其余节点不预先染色。
- 脉冲 = 段内节点外圈的蓝色发光依次亮起，每 `pulseStep`（0.3 s）亮一个，按半衰期 `pulseHalfLife`（0.3 s）衰减：第三个亮起时第二个约剩 50%、第一个约剩 25%，看得出方向和连续性；走完一段停 `pulsePause` 再重来。已连上的节点不再参与脉冲。
- 拖拽线：从脉冲源按住拖拽时，从最后连上的节点拉出一条蓝线，另一端始终跟着指针（指针射线和植株平面的交点），拖拽结束或段走完需要重新按住时消失。原有的连线变蓝反馈保留。
- 连线直角程度：进关时是回路形态的直角折线（`linkElbow` = 1）。每结束一次拖拽，按"已连通节点 / 供给线路节点"的比例平滑降低，全部缺水恢复时为 0（两点直连，树形）；S2 → S3 的变形从树形开始。离开 S2 后那次变形结束时才恢复原值（网络形态本身是直线，看不出变化）。
- 每段最多 5 个节点（含脉冲源，`segmentLength`）。从脉冲源按住，依次拖过段内节点：连线变蓝、节点变蓝，缺水节点恢复。指针漏采时段内最多补 `maxBridgeHops` 个节点。
- 拖到段终点：下一段立即开始，同一次拖拽可以继续往下拖。
- 拖拽中断（松开）：已连上的不回退；从已连通集合重新计算下一段和脉冲源。
- 下一段的目标 = 离已连通集合最近（步数最少）的未恢复问题节点；走完一条枝自动回到分叉点。
- 线路上不出现无法解决的节点：自动挑选果实问题节点时跳过所有线路经过的节点；手动配置导致冲突时打 Warning。
- 从非脉冲源开始拖、或点击非脉冲源：脉冲源快速闪一下提示，不扣分不重置。
- 通关条件不变：缺水全部恢复 + 右键查看过果实 + 询问选 Yes。
- S1 / S3 / S4 交互不受影响。

# Out of scope

- 不改 Circuit 布局、PointerInput；连线渲染只加直角程度倍数，其他形态不受影响。
- 不改占位对白资产的文字（intro 文案"从支撑系统出发"需要策划另行替换）。
