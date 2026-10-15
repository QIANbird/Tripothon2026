# Goal

同一个工程可以打包到 Meta Quest 2 运行：S1–S4 的交互改成手柄操作，UI 全部在头显里可见。PC 版继续可玩。Pick 阶段不在本任务内。

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
- Agent 聊天栏：浮在矩阵前方的固定世界平面，背后做磨砂玻璃效果（模糊后方画面，需要 URP 的 Opaque Texture 或单独的模糊 pass，注意 Quest 性能）。
- 详情弹窗、"是否批准"面板：挂在手柄附近。
- Yes 询问框、开始 / 结束界面：场景内的 3D 面板，射线 + trigger 点按钮。
- 黑屏（`ScreenBlackout`）：改成相机前的遮罩。

# Steps

1. XR 环境：OpenXR + Meta Quest 支持包，XR Plugin Management，Android 平台，单通道立体渲染，Mobile 画质。
2. `MainSceneMenu` 生成 XR Origin，替换固定相机（原机位 = Origin 位置，眼高交给头显）。
3. VR 交互发起方（与 `PointerInput` 并列，事件签名相同），选中状态和"是否批准"面板放在发起方内部；`.inputactions` 加 XR 绑定。
4. UI 改 World Space（字幕 → 聊天栏 → 询问框 → 详情 → 开始 / 结束界面）。
5. `EdgeVignette` 补 XR 宏或在 Mobile 渲染器上关闭。
6. Quest 2 实机性能检查（目标 72 Hz）。

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

- Quest 2 上能从开始界面一路玩到 S4 通关，没有报错。
- trigger 选中 / 取消 / 切换，A 批准，grip 查看详情，摇杆旋转，都按上面的规格工作。
- 字幕在任何朝向都能看到；聊天栏在矩阵前方，有磨砂效果。
- PC 版（键鼠）S1–S4 的交互仍正常。

# Out of scope

- Pick 阶段的 VR 化。
- 教学台词和配音的修改。
- 移动 / 传送（S1–S4 站着或坐着玩）。
