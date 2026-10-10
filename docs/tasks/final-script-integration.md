# Goal

把定稿台词表 `docs/script/02_dialogue.csv` 接进完整流程：Intro → TUT → S1 → S2 → S3 → S4 → Pick，每一句都在表里写的时机、用表里写的通道播出。S2–S4 不再用 `StageAssets` 里的占位对白。

# Existing reference

- `Assets/Scripts/Narrative/Editor/DialogueCsvImporter.cs`：CSV 按 ID 和触发条件分段，生成 `Assets/Data/Narrative/Script/<段key>.asset`。触发条件为“上一句结束”的接在上一段后面，其他触发条件另起一段，段的 key 是这一段第一句的 ID。没有 ID 或没有文本的行会跳过。
- `Assets/Scripts/Stages/TutorialStage.cs` + `StageAssets.LoadScript(key)` + `MainSceneMenu.cs:286`：已经从台词表接对白的阶段，其他阶段照这个写法改。
- `TutorialStage.PopupTitle/PopupBody`：拿一段台词的文本当右键详情内容（第一行做标题），S3 的 Yes 弹窗问题文字也照这个写法从台词里取。
- `AgentQueryDialog.Ask(question, onYes)`：S3 结尾带 Yes 的弹窗。
- `EatSequence.BiteTaken`：Pick 吃第一口的事件。

# 分段结果（定稿表，2026-10-10）

| 段 key | 包含 | 触发时机 | 接到哪里 |
|---|---|---|---|
| INTRO_001 | 001–008 | Intro 进入 | `IntroStage.sequence` |
| INTRO_009 | 009–011 | 播完后玩家点击任意处 | `IntroStage.afterClickSequence`，播完进入 TUT |
| TUT_002 | 002–003 | TUT 进入 | `introSequence` |
| TUT_004 | 004, 006, 007 | 简单问题解决 | `afterEasySequence` |
| TUT_008 | 008–013 | 困难问题失败 | `afterHardSequence` |
| TUT_014 | 014 | 右键详情文字 | `inspectSequence` |
| TUT_015 | 015–016 | 右键详情弹出后 | `afterInspectSequence` |
| S1_001 | 001 | S1 进入 | `introSequence` |
| S1_P01–P06 | 各一句 | 左键点亮起的方块，对白空闲时 | `clickPool`，`{a-b}` 换随机数 |
| S1_002 | 002–004 | S1 通关条件达成 | `completeSequence`，播完弹询问 |
| S1_005 | Agent询问 | S1_004 之后 | `querySequence`，Yes 进 S2 |
| S2_001 | 001–005 | S2 进入 | `introSequence` |
| S2_W01 | 一句 | 从错误起点拖拽，对白空闲时 | `wrongStartSequence` |
| S2_006 | 006–007 | 缺水全部解决 | `waterSolvedSequence` |
| S2_008 | 008 | S2_007 播完且已右键看过果实 | `finishSequence`，播完进 S3 |
| S3_001 | 001–005 | S3 进入 | `introSequence` |
| S3_006 | 006 | 标记第一个虫子 | `firstMarkedSequence` |
| S3_007 | 007–010 | 标记全部虫子 | `allFoundSequence`，播完弹询问 |
| S3_011 | Agent询问 | S3_010 之后 | `querySequence`，Yes 进 S4 |
| S4_001 | 001–004 | S4 进入 | `introSequence` |
| S4_005 | 005 | 每摘一只（摘完前） | `progressSequence`，`Pin` 在 Agent 弹窗，`{current}/{total}` 填进度 |
| S4_006 | 006–007 | 摘除全部虫子 | `allRemovedSequence`，播完才 Complete |
| S4_008 | 008 | 进入 Transition（和变写实同时） | `RealTransitionStage.sequence`，播完且写实模型显现后进 Pick |
| PICK_001 | 001, 002, 004, 005 | Pick 进入 | `introSequence` |
| PICK_003 | 操作提示 | PICK_002 播完 | `controlsHintSequence` → `PcControlsHint` |
| PICK_006 | 006 | 吃第一口 | `firstBiteSequence` |

导入器规则：通道为 `Agent询问` 或 `操作提示` 的行单独成段（key = 自己的 ID），不进 DialoguePlayer、不打断前后分段，阶段脚本只取文本。key 常量在 `StageAssets`。
进关不锁操作；推进方式列不新增逻辑，等待都由阶段玩法事件触发。

# Files likely involved

- `docs/script/02_dialogue.csv`（只补 ID 和空单元格，不改台词）
- `Assets/Scripts/Narrative/Editor/DialogueCsvImporter.cs`
- `Assets/Scripts/Narrative/IntroStage.cs`、`Narrative/Editor/NarrativeSceneBuilder.cs`
- `Assets/Scripts/Interaction/DialogueAdvanceInput.cs` 或 `Narrative/DialoguePlayer.cs`（等点击）
- `Assets/Scripts/Stages/Editor/StageAssets.cs`
- `Assets/Scripts/Stages/S1MatrixStage.cs`、`S2CircuitStage.cs`、`S3NetworkStage.cs`、`S4GeometricStage.cs`、`PickStage.cs`
- 新建 `Assets/Scripts/Player/PC/PickControlsHint.cs`
- `Assets/Scripts/Core/Editor/MainSceneMenu.cs`

# Acceptance criteria

- 导入后 `Data/Narrative/Script/` 里的段和上面的分段表一致，导入没有警告。旧的段资产（S2_001、S3_001 等）被导入器删除，场景里没有丢失的引用。
- Intro：INTRO_002–004 显示在 Agent 弹窗，其余是黑屏白字。INTRO_008 播完后停住，点一下才播 INTRO_009，INTRO_011 播完进入 TUT。
- TUT：简单 → 困难 → 右键 → 转场整条流程走通，每段台词和表一致；右键详情显示“维护单元07……”。
- S1：进关有 S1_001；点亮起的方块时弹出 S1_P 池里的一句（数字每次不同），连点时不叠播；通关后依次播 S1_002–004，然后弹出 S1_005 询问，点 Yes 进入 S2。
- S2：intro 是 S2_001–005；从错误起点拖拽时播 S2_W01；缺水全部解决后播 S2_006–007；之后（且已右键看过果实）播 S2_008，播完直接进入 S3。
- S3：第一次标记播 S3_006；全部标记后播 S3_007–010，然后 Yes 弹窗显示 S3_011 的全文，点 Yes 进入 S4。
- S4：intro 是 S4_001–004；每摘一只，Agent 弹窗显示 S4_005 和进度 1/3、2/3，位置不动，直到摘完才收起；全部摘除后播 S4_006–007 → Transition：S4_008 开始时植株就开始变写实，播完自动进入 Pick，不用按 N。
- Pick：进关播 PICK_001–002，然后左上角出现操作提示（PICK_003），接着播 PICK_004–005；吃第一口时播 PICK_006。
- Shift+数字跳关进出每个阶段时，对白都会停掉，不会把上一关的台词带进下一关。
- 共享代码里不出现设备 API；操作提示只在 `Player/PC/` 里。
- 已有阶段的交互仍正常。

# Decisions（策划已确认 10-10）

- S1 结尾的 Yes 询问保留（S1_005）；S2 结尾没有询问，S2_008 播完直接进 S3（10-10 修订）。
- S1 随机池只写植物各部分的状态，不点名部位 / 资源，不写人的日常任务（10-10 修订）。
- 画面上不再显示“按 N 继续”和旧的键位文字（N 仍是调试键）。
- S2 起点选错提示保留（S2_W01）。
- S1 随机池补到 6 句，编号和百分比用 `{a-b}` 随机。
- TUT_013 在右键之前播。
- 没有 TASTE 视频；PICK_006 只播台词，吃完三口照旧进入 Outro 占位。

# Out of scope

- 视频播放（INTRO_000 / OUTRO_000）和配音接入；`04_media_assets.csv` 里的配音清单已经过期，不在这次更新。
- Transition 占位阶段（S4 和 Pick 之间）和 Outro 占位，保持现状。
- Pick 5b/5c（虚幻化和消散）。
- 演出备注里的视觉效果（例如 S4_008 “植株彻底还原为原始模型”、S3_003 “节点恢复颜色”）：现有的变形和写实度逻辑已经实现的就保持，没有实现的这次不补，只对齐台词时机。
- 操作提示换成涂鸦风贴图。
- 删除 `StageAssets` 里的旧占位方法和 `Data/Narrative` 下的旧对白资产。
