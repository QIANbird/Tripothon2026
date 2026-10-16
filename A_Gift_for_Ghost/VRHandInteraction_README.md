# VR Hand Interaction — Phase 1

独立的 Controller → Hand 位姿映射、食指 Trigger 开合、持续接触长按抓取和彩椒摘取模块。Phase 2 仅在独立测试场景中启用，正式场景保持隔离。

## 项目检查与依赖

- Unity **6000.3.25f1**，URP **17.3.0**，Input System **1.20.0**，XRI **3.3.2**，Adaptive Hands **1.0.0**。
- Unity 实际导入原 `Assets/3D_Objects/hand/…fbx` 后：SkinnedMeshRenderer 0、骨骼 0、BlendShape 0、AnimationClip 0。因此使用 Adaptive Hands 的 `AdaptiveHand_Right.prefab`、骨骼及 Animator。
- 第三方手的输入、物理跟随、动态姿态脚本和 Collider 已在本模块 Prefab 中禁用；只由 `HandGraspVisual` 驱动 `Thumb / Index / Middle / Ring / Pinky` Animator 参数。使用本模块的 URP 材质，不编辑包缓存。
- 此工作区没有安装 OpenXR，也没有可复用的 XR Origin。远端新增了 XR 迁移配置，但涉及本地已有修改的依赖文件；本任务没有合并或改动那些设置。
- 不安装新包、不改 Unity 版本、全局层表、Physics 配置、正式场景或 Build 场景列表。保留原有 PC `Ghost.Pick.HandReach` 流程。
- Runtime 程序集只引用 XRI 和 Input System；生成的手资产引用已有 Adaptive Hands 包。Git 依赖由现有 `packages-lock.json` 锁定，本任务没有重新解析或升级版本。
- macOS/Windows 均使用同一 Unity 版本和仓库内相对路径；没有平台专用运行时代码。Windows、Android/ARM64 和 Quest 的实际兼容性仍需在相应平台验证。

## 快速运行

1. 打开 Unity 项目，等待导入、包解析和编译完成。
2. 打开 `Assets/VRHandInteraction/Generated/HandInteractionTest.unity`，进入 Play，点击 Game 窗口获得键盘焦点。
3. **W/S**：前后；**A/D**：左右；**Q/E**：下降/上升；**方向键**：俯仰/偏航；**Z/X**：滚转；**Space**：模拟食指 Trigger。
4. 将手移到桌面上的测试方块，保持 Space 至少 **0.3 秒**。移动或旋转手应带动物体；松开 Space 应立即释放并下落。
5. 使用两个方块检查：短按不抓取；范围外不抓取；一个手最多一个物体；未到 0.3 秒离开后重新进入必须重新计时。Scene 视图选中手可查看青色检测球。
6. 也可在 Play 中直接调整 `SimulatedControllerPose` 的 Transform，不需要相机或头显。

已交付测试 Scene、RightHand Prefab、GrabbableCube Prefab、Input Actions、Action References 和材质。若资产缺失，可执行 **Ghost → VR Hand Interaction → Create Missing Test Assets**。菜单只补缺失资产，不覆盖已存在资产；先保存当前未命名场景。正式游戏不应包含测试相机或 `EditorSimulation`。

## C# 文件清单

全部为新增文件，未修改原游戏 C#：

- `Runtime/HandPoseFollower.cs`、`HandTriggerInput.cs`：位姿与输入适配。
- `Runtime/HandGraspVisual.cs`：Animator/占位手视觉。
- `Runtime/HoldAuthorization.cs`、`TimedDirectInteractor.cs`：接触计时及 XRI 门控。
- `Runtime/EditorHandSimulator.cs`：独立测试模拟器。
- `Editor/HandInteractionAssets.cs`、`TimedDirectInteractorEditor.cs`：仅补缺失资产的生成器及 Inspector。
- `Tests/EditMode/HandUnitTests.cs`、`Tests/PlayMode/HandIntegrationTests.cs`、`HandSceneSmokeTests.cs`：单元、XRI 集成及场景冒烟测试。

上述路径均相对 `Assets/VRHandInteraction/`；Runtime、Editor、EditMode、PlayMode 各有独立 asmdef。

## Inspector 配置

把 `RightHand.prefab` 放入自己的测试场景，按组件配置：

| 组件 / 字段 | 设置与含义 |
| --- | --- |
| HandPoseFollower → Controller Pose Source | 注入已有 Rig 的手柄 Transform；不得指向当前手自身或其子节点 |
| Hand Visual Transform | `HandVisual` 包装节点，放在交互根节点下，局部位置/旋转保持零；模型朝向微调放在更内层 |
| Position Offset / Rotation Offset | 控制器局部坐标的米制偏移 / 欧拉角偏移；位置不继承源 Transform 的缩放 |
| Tracking Enabled | 开关追踪；关闭会取消计时并释放物体 |
| HandTriggerInput → Trigger Action | 右手选择 `Hand_RightTrigger.asset`；左手选择 Input Actions 中的 `Hand/LeftTrigger` |
| Press Threshold | 默认 0.5；低于阈值立即撤销抓取。不是 Grip，不是 Toggle |
| Use Injected Value | 真机关闭；模拟器和自动化测试开启 |
| TimedDirectInteractor → Minimum Hold Duration | 默认 0.3 秒，按真实经过时间计时 |
| Grab Radius | 默认 0.08 米；保持手交互根节点及父级缩放为 1 |
| Attach Transform | 默认 `Attach` 子节点；可调整抓持点 |
| Interaction Layer Mask | 本模块有效 XRI 掩码，默认 Default，与测试物 `Interaction Layers` 一致；每帧同步到继承的 XRI 配置 |
| Contact Physics Layers | 默认 Default，额外限制可抓取 Collider 的 Physics Layer |
| Interaction Manager | 注入场景现有 Manager；独立测试场景已有一个 Manager |
| XRI Select Input | 保持未绑定；本组件从 Trigger 适配器及计时状态提供 Select，不能再并行添加另一抓取 Interactor |
| HandGraspVisual → Hand Animation Speed | 默认 8，开合归一化值每秒变化速度；不影响抓取授权或释放时机 |

手部 SphereCollider 为 Trigger，Rigidbody 为 Kinematic、无重力；不要同时启用 Adaptive Hands 的物理跟随。测试物为 9 cm 方块，Collider + 动态 Rigidbody（0.15 kg、有重力）+ XRGrabInteractable。抓取方式为 Kinematic，Single Select，Dynamic Attach，关闭 Throw On Detach；释放由 XRI 恢复原始刚体属性。

Physics Layer 控制碰撞检测，XRI Interaction Layer 控制交互资格，二者不是同一套掩码。独立测试均使用 Default；正式接入时由 Rig 负责人配置双方匹配的层，不需要本模块改全局层表。

## 状态与接口

- `HandPoseFollower.SetPoseSource(Transform)` 更换来源；`SetTrackingValid(bool)` 接收 Rig 的实际追踪状态。Transform 无法自行判断设备追踪失效，Rig 必须在断连/丢失追踪时报告 false。
- 位置 = Controller 世界位置 + Controller 世界旋转 × Position Offset；旋转 = Controller 世界旋转 × Rotation Offset。交互前同步根节点，渲染前只刷新视觉包装节点，不写相机。
- `HandTriggerInput` 是唯一读取输入设备 Action 的适配层。自有 Action 在启用时 Enable，禁用时只 Disable 自己开启的 Action；与其他模块共享 Action 时由 Rig 提前启用并管理其生命周期。
- `SetInjectedValue(float)` 与 `useInjectedValue` 支持模拟/外部输入适配；输入、视觉、授权逻辑彼此独立。
- `HoldAuthorization` 不依赖设备。持续接触与持续按键同时成立才计时；优先锁定最近候选，换目标、松手或接触中断都会清零。授权前不 Select、不改父级、不改物体 Rigidbody。
- `TimedDirectInteractor` 复用 XRI 的接触列表与 Select/Release，再复核实时 Collider 重叠，避免上一物理帧残留接触导致隔空抓取。持有后不要求仍在检测范围内。
- 释放发生于输入释放后的下一次 XRI 交互处理，不等待开手动画。关闭组件、来源失效或追踪关闭也会释放。物体销毁会清理候选/选择状态。

### 未来 VR Rig 对接

1. 由 VR 模块配置 OpenXR、Quest Controller Profile、XR Origin 及其控制器追踪，本模块不安装或替换这些组件。
2. 将该 Rig 的右手控制器 Transform 注入 `controllerPoseSource`，使用已有 Interaction Manager。
3. 注入右手食指 Trigger 的 `InputActionReference`，或使用本模块的 `<XRController>{RightHand}/trigger` 绑定。关闭模拟器并取消 `useInjectedValue`。
4. Rig 每次追踪状态变化调用 `SetTrackingValid`；失效时 false，恢复时 true。统一以米为单位且交互层级缩放为 1。
5. 根据真实佩戴姿态调整偏移及 Attach。抓取检测根和视觉包装节点跟随同一控制器，不依赖 Camera.main。
6. 左手使用独立实例，替换为左手视觉和 `<XRController>{LeftHand}/trigger`；每只手独立计时。物体 Single Select 防止双手同时持有。
7. 若控制器已有射线选择功能，由 Rig 的交互组/启用策略安排当前交互模式，避免同一个 Trigger 同时触发其他游戏操作。

## 验证记录

验证平台：macOS，Unity 6000.3.25f1，2026-10-16。原项目正在编辑器中打开，因此使用包含当前 Assets/Packages/ProjectSettings 的临时副本进行 `-batchmode -nographics` 测试，不关闭现有编辑器。

- **编译与测试资产生成：通过**，使用本项目安装的 Unity 版本；原手 FBX 检查也在此步骤实际执行。
- **Edit Mode：4/4 Passed**：连续计时与边界、松手/离开重置、候选切换、局部偏移、左右 Trigger 绑定。
- **Play Mode：15/15 Passed**：范围与短按拒绝、位姿跟随、释放与刚体/重力恢复、多 Collider、双手互斥、追踪丢失/禁用/销毁清理、掩码过滤、视觉开合、实际 Animator 骨骼变化、合成 XR 输入的 Trigger/Grip 区分、生成场景的合成键盘操作，以及 Main 场景启动冒烟。
- **交付审计：通过**：所有新增 Unity 资产均有 `.meta`；生成资产的 GUID 引用均可解析；交付代码/资产与测试副本一致；再次运行生成菜单，已有生成资产的 SHA-256 全部保持不变。
- 测试中的键盘为 Input System 合成设备；仅测试期间调整内存中的焦点输入策略并恢复，不写项目输入设置。
- 报告：[EditMode.xml](../docs/testing/vr-hand-phase1/EditMode.xml)、[PlayMode.xml](../docs/testing/vr-hand-phase1/PlayMode.xml)。详细检查边界见下表。

测试运行方式：Test Runner 中分别运行 `Ghost.VRHandInteraction.EditModeTests` 和 `Ghost.VRHandInteraction.PlayModeTests`。批处理使用 `-runTests -testPlatform EditMode|PlayMode -assemblyNames <程序集名> -testResults <输出.xml>`，不要同时对已打开的同一个项目启动第二个 Editor。

| 验收项 | 结论边界 |
| --- | --- |
| Quest 控制器平移、旋转和实体 Trigger | **Not Tested**，没有真机/可用 Rig；模拟输入不能替代硬件验证 |
| 模拟位姿同步、Trigger/Grip 区分、开合 | 自动化验证；骨骼旋转检查不等同于人工外观验收 |
| 范围限制、短按拒绝、持续接触 0.3 秒授权 | 实际 XRI Play Mode 自动化覆盖 |
| 抓取跟随、立即释放、单物体、刚体恢复 | 实际 XRI Play Mode 自动化覆盖 |
| 原游戏摄像机/场景文件隔离 | 文件变更审计；没有修改正式场景、相机、玩家或彩椒代码 |
| 非 VR 游戏 | Main 场景启动冒烟覆盖；全关卡人工游玩 **Not Tested** |
| 真实手柄对齐、视觉质量、Quest 性能 | **Not Tested** |
| Windows Editor、Android/ARM64 构建、真机运行 | **Not Tested** |

编辑器自动化通过后仍需用户打开独立场景进行人工验收。Quest 3S、Windows、Android 和完整正式游戏回归仍为 **Not Tested**。

## Phase 2：VR 彩椒摘取

Phase 2 复用 Phase 1 的 `TimedDirectInteractor`、`HandPoseFollower`、`HandTriggerInput`、0.3 秒 `HoldAuthorization` 和 XRI `XRGrabInteractable`。它没有第二套手部追踪、输入、长按计时或 VR Rig。

### 资产结构与目标规则

- 原始彩椒树：`Assets/3D_Objects/pepper2/pepper_plant.fbx`。
- 独立测试场景：`Assets/VRHandInteraction/Generated/VRPepperPicking_Test.unity`。
- 当前 Unity 导入扫描实际发现 5 个独立彩椒节点，生成器日志为 `Independent candidates: 5`，没有使用 Proxy。
- VR 专用 `VRPepperPickable` 与现有 PC `PickableFruit` 分离；没有修改 `PickableFruit.cs` 或 `PickStage.cs`。
- 只有 `pepper_red_picked` 或名称以 `_picked` 结尾的目标可摘。其他彩椒保持 Attached、不会启用 XRI Select，并在 Trigger 按下且手接触时显示：`你确定要采摘未成熟的果实吗？再看看`。
- 树本体不添加 Rigidbody、XR Grab Interactable 或手部 Interactor。摘取对象使用 `Attached → Held → Released` 状态，分离和事件只发生在真实 XRI `selectEntered` / `selectExited` 后。

### Phase 2 运行方式

1. 停止当前 Play，等待 Unity 6000.3.25f1 编译完成。
2. 打开 `Assets/VRHandInteraction/Generated/VRPepperPicking_Test.unity`，点击 Play，再点击 Game 窗口。
3. `W/S`、`A/D` 移动手；`Q/E` 上下移动；方向键旋转；`Z/X` 滚转；Space 模拟右手食指 Trigger。
4. 靠近成熟 `pepper_red_picked`，持续按住 Space 至少 0.3 秒，彩椒进入 Held 并随 XRI 手部移动；松开 Space 后释放并恢复刚体物理。
5. 靠近其他彩椒并按住 Space，只显示未成熟提示，不会摘取。
6. 左上角调试文本显示当前彩椒、状态、Trigger 和摘取计数。该调试组件只在测试场景使用，正式场景不挂载。

测试场景可以通过 **Ghost → VR Hand Interaction → Create Pepper Picking Test Scene** 重新生成；它只生成独立测试场景，不调用 Main 场景构建器，不修改正式场景。重新生成前停止 Play，手动修改同名测试场景会被覆盖。

### Phase 2 对接接口

`PepperPickingManager` 暴露 `PickedPepperCount`、`TotalPepperCount`、`CurrentHeldPepper`，并转发 `OnGrabStarted(GameObject)`、`OnFruitPicked(GameObject)`、`OnReleased(GameObject)`。未来关卡可订阅这些事件，不需要读取 Trigger 或直接移动 Rigidbody。

未来 Rig 对接只需将 Controller Transform 注入 `HandPoseFollower.controllerPoseSource`，将食指 Trigger Action 注入 `HandTriggerInput.triggerAction`，将现有 `XRInteractionManager` 注入 `PepperPickingManager.interactionManager`，然后关闭 `EditorHandSimulator`。不需要替换几何手、主相机或 XR Origin。

### Phase 2 验证状态

- Unity 编译和测试场景生成：Passed。
- Phase 2 Edit Mode：6/6 Passed。
- Phase 1 历史验证：Edit Mode 4/4、Play Mode 15/15 Passed。
- 用户人工验证：待完成。
- Quest 3S、OpenXR Rig、真实控制器、Windows Editor、Android/ARM64 和完整正式游戏回归：**Not Tested / Requires Quest 3S Hardware**。

### 编辑器验收修复：手模型超出画面

用户首次人工测试发现只有方块可见。原因是 AdaptiveHand_Right 原始包 Prefab 带有局部 Y=1 米的示例摆放偏移，而生成器未归零；检测位置与模型因此相差一米。已修正交付 RightHand Prefab，并让生成器清零模型局部位置/旋转（保留左右手镜像缩放）。整场景测试增加了模型与交互根对齐、启用 Renderer 位于测试相机视锥内的断言。视锥检查不替代人工渲染外观验收。

更新后先退出 Play，等待 Unity 导入/编译完成，再重新进入 Play；不需要重新生成场景。如果此前在场景实例上手动设置过模型位置覆盖，请将 `RightHand/HandVisual/AdaptiveHand_Right` 的 Local Position 设为 `(0,0,0)`。
