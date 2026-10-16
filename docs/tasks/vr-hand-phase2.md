# Goal

在不重建 VR Rig、不修改正式游戏场景和不替换 Phase 1 几何手的前提下，玩家可以用现有虚拟手靠近彩椒，持续按住食指 Trigger 0.3 秒摘下成熟彩椒，摘下后的彩椒跟随手移动，松开后按 XRI 刚体规则释放。只有名称为 `pepper_red_picked` 或以 `_picked` 结尾的彩椒可摘；未成熟彩椒显示提示“你确定要采摘未成熟的果实吗？再看看”，不会被摘下。

# Existing reference

- Phase 1 手部 Prefab：`A_Gift_for_Ghost/Assets/VRHandInteraction/Generated/RightHand.prefab`
- 位姿同步：`A_Gift_for_Ghost/Assets/VRHandInteraction/Runtime/HandPoseFollower.cs`
- Trigger 输入：`A_Gift_for_Ghost/Assets/VRHandInteraction/Runtime/HandTriggerInput.cs`
- 0.3 秒接触授权和 `XRDirectInteractor`：`A_Gift_for_Ghost/Assets/VRHandInteraction/Runtime/TimedDirectInteractor.cs`
- 计时状态：`A_Gift_for_Ghost/Assets/VRHandInteraction/Runtime/HoldAuthorization.cs`
- 测试输入与键盘模拟：`A_Gift_for_Ghost/Assets/VRHandInteraction/Runtime/EditorHandSimulator.cs`
- 彩椒原始资产：`A_Gift_for_Ghost/Assets/3D_Objects/pepper2/pepper_plant.fbx`
- 现有 PC Pick 实现（本任务不修改）：`A_Gift_for_Ghost/Assets/Scripts/Pick/PickableFruit.cs`、`A_Gift_for_Ghost/Assets/Scripts/Stages/PickStage.cs`
- 运行时写实模型中的已有目标名：`pepper_red_picked`，由 `RealModelHandoff` 实例化；Phase 2 使用独立 VR 组件，不接管 PC Pick 流程。

# Files likely involved

- `A_Gift_for_Ghost/Assets/VRHandInteraction/Runtime/VRPepperPickable.cs`
- `A_Gift_for_Ghost/Assets/VRHandInteraction/Runtime/PepperPickingManager.cs`
- `A_Gift_for_Ghost/Assets/VRHandInteraction/Runtime/PepperPickingDebugView.cs`
- `A_Gift_for_Ghost/Assets/VRHandInteraction/Editor/HandInteractionAssets.cs`
- `A_Gift_for_Ghost/Assets/VRHandInteraction/Generated/VRPepperPicking_Test.unity`
- `A_Gift_for_Ghost/Assets/VRHandInteraction/Tests/EditMode/PepperPickingUnitTests.cs`
- `A_Gift_for_Ghost/VRHandInteraction_README.md`
- `A_Gift_for_Ghost/docs/` 下的架构、状态和测试记录

所有新增 Unity 资产均提交对应 `.meta`。没有修改 `Packages/manifest.json`、`packages-lock.json`、`ProjectSettings`、`Main.unity`、主摄像机、XR Origin、Locomotion、彩椒 FBX 或 PC Pick 脚本。

# Implementation

`VRPepperPickable` 使用 `PepperPickState.Attached/Held/Released`。它在 `XRGrabInteractable.selectEntered` 后才从树上脱离，在 `selectExited` 后进入 Released；不监听 Trigger 按下，不提前移动物体，不另写一套 Rigidbody 跟随逻辑。`TimedDirectInteractor` 继续负责接触、Trigger 和 0.3 秒授权。

`PepperPickingManager` 从树实例注册显式彩椒 Transform，并转发 `OnGrabStarted(GameObject)`、`OnFruitPicked(GameObject)`、`OnReleased(GameObject)`，提供 `PickedPepperCount`、`TotalPepperCount` 和 `CurrentHeldPepper`。扫描器只将独立彩椒网格注册为目标；当前导入的测试树发现 5 个独立彩椒节点。成熟资格只由目标名决定：`pepper_red_picked` 或 `_picked` 后缀。其他彩椒仍可作为接触对象被识别，但不会启用可选择的 XRI Interactable。

`PepperPickingDebugView` 只挂在独立测试场景，显示当前彩椒、状态、Trigger 和摘取计数，并在手按住 Trigger 接近未成熟彩椒时显示中文提示。正式游戏不挂该调试组件。

测试场景实际路径为：

`A_Gift_for_Ghost/Assets/VRHandInteraction/Generated/VRPepperPicking_Test.unity`

它包含 Phase 1 几何手、键盘模拟器、独立 `XRInteractionManager`、彩椒树、5 个彩椒目标、地面、掉落区域、观察相机和灯光。生成菜单为 **Ghost → VR Hand Interaction → Create Pepper Picking Test Scene**；它只创建独立测试资产，不重建 `Main.unity`。如果资产导入结构没有独立彩椒网格，生成器使用独立 Proxy 进行交互验证；当前实际生成日志为 `Independent candidates: 5`，因此本次没有使用 Proxy。

## Editor usage

1. 使用 Unity **6000.3.25f1** 打开 `A_Gift_for_Ghost/`，等待包解析和脚本编译结束。
2. 打开 `Assets/VRHandInteraction/Generated/VRPepperPicking_Test.unity`，进入 Play，点击 Game 窗口获得焦点。
3. `W/S`、`A/D`：移动手；`Q/E`：下降/上升；方向键：旋转；`Z/X`：滚转；`Space`：模拟右手食指 Trigger。
4. 靠近 `pepper_red_picked`，持续按住 Space 至少 0.3 秒，彩椒进入 Held；移动手时由 XRI 跟随，松开 Space 后进入 Released 并受刚体物理影响。
5. 靠近其他彩椒并按住 Space，只显示“你确定要采摘未成熟的果实吗？再看看”，不会摘取。
6. 测试过程中确认树根、树枝、叶片和其他彩椒没有随手移动；随后可以摘取目标彩椒。

重新生成测试场景前先停止 Play。若场景已被手动修改，生成菜单会覆盖同名独立测试场景；正式场景不受影响。Phase 1 方块场景仍使用 `HandInteractionTest.unity`，操作方式不变。

## Future Rig integration

不需要替换 `RightHand.prefab` 或重建追踪系统。将未来 Quest Rig 的右手 Controller Transform 注入 `HandPoseFollower.controllerPoseSource`，将右手 Trigger Action 注入 `HandTriggerInput.triggerAction`，把同一 `XRInteractionManager` 注入 `PepperPickingManager.interactionManager`，关闭 `EditorHandSimulator`。Quest、OpenXR、XR Origin、相机和移动由其他模块提供。

# Acceptance criteria

- 成熟目标 `pepper_red_picked` 可通过接触 + 持续 Trigger 0.3 秒摘下。
- 未接触、范围外和短按不会摘取。
- 未成熟彩椒不可摘，并显示中文提示。
- 摘下彩椒通过 XRI 跟随手移动和旋转，松开后立即释放并恢复刚体行为。
- 一次最多持有一颗；同一彩椒不会重复触发摘取事件；其他彩椒和整棵树不移动。
- Phase 1 方块抓取保持可用；正式 Main 场景不注入 Phase 2 组件。
- Edit Mode 组件测试和 Unity 编译通过。
- Quest 3S、Windows Editor、Android/ARM64 构建和真实控制器仍须在对应环境验证。

## Actual validation

- Unity 6000.3.25f1 批处理编译和 Phase 2 测试场景生成：Passed。
- Phase 2 Edit Mode：6/6 Passed。
- Phase 1 的 Edit Mode/Play Mode 历史验证：4/4、15/15 Passed，报告见 `docs/testing/vr-hand-phase1/`。
- 真实 Quest 3S 控制器、OpenXR Rig、Windows Editor、Android/ARM64、真实摘取和完整正式游戏回归：**Not Tested / Requires Quest 3S Hardware**。
- 当前交付等待用户在 Unity 独立测试场景中进行人工验收。

# Out of scope

- 不修改或拆分原始 `pepper_plant.fbx`。
- 不修改现有 `PickableFruit`、`PickStage`、正式场景、主相机、XR Origin、Locomotion 或全局 OpenXR 设置。
- 不实现字幕、音效、剧情、吃彩椒、浇水、修剪、施肥、评分、成就或新关卡。
- 不绑定原始 Tripo 手模型骨骼，不替换 Phase 1 Adaptive Hands 几何手。
- 不提交 `Library/`、`Temp/`、`Logs/`、`Obj/`、`UserSettings/` 等机器生成目录。
