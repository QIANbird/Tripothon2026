# Goal

1. Outro 结局视频换成 `Assets/Video/Outro_02.mp4`。
2. 结束界面改版：黑底，中央浮着玩家摘下的那个彩椒（3D），绕 45° 斜轴缓缓转动；彩椒旁边一行白字"感谢游玩"；底部字幕"按下A返回开始界面"。按 A 回到开始界面。
3. 新增暂停界面：游戏中按 menu 键呼出，半透明幕布盖住后面的画面；中央一个 `#E5FF71` 色的菱形缓缓旋转；底部"返回开始界面"选项（按 A 或射线 + 扳机点击确认）；再按一次 menu 关闭暂停回到游戏。

# Existing reference

- `Scripts/Core/Editor/MainSceneMenu.cs`：场景唯一来源。`OutroVideoPath`（41 行）指向 `Outro.mp4`；`BuildOutroStage`（935 行）；`BuildFlowScreens` / `BuildFlowScreen<EndScreen>`（974 行起）搭开始 / 结束界面。不手改 `Main.unity`。
- `Scripts/Stages/OutroVideoStage.cs`：播完 `Complete()` → `GameFlow.FlowFinished`。
- `Scripts/Core/FlowScreen.cs`、`EndScreen.cs`、`StartScreen.cs`：CanvasGroup 淡入淡出 + 一个按钮；`EndScreen.OnButton` 重载场景回到开始界面（保留这个做法）。
- `Scripts/Core/Editor/XRUIBuilder.cs:90`：开始 / 结束界面在 VR 下是头前 2 m 的 LazyFollow 面板，`AddRayTarget(..., approveKey:)` 决定按钮是扳机点还是 A 确认；`BuildScreenBackdrop` + `XRScreenBackdrop` 给 VR 补全视野黑幕。
- `Scripts/Interaction/XR/XRUIRayTarget.cs`：`approveKey` 按钮由 `XRControllerInput` 按 A 时 `TryApprove`；开始界面 Awake、询问框 Yes 已经这样用。
- `Scripts/Interaction/XR/XRControllerInput.cs`：`InputSystem_Actions.inputactions` 的 `Gameplay` map（select / approve / inspect / rotate）。menu 键的 action 加在同一个资产里。
- `Scripts/Stages/PickStage.cs:200`：摘的果实是写实模型里的 `fruitObjectName` 子物体（`PickableFruit`）。结束界面的彩椒用同一个网格 / 材质，不是运行时那份（那份已被咬小、Exit 时已随玩家消失）。
- `Scripts/Core/GameFlowDebug.cs`：N / Shift+数字跳关，暂停期间要屏蔽。

# Design

**1. 视频替换**

- `OutroVideoPath` 改为 `Assets/Video/Outro_02.mp4`，重新 Build Main Scene。旧 `Outro.mp4` 先保留不删（删除另行确认）。
- 检查 `Outro_02.mp4.meta` 的导入设置与旧视频一致（转码、Quest 可解码的 H.264）；宽高比由 `BuildOutroStage` 从 clip 读取，不用改。

**2. 结束界面**

- 布局（PC 全屏 / VR 头前面板，同现在）：纯黑底；中央偏左是彩椒展示，右侧一行白字"感谢游玩"；底部居中字幕"按下A返回开始界面"（字号和现有字幕一致）。
- 彩椒展示：Build Main Scene 时从写实模型 prefab 里取 `fruitObjectName` 的网格和材质，生成一个独立的展示物体 `EndPepper`（不复用运行时果实）。
  - 新组件 `EndPepperSpin`：绕本地 `(1,1,0).normalized`（与竖直方向成 45°）的轴匀速转，默认 20°/s，用 `Time.unscaledDeltaTime`；加一点上下浮动（振幅 1 cm，周期 3 s）。
  - PC：彩椒放在一个专用相机前，渲染到 RenderTexture，结束界面用 RawImage 显示（UI 在 Overlay，3D 物体不能直接画进去）。专用相机只看一个独立 Layer，平时关闭，结束界面出现时开。
  - VR：直接把彩椒放在结束界面面板前方的世界空间里（面板中心前 0.1 m），跟面板一起 LazyFollow；不走 RenderTexture，省一个相机。
  - 两种模式选一种实现路径时，以 VR 为准设计、PC 上降级为 RT；不要在 shared 代码里判断设备，放到 `XRUIBuilder` / PC 构建分支里。
- 确认方式：A 键（VR，`approveKey: true`）/ PC 上 Enter、空格或鼠标点底部提示。不再有扳机点的"重新开始"按钮。底部提示本身就是那个 `Button`，复用 `FlowScreen.button`。
- 出现时机不变：`FlowFinished` → 淡入。

**3. 暂停界面**

- 新组件 `Core/PauseMenu.cs`（共享逻辑，不碰设备输入）：`Toggle()` / `Open()` / `Close()` / `ReturnToStart()`，事件 `PausedChanged(bool)`，静态 `IsPaused`。
  - 只在游戏进行中可开：`flow.HasStarted && !flow.IsFinished`，且开始 / 结束界面不可见。Outro 视频期间也可开（暂停视频）。
  - 打开：`Time.timeScale = 0`、`AudioListener.pause = true`、Outro 的 `VideoPlayer.Pause()`；关闭时恢复原值（记住打开前的 timeScale）。
  - 暂停期间：`PointerInput` / `XRControllerInput` 不派发节点交互、`DialogueAdvanceInput` 不推进、`GameFlowDebug` 不跳关、`FirstPersonMotor` 不动（timeScale 0 能挡住大部分，按事件补齐剩下用 unscaled time 或直接读输入的地方）。
  - `ReturnToStart()`：先恢复 timeScale / 音频，再 `SceneManager.LoadScene` 重载（同 `EndScreen`，抽一个共用静态方法）。
- 输入：
  - `InputSystem_Actions.inputactions` 加 action `Pause`：`<XRController>{LeftHand}/menuButton`、`<Keyboard>/escape`。
  - PC 读取放 `Player/PC`（或现有 PC 输入组件），VR 读取放 `XRControllerInput`，都只调 `PauseMenu.Toggle()`。
  - PC 打开暂停时解锁光标（`PcCursorLock`），关闭时恢复 Pick 阶段原状态。
- 画面：
  - 全屏幕布：黑色 alpha ≈ 0.6，淡入 0.25 s（unscaled）。VR 下幕布用 HeadLocked 的 `XRScreenBackdrop` 同款（alpha 跟 CanvasGroup），UI 面板头前 1.5 m LazyFollow；暂停时位置在打开那一刻对准视线。
  - 中央菱形：UI Image（正方形旋转 45°，颜色 `#E5FF71`），绕 Z 轴缓慢旋转 ~30°/s，或轻微呼吸缩放；用 unscaled time。VR 下也是 UI，不需要 3D。
  - 底部选项"返回开始界面"：Button；VR 下 `AddRayTarget` 要同时支持扳机点击和 A 确认（现在 `approveKey` 是二选一，加一个"两者都可"选项，不改现有按钮行为）。
  - 排序：高于字幕、黑屏、HUD 和 Outro 视频（`OutroVideoSortingOrder = 250`），低于开始 / 结束界面。

# Files likely involved

- `Scripts/Core/Editor/MainSceneMenu.cs`：视频路径；结束界面重搭；暂停界面、彩椒展示物体、PC 彩椒相机 + RT。
- `Scripts/Core/Editor/XRUIBuilder.cs`：结束界面 A 确认；彩椒在 VR 面板前；暂停界面面板 + 幕布 + 按钮。
- `Scripts/Core/EndScreen.cs`：按钮语义变化，可能顺带开关彩椒相机 / 展示物体。
- 新建 `Scripts/Core/PauseMenu.cs`、`Scripts/Core/EndPepperSpin.cs`（+ `.meta`）。
- `Scripts/Interaction/XR/XRUIRayTarget.cs`：扳机 + A 都可确认的选项。
- `Scripts/Interaction/XR/XRControllerInput.cs`、PC 输入组件：读 Pause action；暂停时不派发交互。
- `InputSystem_Actions.inputactions`：新 `Pause` action。
- `Scripts/Core/GameFlowDebug.cs`、`Scripts/Interaction/DialogueAdvanceInput.cs`：暂停时屏蔽。
- `docs/CURRENT_STATE.md`、`docs/ARCHITECTURE.md`：按 CLOSEOUT 更新（流程、新组件、输入映射）。

# VR notes

- 依照 `docs/VR_GUIDELINES.md`：面板距离 ≥ 1.5 m，字号不小于现有字幕；幕布 HeadLocked，内容 LazyFollow。
- 彩椒展示在 VR 是真 3D 物体，Quest 上只多一个网格的 draw call；不要在 VR 里开额外相机。
- `timeScale = 0` 不影响 XR 头部追踪和 XRI 射线（它们用 unscaled），但要确认 `XRWorldCanvas` 的 LazyFollow 用的是 unscaled time，否则暂停时面板不跟随。

# Acceptance criteria

- Outro 播的是 Outro_02，比例正确，播完进入结束界面。
- 结束界面：黑底，中央彩椒（与摘的那颗外观一致）绕 45° 轴缓慢转动、轻微浮动；旁边白字"感谢游玩"；底部"按下A返回开始界面"。
- VR 按 A / PC 按 Enter·空格·点击 → 回到开始界面，再玩一遍状态干净（同 start-restart-screens 的验收）。扳机不会误触。
- 游戏中按 menu（VR 左手柄）/ Esc（PC）出现暂停：后面画面被半透明幕布盖住但可见，中央 `#E5FF71` 菱形缓慢旋转，所有动画、对白、音频、Outro 视频都停住。
- 暂停中节点点不到、对白不推进、N 不跳关、玩家不移动；再按一次 menu / Esc 原样恢复，计时和对白从暂停处继续。
- 暂停中按 A 或射线点"返回开始界面" → 回到开始界面，`timeScale` 为 1、音频正常。
- 开始界面和结束界面上按 menu 不弹暂停。
- 已有阶段的交互、开始界面 Awake（A）、询问框 Yes（A）、跳关仍正常。

# Out of scope

- 暂停里的设置、音量、继续游戏按钮（继续靠再按 menu）、退出游戏。
- 删除旧 `Outro.mp4`（另行确认）。
- 结束界面的其他动画（粒子、镜头运动）、彩椒的特殊材质 / 发光。
- 存档、从中途继续。
