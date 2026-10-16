# Goal

按 `docs/script/02_dialogue.xlsx` 最新版（10-16 20:05）更新台词，并在教学阶段加两张操作提示弹窗：TUT_003 播完后方块右边滑出"Select / Confirm"，TUT_012 播完后滑出"Select / Details"，玩家照做后弹窗缓动滑走。弹窗是一张手柄示意图垫在灰色半透明圆角底板上（样式见下）。

# 台词改动（xlsx 为准）

xlsx 是策划源文件，比 `docs/script/02_dialogue.csv` 新。实现时先把 csv 同步成 xlsx（xlsx 不改），再用 Ghost/Narrative/Import Dialogue CSV 导入。

只有文本变化、不影响流程的行：

- INTRO_006 / 007 / 008 / 009（INTRO_009 触发条件改成"玩家按下任意键"，推进方式不变，仍是现有的等一次点击 / 按键）
- TUT_002 / 003 / 010 / 011 / 015 / 016
- S1_001、S3_005（"请协助我标记可疑节点。"）

文本 + 触发条件都变、影响教学流程的行：

| ID | 新文本 | 触发条件 | 影响 |
|---|---|---|---|
| TUT_012 | 选中节点后，除了向agent直接提交，也可以自己查看任务详情。 | 上一句结束 | 仍是 `afterHardSequence`（TUT_008 段）最后一句，播完进 `WaitInspect`，同时滑出 details 弹窗 |
| TUT_013 | 不过它们经过了agent的自动压缩……需要时间的过程。 | **玩家执行一次查看details** | 原来接在 TUT_012 后面，现在移到右键之后，导入后单独成段 `TUT_013` |
| TUT_014 | （不变，详情内容） | 玩家查看details时 | 不变 |
| TUT_015–016 | 这些数据直观、清晰、确定……/ 别着急，我们慢慢来。 | 右键详情弹出后 | 不变，仍是 `TUT_015` 段 |

右键之后的播放顺序变成 TUT_013 → TUT_015 → TUT_016 → 转场。`TutorialStage` 加一个 `afterInspectIntroSequence`（或改成按顺序播两段），`StageAssets` 加 `TutorialAfterInspectIntroKey = "TUT_013"`，`MainSceneMenu.BuildTutorialStage` 挂上。

xlsx 里 S2 段的 ID 编号和现在的 csv 不同（xlsx 少了 S2_006 一行，S2_007–009 对应 csv 的 S2_008–010），**csv 的 S2 段已在 10-16 按玩法接好（S2_001–007 进关、S2_008–009 通关、S2_010 看果实后），不跟着 xlsx 重新编号**。S1_H01 / S2_H01 两句提示 csv 里已有、已实现，xlsx 里 S1_H01 的 ID 是空的，同样以 csv 为准。同步时只把上面列出的文本 / 触发条件写进 csv，其他行不动。

`Assets/Audio/VO/` 里这些 ID 的旧配音和新文本不再对应，配音交付前先删掉绑定还是保留，见 Open decision。

# 教学弹窗

## 资产（`Assets/Art/UI/Guide/`）

- `guide_submit.png`（1299×944，透明底）：Select（扳机）+ Confirm（A）
- `guid_details.png`（1299×944，透明底）：Select（扳机）+ Details（握键）
- 不用：`confirm_A.png`（A 键图标）、`details_trigger.png`（黑底高亮层）、`guide_details.jpg`（旧 jpg 版）。保留在目录里不删。
- `guide_suubmit.jpg.meta` 是没有对应文件的孤儿 meta，`guide_submit.png` / `guid_details.png` 还没有 meta。Unity 导入时会自动处理；导入设置和 `guide_s1s2.png` 一样（Sprite / Default、不压 mip、Clamp）。

## 样式

参考图：灰色半透明圆角矩形底板，手柄图居中放在上面，四周留约 6–8% 的边距。底板颜色 #D9D9D9 左右、透明度约 0.85（可调），圆角约为高度的 8%。图片本身不再加描边或阴影。

## 摆放与动画

- World Space，和 `ControlsGuide` 一样无光照、世界坐标固定、只绕竖直轴转向相机，不加 Collider（不挡射线）。可以复用 `Ghost/GuideUnlit` 着色器给图片和底板各一块面片，或用一块 World Space Canvas（Image + 圆角 Sprite）——选和现有 `ControlsGuide` / `PauseWorldButton` 更接近、改动更少的那种。
- 位置：演示方块（`TutorialStage.DemoPose()` 换算到世界坐标）右侧，水平偏移、高度、尺寸放在 `ControlsGuideSettings.asset` 新加的"教学弹窗"一栏里，和其它提示图一起调。默认高度约 0.16 m（和 `s1s2` 提示图一致）。
- 滑入：从方块右侧再往外 ~0.1 m、透明度 0 开始，0.35 s 缓动（ease-out）到目标位置、透明度 1。滑出反过来（ease-in），结束后隐藏。
- PC 和 VR 都显示（同 `ControlsGuideSettings.showOnPC`，图画的是 Quest 手柄）。暂停时跟着暂停界面一起被盖住即可，不单独处理。

## 时机（挂在 `TutorialStage` 已有的阶段切换点上）

| 弹窗 | 滑入 | 滑出 |
|---|---|---|
| Select / Confirm | `BeginEasy`（TUT_003 播完、方块挂上简单问题） | 简单问题被解决（`OnIssueResolved`，即 `phase` 离开 `Easy`） |
| Select / Details | `BeginInspect`（TUT_012 播完、进入 `WaitInspect`） | 玩家第一次查看详情（`HandleInspected`） |

跳关 / 离开教学（`Exit`）时两张都立即隐藏，不播动画。同一时刻只会有一张。

# Existing reference

- `Assets/Scripts/Stages/TutorialStage.cs`：阶段机（`Phase.Easy` / `WaitInspect` 等）、`BeginEasy` / `BeginInspect` / `HandleInspected` / `Exit`，弹窗的开关点都在这里。
- `Assets/Scripts/Core/ControlsGuide.cs` + `ControlsGuideSettings.cs`：世界坐标提示图的现成实现（转向相机、淡入淡出、参数资产、编辑模式预览），弹窗照这个写。
- `Assets/Art/UI/Guide/Guide.mat` / `GuideUnlit.shader`：提示图材质。
- `Assets/Scripts/Narrative/Editor/DialogueCsvImporter.cs`：触发条件不是"上一句结束"就新开一段，所以 TUT_013 改触发条件后会自动单独成段，不用改导入器。
- `Assets/Scripts/Stages/Editor/StageAssets.cs`：段 key 常量。
- `Assets/Scripts/Core/Editor/MainSceneMenu.cs` `BuildTutorialStage`：挂对白资产、生成场景物体。改完必须跑 Ghost → Core → Build Main Scene。

# Files likely involved

- `docs/script/02_dialogue.csv`
- `Assets/Data/Narrative/Script/*.asset`、`DialogueLibrary.asset`（导入生成）
- `Assets/Scripts/Stages/TutorialStage.cs`
- `Assets/Scripts/Stages/Editor/StageAssets.cs`
- `Assets/Scripts/Core/Editor/MainSceneMenu.cs`
- `Assets/Scripts/Core/ControlsGuideSettings.cs`（加教学弹窗一栏）
- 新建 `Assets/Scripts/Core/TutorialGuidePopup.cs`（或同类名字）
- `Assets/Art/UI/Guide/`（新图的 meta、底板 Sprite / 材质）

# Acceptance criteria

- csv 里上面列出的行和 xlsx 文本一致；重新导入后出现 `Script/TUT_013.asset`，TUT_008 段只到 TUT_012，其余段 key 不变。
- 教学：TUT_003 播完，方块开始闪烁，同时 Select / Confirm 弹窗从方块右边滑入；解决简单问题后滑出。
- TUT_008–012 播完，Select / Details 弹窗滑入；按住右键（PC）/ 选中后按住握键（VR）看到"维护单元07"详情后弹窗滑出，依次播 TUT_013 → TUT_015 → TUT_016，然后方块融入矩阵进 S1。
- 弹窗是灰色半透明圆角底 + 手柄图，和参考图一致；不挡射线，不影响点方块。
- 教学中途跳关（N / Shift+数字）后弹窗不残留；再跳回教学能重新完整走一遍。
- INTRO / S1_001 / S3_005 显示新文本。
- 已有阶段的交互仍正常（S1–S4、Pick、暂停、键位提示图）。

# Open decision

- 改了文本的台词（INTRO_006–009、TUT_002/003/010–013/015/016、S1_001、S3_005）旧配音对不上新文本。默认：先保留旧绑定（有声音但内容不一致），等新配音交付后按文件名覆盖。如果要静音等新配音，删掉 `Assets/Audio/VO/` 里对应文件即可，`VoiceClipBinder` 会自动清引用。

# Out of scope

- 不改 xlsx；不按 xlsx 重新编号 S2 段。
- 不改 S1_H01 / S2_H01 / S2 补缺口玩法 / S3 / S4 逻辑。
- 不改现有 `ControlsGuide` 提示图（S1–S4、Pick）和 Pause/Help 按钮。
- 不改演示方块的选点、放大和融入矩阵动画。
