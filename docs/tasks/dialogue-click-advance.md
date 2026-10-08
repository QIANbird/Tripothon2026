# Goal

玩家点击（或按空格）可以推进对白：打字机还没打完时，第一下补全整句；已经打完时，第二下立刻进入下一句。不再强制等满按字数估算的时长。

# Existing reference

- `Assets/Scripts/Narrative/DialoguePlayer.cs`：`Skip()` 已实现（跳过当前句、不等 pauseAfter、最后一句跳过时正常回调 onComplete），但目前没有任何地方调用。
- `Assets/Scripts/Narrative/SubtitlePanel.cs`：打字机状态在 `revealing` / `fullText`，目前不对外暴露。
- `Assets/Scripts/Interaction/PointerInput.cs`：PC 端输入发起方的写法（读 Gameplay 动作表、只发事件、不含玩法逻辑），照这个模式写。

# Files likely involved

- `Assets/Scripts/Narrative/SubtitlePanel.cs`：暴露 `IsRevealing` 和 `CompleteReveal()`。
- `Assets/Scripts/Narrative/DialoguePlayer.cs`：新增 `Advance()`，打字机未完成时补全，否则调 `Skip()`；需要拿到 SubtitlePanel 的打字机状态，或改由面板转发。
- 新建 `Assets/Scripts/Interaction/DialogueAdvanceInput.cs`（PC 端）：读输入，调用 `DialoguePlayer.Advance()`。
- `Assets/InputSystem_Actions.inputactions`：Gameplay 表新增 `Advance` 动作（鼠标左键 + Space + Enter）。
- `Assets/Scripts/Core/Editor/MainSceneMenu.cs`：搭场景时挂上 `DialogueAdvanceInput`。

# Acceptance criteria

- 打字中点击：整句立刻显示完整，不跳到下一句。
- 整句显示后点击：立刻进入下一句，不等剩余时长和 pauseAfter。
- 不点击时行为和现在一致（按音频长度或字数自动推进）。
- 开场最后一句被点掉后，正常进入教学（onComplete 照常回调）。
- 字幕和 Agent 弹窗两种通道都能推进。
- 在 HUD 按钮上（例如 AI 询问的 Yes）点击，不会推进对白。
- 共享代码里不出现 `UnityEngine.Input`，也不出现鼠标、键盘等设备 API。设备相关代码只放在 `DialogueAdvanceInput`。
- 已有阶段的交互仍正常：S1 点方块、S2 拖拽连线、S3/S4 旋转和标记都不受影响。

# Open decision（实现前确认，默认值见括号）

- 有对白在播时，左键点中节点，算推进对白还是算点节点？（默认：两个都触发。点节点照常生效，对白也推进一句。如果策划觉得误触多，改成“指针下有节点时只点节点，不推进”。）
- 带配音的台词被跳过后，配音一起停掉。（默认：停，`Skip` 已经这样做。）

# Out of scope

- “等待点击才推进”的台词类型（台词表里的“推进方式 = 点击”）。
- 整段跳过（例如长按跳过开场）。
- 视频跳过。
- VR 手柄的推进输入（以后另写一个 XR 端发起方，调用同一个 `Advance()`）。
