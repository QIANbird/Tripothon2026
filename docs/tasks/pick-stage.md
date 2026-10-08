# Goal

Pick 阶段：写实植株固定在玩家前方 2–3 m，不能转。玩家用键鼠第一人称走过去、蹲下、对准果实，用胶囊手臂伸手摘下，然后吃掉，进入 Outro。交互用 XRI 实现，以后迁到 VR 时只换交互发起方。

# Existing reference

- `Assets/Scripts/Stages/S4GeometricStage.cs`、`TutorialStage.cs`：Stage 的生命周期写法（Enter 订阅 + `ctx.ResetShared()`，Exit 还原）。
- `Assets/Scripts/Core/Editor/MainSceneMenu.cs`：`Stages` 表里第 61 行的 `Pick` 占位；`BuildPointer` 是挂组件、填引用的参考写法。
- `Assets/Scripts/Morph/RealModelHandoff.cs`：写实模型实例 `ModelInstance`（Plant/RealModel 下面），果实要从这里找。
- `Assets/Scripts/Interaction/DialogueAdvanceInput.cs`：PC 专用输入组件的写法。
- `Assets/Scripts/Morph/PlantFit.cs`、`Interaction/TargetRotator.cs`：Pick 阶段要停用，参考它们现有的 Enable/Disable 方式。
- 外部：Starter Assets – First Person Controller（Unity 官方）；XRI 3.x 的 `XRRayInteractor` / `XRGrabInteractable` / Input Action 绑定；Animation Rigging 的 `TwoBoneIKConstraint`。

# Files likely involved

- `Packages/manifest.json`：新增 `com.unity.xr.interaction.toolkit`、`com.unity.animation.rigging`（版本锁定）。**不要提交 tripo 那一行。**
- `Assets/InputSystem_Actions.inputactions`：新建 `Player` 表，包含 Move（WASD）、Look（鼠标 delta）、Crouch（C / 左 Ctrl）、Grab（左键）。
- 新建 `Assets/Scripts/Player/`：
  - `FirstPersonMotor.cs`：可以用 Starter Assets 的控制器，加上蹲下；如果不兼容（例如强依赖 Cinemachine），就自己写 CharacterController 版本。只读 Action。
  - `PlayerRig.cs`：Pick 阶段打开或关闭玩家，进入时对齐当前相机位置。
- `Assets/Scripts/Player/PC/`：屏幕中心准星、光标锁定。这些是 PC 专用代码。
- 新建 `Assets/Scripts/Pick/`：
  - `PickableFruit.cs`：挂在果实上，配合 XRI Interactable，负责 Collider、高亮、从植株上脱离。
  - `HandReach.cs`：驱动胶囊手臂的 IK 目标，在待机位和果实位之间插值，摘下后把果实挂到手上。
  - `EatSequence.cs`：吃的占位动画（手移到嘴前，果实缩没），完成时发事件。
- 新建 `Assets/Scripts/Stages/PickStage.cs`：替换占位阶段。
- `Assets/Scripts/Core/Editor/MainSceneMenu.cs`：搭 PlayerRoot → CameraPivot → Camera + Arms(胶囊) + XR Ray Interactor，加地面和边界碰撞，把 Pick 阶段换成 `PickStage` 并填好引用。
- `docs/CURRENT_STATE.md`、`docs/ARCHITECTURE.md`：收尾时更新。"相机永远不动"要改成"Pick 阶段除外"，并补上 Player 系统。

# Implementation phases（每个阶段单独可以验收）

1. **装包和输入**：装 XRI 和 Animation Rigging，确认能编译、不连头显也能进 Play、现有阶段不受影响。新建 `Player` Action 表。
2. **移动**：先试 Starter Assets，不行就自己写。PlayerRig 能在 Pick 阶段打开、离开时关掉。地面和边界碰撞。蹲下。
3. **固定植株**：Pick 阶段停用 PlantFit 和 Rotator，植株按真实尺寸放在玩家前方；跳进跳出这一关，植株和相机都能还原。
4. **可摘果实**：确定果实在 `pepper_plant.fbx` 里是不是独立网格，然后给果实加 Collider 和 Interactable。如果不是独立网格，用一个占位球体放在果实的位置，并隐藏原来的果实区域，或者先整体保留。XRI 从屏幕中心发射线，并且限制在伸手可及的距离内。准星和高亮。
5. **手臂和吃**：胶囊手臂 + Two Bone IK 伸向果实，摘下后果实挂到手上；播吃的占位动画；播对白，进入 Outro。
6. **收尾**：按 CLOSEOUT 更新文档。

# Acceptance criteria

- 从 S4 按 N 进入 Pick（或用 Shift+8 跳关）时，画面不跳；光标锁定，出现屏幕中心准星。
- WASD 移动速度约 1.5–3 m/s，不能穿过植株，不能走出边界；鼠标转头，俯仰有上下限；C / Ctrl 蹲下，相机平滑降到约 1.0 m。
- 植株不能旋转，按真实尺寸固定，不再被 PlantFit 缩放。
- 准星对准果实，并且离果实在伸手范围内（约 0.7 m）时，果实高亮；范围外不高亮，也摘不了。
- 按左键时，胶囊手臂伸过去，果实脱离植株并跟着手走。
- 摘下后自动或按键触发吃的动画，结束后播对白，然后进入 Outro。
- 离开 Pick 阶段时（N 或跳关），玩家关闭，光标解锁，相机回到固定机位，PlantFit 恢复，被摘的果实还原；再进 Pick 可以重新摘。
- 共享代码里不出现 `UnityEngine.Input` 和设备 API；PC 专用代码只放在 `Player/PC/`。
- `PickStage`、`PickableFruit` 不读 `Camera.main`，也不读鼠标坐标。
- 已有阶段的交互仍正常：S1–S4 的点击、拖拽、旋转、右键详情，以及对白推进都不受影响；装 XRI 后 Play 不报 XR 相关错误。

# Open decision（实现前确认，默认值见括号）

- 果实数量：1个，已经在模型中分离并单独命名为pepper_picked之类的名字
- 吃的触发方式：键盘是按下E键，VR之后再做调整。
- 对白：（默认：用 `[占位]` 文本，Enter 时一段，吃完后一段，经 `StageAssets` 生成）
- XRI 在 PC 上不连头显时，需要 XR Origin 吗？（默认：不用 XR Origin，只用 XRI 的 Interaction Manager + Ray Interactor 挂在相机上；VR 迁移时再换）
- 对白播放时还能移动吗？（默认：能，不锁玩家）

# Out of scope

- 真实手臂模型、正式动画、音效（由用户制作，之后替换胶囊）。
- VR 手柄绑定、XR Origin、Near-Far Interactor。
- 跳跃、奔跑、动画化的蹲下姿态。
- 重新采样或修改 `pepper_plant`（节点 id 不能变）。
- Outro 阶段的内容。

# Session split（评估）

建议分 **3 个对话**，每个对话结束时都做一次 Play 验收：

1. 阶段 1–3：装包、输入、移动、固定植株。风险集中在包兼容性和场景构建，跟后面的交互无关。
2. 阶段 4–5：果实、XRI 交互、IK 手臂、吃。依赖第 1 个对话装好的包和 PlayerRig。
3. 阶段 6 + 修 bug：文档收尾，跳关回归测试。如果第 2 个对话还有余量，可以合并进去。

理由：阶段 4 要先确认果实网格的结构，结果可能改变做法；阶段 1 装包也可能需要在编辑器里手动处理（例如 XRI 导入示例或项目验证弹窗）。把这两块不确定的东西放在不同的对话里，上下文会比较干净。
