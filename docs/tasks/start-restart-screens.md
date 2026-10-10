# Goal

游戏加上开始界面和结束界面：Play 后先停在开始界面，点"开始"才进入 Intro；最后一关结束后弹出结束界面，点"重新开始"回到开始界面，从头再玩一遍，状态干净。

# Existing reference

- `Assets/Scripts/Core/GameFlow.cs`：`Start()` 里直接 `EnterStage(0)`；`Next()` 在最后一关只打日志。开始 / 结束都从这里接入。
- `Assets/Scripts/Agent/AgentQueryDialog.cs` + `AgentUIBuilder.cs:168`：现成的 UGUI Button 写法（EventSystem + InputSystemUIInputModule），新界面照抄。
- `Assets/Scripts/Agent/AgentUIStyle.cs`：`CreateHudCanvas(name, parent, sortOrder)`、`CreateRect` 等样式工具。
- `Assets/Scripts/Narrative/ScreenBlackout.cs`：`SetImmediate` / `FadeTo`，开始界面和结束界面的黑底与淡入淡出。
- `Assets/Scripts/Narrative/IntroStage.cs`：Intro 进关时 `blackout.SetImmediate(true)`，所以开始界面本身用黑底，点开始后无缝接 Intro 黑屏。
- `Assets/Scripts/Player/PC/PcCursorLock.cs`：Pick 阶段锁定光标。结束界面出现时必须解锁光标。
- `Assets/Scripts/Core/Editor/MainSceneMenu.cs`：场景唯一来源。新 UI 和组件在这里搭，不手改 `Main.unity`。
- `Assets/Scripts/Core/GameFlowDebug.cs`：N / Shift+1–9 跳关。

# Design

**开始**

- `GameFlow` 加 `autoStart`（默认 false，Inspector 可勾，方便调试直接进关）。为 false 时 `Start()` 不进关，等外部调 `Begin()`。
- `Begin()` = `EnterStage(0)`；已经开始过就忽略。
- 开始界面（10-10 改）：策划底图 `Assets/Art/UI/StartScreen/start_bg.png`（标题画在图里）按宽高比铺满屏幕；开始按钮是番茄下方的环 `awake_ring.png` + "AWAKE" 字样，宽高比从底图读取（换图后重新 Build Main Scene），按钮位置用底图比例定在 `MainSceneMenu.BuildStartScreen` 的常量里，随底图缩放。点按钮 → 界面淡出 → `flow.Begin()`。Intro 自己会把黑屏设上，衔接不跳。
- 开始界面显示期间：植株不可交互（`PointerInput` 不派发、对白不播放）。最简单的做法是 GameFlow 没进关时什么 Stage 都没 Enter，加上黑底挡住植株。
- 调试：开始界面上按 N 或 Shift+数字，等同于先 `Begin()` 再跳关（`GameFlowDebug` 里 `CurrentIndex < 0` 时先 Begin）。

**结束**

- `GameFlow` 加事件 `FlowFinished`：最后一关调 `Complete()`，或在最后一关按 N（`Next()` 越界）时触发一次。
- 当前最后一关 Outro 是 `PlaceholderStage`，没有通关条件，所以眼下靠 N 结束。Outro 以后做成真实阶段后，它 `Complete()` 就会自动走到结束界面，不用改这次的代码。
- 结束界面：订阅 `FlowFinished` → 黑屏淡入 → 显示"感谢游玩"+ "重新开始"按钮；同时解锁光标、显示指针。
- 点"重新开始"→ `SceneManager.LoadScene(当前场景)` 重新加载场景，回到开始界面。

**为什么重新加载场景而不是 `JumpTo(0)`**

各系统都有运行时状态（`NodeMorpher` 写实度、`PlantFit` Hold、`PlayerRig` 接管的相机、Pick 摘下的果实、`NodeIssueSystem`、对白队列……）。逐个复位容易漏，重新加载场景最干净。代价是 ScriptableObject 不会重置：确认运行时没有往 SO 里写状态（ARCHITECTURE 约定 SO 运行时只读）。

# Files likely involved

- 新建 `Assets/Scripts/Core/FlowScreen.cs`：两个界面共用的淡入淡出 + 按钮基类。
- 新建 `Assets/Scripts/Core/StartScreen.cs`：开始界面控制，持有 `GameFlow`、按钮、CanvasGroup，点击后淡出并 `Begin()`。
- 新建 `Assets/Scripts/Core/EndScreen.cs`：订阅 `FlowFinished`，显示结束界面，点击重新加载场景。光标不需要额外处理：Pick 阶段 Exit 时玩家根物体停用，`PcCursorLock.OnDisable` 已经恢复光标；`FlowFinished` 前 `GameFlow` 会先 Exit 最后一关。（实现时确认，10-10）
- `Assets/Scripts/Core/GameFlow.cs`：`autoStart`、`Begin()`、`HasStarted`、`FlowFinished`。
- `Assets/Scripts/Core/GameFlowDebug.cs`：未开始时先 `Begin()`。
- `Assets/Scripts/Core/Editor/MainSceneMenu.cs`：搭 StartScreen / EndScreen 的 HUD Canvas（sortOrder 要高于字幕、黑屏和其他 HUD），连好引用。
- `ProjectSettings/EditorBuildSettings.asset`：现在只有 `SampleScene`。重新加载场景需要 `Main.unity` 在 Build Settings 里，加进去（并作为第 0 个）。只改这一项。
- `docs/CURRENT_STATE.md`、`docs/ARCHITECTURE.md`：按 CLOSEOUT 更新（GameFlow 新接口、流程最前和最后多了两个界面）。

# VR notes

- 比赛期沿用 Screen Space Overlay HUD（同现有技术债），但不要写依赖屏幕像素坐标的布局，用锚点居中。
- 按钮走 UGUI Button + EventSystem，VR 版用 XR UI 射线点同一个按钮。
- 开始 / 结束逻辑（`Begin`、`FlowFinished`、重载场景）不依赖输入设备。

# Acceptance criteria

- Play 后停在开始界面，看不到植株，不播任何对白；N 以外的按键和鼠标点击空白处都不会开始游戏。
- 点"开始"后界面淡出，Intro 黑屏 + 字幕正常开始，往后流程与现在一致。
- 开始界面上按 N / Shift+数字可直接进关（调试）。
- 走到 Outro 按 N（或最后一关 `Complete()`）后出现结束界面；光标可见、可点击（包括从 Pick 跳到 Outro 的情况）。
- 点"重新开始"回到开始界面；再点开始，从 Intro 完整再玩一遍，植株回到 Matrix、写实度为 0、果实在植株上、相机在固定机位，无报错。
- `autoStart` 勾上时行为和现在一样（Play 直接进 Intro）。
- 已有阶段的交互和跳关仍正常。

# Out of scope

- 设置菜单、暂停菜单、音量、退出游戏按钮。
- Outro 结局剧情本身（仍是占位）。
- 存档 / 从中途继续。
- 开始界面的美术、Logo、背景动画（先用文字 + 黑底占位）。
- HUD 改 World Space。
