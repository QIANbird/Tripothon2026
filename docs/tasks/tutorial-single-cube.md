# Goal

教学阶段改成只有一个大方块：黑暗中只有它在明暗循环。玩家先左键点它（简单问题，一次解决，授权 Agent 处理）；它再次亮起变成困难问题，点了也解决不了，温柔女声引导玩家"主动介入"，转入右键教学；玩家右键查看它的详情后，方块缩小、后退，融入 S1 的方块矩阵，矩阵其余方块渐显，进入 S1。

原教学的三种难度只删掉中等难度一步。简单和困难保留，都放在这一个方块上。

**台词原则：** Unity 里已经录入的教学台词（`TutorialIntro` / `TutorialAfterEasy` / `TutorialAfterHard`）保持原句，只做三件事：① 把困难失败那句结尾从"也许你该亲自介入纠因了"改成"也许这时，你该主动介入一下"；② "这是你的agent，你刚刚批准了它帮你处理一项事物。"说话人从 Agent 改成亲切的声音（xlsx / 策划确认）；③ 从 `docs/script/02_dialogue.xlsx` 的 TUT 段加入新增的右键教学四句。xlsx / csv 里其他改动（说话人、S2、S3 等）不进这次任务。中等难度那两句（`TutorialAfterMedium`）随步骤一起删掉。

# Existing reference

- `Assets/Scripts/Stages/TutorialStage.cs`：当前教学（整面 Matrix 上 Easy → Medium → Hard 三个节点）。保留 Easy 和 Hard 的写法（`ctx.issues.AddIssue`、`OnIssueResolved` / `OnIssueAttempted`、Hard 失败后等待），删掉 Medium 一步，三个节点合并成一个 `demoNode`。
- `Assets/Scripts/Gameplay/NodeIssueSystem.cs`：Easy 点一次解决；Hard 每次点击只暂停 2–3 s 再闪（`hardRetryDelay`）。同一节点在 Easy 解决后可以再 `AddIssue` 成 Hard。
- `Assets/Scripts/Stages/IssueStage.cs` + `StageContext.SetInspectProvider` / `Inspected`：右键详情的接入点。教学的右键内容通过 InspectProvider 提供，`Inspected(id)` 用来判断"玩家右击了方块"。
- `Assets/Scripts/Interaction/NodePicker.cs`：拾取只认节点（包围球，半径跟节点缩放走），所以演示方块必须是一个植株节点，放大后自然更好点中。
- `Assets/Scripts/Morph/NodeMorpherStates.cs`：`SetBlink` / `Hide(id, immediate)` / `Show` / `SetVisible`，用来让其余节点先隐藏、最后渐显。
- `Assets/Scripts/Morph/NodeMorpher.cs`：`currentPoses` 在 `Update` 里由形态插值得出（:180）。需要在这里加"单节点姿态覆盖"。
- `Assets/Data/Narrative/TutorialIntro.asset`、`TutorialAfterEasy.asset`、`TutorialAfterHard.asset`：Unity 里正在用的教学台词，是这次的底稿。
- `docs/script/02_dialogue.xlsx` TUT 段：只取新增的右键四句（TUT_004 / TUT_005 / TUT_006 / TUT_007 的演出）。xlsx 和 csv 内容相同，xlsx 是策划源文件。
- `Assets/Scripts/Core/Editor/MainSceneMenu.cs:275` `BuildTutorialStage`：搭场景时给教学挑节点、挂对白资产。`NearestInMatrix` 的选点方式可复用。

# 台词（以 Unity 现用资产为底，只加改动）

实现时改对白资产，并同步写回 `docs/script/02_dialogue.csv` 的 TUT 段（xlsx 不改）。xlsx 里 TUT 段目前是"简单点击 → 直接右键教学"，没有困难那一步；csv 按下面这张表重写，让它和游戏流程一致。导入后的分段供阶段脚本引用。

| 资产 / 导入后的段 | 说话人 | 文本 | 来源 |
|---|---|---|---|
| **Intro**（进关即播，对应旧 `TutorialIntro`） | 亲切的声音 | 看到那个在闪的方块了吗？它代表一处需要处理的异常事务。 | Unity 现用，不动 |
| | Agent | 点击异常节点，即对其进行一次处理。 | Unity 现用，不动 |
| **Easy 解决后**（对应旧 `TutorialAfterEasy` 第一句） | 亲切的声音 | 这是你的agent，你刚刚批准了它帮你处理一项事物。 | Unity 现用文本，说话人按 xlsx / 策划改成亲切的声音 |
| **Hard 失败后**（对应旧 `TutorialAfterHard`） | Agent | 尝试解决失败。当前权限下没有可执行的方案，稍后重试。 | Unity 现用，不动 |
| | 亲切的声音 | 但总有一些时候，当甩手掌柜解决不了问题。当节点仍在闪烁，就意味着也许这时，你该主动介入一下。 | Unity 现用第二句，只改结尾：原"也许你该亲自介入纠因了" → "也许这时，你该主动介入一下"。句首多余空格去掉 |
| **右键教学**（xlsx 新增） | 亲切的声音 | 右键点击可以查看任务详情，不过它们经过了agent的自动压缩，也许现在的你还想不起具体的细节——重新建立对真实的认知是一个需要时间的过程。试着右键这个节点，看看现在的你能看到多少吧。 | xlsx TUT_004 |
| **玩家右击后** | Agent | 维护单元07：稳定度下降 / 建议操作：补充资源 / 预计改善：+12% | xlsx TUT_005。默认只显示在右键 HUD 详情里，不另走 Agent 弹窗（见 Open decision） |
| | 亲切的声音 | 直观、清晰、安心……也有些令人感到悬浮。别着急，接下来的练习将帮助你慢慢回想起具象世界的样子。 | xlsx TUT_006 |
| **转场** | — | （无台词） | 演出：方块后退、缩小，融入矩阵，其余方块渐显，进入 S1。xlsx TUT_007 原备注"逐渐暗下去 → 完全变黑"作废 |

**明确不改 / 不用：**

- `TutorialAfterEasy` 第二句"有些复杂问题需要多次批准。再看这一个。"和整段 `TutorialAfterMedium`（"已处理。共授权三次。" / "最后一个……试试看。"）——中等步骤删掉，这三句一起去掉。
- xlsx 里 INTRO / S1 / S2 / S3 / S4 / PICK 的改动（说话人、S2_004 触发条件等）这次不导入、不覆盖 Unity 现有资产。
- csv 第 14 行重复的空 `TUT_001`（触发条件 `----`）删掉。

# Flow

1. **进关 / Intro**：只有演示方块可见，放大后摆在画面正中、离镜头近一些。其余节点立即隐藏，不可拾取。播 Intro 两句。播完后同一方块挂 Easy 问题，明暗闪烁，等待左键。
2. **简单问题**：左键点中 → Easy 解决（浅蓝 → 常亮）→ 播 Easy 解决后那一句（"这是你的agent……"）。
3. **困难问题**：上一句播完，同一方块挂 Hard 问题，重新闪烁，等待左键。
4. 左键点中 → Hard 尝试失败（暂停 2–3 s 后会再闪）→ 播 Hard 失败两句（Agent + 温柔女声改过结尾的那句）+ 右键教学那句。这段播放期间左键再点方块只按 Hard 规则暂停，不重播台词。
5. **右键教学**：右键教学句播完前，右键详情照常可看但不推进流程；播完后右键按住方块 → HUD 详情显示 TUT_005 的三行文字，`Inspected` 触发后播 TUT_006。
6. **转场**：TUT_006 播完 → 移除方块上的 Hard 问题；方块从演示姿态插值回它在 Matrix 里的位置和大小（后退、缩小），同时其余节点按 `Show` 渐显，构成完整矩阵。动画结束后 `Complete()`，进入 S1（S1 也是 Matrix，不再变形）。

# Files likely involved

- `docs/script/02_dialogue.csv`：按上表重写 TUT 段（xlsx 不动），然后用菜单 Ghost/Narrative/Import Dialogue CSV 导入。导入只覆盖 TUT 段对应的 `Script/TUT_*.asset`；其他段落的 Script 资产即使 csv 内容不同，也不要手改。
- `Assets/Data/Narrative/TutorialIntro.asset`、`TutorialAfterEasy.asset`、`TutorialAfterHard.asset`：按上表改文本（Hard 那句结尾、Easy 去掉第二句）；或改为引用导入后的 `Script/TUT_*.asset`。`TutorialAfterMedium.asset` 确认不再引用后删除（连同 `.meta`）。
- `Assets/Scripts/Morph/NodeMorpher.cs`：新增单节点姿态覆盖，例如 `SetPoseOverride(int id, NodePose pose, float weight)` / `ClearPoseOverride(int id)`。`Update` 里算完形态插值后按 weight 在原姿态和覆盖姿态间 Lerp。只是表现层功能，拾取和光晕自动跟着 `CurrentPoses` 走。
- `Assets/Scripts/Stages/TutorialStage.cs`：重写为上面的六步。字段：`demoNode`、演示姿态（本地位置和缩放，Inspector 可调）、Intro / Easy / Hard+右键 / 右击后 四段对白、转场时长。任务面板只保留"简单异常""困难异常"两行。Exit 时清掉姿态覆盖、问题和隐藏状态，保证跳关不残留。
- `Assets/Scripts/Core/Editor/MainSceneMenu.cs`：`BuildTutorialStage` 改为挑一个 `demoNode`（画面中部、非果实），挂上面对白资产。
- `Assets/Scripts/Stages/Editor/StageAssets.cs`：`EnsureTutorialAfterMedium` 删掉；Intro / AfterEasy / AfterHard 的占位文本改成上表（已有资产不覆盖，所以还要直接改资产）。迁移菜单里 Tutorial 那条可删。

# Acceptance criteria

- 进入教学时画面上只有一个大方块在明暗循环，其余节点不可见、点不到。进关先播 Intro 两句（"看到那个在闪的方块了吗……" / "点击异常节点……"）。
- 方块足够大，鼠标容易点中；不被左侧任务面板、下方字幕和 Agent 弹窗遮挡。
- 第一次左键：方块解决（浅蓝后常亮），出现"这是你的agent，你刚刚批准了它帮你处理一项事物。"没有中等难度台词，也没有"再看这一个"。
- 上一句播完后方块重新闪烁；第二次左键：方块暂停闪烁，依次出现"尝试解决失败……"、改过结尾的温柔女声（"……也许这时，你该主动介入一下。"）、xlsx 的右键教学句。
- 右键教学句播完前右键不推进流程；播完后右键按住方块，HUD 弹出"维护单元07……"三行，随后播"直观、清晰、安心……"。
- 那句结束后，方块平滑后退并缩小到 Matrix 中的位置，其余方块渐显，过渡结束后直接是 S1 的矩阵，没有跳变、没有黑屏，方块不再闪烁。
- 对白点击推进（`DialogueAdvanceInput`）在教学中正常工作；点掉任意一段的最后一句，流程照常进入下一步。
- 调试跳关进出教学（直接跳到 S1，或从 S1 跳回教学）后，没有残留的放大节点、隐藏节点或闪烁问题。
- 共享代码里不出现设备 API；右键 / 左键仍只通过 `StageContext` 的 Tap / Inspect 事件进入阶段。
- S1 及之后阶段的现有对白资产不被这次任务覆盖或改写。
- 已有阶段的交互仍正常：S1 点方块、S2 拖拽连线、S3/S4 旋转和标记、Pick 都不受影响。

# Result（10-09）

已实现，Play 里通过 MCP 走通（输入用 `execute_code` 临时注入 Mouse 设备事件，对白用空格推进）：

- 进关只显示演示方块（节点 127，Leaf），放大 6 倍、居中闪烁；191 个节点里只有 1 个可见。
- 左键一次 → Easy 解决，变常亮，播"这是你的agent……"（亲切的声音）。
- 之后方块挂 Hard 再闪；左键 → 失败，播 Agent "尝试解决失败……" + 温柔女声两句。
- 右键 → HUD 标题"维护单元07：稳定度下降"，正文两行，随后播"直观、清晰、安心……"。
- 转场：方块缩回矩阵槽位（缩放 0.192 → 0.032），其余节点陆续显现，进入 S1，191/191 可见。
- 跳关：教学 → S1 → 教学，两次都干净；转场中途跳到 S1 也没有残留姿态、问题和隐藏。Console 无错误。
- 旧资产 `TutorialIntro` / `TutorialAfterEasy` / `TutorialAfterMedium` / `TutorialAfterHard` 已确认无引用，移到回收站。

没验证：人手实际点击手感（方块大小、闪烁节奏、转场时长）；S2–Pick 的回归只确认了编译和 S1 能进入，没逐关重玩。

台词表实际编号（和上面"台词"表的分段对应）：TUT_002–003 Intro，TUT_004 简单解决后，TUT_006–008 困难失败 + 右键教学，TUT_009 右键详情，TUT_010 右击后，TUT_011 转场。

# Open decision（实现前确认，默认值见括号）

- TUT_005（维护单元07……）走 Agent 弹窗还是只在右键 HUD 详情里显示？（默认：只在 HUD 详情框里显示，因为它就是右键查看的结果。）
- 演示方块用哪个节点？（默认：一个普通节点，不用果实。）
- 演示方块摆多大、多近？（默认：Matrix 节点尺寸的 6–8 倍，画面中心略偏上，Inspector 可调。）
- 转场时长。（默认：方块后退 1.5 s，其余节点在后半段 1 s 内渐显。）
- Intro 两句是进关立刻播，还是方块出现后再播？（默认：进关立刻播，播完才允许点击。和现在 TutorialIntro 的时机一致。）

# Out of scope

- S1 本身的流程、问题生成和台词。
- 把 xlsx 里 INTRO / S1–S4 / PICK 的改动导入 Unity。
- 其他阶段改用 `Script/*.asset` 的对白（只换教学这一段）。
- 右键操作提示 UI、新手引导箭头。
- VR 的演示姿态适配（以后按 `docs/VR_GUIDELINES.md` 再调，字段已做成可配）。
