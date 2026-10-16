# Goal

同一个工程可以打包到 Meta Quest 3 运行：S1–S4 的交互改成手柄操作，UI 全部在头显里可见。PC 版继续可玩。Pick 阶段不在本任务内。

# Interaction spec (VR)

- 射线：右手柄射线指向节点时只做悬停高亮。
- 选中：射线对着方块按 trigger → 选中。方块略微放大，并朝玩家方向突起一点（`NodeMorpher.SetPoseOverride`）。手柄旁出现悬浮面板"是否批准"。
  - 对同一个方块再按一次 trigger → 取消选中。
  - 对另一个方块按 trigger → 切换选中。
- 批准：选中后按 A → `Tap(id)`。Medium 难度每按一次 A 算一次点击。
- 详情：选中后按住 grip → `InspectStart(id)`，松开 → `InspectEnd`。
- 对白推进：没有选中方块时按 A → `Advance`。
- S2 拖拽连线：按住 trigger 扫射线，沿用 `DragStart` / `DragOver` / `DragEnd`。短按 = 选中，按住移动超过阈值 = 拖拽。
- 旋转：右摇杆 → `DragEmpty(delta)`。S3 只取左右（yaw），S4 左右 + 上下（yaw + pitch）。轴过滤由 Stage 决定，发起方只发原始 delta。
- 教学台词里的"右键"之后改成 grip，和教程引导一起做，不在本任务内。

# UI spec (VR)

- 所有 Screen Space Overlay Canvas 改成 World Space，接 XRI 的 UI 交互（`TrackedDeviceGraphicRaycaster`）。
- 字幕：懒跟随面板（XRI `LazyFollow`），距离约 1.5 m，略低于视线中心。头转出死区后平滑跟上，始终可见但不锁死在脸上。黑屏时同样可见。
- Agent 聊天栏：跟右手柄，在手柄左上方（批准面板在右上方），只有头像和消息气泡，没有底板（10-16 用户决定，取代原来的"矩阵前方 + 磨砂玻璃"）。
- "是否批准"面板：手柄射线下方，不挡视线；按扳机、按 A、按握键或取消选中后收起。
- 详情弹窗：贴在被查看的节点旁边，固定在 S1/S2 平面矩阵的深度、朝向固定，S3/S4 里不随节点深度变大变小。
- 带 YES 的询问框：视野正中，比矩阵离玩家近，跟头、面向玩家。Caution 卡留在 Agent 聊天栏里。
- Yes 询问框、开始 / 结束界面：场景内的 3D 面板。询问框 Yes 和开始界面 Awake 按 A 确认（不用瞄准，扳机不点它们，10-16 用户决定）；结束界面按钮仍是射线 + trigger。
- 黑屏（`ScreenBlackout`）：改成相机前的遮罩。

# Steps

1. XR 环境：OpenXR + Meta Quest 支持包，XR Plugin Management，Android 平台，单通道立体渲染，Mobile 画质。
2. `MainSceneMenu` 生成 XR Origin，替换固定相机（原机位 = Origin 位置，眼高交给头显）。
3. VR 交互发起方（与 `PointerInput` 并列，事件签名相同），选中状态和"是否批准"面板放在发起方内部；`.inputactions` 加 XR 绑定。
4. UI 改 World Space（字幕 → 聊天栏 → 询问框 → 详情 → 开始 / 结束界面）。
5. `EdgeVignette` 补 XR 宏或在 Mobile 渲染器上关闭。
6. Quest 3 实机性能检查（目标 72 Hz，稳定后可试 90 Hz）。

# Existing reference

- `Scripts/Interaction/PointerInput.cs`：事件集合和手势判定。
- `NodeMorpher.SetPoseOverride`：教学放大演示方块的用法（`TutorialStage`）。
- `Scripts/Player/`：Pick 阶段已用的 XRI 射线（`XRRayInteractor`）。

# Files likely involved

- `Packages/manifest.json`、`ProjectSettings/`（XR、平台、画质）
- `Scripts/Core/Editor/MainSceneMenu.cs`
- 新建 `Scripts/Interaction/XR/`（VR 发起方、批准面板）
- `InputSystem_Actions.inputactions`
- `Scripts/Narrative/SubtitlePanel.cs`、`AgentChatFeed.cs`、`ScreenBlackout.cs`，`Scripts/Agent/*`，`FlowScreen`
- `Scripts/Stages/S3*`、`S4*`（旋转轴过滤）

# Acceptance criteria

- Quest 3 上能从开始界面一路玩到 S4 通关，没有报错。
- trigger 选中 / 取消 / 切换，A 批准，grip 查看详情，摇杆旋转，都按上面的规格工作。
- 字幕在任何朝向都能看到，位于视野下方；聊天栏跟着手柄。
- PC 版（键鼠）S1–S4 的交互仍正常。

# Out of scope

- Pick 阶段的 VR 化。
- 教学台词和配音的修改。
- 移动 / 传送（S1–S4 站着或坐着玩）。
