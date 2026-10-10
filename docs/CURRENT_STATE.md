# Current State

快照日期：2026-10-08 · `dev/auto` 已合并到 `main`

## Current stage

第一阶段（比赛 demo 主流程 G0–G9）开发完成。S1–S4 可以玩通；Transition 播 S4_008 并变成写实模型后自动进入 Pick；Pick 是最后一关（Outro 占位阶段 10-10 已去掉）。Pick 阶段完成第 1–4 步和 5a（移动、固定植株、XRI 摘果实、举到嘴边、咬一口缩小）；虚幻化和消散（5b/5c）还没做；吃完最后一口等 `completeDelay`（2 s）自动通关，进入结束界面播结尾视频（10-10，**未经 Play 验收**）。
最新一轮 UX 调整（HUD、右键详情、PlantFit 适配、Gizmos 竖线）已通过 Play 验收（10-08）。

## Features currently working

- 流程：开始界面（策划底图 + 番茄下方"环 + AWAKE"按钮，点它开始）→ Intro（黑屏 + 字幕）→ Tutorial → S1 Matrix → S2 Circuit → S3 Network → S4 Geometric → Transition（S4_008 + 变写实，自动继续）→ Pick（吃完自动通关）→ 结束界面（先全屏播结尾视频 `Assets/Art/Video/ending.mp4`，播完露出按钮；点"重新开始"重载场景回到开始界面）。进关和变形同时发生。开始 / 结束界面见 `docs/tasks/start-restart-screens.md`（10-10，**未经 Play 验收**，只做过编译检查）；文案是占位。结尾视频 10-10 接入，**未经 Play 验收**，需重新执行 MainSceneMenu 生成场景才会出现。
- 教学（`docs/tasks/tutorial-single-cube.md`）：只有一个放大 6 倍的演示方块，其余节点隐藏。左键一次解决（简单问题）→ 同一方块变成困难问题，点了失败 → 温柔女声引导右键 → 右键详情显示"维护单元07……" → 方块 1.5 s 后退缩回矩阵槽位，其余方块陆续显现，直接进入 S1。台词全部来自台词表 TUT 段（`Script/TUT_002/004/008/014/015`）。已在 Play 里走通（10-09，含跳关进出和转场中途跳走）。
- 调试：N 下一关，Shift+1–9 跳关（两个方向都能干净进出）。开始界面上按 N 等于点开始，Shift+数字直接进关；在最后一关按 N 弹出结束界面。`GameFlow.autoStart` 勾上可跳过开始界面。
- 节点问题：Easy / Medium / Hard 三档；Medium 两次点击之间隔 5 s 再闪；Hard 永远解决不了。
- 左键点节点：授权 Agent 处理一次，同时触发亮度脉冲反馈。
- 右键按住：左侧 HUD 弹出详情，松开 2.5 s 后淡出；详情深度随阶段变化（Status → Project → Physical）。标题按阶段取名：S2 项目名、S3 外观名、S4 真实部位名（不带编号），来自 `NodeDetails.asset`，可用菜单 Ghost/Narrative/Import Node Text CSV 从 `docs/script/03_node_text.csv` 导入。
- S2：脉冲引路（`docs/tasks/S2_PULSE_ROUTE.md`）。脉冲源蓝色闪烁 + 外圈呼吸发光，段内节点外圈依次亮起并慢慢衰减（`NodeHaloRenderer`），每段最多 5 个节点；拖拽时一条蓝线连着最后连上的节点和指针；连线进关时是直角折线，每结束一次拖拽按连通比例往两点直连过渡，全部连通时变成树形直线（S3 变形从这里开始）；从脉冲源按住沿脉冲拖过这一段连上（线和节点变蓝，缺水恢复），终点成为下一个脉冲源，自动指向最近的缺水节点；中途松开不回退。彩椒上共 16 段。果实不在线路上；右键查看果实是通关条件之一。已通过 Play 验收（10-09）。
- S3：拖空白处旋转网络，左键标记 3 个虫子节点，弹出 AI 询问，选 Yes 进下一关。
- S4：旋转找叶背虫子并点击摘除（含虫子 FBX 实例），每摘一只写实度提高一档。
- 对白分流：亲切的声音 = 底部字幕（浅色背景黑字、黑屏时白字）；没有温度的声音 = 左侧聊天栏（`AgentChatFeed`）。`Agent弹窗_02` 是头像 + 半透明灰气泡，`Agent弹窗_01` 是 Caution 标签卡；不显示署名。栏在屏幕左侧（`AgentUIStyle.HudFeedTopLeft` = (50, -170)，宽 470，最多高 780）；右键详情也用 `Agent弹窗_02` 气泡样式，排在栏下方。字幕白色描边和外发光只在 PICK 阶段打开（`SubtitlePanel.OutlineEnabled`，PickStage 进出时开关），其他阶段不加。新消息从底部滑入、向下堆叠，最上面那条播完 3 s 后淡出，同时最多 8 条。询问框（`Agent弹窗_01_query`）是 Caution 卡 + YES，单独在屏幕右侧，不进滚动栏。图标先用占位几何图形，可覆盖 `Assets/Art/UI/AgentChat/` 同名文件。见 `docs/tasks/agent-chat-bubbles.md`（10-10，**未经 Play 验收**，只做过编译检查）。改完后必须用菜单 Ghost → Core → Build Main Scene 重建场景。
- 台词表：`docs/script/02_dialogue.csv` 在编辑器里自动导入（也可用菜单 Ghost/Narrative/Import Dialogue CSV），生成 `Assets/Data/Narrative/Script/*.asset` 和 `DialogueLibrary.asset`；台词可以单独指定通道（字幕 / `Agent弹窗_02` 聊天气泡 / `Agent弹窗_01` Caution 卡）；通道为 `Agent询问`、以 `_query` 结尾或 `操作提示` 的行单独成段，只取文本（Yes 询问框、Pick 左上角提示）。
- 定稿台词已接入全流程（`docs/tasks/final-script-integration.md`，10-10，**未经 Play 验收**，只做过编译检查）：Intro 分两段，INTRO_008 后等一次点击；S1 进关台词、点方块随机池（S1_P*，只写植物部位状态、不点名，`{a-b}` 运行时换随机数）、通关台词 + 询问；S2 错误起点提示，缺水解决播 S2_006–007，看过果实后播 S2_008 直接进入 S3（没有询问）；S3 第一次标记台词、全部标记台词 + 询问（S3_011 全文）；S4 每摘一只虫子，Agent 弹窗固定显示 S4_005 + 进度 n/3（`SubtitlePanel.Pin`），摘完收起，播 S4_006–007 后进入 Transition；Pick 进关台词、PICK_002 后显示操作提示（`Player/PC/PcControlsHint`）、吃第一口台词。段 key 常量在 `StageAssets`。旧的 S2–S4 占位对白资产和 `Ensure*` 方法还在，但不再挂到场景。
- 配音：`Assets/Audio/VO/` 下名为 `<台词ID>` 或 `VO_<台词ID>` 的音频自动挂到 `Script/` 里同 ID 台词的 `clip`（`VoiceClipBinder`，导入/替换音频时自动执行，也可用菜单 Ghost/Narrative/Bind Voice Clips）；导入设置强制单声道 + Vorbis。有 clip 的台词按音频时长播放。重新导入 CSV 会保留已挂的 clip。已放入 37 句人声（10-10，**未经编译和 Play 验收**）。音效 / BGM 还没有接入入口。
- 对白推进：左键点空白处或按空格 / 回车，打字中先补全整句，再点进入下一句；点中节点时只算点节点（全黑屏时除外）。
- PlantFit：相机固定（眼高 1.6 m、水平正视），按形态缩放、居中植株。
- Pick：进关时 PlayerRig 接管主相机（画面不跳），光标锁定，屏幕中心准星；WASD 约 2 m/s，鼠标转头（俯仰 -75°…70°），C / 左 Ctrl 切换蹲下（眼高 1.6 → 1.0 m）。植株按真实尺寸（约 0.7 m 高）固定在前方 2.5 m 的矮台上，不能转；有地面、四面不可见边界和植株挡板。离开时相机回固定机位、光标解锁、PlantFit 恢复适配。
- Pick 摘和吃：头上的 `XRRayInteractor`（屏幕中心，左键 = `Player/Grab`）对准 `pepper_red_picked`（运行时加 SphereCollider + `XRSimpleInteractable`），眼睛到果实 ≤ 0.7 m 才可摘，悬停时果实提亮、准星变色；左键后手臂（Two Bone IK）伸过去，果实挂到掌心收回。E 举到嘴边，之后每次 E 咬一口缩小一档，发 `EatSequence.BiteTaken`。离关时果实回到植株上。
- 画面压暗边（`docs/tasks/screen-edge-vignette.md`，10-10）：PC / Mobile 渲染器上的 Full Screen Pass（`EdgeVignette`）+ `Ghost/EdgeVignette` 着色器，四周不规则、缓慢流动的暗边，亮背景压黑、暗背景微微发白；PC 材质带边缘虚化，Mobile 关闭。Screen Space Overlay UI 不受影响。菜单 Ghost/PostFX/Setup Edge Vignette 可重新挂载；在渲染器上取消勾选该 Feature 即关闭。参数只做过截图检查，未完整 Play 验收。
- 已装 XRI 3.3.2（含 `Assets/XRI/Settings`）和 Animation Rigging 1.4.1，还没在场景里使用；不连头显进 Play 无 XR 报错。

## Important files

- `docs/PROJECT_SUMMARY.md`：给人看的设计文档（玩法规则、模块历史、人工验收清单），agent 不维护。
- `docs/ARCHITECTURE.md`：系统、接口、数据流、稳定扩展点。`docs/VR_GUIDELINES.md`：VR 迁移规则。
- `Assets/Scripts/Core/Editor/MainSceneMenu.cs`：**Main.unity 的唯一来源**。菜单 Ghost → Core → Build Main Scene 会整个覆盖场景。
- `Assets/Scripts/Stages/StageContext.cs`：各阶段共用的引用和工具。
- `Assets/Data/Morph/pepper_plant.asset`：最终植株的节点集；S1/S2 写死了其中的节点 id。
- `Assets/Data/Narrative/*.asset`：对白和详情文本，均为 `[占位]`，由 `StageAssets` / `NarrativeAssets` 生成。

## Known bugs

- 仓库里 `Main.unity` 的状态是否与最新代码一致不确定：改了 `MainSceneMenu` 后必须重新跑菜单。
- 在 MCP 下执行 Build Main Scene，如果当前场景有未保存修改，会弹出保存对话框，卡住编辑器。
- 只有 3 个虫子节点（188/189/190）。虫子 FBX 还没放进彩椒模型，见 `docs/PROJECT_SUMMARY.md` G9 待办。重新采样会让节点重新编号，S1/S2 写死的 id 会失效。
- G9（S4）没有经过完整的 Play 验收。
- 主角（Protagonist）台词的显示样式还没经策划确认。

## Architectural invariants

- 相机永远不动，Pick 阶段除外（由 `PlayerRig` 接管，离开时还原）。其他阶段构图靠 PlantFit 缩放和平移植株（`docs/VR_GUIDELINES.md` 第 3 节）；Pick 阶段用 `PlantFit.Hold` 固定真实尺寸，`Release` 恢复。
- 输入只读 Input Action（`Gameplay` / `Debug` / `Player` 表），不读设备。PC 专用代码（光标锁定、准星）只放在 `Scripts/Player/PC/`。
- 节点不是 GameObject：拾取走 `NodePicker`，显示走 `NodeMorpher` 的状态接口，不要给节点加 Collider。
- 场景由代码生成，不要手动改 `Main.unity`；改场景就改 `MainSceneMenu` 再跑菜单。
- 阶段 Enter 时订阅、Exit 时退订，并调用 `ctx.ResetShared()`，保证跳关可逆。
- 不重新采样 `pepper_plant`，除非同时重查 S1/S2 的节点 id。
- 【比赛期技术债】UI 是 Screen Space Overlay HUD，赛后改回 World Space（`docs/VR_GUIDELINES.md` 第 5 节）。
- 不要提交 `Packages/manifest.json`、`packages-lock.json` 里的 `com.tripo3d.unitybridge` 一行（本机路径）。`runInBackground: 1` 已提交（10-08），让编辑器失焦时 Play 模式和 MCP 照常运行。

## Next planned milestone

