# Current State

快照日期：2026-10-08 · `dev/auto` 已合并到 `main`

## Current stage

第一阶段（比赛 demo 主流程 G0–G9）开发完成。S1–S4 可以玩通；Transition 播 S4_008 并变成写实模型后自动进入 Pick；Outro 全屏播放结局视频 `Assets/Video/Outro.mp4`，播完自动进入结束界面（`OutroVideoStage`，10-11，**只做过编译检查，Main.unity 未重建、未经 Play 验收**；没有视频时停在 Outro，按 N 继续）。Pick 阶段完成第 1–4 步和 5a（移动、固定植株、XRI 摘果实、举到嘴边、咬一口缩小）；虚幻化和消散（5b/5c）还没做；吃完第 3 口后等台词播完、停 1 s 自动进入 Outro（10-11，只做过编译检查）。
最新一轮 UX 调整（HUD、右键详情、PlantFit 适配、Gizmos 竖线）已通过 Play 验收（10-08）。

## Features currently working

- 流程：开始界面（策划底图 + 番茄下方"环 + AWAKE"按钮，点它开始）→ Intro（黑屏 + 字幕）→ Tutorial → S1 Matrix → S2 Circuit → S3 Network → S4 Geometric → Transition（S4_008 + 变写实，自动继续）→ Pick → Outro（结局视频）→ 结束界面（点"重新开始"重载场景回到开始界面）。进关和变形同时发生。开始 / 结束界面见 `docs/tasks/start-restart-screens.md`（10-10，**未经 Play 验收**，只做过编译检查）；文案是占位。
- 教学（`docs/tasks/tutorial-single-cube.md`）：只有一个放大 6 倍的演示方块，其余节点隐藏。左键一次解决（简单问题）→ 同一方块变成困难问题，点了失败 → 温柔女声引导右键 → 右键详情显示"维护单元07……" → 方块 1.5 s 后退缩回矩阵槽位，其余方块陆续显现，直接进入 S1。台词全部来自台词表 TUT 段（`Script/TUT_002/004/008/014/015`）。已在 Play 里走通（10-09，含跳关进出和转场中途跳走）。
- 调试：N 下一关，Shift+1–9 跳关（两个方向都能干净进出）。开始界面上按 N 等于点开始，Shift+数字直接进关；在最后一关按 N 弹出结束界面。`GameFlow.autoStart` 勾上可跳过开始界面。
- 节点问题：Easy / Medium / Hard 三档；Medium 两次点击之间隔 5 s 再闪；Hard 永远解决不了。S1 不再使用 Medium（枚举保留）。
- 左键点节点：授权 Agent 处理一次，同时触发亮度脉冲反馈。
- 右键按住：左侧 HUD 弹出详情，松开 2.5 s 后淡出；详情深度随阶段变化（Status → Project → Physical）。标题按阶段取名：S2 项目名、S3 外观名、S4 真实部位名（不带编号），来自 `NodeDetails.asset`，可用菜单 Ghost/Narrative/Import Node Text CSV 从 `docs/script/03_node_text.csv` 导入。
- S1 / S2 缺失节点（`docs/tasks/s1-s2-missing-nodes.md`，10-16，**只做过离线编译检查（运行时 + Editor 程序集），`Main.unity` 未重建、未经 Play 验收**）：`MissingNodePicker` 按规则在叶子 / 茎 / 果实 / 根系各挑一个中间节点（`StageContext.MissingNodes()`，Inspector 可覆盖，运行时打 Log），S1、S2 结果相同。
  - S1：没有中等问题；困难问题挂在这 4 个节点上。简单问题全部解决、且该节点至少失败过 1 次后右键一次 → 朝 `ViewerPosition()` 飘出约 12 cm、发红光、永久不退回；4 个都浮出 → S1_002–004 + S1_005 询问 → S2。变形到 S2 时这 4 个节点直接飘到散落位置。
  - S2：其余节点照常组成回路；缺失节点自身和子节点的连线隐藏，缺口（Circuit 槽位）有呼吸外发光，4 个节点散落在回路左 / 右 / 下方。按住散落节点拖动（跟 `CurrentRay`，平面垂直于起始射线）：进正确缺口 `snapRadius`（0.0525）内两侧连线伸出、发光变强，松手落位；错误缺口松手飘回 + S2_WRONG；空白处静默飘回。补齐前点 / 拖其他节点播 S2_NOPATH（空闲时才播，5 s 冷却）。4 个补齐 → S2_PATCHED → 自动浇水：脉冲从树根沿供给线路（树根 → 各缺水节点）按层推进，每层外发光闪一下、连线变蓝、缺水节点恢复，直角程度按层降到 0 → S2_006–007 → 右键查看果实 → S2_008 → S3。原来"从脉冲源拖线"玩法已删掉（`S2_PULSE_ROUTE.md` 只保留了脉冲视觉）。
  - 音效挂点：拖散落节点 = `SFX_S2_DRAG` 循环，浇水每推进一层 = `SFX_S2_NODE_LINK`（音高逐层升高），落位 = `SFX_S2_SEGMENT_DONE`，放错 = `SFX_S2_WRONG`，浇完 = `SFX_S2_ALL_FIXED`（后三个还没交付，交付前静默）。
  - 新台词 S2_NOPATH / S2_WRONG / S2_PATCHED 是占位文案（`02_dialogue.csv` 待确认列已标注）。
- S3：拖空白处旋转网络，左键标记 3 个虫子节点，弹出 AI 询问，选 Yes 进下一关。
- S4：旋转找叶背虫子并点击摘除（含虫子 FBX 实例），每摘一只写实度提高一档。
- 对白分流：亲切的声音 = 底部字幕（浅色背景黑字、黑屏时白字）；没有温度的声音 = 左侧聊天栏（`AgentChatFeed`）。`Agent弹窗_02` 是头像 + 半透明灰气泡，`Agent弹窗_01` 是 Caution 标签卡；不显示署名。栏在屏幕左侧（`AgentUIStyle.HudFeedTopLeft` = (50, -170)，宽 470，最多高 780）；右键详情也用 `Agent弹窗_02` 气泡样式，排在栏下方。字幕白色描边和外发光只在 PICK 阶段打开（`SubtitlePanel.OutlineEnabled`，PickStage 进出时开关），其他阶段不加。新消息从底部滑入、向下堆叠，最上面那条播完 3 s 后淡出，同时最多 8 条。询问框（`Agent弹窗_01_query`）是 Caution 卡 + YES，单独在屏幕右侧，不进滚动栏。图标先用占位几何图形，可覆盖 `Assets/Art/UI/AgentChat/` 同名文件。见 `docs/tasks/agent-chat-bubbles.md`（10-10，**未经 Play 验收**，只做过编译检查）。改完后必须用菜单 Ghost → Core → Build Main Scene 重建场景。
- 台词表：`docs/script/02_dialogue.csv` 在编辑器里自动导入（也可用菜单 Ghost/Narrative/Import Dialogue CSV），生成 `Assets/Data/Narrative/Script/*.asset` 和 `DialogueLibrary.asset`；台词可以单独指定通道（字幕 / `Agent弹窗_02` 聊天气泡 / `Agent弹窗_01` Caution 卡）；通道为 `Agent询问`、以 `_query` 结尾或 `操作提示` 的行单独成段，只取文本（Yes 询问框、Pick 左上角提示）。
- 定稿台词已接入全流程（`docs/tasks/final-script-integration.md`，10-10，**未经 Play 验收**，只做过编译检查）：Intro 分两段，INTRO_008 后等一次点击；S1 进关台词、点方块随机池（S1_P*，只写植物部位状态、不点名，`{a-b}` 运行时换随机数）、通关台词 + 询问；S2 补缺口提示（S2_NOPATH / S2_WRONG / S2_PATCHED），缺水解决播 S2_006–007，看过果实后播 S2_008 直接进入 S3（没有询问）；S3 第一次标记台词、全部标记台词 + 询问（S3_011 全文）；S4 每摘一只虫子，Agent 弹窗固定显示 S4_005 + 进度 n/3（`SubtitlePanel.Pin`），摘完收起，播 S4_006–007 后进入 Transition；Pick 进关台词、PICK_002 后显示操作提示（`Player/PC/PcControlsHint`）、吃第一口台词。段 key 常量在 `StageAssets`。旧的 S2–S4 占位对白资产和 `Ensure*` 方法还在，但不再挂到场景。
- 配音：`Assets/Audio/VO/` 下名为 `<台词ID>` 或 `VO_<台词ID>` 的音频自动挂到 `Script/` 里同 ID 台词的 `clip`（`VoiceClipBinder`，导入/替换音频时自动执行，也可用菜单 Ghost/Narrative/Bind Voice Clips）；导入设置强制单声道 + Vorbis。有 clip 的台词按音频时长播放。重新导入 CSV 会保留已挂的 clip。音频被删掉时自动清掉丢失的引用。已换成 `最终配音` 交付的 38 句 `VO_*.wav`，全部挂上（10-10，未 Play 验收）。
- 音效 / 音乐 / 环境音（10-10，**未经编译和 Play 验收**）：文件放 `Assets/Audio/SFX|BGM|AMB/`，文件名 = 05_audio_list 编号，变体加 `_01/_02`；`AudioLibraryBuilder` 自动生成 `Assets/Data/Audio/AudioLibrary.asset`（菜单 Ghost/Audio/Rebuild Audio Library）。BGM / AMB 导入为 Streaming，SFX 加载时解压。`GameAudio`（场景里由 Build Main Scene 生成）进关时按 `stageAudio` 表交叉淡入切换音乐 / 环境音（Intro 起播 BGM_DIGITAL，开场动画音效包含在里面）；还没交付的编号跳过、保持上一首。BGM：一首 BGM_DIGITAL 从 Intro 贯穿到 Outro 之前，进 Outro 淡出；AMB_BALCONY 从 Transition 起（还没交付）。音效由 `GameAudioCues` 订阅玩法事件播放（不改玩法逻辑）：点节点授权（TUT/S1）、Agent 弹窗出现（S1 随机池改为数据念白）、Agent 打字循环、询问框（S3 为高风险警告）、TUT 方块融入矩阵、每次变形（`SFX_MORPH_01–04` 按目标形态）、S2 拖散落节点循环 + 浇水推进一层（音高逐层升高）+ 落位 / 放错 / 浇完、S3 标记虫子、S4 摘虫 + 拖拽旋转时叶子沙沙、Pick 摘下果实、咬一口。为此给 Tutorial / S2 / S3 / S4 / `AgentQueryDialog` / `AgentChatFeed` 加了只发不收的事件，`TargetRotator` 加了 `IsDragging`。已编译通过（10-10），**`Main.unity` 还没有重建，场景里还没有 `GameAudio`，未经 Play 验收**。
- 对白推进：左键点空白处或按空格 / 回车，打字中先补全整句，再点进入下一句；点中节点时只算点节点（全黑屏时除外）。
- PlantFit：按 `XR Origin/ViewAnchor`（名义眼位，眼高 1.6 m、水平正视，不跟头动）缩放、居中植株。 VR 视角（10-16，只做过编译检查）：`XR Origin` 上的 `XRViewCalibration` 在头显跟踪后把 ViewAnchor 挪到实际眼高并低头 10°，PlantFit 发现视点变化后平滑重新适配；构图视野上限 `maxFitFov` 60°（头显视野约 96°，按满视野植株太大）；S2（Circuit）按 `formOverrides` 放到 1.2 m。Pick：`XRPlayerHeadTracking` 让 `FirstPersonMotor` 在头显跟踪时不再加 1.6 m 眼高、不做鼠标俯仰和蹲下。PC 画面不变。
- 视角 rig（`docs/tasks/vr-migration.md` 第 1–2 步，10-15，**只做过编译检查，`Main.unity` 未重建、未 Play / 实机验收**）：Build Main Scene 生成 `XR Origin`（地面点 (0, 0, -1.9)，Floor 模式）→ `Camera Offset`（y = 1.6）→ `Main Camera`（Tracked Pose Driver 跟 `<XRHMD>`）。PC 没有 XR Loader，相机停在原固定机位 (0, 1.6, -1.9)；Quest 上眼高和朝向来自头显。XR：Android 用 OpenXR + Meta Quest Support（目标设备 Quest 3，同时勾着 Quest 2 / Pro / 3S；Touch / Touch Plus / Touch Pro 控制器配置），Vulkan、IL2CPP、ARM64、单通道立体，Android 默认画质 Mobile；Standalone 不加载 XR。打包用 Build Profile `Assets/Settings/Build Profiles/Meta Quest.asset`。
- VR 交互发起方（`docs/tasks/vr-migration.md` 第 3 步，10-15，**只做过编译检查，`Main.unity` 未重建、未 Play / 实机验收**）：玩法层引用 `InteractionRouter`（`Pointer` 物体上），头显在运行时选右手柄的 `XRControllerInput`（`XR Origin/Camera Offset/Right Aim`，跟手柄 aim pose），否则选 `PointerInput`。VR 手势：射线指节点只高亮射线；扳机短按节点 = 选中（放大 1.25 倍并朝玩家突起 4 cm，手柄右上方出现"是否批准？"卡片），再按同一个 = 取消，按另一个 = 切换，按空白 = 取消；选中时 A = `Tap`（每按一次算一次），没选中时 A = 推进对白；选中时按住握键 = 查看详情；S2 从节点按住扳机扫过 = 拖拽连线；右摇杆 = 旋转（S3 只偏航，`TargetRotator.allowPitch` 由 S3 关掉，S4 偏航 + 俯仰）。S3/S4 里扳机按住不进节点拖拽（PC 那两关的节点拖拽只用来转物体）。`Gameplay` 表加了 `Approve`、`Rotate` 和右手柄 XR 绑定（扳机 / 握键 / A / 摇杆）。批准卡片仍是旧样式。
- VR UI（`docs/tasks/vr-migration.md` 第 4 步，10-16，**只做过编译检查，`Main.unity` 未重建、未 Play / 实机验收**）：PC 上所有 HUD 不变；头显运行时 `XRWorldCanvas`（`Interaction/XR/`，由 `Core/Editor/XRUIBuilder` 在 Build Main Scene 时挂上）把 Canvas 换成 World Space，UI 用 ZTest Always 不被植株挡。字幕 = LazyFollow，头前 1.5 m、低 0.42 m（视野下方），VR 折行宽 0.9 m；Agent 聊天栏单独成一块 Canvas（`AgentFeedHUD`），跟右手柄（手柄左上方，和批准卡片错开，最新消息在下），只有头像和气泡、没有底板；带 YES 的询问框在视野正中、头前 0.55 m（比矩阵近），LazyFollow 跟头面向玩家；详情弹窗贴在被查看节点的右侧，在这个节点（含选中突起）正面前方 0.1 m、跟着节点走，朝向固定，1 mm/px（10-16 按反馈调整）；批准卡片缩到 70%，在手柄射线前方 0.18 m、下方 0.08 m（`XRApprovePanel.offset`），按扳机、按 A 批准、握键查看或取消选中时收起；开始 / 结束界面和结局视频 = 头前 2–2.2 m 的 LazyFollow 面板；黑屏挂在相机前 0.1 m。按钮（Yes / AWAKE / 重新开始）用 Collider 桥接（`XRUIRayTarget` 实现 `IInteractable`，扳机短按 → `Button.onClick`），不用 XRI UI 射线；界面黑底和询问卡片也挡射线。阶段面板、PC 键位提示、准星不处理。
- Pick：进关时 PlayerRig 接管主相机（画面不跳），光标锁定，屏幕中心准星；WASD 约 2 m/s，鼠标转头（俯仰 -75°…70°），C / 左 Ctrl 切换蹲下（眼高 1.6 → 1.0 m）。植株按真实尺寸（约 0.7 m 高）固定在前方 2.5 m 的矮台上，不能转；有地面、四面不可见边界和植株挡板。离开时相机回固定机位、光标解锁、PlantFit 恢复适配。
- Pick 摘和吃：头上的 `XRRayInteractor`（屏幕中心，左键 = `Player/Grab`）对准 `pepper_red_picked`（运行时加 SphereCollider + `XRSimpleInteractable`），眼睛到果实 ≤ 0.7 m 才可摘，悬停时果实提亮、准星变色；左键后手臂（Two Bone IK）伸过去，果实挂到掌心收回。E 举到嘴边，之后每次 E 咬一口缩小一档，发 `EatSequence.BiteTaken`。离关时果实回到植株上。
- 画面压暗边（`docs/tasks/screen-edge-vignette.md`，10-10）：PC / Mobile 渲染器上的 Full Screen Pass（`EdgeVignette`）+ `Ghost/EdgeVignette` 着色器，四周不规则、缓慢流动的暗边，亮背景压黑、暗背景微微发白；PC 材质带边缘虚化，Mobile 关闭。Screen Space Overlay UI 不受影响。菜单 Ghost/PostFX/Setup Edge Vignette 可重新挂载；在渲染器上取消勾选该 Feature 即关闭。参数只做过截图检查，未完整 Play 验收。
- 已装 XRI 3.3.2（含 `Assets/XRI/Settings`）和 Animation Rigging 1.4.1，还没在场景里使用；不连头显进 Play 无 XR 报错。

## Important files

- `docs/PROJECT_SUMMARY.md`：给人看的设计文档（玩法规则、模块历史、人工验收清单），agent 不维护。
- `docs/ARCHITECTURE.md`：系统、接口、数据流、稳定扩展点。`docs/VR_GUIDELINES.md`：VR 迁移规则。
- `Assets/Scripts/Core/Editor/MainSceneMenu.cs`：**Main.unity 的唯一来源**。菜单 Ghost → Core → Build Main Scene 会整个覆盖场景。
- `Assets/Scripts/Stages/StageContext.cs`：各阶段共用的引用和工具。
- `Assets/Data/Morph/pepper_plant.asset`：最终植株的节点集。S1/S2 的 4 个缺失节点由 `MissingNodePicker` 按器官和连线拓扑挑选，不写死 id（Inspector 可覆盖）。
- `Assets/Data/Narrative/*.asset`：对白和详情文本，均为 `[占位]`，由 `StageAssets` / `NarrativeAssets` 生成。

## Known bugs

- 仓库里 `Main.unity` 的状态是否与最新代码一致不确定：改了 `MainSceneMenu` 后必须重新跑菜单。
- 在 MCP 下执行 Build Main Scene，如果当前场景有未保存修改，会弹出保存对话框，卡住编辑器。
- 只有 3 个虫子节点（188/189/190）。虫子 FBX 还没放进彩椒模型，见 `docs/PROJECT_SUMMARY.md` G9 待办。重新采样会让节点重新编号；S3 虫子节点（188/189/190）会失效。S1/S2 缺失节点按拓扑重算。
- G9（S4）没有经过完整的 Play 验收。
- 主角（Protagonist）台词的显示样式还没经策划确认。

## Architectural invariants

- 相机不由代码移动（VR 下只跟头显），Pick 阶段除外（由 `PlayerRig` 接管，离开时还原）。其他阶段构图靠 PlantFit（按 ViewAnchor，不按跟头的相机） 缩放和平移植株（`docs/VR_GUIDELINES.md` 第 3 节）；Pick 阶段用 `PlantFit.Hold` 固定真实尺寸，`Release` 恢复。
- 输入只读 Input Action（`Gameplay` / `Debug` / `Player` 表），不读设备。PC 专用代码（光标锁定、准星）只放在 `Scripts/Player/PC/`。
- 节点不是 GameObject：拾取走 `NodePicker`，显示走 `NodeMorpher` 的状态接口，不要给节点加 Collider。
- 场景由代码生成，不要手动改 `Main.unity`；改场景就改 `MainSceneMenu` 再跑菜单。
- 阶段 Enter 时订阅、Exit 时退订，并调用 `ctx.ResetShared()`，保证跳关可逆。
- 不重新采样 `pepper_plant`。S1/S2 缺失节点按拓扑挑选，重新采样后结果可能变，但不需要改代码。
- 【比赛期技术债】UI 是 Screen Space Overlay HUD，赛后改回 World Space（`docs/VR_GUIDELINES.md` 第 5 节）。
- 不要提交 `Packages/manifest.json`、`packages-lock.json` 里的 `com.tripo3d.unitybridge` 一行（本机路径）。`runInBackground: 1` 已提交（10-08），让编辑器失焦时 Play 模式和 MCP 照常运行。

## Next planned milestone

