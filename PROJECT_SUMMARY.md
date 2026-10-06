# A Gift for Ghost：项目总结

> 当前设计与开发计划的快照（2026-10-05）。工程约定见 [AGENTS.md](AGENTS.md)，环境初始化见 [SETUP.md](SETUP.md)。

## 1. 比赛约束

- 3 人，48 小时，做一条可以从头玩到尾的最小线性流程（约 10 分钟）。
- PC 键鼠 demo，Unity 6000.3.25f1 + URP。以后会在同一个工程上迁移到 Quest 2 VR，所以设计要对 VR 友好（见 AGENTS.md）。
- 方向是重体验、重交互创新，不追求成熟玩法。
- 可用的 AI 工具：Tripo3D（有 API，计划用于批量生成模型）、Pixverse（视频、图片、音频）、WorldLabs、Tapnow、TapTapMaker。
- 待确认：工具表里写着"SR 设备：必须选择 SR 赛道"，具体要求要问主办方。

## 2. 主题

玩家扮演 Ghost，一个生活被 Agent 以 **Full Proxy（完全代理）** 模式接管的人。他的工作、关系和生活都被压缩成了任务、指标和"已完成"。

游戏过程就是他一步步拿回主体性：**批准输出 → 检查输入 → 审核推理 → 验证结果 → 接管决策 → 亲自行动**。

- 现实锚点是一株彩椒。Agent 口中的"核心项目"就是这株彩椒，"成员 A"就是母亲。
- 结局不是摧毁 Agent，而是把它调成 **Assistive（辅助）** 模式。Agent 可以举灯、给建议、稳住动作，但不能替人决定看什么、怎么理解别人、什么值得珍惜。
- 立场不反 AI。重点是校准信任：可逆、低风险的事可以交给 Agent；模糊、不可逆、涉及价值判断的事要查证据，或者自己来做。

## 3. 视觉核心："Encoded States"

全游戏只有**同一组节点**（约 150–300 个方块或碎片），在几套布局之间变形。它们始终代表那株彩椒，只是抽象程度在降低：

方块矩阵 → 直角回路 → 3D 网络 → 几何植株 → 写实彩椒

- 美术风格：浅灰背景，黑、灰、蓝灰色的方块和细线，写实阶段才出现绿色和红色。参考策划给的 6 张 "ENCODED STATES" 概念图。
- 材质用 Unlit 或 Simple Lit，配 GPU Instancing，保持 Quest 的性能预算。

## 4. 游戏流程与玩法（2026-10-06 版，以策划流程图为准）

流程：**开场剧情 → 新手教学 → S1 → S2 → S3 → S4 → 采摘 → 结局剧情**

### 通用机制：节点问题（在教学里教会，S1–S3 沿用）

- 每个节点都能点击，状态分正常和异常两种。异常的节点会在原色和白色高亮之间闪烁。
- 点击异常节点，等于授权 Agent 尝试解决一次。成功就停止闪烁；失败的话，过一段时间会重新闪烁。
- 问题分三档难度：
  - **简单**：点一次就解决。
  - **中等**：要点 3 次。
  - **困难**：解决不了。点一次只会暂停，2–3 秒后又开始闪烁。
- 点击节点会弹出详情。详情的内容随阶段变得越来越具体：
  - S1：只有任务状态、进度和置信度。
  - S2：项目语言的模糊描述，比如根叫"支撑系统"，叶叫"边缘系统"。
  - S3：物体本身的描述，包括颜色、形状、纹理和质感。

### 各段落

| 段落 | 形态 | 交互 | 机制 | 通关条件 |
|---|---|---|---|---|
| 开场剧情 | 黑屏 | 无 | 只有语音和字幕。有情感的女声："嗨，你醒了。"机械女声："神经连接已恢复。检测到稳定的意识活动。语言和运动通道尚未恢复。"有情感的女声："你的感官系统与运动神经都沉寂了太久了。我现在是通过脑机接口与你对话。别担心，我们会通过一些虚拟世界的练习帮助你恢复行为能力。先从简单的开始，注意观察你行动的反馈。" | 播放完毕 |
| 新手教学 | Matrix | 点击 | 用少量节点教会上面的通用机制 | 三种难度各体验一次（具体待策划补充） |
| S1 | Matrix | 点击 | 随机冒出约 5–6 个简单问题，再加若干中等问题；困难问题固定为 4 个果实节点，代表"果实出现严重问题"；详情只显示任务状态、进度和置信度 | ① 简单和中等问题全部解决；② 困难问题累计尝试 3 次以上；③ 满足 ①② 后弹出 AI 询问"是否要进一步查看信息"，选 Yes |
| S2 | Circuit | 点击；从节点出发拖拽 | 问题源于植物缺水，土壤、叶片和果实节点都出问题。玩家从根节点出发，沿连线拖到最远的问题节点，经过的节点都会恢复，但果实除外，因为它其实有虫，只是还看不出来。手指划过的连线变成蓝色。点击果实等节点，会看到项目语言的描述 | ① 缺水问题解决；② 查看过问题节点（果实）；③ 弹出 AI 询问，选 Yes |
| S3 | Network | 点击；拖拽空白处旋转结构 | 节点开始显示颜色，玩家要靠颜色认出所有虫子节点；点过的虫子节点保持高亮；点击节点会显示物体描述 | ① 所有虫子节点都点过；② 弹出 AI 询问"除虫是精细操作，机械臂摘除有伤害植物的风险，是否要进一步手动介入"，选 Yes |
| S4 | Geometric，逐步变写实 | 点击；拖拽旋转盆栽 | 旋转盆栽，找到藏在叶背的虫子并点击摘除。每摘除一只，植株就更写实一档，从浅色几何体变成鲜艳的几何体，但始终不是写实模型 | 所有虫子都已摘除 |
| 过渡 | Geometric 到 Real | 无 | 播放一段独白或 Agent 语音，然后彻底换成写实模型（已有 RealModelHandoff） | 播放完毕 |
| 采摘 | Real | 无（动画） | 动画：玩家的手出现，摘下一颗写实果实 | 动画结束 |
| 结局剧情 | 待定 | 待定 | 见剧情和美术资产表 | 待定 |

### 这一版带来的简化

- **相机始终固定**，玩家不需要走动。"旋转"指旋转眼前的物体（S3 的结构、S4 的盆栽），不是转动视角。
- **双手只在采摘动画里出现**，不需要做一套可操控的手部系统。
- 所以 3C 只剩三样：固定相机、鼠标光标，以及一个旋转目标物体的控制器。

### 待策划补充

- 教学的具体步骤。
- 每个阶段、每类节点的详情文本，AI 询问和独白的台词。
- S1 中等问题的数量。
- ~~切换阶段的时机~~ 已定（10-06）：选 Yes 之后变形和进入下一关同时发生，变形本身就是进入下一关的标志。
- S4 写实度分几档，和虫子数量怎么对应。
- ~~番茄还是彩椒~~ 已定（10-06）：最终用彩椒，即现有的 `pepper_plant` 模型。代码里统称 Fruit。

## 5. 技术架构

### 5.1 节点变形系统（第一个原型）

```
PlantNode            // 每个节点一份数据
  id                 // 等于在列表中的下标
  organ              // Soil / Root / Stem / Leaf / Bud / Fruit / Bug
  parentId           // 植物拓扑关系，S2 的连线和 S3 的网络都由它生成；父节点必须排在子节点前面
  depth              // 离根的层数，用于错峰动画
  poses[5]           // 每种形态一套：position / rotation / scale（下标对应 MorphForm）
PlantNodeSet         // ScriptableObject，存一整株植物的节点，带 Validate() 校验
MorphForm            // Matrix / Circuit / Network / Geometric / Real，共 5 种形态
NodeMorpher          // 把所有节点从当前布局插值到目标布局（带缓动和错峰延迟）
GameFlow / Stage     // 阶段状态机，见第 9 节 G1
```

布局倒着生成，从写实彩椒往回推：

1. **写实形态**：在彩椒模型上采样锚点。原型阶段先用程序生成一株假植物（递归分枝）。
2. **几何植株**：节点留在原位，各自换成三角片或长方块。
3. **3D 网络**：保留父子连接，把位置拉开、打散。
4. **回路**：投影到一个平面，连线改成直角折线（LineRenderer）。
5. **矩阵**：把节点顺序打乱，排成网格，让人看不出植物轮廓。

形态和段落的对应（10-06 版）：教学和 S1 = Matrix，S2 = Circuit，S3 = Network，S4 = Geometric（逐步变写实），过渡和采摘 = Real。

原型目标：按数字键 1–5 切换形态，节点平滑飞到新布局；跑完能判断视觉成不成立。

### 5.2 交互框架

- 射线交互器：PC 端用屏幕中心准星或鼠标射线，VR 版换成手柄射线。被交互物上的代码不需要改。
- `IInteractable` 事件：`OnTap`、`OnHold(progress)`、`OnDrag`、`OnGrab`、`OnRelease`。
- 玩家视角固定，观察时旋转眼前的物体，不动相机，以免 VR 里致晕。
- Agent 的话显示在 World Space 面板上，配合语音。
- 输入全部走 `Assets/InputSystem_Actions.inputactions` 里的 Action（已有 Point、Click、Look、Interact 等）。

### 5.3 共享数据接口（开工前定死）

1. **节点数据**：技术美术负责生成，程序读取后挂交互。
2. **对话数据**（ScriptableObject）：字段有 id、说话人、文本、音频片段、触发事件；同一条消息有摘要版和原文版。
3. **阶段事件名**：`StageEnter`、`StageGoalComplete`、`TransitionStart`、`TransitionEnd`。

### 5.4 建议的目录

```
Assets/Scripts/Core/          GameFlow、Stage 基类、事件
Assets/Scripts/Morph/         PlantNode、布局生成、NodeMorpher
Assets/Scripts/Interaction/   IInteractable、射线交互器、抓取
Assets/Scripts/Stages/        S1–S4 各阶段的玩法逻辑
Assets/Scripts/UI/            World Space 对话面板
Assets/TripoModels/           Tripo 生成的资产（FBX，通过导入设置校正尺寸）
tools/tripo/                  批量生成脚本 + assets.json 清单
```

### 5.5 工程现状

- 场景：`Assets/Scenes/Begining.unity`（尚未提交）、`SampleScene.unity`。
- 脚本：只有 `Assets/Scripts/PlayerMovement.cs`。这个脚本直接读 `Keyboard.current`，违反了 AGENTS.md "输入只读 Action"的约定，以后要改成读 `Move` Action。这个设计里玩家基本不用走动，也可以直接删掉。
- 已装的包：URP 17.3、Input System 1.20、Timeline、Funplay MCP for Unity（`com.gamebooom.unity.mcp`，git URL 安装，供 AI 助手截图、读编译错误、模拟输入；服务只监听 127.0.0.1）。
- 参考模型：`Assets/TripoModels/pepper_plant_3d_model/`，就是最终使用的彩椒。约 5.1 万三角面，2K 贴图；高约 1 m（Tripo 归一化尺寸，未按真实尺寸校正）。尺寸还需按真实尺寸校正。
- 变形原型进度：M1 数据结构已完成（`Assets/Scripts/Morph/PlantNode.cs`、`PlantNodeSet.cs`）。M2 程序化假植物已完成（`FakePlantGenerator.cs`，菜单 Ghost → Morph → Generate Fake Plant，生成 `Assets/Data/Morph/FakePlant.asset`，206 个节点，约 0.67 × 0.64 × 0.56 m）。叶片由两个节点组成（内侧宽、外侧窄），虫子贴在叶背，以那片叶为父节点。M3 布局生成已完成（`LayoutGenerator.cs`，生成假植物时自动生成另外 4 种形态）：Geometric 原位、旋转量化到 45°、茎段留缝；Network 从中心放大 1.8 倍加抖动；Circuit 投影到 XY 平面并吸附 3.5 cm 网格，格子不重叠；Matrix 打乱后排成 15 × 14 方阵。所有形态共用中心点 (0, 0.32, 0)，平面形态面朝 -Z。M4 变形动画已完成（`NodeMorpher.cs`、`Assets/Art/Morph/MorphNode.shader` + `MorphNode.mat`）：节点不是 GameObject，用 `Graphics.RenderMeshInstanced` 一次画完，颜色是逐实例属性；每个节点 1.2 s 缓动（三次 ease-in-out），按深度错峰最多 0.9 s，另加随机 0.35 s；往写实方向从根往外长，往抽象方向从枝梢先散开；中途切换从当前位置继续。颜色按形态插值：Matrix/Circuit/Network 只用黑、灰、蓝灰，Geometric 混入 40% 写实色，Real 为写实色。运行时在 Inspector 改 `inspectorTarget` 即可触发变形，`MorphStarted` / `MorphCompleted` 事件供 GameFlow 使用。M5 输入已完成：`InputSystem_Actions.inputactions` 新增 `Debug` 动作表（`MorphStage1`–`MorphStage5`，数字键 1–5），由 `MorphDebugSwitcher.cs` 读取。M6 场景已完成：菜单 Ghost → Morph → Build Prototype Scene 生成 `Assets/Scenes/MorphPrototype.unity`（浅灰背景，相机眼高 1.6 m、距植株 1.9 m，植株放在 0.75 m 台面高度）。M7 连线已完成（`NodeLinkRenderer.cs` + `Assets/Art/Morph/MorphLine.shader`）：所有父子连线合成一个 Lines 拓扑的动态网格，一次绘制；回路是直角折线，网络是直线，几何植株淡出到 15%，矩阵和写实没有连线；土块不连线。T2 模型采样已完成（`Editor/MeshAnchorSampler.cs`，菜单 Ghost → Morph → Sample Plant From Selected Model）：按子物体名前缀识别部位，表面最远点采样约 224 个节点，颜色取自贴图，拓扑用最小生成树从茎底部长出；高度归一化到 0.6 m，`PlantNodeSet` 记录来源模型和缩放偏移。写实交接已完成（`RealModelHandoff.cs` + `Assets/Art/Morph/RevealLit.shader`）：节点只负责抽象到几何植株，到写实形态时真模型从下往上按方块噪声溶解显现（clip，不透明），同一高度的节点同步缩小消失；离开写实时模型先褪去再开始变形。模型实例在运行时生成，`ModelInstance` 供 S4 抓取使用。节点形状按部位区分（`NodeShapes.cs`）：抽象形态全是立方体；几何植株和写实形态里叶片和花苞换成菱形薄片（八面体压扁），果实和虫子换成低多边形球，形状交接时立方体缩小、部位形状长大。叶片和果实节点按归属表面点的主方向（PCA）定朝向和长宽；每个果实固定 2 个节点。几何植株保持写实朝向，整体缩到 85%，茎段缩短到 75% 露出缝隙。绘制按网格分 3 批（立方体、八面体、球）。

## 6. 除变形和交互外还要做的内容

- **对话系统**：播放器、字幕、排队与打断、摘要和原文的对照呈现。
- **开场与结局**：开机演出、治疗舱剥落效果、调到 Assistive 模式的交互。
- **交互引导**：每个新动作第一次出现时给提示，用场景里的光和声音引导视线。
- **音频**：每个阶段的环境音乐（从电子逐渐过渡到自然）；关键音效（滴水声、连线、摘彩椒的撕裂声）；配音（Agent 用合成腔，母亲最好真人录）。
- **资产**：写实彩椒（已有）、虫子、治疗舱、玩家的手（Tripo 生成）。
- **调试跳关**：一键跳到任意阶段，联调和答辩现场都会用到。
- **提交材料**：Windows 包、1–2 分钟演示视频、答辩 PPT、操作说明。

## 7. 分工

| 角色 | 负责 |
|---|---|
| A 程序 | 交互框架、GameFlow、调试跳关、对话系统、S1–S4 玩法逻辑 |
| B 技术美术 | 节点变形系统与布局、Shader（连线、脉冲、标注）、场景与灯光、过渡演出、结局剥落、Tripo 资产导入 |
| C 策划兼内容 | 每个阶段的一页纸设计、全部台词与消息文本、音频生成和录制、Tripo 提示词清单、试玩反馈、视频和 PPT |

## 8. 时间表（T = 开工后的小时数）

| 时间 | 目标 |
|---|---|
| T0–2 | 定死共享接口，完成每个阶段的一页纸设计 |
| T2–14 | 并行开发：框架加 S1、变形原型、全部台词和资产生成 |
| T14–22 | 轮流睡觉，接入 S2 和 S3 |
| **T24** | **里程碑：4 个阶段的灰盒能从头走到尾**（还不行就继续砍） |
| T24–38 | S4 的抓取手感、过渡演出、音频接入、开场和结局 |
| T38–42 | 功能冻结，只修 bug，在另一台电脑上打包测试 |
| T42–48 | 演示视频、PPT、提交 |

## 9. 开发模块与顺序

每个模块都可以单独开一段对话来执行。开头这样写："读 PROJECT_SUMMARY.md，做模块 Gx。"模块做完后，把它在本节的状态改成"完成"，并写上实际的接口和文件名，下一段对话才能接上。

### 依赖关系

```
G0 提交现状
 └─ G1 流程骨架 ──┬─ G2 节点拾取与输入 ──┐
                  ├─ G3 节点状态与表现 ──┤
                  ├─ G4 Agent 界面 ──────┼─ G6 教学 + S1 → G7 S2 → G8 S3 → G9 S4 → G10 收尾流程
                  └─ G5 对话与语音 ──────┘
G11 音频与打磨（贯穿后期）
```

G1 完成后，G2–G5 互不依赖，可以分给不同的人并行做。G6 起按顺序推进。

### 模块清单

**G0 提交现状（10 分钟）** 状态：**完成（10-06）**。`dev/auto` 已创建并推送。Tripo3d Unity Bridge 按用户决定只留在本地，没有进仓库：本机的 `manifest.json` 和 `packages-lock.json` 里保留了这一行，所以这两个文件会一直显示为已修改。**以后提交这两个文件时，必须排除 `com.tripo3d.unitybridge` 这一行。**
- 从 `main` 创建 `dev/auto` 分支（见第 10 节）。
- `Packages/manifest.json` 里的 `com.tripo3d.unitybridge` 指向本机下载目录（`file:C:/Users/qianc/Downloads/...`），队友拉下来后会报"包解析失败"。处理方式有两种，要先问用户选哪个：
  - 从 manifest 里移除，只在本机用；
  - 拷贝到 `A_Gift_for_Ghost/Packages/` 下，作为嵌入包。
- 分批提交：
  - 包依赖（`manifest.json`、`packages-lock.json`）；
  - `Mobile_RPAsset` 的改动，先看 diff，确认是有意改的再提交；
  - `Assets/3D_Objects/bug/`：Tripo 生成的虫子模型，已走 LFS；
  - 本文件。
- 推送 `dev/auto`。

**G1 流程骨架**（`Scripts/Core/`） 状态：**完成（10-06）**
- 文件（命名空间 `Ghost.Core`）：
  - `Stage.cs`：阶段基类（抽象 MonoBehaviour）。字段 `stageName`、`changesForm`、`form`；`virtual Enter()` / `virtual Exit()`；`protected Complete()`；`event Action<Stage> Completed`；`IsActive`；`virtual string PanelText`（阶段面板显示的字）。
  - `GameFlow.cs`：阶段状态机。字段 `Stage[] stages`、`NodeMorpher morpher`；`Next()`、`JumpTo(int index)`、`CurrentIndex`、`CurrentStage`、`FormAt(int index)`；事件 `StageEntered(Stage)`、`StageExited(Stage)`。`Start()` 进入第 0 关；当前阶段 `Complete()` 后自动 `Next()`。进入阶段时 `morpher.MorphTo(...)` 和 `stage.Enter()` 在同一帧调用（变形就是进入下一关的标志）。
  - `PlaceholderStage.cs`：占位阶段，字段 `message`，没有通关条件，靠 N 键过关。
  - `StagePanel.cs`：World Space 面板，订阅 `StageEntered`，显示 `stage.PanelText`。
  - `GameFlowDebug.cs`：读 `Debug` 动作表的 `NextStage`（N）和 `JumpStage1`–`JumpStage9`（Shift + 1–9，OneModifier 组合绑定，跳到下标 0–8）。
  - `Editor/MainSceneMenu.cs`：菜单 Ghost → Core → Build Main Scene，生成 `Assets/Scenes/Main.unity`（已存在则覆盖）。
- 阶段顺序（下标）：0 Intro、1 Tutorial、2 S1（Matrix）、3 S2（Circuit）、4 S3（Network）、5 S4（Geometric）、6 Transition（Real）、7 Pick（Real）、8 Outro（Real）。
- 和计划的出入：
  - Intro 的 `changesForm = false`，停在植株初始形态 Matrix。开场要黑屏的话，由 G5/G10 再处理。
  - 跳关时，如果目标阶段不变形，`GameFlow` 会往前找最近一个会变形的阶段，用它的形态（`FormAt`）。所以跳到任何一关，形态都是对的。
  - 文字用 UGUI 旧版 `Text` + 内置动态字体 `LegacyRuntime.ttf`，没有用 TMP：工程里没有导入 TMP Essential Resources，TMP 默认字体也没有中文。动态字体缺字时会用系统字体补字，中文能正常显示。G4 如果改用 TMP，要另外准备中文字体资产。
  - Main 场景里不放 `MorphDebugSwitcher`：它读不带修饰键的数字键 1–5，会和 Shift + 数字跳关冲突。形态只由 GameFlow 控制。
- 写新阶段：继承 `Stage`。重写 `Enter()` 时先调 `base.Enter()`；重写 `Exit()` 时最后调 `base.Exit()`。满足通关条件时调用 `Complete()`。需要改面板文字就重写 `PanelText`。
- 往主场景加阶段或换阶段：改 `MainSceneMenu.cs` 里的 `Stages` 表（名字、是否变形、形态、占位文字），再执行菜单。真实阶段做好后，把对应那一行的 `AddComponent<PlaceholderStage>` 换成新子类（可以给 `StageSpec` 加一个类型字段）。注意：菜单会覆盖整个 Main.unity，在场景里手动做的改动会丢失，要做的改动都写进菜单代码。
- 验收（10-06，通过 MCP）：Play Main.unity，按 9 次 N，从 Intro 走到 Outro，每关的形态和面板文字都正确；截图看过 S2（直角回路）、S4（几何植株）、Transition（写实彩椒）。在 Outro 再按 N 无反应。Shift + 4 跳到 S2，形态变回 Circuit。Console 里没有错误。
- 已知：编辑器在后台、没有焦点时，Play 模式不跑帧。用 MCP 自动测试时，要先在 Play 模式里设置 `Application.runInBackground = true`。这只影响自动化测试，不影响人工游玩。
- 人工验收：
  1. 打开 Unity，菜单 Ghost → Core → Build Main Scene。确认 `Assets/Scenes/Main.unity` 已打开，Hierarchy 里有 Main Camera、Plant、GameFlow（下面 9 个阶段子物体）、StagePanel。
  2. 按 Play。植株上方的面板显示"开场剧情：按 N 继续"，植株是灰色方块矩阵。
  3. 按 N：显示"新手教学：按 N 完成"，形态不变（仍是 Matrix）。再按 N 到 S1，仍是 Matrix。
  4. 再按 N 到 S2：节点飞成平面的直角回路，带连线。
  5. 再按 N 到 S3：变成散开的 3D 网络。再按 N 到 S4：变成带颜色的几何植株。
  6. 再按 N 到"过渡"：写实彩椒从下往上显现。再按 N 到"采摘"，再按 N 到"结局剧情（最后一关）"，形态都保持写实。再按 N 无反应，Console 提示"已经是最后一关"。
  7. 按 Shift + 4：跳回 S2，彩椒褪去，节点变回回路。按 Shift + 1：回到开场，变回矩阵。单按数字键（不按 Shift）不应有任何反应。
  8. 全程 Console 没有红色错误。退出 Play。

**G2 节点拾取与输入**（`Scripts/Interaction/`） 状态：**完成（10-06）**
- 文件（命名空间 `Ghost.Interaction`）：
  - `NodePicker.cs`：射线和节点包围球求交。字段 `morpher`、`radiusFactor`（0.6，半径 = 节点最大缩放分量 × 系数）、`minPickRadius`（0.012 m，小节点也能点到）、`hiddenScale`（0.002，缩放小于它的节点视为隐藏）。`int Pick(Ray ray, out float distance)`、`int Pick(Ray ray, Func<int,bool> filter, out float distance)`，没命中返回 -1；属性 `Func<int,bool> Filter`（默认筛选，null = 全部）；`Organ? OrganOf(int id)`；`bool TryGetNodeWorldPosition(int id, out Vector3 position)`（G4 放详情弹窗用）；`NodeCount`。写实模型完全显现（`RealReveal >= 1`）时不拾取。
  - `PointerInput.cs`：PC 端交互发起方，读 `Gameplay` 动作表的 `Point` / `Select`。字段 `rayCamera`、`picker`、`actions`、`dragThreshold`（8 px）、`allowNodeDrag`、`allowEmptyDrag`、`dragSampleStep`（6 px，拖得快也不漏节点）、`physicsMask`、`maxDistance`。
    - 事件：`Action<int> Tap`、`Action<int> DragStart`、`Action<int> DragOver`（每进入一个新节点发一次，不含起点）、`Action DragEnd`、`Action<Vector2> DragEmpty`（每帧像素增量）、`Action DragEmptyEnd`、`Action<int> HoverChanged`（-1 = 无，只做高亮）、`Action<IInteractable> InteractableTapped`。
    - 状态：`bool InputEnabled { get; set; }`（关掉时结束正在进行的拖拽并清悬停）、`PointerPosition`、`CurrentRay`、`HoveredNode`、`HoveredInteractable`、`HoverDistance`、`IsPressed`、`IsDraggingNode`、`IsDraggingEmpty`。
    - 规则：按下和松开在同一目标上、移动小于阈值才算点击；节点和带 `IInteractable` 的 Collider 谁近算谁（Collider 更近会挡住节点）；从按钮上拖开什么都不发；`allowNodeDrag = false` 时从节点出发的拖拽什么都不发（也不算点击）。
  - `IInteractable.cs`：`OnTap()`、`OnHoverEnter()`、`OnHoverExit()`。实现脚本挂在 Collider 所在物体或它的父物体上。
  - `InteractableButton.cs`：通用按钮，`UnityEvent onTap / onHoverEnter / onHoverExit`、`bool interactable`。World Space Canvas 上的按钮要另加贴合大小的 `BoxCollider`（G4 的 Yes 按钮用它）。
  - `PointerCursor.cs`：光标表现，独立组件。默认只显示系统光标；可选 `marker`（不带 Collider 的小物体）跟随命中点。VR 版整个替换。
  - `PointerDebugLogger.cs`：调试日志 `[Pointer] Tap node 35(Fruit)`、`DragStart / DragOver / DragEnd path: 2(Root) > 13(Stem) > …`、`DragEmptyEnd total (x, y) over N frames`。`logEvents` 总开关，`logEveryEmptyDelta`、`logHover` 默认关。**G6 起在 `MainSceneMenu.BuildPointer` 里把它关掉（`logger.logEvents = false` 或不加这个组件）。**
- 输入：`InputSystem_Actions.inputactions` 新增 `Gameplay` 动作表：`Point`（PassThrough / Vector2，`<Mouse>/position`、`<Pen>/position`）、`Select`（Button，`<Mouse>/leftButton`、`<Pen>/tip`），分组 Keyboard&Mouse。VR 时给这两个 Action 加 XR 绑定，或者直接换掉 `PointerInput`。
- **本次没有提交 `Main.unity`**：提交时工作区的 Main.unity 已经被 G5 重新生成，引用了还没提交的 Narrative 脚本。拉下代码后执行一次菜单 Ghost → Core → Build Main Scene 就会带上 Pointer。
- 主场景：`MainSceneMenu.BuildPointer()` 新建 `Pointer` 物体，挂 NodePicker、PointerInput、PointerCursor、PointerDebugLogger。阶段脚本用 `Object.FindFirstObjectByType<PointerInput>()` 或在菜单里给阶段填引用。
- 阶段怎么用：
  ```csharp
  public override void Enter() { base.Enter(); pointer.Tap += OnTap; pointer.DragOver += OnDragOver; pointer.allowNodeDrag = true; }
  public override void Exit()  { pointer.Tap -= OnTap; pointer.DragOver -= OnDragOver; base.Exit(); }
  ```
  一定在 `Exit()` 里退订，否则下一关还会收到事件。拖拽开始节点在 `DragStart` 里，`DragOver` 只报后续节点。变形期间（`morpher.IsMorphing`）要不要响应由阶段自己判断，PointerInput 不管。
- 筛选：`picker.Filter = id => picker.OrganOf(id) == Organ.Bug;`（离开阶段时设回 null），或者调用 `picker.Pick(ray, filter, out d)`。被筛掉的节点不挡射线，后面的节点能点到。
- 和计划的出入：
  - 没复用 `UI/Point`、`UI/Click`，单独建了 `Gameplay` 表，免得和 UI 模块冲突，以后加 XR 绑定也清楚。`Point` 必须是 PassThrough：做成 Value 时，鼠标不动的那几帧 Action 不更新，第一版空白拖拽因此没触发。
  - 隐藏判定只看 `CurrentPoses` 的缩放（G3 未提交，不依赖它的 API）。**G3 提交后**：拾取应改用 `morpher.GetNodeVisibility(id)`，小于 0.5 的跳过（隐藏动画不改 `CurrentPoses`，现在被 `Hide` 的节点仍能点到）。在那之前可以用 `picker.Filter = id => !morpher.IsHidden(id)` 临时处理。
  - MCP 的 `simulate_mouse_click` / `simulate_mouse_drag` 只走 UI 和 Physics，不进 Input System，测不了 Action。验收改为在 Play 模式下用 `execute_code` 加一个临时 `Mouse` 设备，逐帧 `InputSystem.QueueStateEvent` 注入位置和按键，跑完移除设备。
- 验收（10-06，Main.unity，`JumpTo(3)` 到 S2 Circuit，191 个节点）：点 23、35、188、158、2 五个节点，Console 依次打印 `Tap node 23(Leaf)`、`35(Fruit)`、`188(Bug)`、`158(Soil)`、`2(Root)`；从根 2 拖到果实 35，打印 `DragEnd path: 2(Root) > 13(Stem) > 156(Fruit) > 22(Stem) > 25(Stem) > 30(Stem) > 26(Leaf) > 52(Stem) > 35(Fruit)`；空白处 (60,60) 拖到 (200,90)，打印 `DragEmptyEnd total (140.00, 30.00) over 10 frames`；临时放一个带 `InteractableButton` 的立方体，点它触发 `onTap` 和 `Tap interactable`。Console 没有错误。没做截图检查。
- 人工验收：
  1. 菜单 Ghost → Core → Build Main Scene。Hierarchy 里应多出 `Pointer`（NodePicker、PointerInput、PointerCursor、PointerDebugLogger），Inspector 里 PointerInput 的 Ray Camera、Picker、Actions 已填好。
  2. Play，按 Shift + 4 跳到 S2（回路）。鼠标单击任意节点，Console 打印 `[Pointer] Tap node <id>(<部位>)`；点在叶、果、根上部位要对。点空白处无输出。
  3. 从根附近的节点按住拖过几个节点再松开：先打印 `DragStart`，经过每个节点一条 `DragOver`，松开打印 `DragEnd path: …`，顺序就是划过的顺序。
  4. 在空白处按住拖动再松开：打印 `DragEmptyEnd total (…) over N frames`，方向和拖动方向一致（向右 x 为正，向上 y 为正）。
  5. 按下节点后稍微抖动（小于 8 像素）再松开，仍算点击。
  6. 取消 PointerDebugLogger 的 Log Events，再点节点不再打印。全程 Console 没有红色错误。

原计划：
- 节点不是 GameObject，不能用 Physics 射线。要写 `NodePicker`：用鼠标射线和 `NodeMorpher.CurrentPoses` 里每个节点的包围球求交，返回最近的那个节点 id。被隐藏或缩没的节点要跳过。
- 写 `PointerInput`：读取 Point 和 Click 两个 Action，对外发出这几个事件：
  - `Tap(nodeId)`：点击节点。
  - `DragStart(nodeId)`、`DragOver(nodeId)`、`DragEnd()`：从节点出发的拖拽。
  - `DragEmpty(delta)`：在空白处拖拽，S3 和 S4 用它旋转物体。
  - 点击和拖拽靠移动距离阈值区分。
- 普通物体（Yes 按钮这类）用 `IInteractable` 加 Physics 射线。
- 光标或准星做成独立组件，以后换 VR 射线时直接替换。
- 验收：测试场景里点节点，打印出 id 和部位；从节点拖拽时，打印出经过的节点序列。

**G3 节点状态与表现**（`Scripts/Morph/` 扩展、`Scripts/Gameplay/`） 状态：未开始
- 给 `NodeMorpher` 加逐节点的覆盖接口，至少包括：
  - 闪烁：在原色和白色之间切换，频率可调。
  - 常亮高亮。
  - 恢复色。
  - 隐藏：带缩小动画。
  - 整体 `Realness`（0–1），供 S4 逐档变写实。
- 给 `NodeLinkRenderer` 加逐条连线的颜色覆盖，S2 用它把划过的线染成蓝色。
- 写 `NodeIssueSystem`：
  - 每个问题记录：节点 id、难度（Easy / Medium / Hard）、还需点击次数、失败后重新闪烁的延迟、是否已解决、已尝试次数。
  - 生成方式两种：按规则随机生成，或者在指定节点上生成。
  - 对外发出 `IssueAttempted`、`IssueResolved` 两个事件，供阶段判定通关。
- 验收：测试场景里挂上三种难度的问题，点击后的表现和第 4 节"通用机制"一致。

**G4 Agent 界面**（`Scripts/Agent/`，World Space Canvas） 状态：**完成（10-06）**
- 文件（命名空间 `Ghost.Agent`）：
  - `AgentTaskPanel.cs`：任务列表 + 指标面板。另含 `enum TaskState { Pending, Running, Done, Failed }`、`struct AgentTask { string label; TaskState state; }`（构造 `new AgentTask(label, state)`）。
  - `NodeDetailPopup.cs`：节点详情弹窗，带引线和锚点方块指回节点。
  - `AgentQueryDialog.cs`：AI 询问框，一句提问 + Yes 按钮（`InteractableButton` + 贴合大小的 `BoxCollider`，走 G2 的 PointerInput）。
  - `AgentUIBuilder.cs`：运行时 / 编辑器都能调用的构建器，生成三个界面的完整层级并填好引用。
  - `AgentUIStyle.cs`：配色常量和 UGUI 小工具（内置 `LegacyRuntime.ttf`，同 StagePanel）。
  - `AgentUITestDriver.cs`：只用于测试场景的假数据驱动。
  - `Editor/AgentUITestMenu.cs`：菜单 Ghost → Agent → Build Agent UI Test Scene，生成 `Assets/Scenes/G4Test.unity`（Additive 新建、保存后关闭，不打断当前场景；**当前场景是未保存的 Untitled 时会失败**，先打开任意已保存场景）。
- 接口：
  - `AgentTaskPanel`：`SetTitle(string)`、`SetModeLabel(string)`（"FULL PROXY" / "ASSISTIVE"，空串隐藏）、`SetTasks(IList<AgentTask>)`、`int AddTask(string, TaskState = Pending)`、`UpdateTask(int index, TaskState)`、`UpdateTask(int index, string label, TaskState)`、`GetTask(int)`、`TaskCount`、`ClearTasks()`；指标 `SetMetric(string key, float value01)`（进度条 + 百分比）、`SetMetric(string key, float value01, string display)`（进度条 + 自定义文字如 "3 / 7"）、`SetMetric(string key, string value)`（纯文字）、`RemoveMetric(key)`、`ClearMetrics()`；`Show()` / `Hide()`；事件 `Action<int, TaskState> TaskChanged`。指标按 key 首次出现的顺序排列，面板高度随内容变化（顶边不动）。
  - `NodeDetailPopup`：`Show(Vector3 worldPos, string title, string body)`、`bool ShowAtNode(NodePicker picker, int nodeId, string title, string body)`（变形中跟随节点）、`SetText(title, body)`、`Hide()`、`Visible`、`NodeId`。字段 `sideOffset`（0.1 m）、`towardCamera`（0.35 m，往相机拉近，字更大也不和节点穿插）、`autoFlip`（节点在画面右半边时弹窗翻到左侧，一次显示内不来回翻）、`yawOnly`、`autoHideSeconds`（0 = 不自动隐藏）。没有 Collider，不挡射线。
  - `AgentQueryDialog`：`Ask(string question, Action onYes)`（再次调用替换提问和回调）、`Hide()`、`ConfirmYes()`（代码确认，以后键盘 / 手柄确认可以接这里）、`Visible`、`Question`、事件 `Action<string> Answered`。点 Yes 后先隐藏，再调 `onYes`，再发 `Answered`。`inputDelay`（0.3 s）内不接受点击，防止上一关的点击误触。悬停时按钮变蓝灰（只是反馈，信息不依赖悬停）。
  - `AgentUIBuilder.BuildAll(Transform parent, Camera camera)` 返回 `AgentUI { root, taskPanel, detailPopup, query }`；也可单独 `BuildTaskPanel(parent, cam, pos)`、`BuildDetailPopup(parent, cam)`、`BuildQueryDialog(parent, cam, pos)`。默认位置按 Main 的固定相机（(0, 1.6, -1.9)）设计：任务面板在植株左侧（顶边 (-0.84, 1.46, -0.6)，离相机约 1.5 m），询问框在植株右侧（(0.82, 1.26, -0.6)，在植株前面，Yes 的 Collider 比节点近）。叠放顺序：任务面板 < 弹窗（sortingOrder 10）< 询问框（20）。
- G6 接入 Main（加到 `MainSceneMenu.BuildMainScene` 里 `BuildPointer(...)` 之后，需要 `using Ghost.Agent;`）：
  ```csharp
  var agentUI = AgentUIBuilder.BuildAll(null, camera);
  // 给阶段填引用：stage.taskPanel = agentUI.taskPanel; stage.detailPopup = agentUI.detailPopup; stage.query = agentUI.query;
  ```
- 阶段怎么用（S1 示例）：
  ```csharp
  public override void Enter()
  {
      base.Enter();
      taskPanel.SetTitle("S1 · 植株维护");
      taskPanel.SetModeLabel("FULL PROXY");
      taskPanel.SetTasks(new[] { new AgentTask("叶片异常 ×6", TaskState.Running), new AgentTask("果实严重异常 ×4", TaskState.Pending) });
      taskPanel.SetMetric("完成率", 0f);
      taskPanel.SetMetric("置信度", 0.4f);
      pointer.Tap += OnTap;
  }
  void OnTap(int id)
  {
      var organ = picker.OrganOf(id) ?? Organ.Stem;
      string body = detailTable.Get(id, organ, DetailDepth.Status); // G5 详情表（占位符替换按 G5 说明）
      detailPopup.ShowAtNode(picker, id, $"节点 #{id}", body);
  }
  void OnConditionsMet()
  {
      query.Ask("是否要进一步查看信息？", () => Complete());
  }
  public override void Exit() { pointer.Tap -= OnTap; detailPopup.Hide(); query.Hide(); base.Exit(); }
  ```
  弹窗和询问框跨关共用，离开阶段时记得 `Hide()`。结局切到 Assistive 时 `taskPanel.SetModeLabel("ASSISTIVE")`。
- 风格：浅色半透明底（0.96 灰白）+ 2 px 黑灰细边框；字黑 / 灰，强调色蓝灰 (0.33, 0.43, 0.55)，失败用暗红；任务前的方块标记：空心灰 = 未开始、实心蓝灰（缓慢呼吸，`pulseRunning`）= 进行中、实心黑 = 完成、空心暗红 = 失败；右侧大写状态词（PENDING / RUNNING / DONE / FAILED），状态不只靠颜色区分。标题区 "AGENT · TASKS" / "AGENT · QUERY" 配蓝灰小方块，模式标签是黑底反白小块。字号：任务 38 px、指标 32 px、弹窗标题 42 / 正文 38、提问 38、YES 40（1000 px = 1 m）。Canvas 和相机画面平行（不是朝向相机位置），平面屏幕上不出现梯形变形。
- 和计划的出入：
  - 详情弹窗的文本由阶段脚本传入（从 G5 的 `NodeDetailTable` 取），弹窗本身不引用 G5。
  - 构建器放在运行时程序集（`Ghost.Agent.AgentUIBuilder`），编辑器菜单和以后运行时生成都能用。
- 验收（10-06，通过 MCP，G4Test.unity Play）：截图（960×540）里任务面板（标题、4 条任务含四种状态、3 个指标条）、果实节点 35 旁的详情弹窗（带引线）、询问框都清楚可读，互不遮挡，植株可见。用临时虚拟 `Mouse` 设备点 Yes 按钮中心（屏幕 (813, 263)）：Console 打印 `[Pointer] Tap interactable YesButton` 和 `[G4] Yes`，询问框隐藏，回调把任务 4 改成 RUNNING。点节点 10：打印 `[Pointer] Tap node 10(Root)`，弹窗移到节点 10 旁并显示 `节点 #10 · Root`。Console 没有错误。
- 没做：VR 下的摆放（以后按 XR Origin 头部高度重新调默认位置，或把面板挂到跟随玩家的锚点）；询问框的键盘 / 手柄确认（已留 `ConfirmYes()`）。
- 人工验收：
  1. 打开任意已保存的场景（比如 Main），菜单 Ghost → Agent → Build Agent UI Test Scene，Console 打印 `[G4] 生成测试场景`。打开 `Assets/Scenes/G4Test.unity`。
  2. Play。左侧是任务面板：标题"S1 · 植株维护"、右上"FULL PROXY"、4 条任务（DONE / RUNNING / FAILED / PENDING，方块标记各不相同，RUNNING 的方块缓慢明暗变化）、完成率 62%、置信度 38%、处理进度 7 / 10。站在 2 m 外看屏幕，文字要能读清。
  3. 植株中部的果实节点旁有详情弹窗，细线指向节点。右侧是询问框"已达到当前权限下的处理上限。是否要进一步查看信息？"和 YES 按钮。
  4. 鼠标移到 YES 上按钮变蓝灰，移开恢复。点 YES：Console 打印 `[G4] Yes`，询问框消失，任务"生成报告"变成 RUNNING。
  5. 点任意节点：弹窗移到这个节点旁边，标题显示节点 id 和部位。点画面右半边的节点，弹窗出现在节点左侧。
  6. 选中 AgentUITestDriver，Inspector 右键：Ask Query（询问框重新出现）、Advance Tasks（茎部异常变 DONE、完成率 85%）、Switch To Assistive（标签变"ASSISTIVE"）。全程 Console 没有红色错误。

**G5 对话与语音**（`Scripts/Narrative/`） 状态：**完成（10-06）**
- 文件（命名空间 `Ghost.Narrative`；编辑器代码在 `Ghost.Narrative.EditorTools`）：
  - `DialogueLine.cs`：`enum Speaker { EmotionalFemale, MechanicalFemale, Protagonist, Agent }`；`[Serializable] class DialogueLine`：`speaker`、`text`、`AudioClip clip`、`float durationOverride`（> 0 时直接用）、`float pauseAfter`（< 0 用播放器默认间隔）。和计划的出入：台词不是单独的 ScriptableObject，而是 `DialogueSequence` 里的列表项，策划在一个资产里按顺序填整段。
  - `DialogueSequence.cs`：ScriptableObject（Create → Ghost → Dialogue Sequence），`startDelay`、`List<DialogueLine> lines`。
  - `DialoguePlayer.cs`：MonoBehaviour。
    - `void Play(DialogueSequence seq, Action onComplete = null)`：打断当前播放、清空队列，立刻播放（被打断的那段不回调）。
    - `void Enqueue(DialogueSequence seq, Action onComplete = null)`：排队，空闲时立刻开始。
    - `void Skip()`：跳过当前这句（最后一句被跳过时整段正常结束并回调）。`void Stop()`：停止并清空，不回调、不发 `SequenceFinished`。
    - `bool IsPlaying`、`DialogueSequence CurrentSequence`、`DialogueLine CurrentLine`、`float DurationOf(DialogueLine)`。
    - 事件：`LineStarted(DialogueLine)`、`LineFinished(DialogueLine)`、`SequenceFinished(DialogueSequence)`、`Stopped`。
    - 时长：`durationOverride` > 音频长度 > 字数 / `charsPerSecond`（默认 5 字/秒，下限 `minDuration` 1.5 s）。句间默认间隔 `defaultPause` 0.4 s。音频走 2D `AudioSource`（脑机接口直接听到，不做空间化）。回调里可以安全地再调 Play / Stop。
  - `SubtitlePanel.cs`：World Space 字幕，订阅 DialoguePlayer。说话人名字 + 台词，颜色按说话人（`styles` 数组可在 Inspector 改）：有情感的女声"她"暖橙、机械女声"系统"冷灰蓝、主角"我"白、Agent 青绿。带简单打字机效果（`typewriterCharsPerSecond` 30，设 0 关闭）。没有台词时隐藏。
  - `ScreenBlackout.cs`：相机前 1.4 m 的 World Space 黑板（CanvasGroup），`SetImmediate(bool black)`、`FadeTo(bool black, float fade = -1)`、`IsBlack`。只遮挡，不改相机。结局等也可以复用。
  - `IntroStage.cs`：`Stage` 子类，字段 `player`、`sequence`、`blackout`、`fadeOutTime`。`Enter()` 立刻黑屏并 `player.Play(sequence, Complete)`；`Exit()` 停止对白、黑屏 1.2 s 淡出。`PanelText` 为空。
  - `NodeDetailTable.cs`：ScriptableObject（Create → Ghost → Node Detail Table）。`enum DetailDepth { Status, Project, Physical }`（S1 / S2 / S3）。
    - `string Get(Organ organ, DetailDepth depth)`、`string Get(int nodeId, Organ organ, DetailDepth depth)`（先查 `overrides` 里的节点专属文字）。
    - `string Format(int nodeId, Organ organ, DetailDepth depth, params (string key, object value)[] values)`：取文字并替换 `{key}` 占位符；`static string Fill(template, values)`。
    - `static DetailDepth DepthForStage(string stageName)`：Tutorial / S1 → Status，S2 → Project，其余 → Physical。
  - `Editor/NarrativeAssets.cs`：菜单 Ghost → Narrative → Create Default Assets；`EnsureIntroSequence()`、`EnsureNodeDetails()`。资产已存在时不覆盖（策划改过的内容不会丢；要恢复默认就删掉资产再执行）。
  - `Editor/NarrativeSceneBuilder.cs`：`AddIntroStage(stageGo, cameraTransform)`、`EnsureDialogue(cameraTransform)`（全场景只建一套 Dialogue + SubtitlePanel）。
- 资产：`Assets/Data/Narrative/Intro.asset`（开场三句，暂无配音，按字数估算，约 2 + 7 + 17 s，开头先黑 1.5 s）、`Assets/Data/Narrative/NodeDetails.asset`。
- 主场景：`MainSceneMenu.cs` 里 Intro 一行改为 `NarrativeSceneBuilder.AddIntroStage(...)`，其余阶段仍是占位。场景里新增 `Dialogue`（DialoguePlayer + AudioSource）、`SubtitlePanel`（相机前 1.2 m、视线下方 0.3 m，1.4 m × 0.4 m，正文 52 号字）、`Main Camera/Blackout`。
- G6 / G10 怎么用：
  - 播对白：在 `MainSceneMenu` 里用 `NarrativeSceneBuilder.EnsureDialogue(cameraGo.transform)` 拿到播放器，赋给阶段的字段；阶段里 `player.Play(seq, () => ...)`。AI 询问前的提示、过渡独白都建成新的 `DialogueSequence` 资产（建议放 `Assets/Data/Narrative/`），可以照 `NarrativeAssets` 写 Ensure 方法。阶段 `Exit()` 里记得 `player.Stop()`。
  - 查详情：`table.Format(node.id, node.organ, NodeDetailTable.DepthForStage(stageName), ("status", "异常"), ("progress", 42), ("confidence", 63))`，结果交给 G4 的详情弹窗。S1 模板占位符：`{id}`、`{status}`、`{progress}`、`{confidence}`。
- 给策划（C）：`NodeDetails.asset` 里 7 个部位 × 3 档全部是**占位文字**，以"[占位]"开头，直接在 Inspector 里改；个别节点要专属文字就加到 `overrides`（节点 id + 深度 + 文字）。开场台词在 `Intro.asset`，有配音后把 AudioClip 拖进对应台词的 `clip`，时长自动按音频走。字幕说话人显示名（她 / 系统 / 我 / Agent）也是暂定，在场景 `SubtitlePanel` 的 `styles` 里改（注意重新生成场景会恢复默认，长期要改的话改 `SubtitlePanel.cs` 里的默认值）。
- 验收（10-06，通过 MCP）：生成 Main 场景后 Play，开场全黑，三句字幕依次出现，暖橙 / 冷灰蓝 / 暖橙；长句 4 行完整显示在底板内；播完自动进入 Tutorial，黑屏淡出，植株和"新手教学"面板恢复。回到 Intro 播放中按 N：立刻进入 Tutorial，对白停止（`IsPlaying` = false）、黑屏透明度归 0、字幕隐藏。Console 没有错误。没有配音资源，音频播放路径未实测。
- 人工验收：
  1. 菜单 Ghost → Core → Build Main Scene，Hierarchy 里有 `Dialogue`、`SubtitlePanel`、`Main Camera/Blackout`，`GameFlow/Intro` 上是 IntroStage。
  2. Play：画面全黑，约 1.5 s 后出现"她：嗨，你醒了。"（暖橙色）。
  3. 接着"系统：神经连接已恢复……"（冷灰蓝），再是"她"的长句（暖橙，约 4 行，不溢出底板）。字在当前距离下清晰可读。
  4. 长句结束后自动进入新手教学：黑屏约 1 s 淡出，看到灰色方块矩阵和"新手教学：按 N 完成"。
  5. 重新 Play，在对白中途按 N：立刻进入教学，字幕消失，黑屏淡出，之后不会再冒出对白。
  6. 按 Shift + 1 回到开场：重新黑屏，从第一句开始播放。
  7. 打开 `Assets/Data/Narrative/NodeDetails.asset`，7 个部位 × 3 档文字都在，都以"[占位]"开头。
  8. 全程 Console 没有红色错误。

**G6 教学 + S1**（`Scripts/Stages/`） 状态：未开始，依赖 G1–G5
- 教学：用少量节点逐个演示简单、中等、困难三种问题，并配上提示文字。
- S1：随机生成简单问题和中等问题，困难问题固定在 4 个果实节点上；详情只显示任务状态、进度和置信度。按第 4 节的条件判定通关，然后弹出 AI 询问，玩家选 Yes 后进入 S2。

**G7 S2 连线修复** 状态：未开始
- 设置缺水问题：土壤、叶片和果实节点都有问题。
- 拖拽路径校验：必须从根节点出发，每一步只能走到相邻节点（父节点或子节点）。
- 经过的节点恢复，果实除外；划过的连线染成蓝色。
- 点击节点显示项目语言的描述，记下"已查看果实"。
- 通关条件满足后弹出 AI 询问。

**G8 S3 找虫** 状态：未开始
- 写 `TargetRotator`：在空白处拖拽时，旋转 NodeMorpher 的根物体，只绕竖直轴并限制俯仰角，带惯性但不要过快。S4 复用这个组件。
- 节点按部位显示颜色。被点过的虫子节点保持高亮，点击其他节点显示物体描述。
- 所有虫子都点过之后，弹出 AI 询问。

**G9 S4 除虫变写实** 状态：未开始
- 进入时变形到 Geometric，用 `TargetRotator` 旋转盆栽。
- 点击叶背的虫子节点将它摘除：先播放缩小动画，然后隐藏。
- 每摘除一只，`Realness` 提升一档。所有虫子摘完即通关。

**G10 收尾流程** 状态：未开始
- 过渡：播放独白，然后调用 `MorphTo(Real)` 交给写实模型。
- 采摘：用 Timeline 做动画，手部模型伸进画面摘下一颗果实。果实在模型里要能单独拆成子物体。
- 结局剧情：按剧情和美术资产表来做。

**G11 音频与打磨** 状态：未开始
- 环境音乐、点击和解决音效、闪烁提示音。
- 每个新动作第一次出现时给出提示。
- 打包 Windows 版本，换一台电脑测试。

### 分工建议

- 程序：G0 → G1 → G2 → G6 → G7。
- 技术美术：G3 → G8 → G9 → G10（表现部分）。
- 策划兼内容：G4 的界面视觉、G5 的全部文本和配音、G11。

## 10. 长时间自动开发的 Git 与备份规则

用户已明确授权：AI 助手执行第 9 节的模块时，可以按以下规则自行提交和推送，**不必每次询问**。这条授权覆盖 AGENTS.md 第 8 节"只在用户明确要求时提交"的约定。

### 分支

- 自动开发统一在 `dev/auto` 分支上进行，不直接提交到 `main`。
- 每段新对话开始时，先确认当前分支：执行 `git switch dev/auto`。分支不存在时，从 `main` 创建：`git switch -c dev/auto`。
- 合并回 `main` 由用户决定。G1、G6、G9 完成时提醒用户合并。

### 什么时候提交

- **每完成一个模块 G，都要提交并推送**：`git push -u origin dev/auto`。
- 模块进行中，遇到以下情况也要提交：
  - 一个可编译、可运行的子步骤完成了；
  - 即将做大范围重构，或者要修改场景、Prefab；
  - 连续工作超过约 1 小时。
- 提交前确认 Unity 编译没有错误（通过 MCP 读取编译错误）。编译不过的代码不提交；如果必须保存现场，在提交信息里标明 `WIP`。

### 提交信息格式

```
G<编号>: <做了什么>

<可选：关键文件、已知问题>
```

例：`G2: Add NodePicker ray-sphere picking and PointerInput events`。进行中的提交写 `G2 WIP: ...`。

### 提交什么、不提交什么

- 只 `git add` 本次改动的具体路径，不用 `git add .` 或 `git add -A`。
- 资源必须和它的 `.meta` 文件一起提交（见 AGENTS.md 第 8 节）。新增二进制格式时，先在 `.gitattributes` 里加 LFS 规则。
- 以下内容不提交：
  - `ProjectVersion.txt` 的改动；
  - 个人文件夹（如 `美术参考-Xie/`）；
  - 指向本机路径的包依赖（`"file:C:/Users/..."`）；
  - 打包产物；
  - 密钥（Tripo API key 只放在环境变量里）。
- `Packages/manifest.json` 和 `ProjectSettings/` 的改动单独提交，并在提交信息里说明原因。

### 禁止的操作

不用 `--force`、`reset --hard`、`clean -f`、`branch -D`，不改写已推送的历史。出了问题，用 `git revert` 回退，或者新建一个提交来修复。

### Unity MCP 使用规则

- 连接方式：Funplay MCP 是本机的 HTTP 服务，地址 `http://127.0.0.1:23558/`，只监听 127.0.0.1。会话里没有加载 MCP 原生工具时，用 `curl` 发 JSON-RPC 请求：`tools/call`，参数是 `{"name":..., "arguments":...}`。
- 实测延迟（10-06）：普通读取 30–150 ms，`capture_game_view` 存成文件约 0.3 s。共有 40 个工具。
- **以代码为主，MCP 只用来看效果。**脚本、Shader、数据生成和场景搭建都写成代码，场景搭建做成编辑器菜单脚本（参考 Ghost → Morph → Build Prototype Scene），再通过 MCP 的 `execute_menu_item` 执行。
- 编辑器开着的时候，不要用文本方式手改 `.unity`、`.prefab`、`.asset`，编辑器会用内存里的版本把改动覆盖掉。
- MCP 只在这几种场合使用：
  - 改完代码后，调 `prepare_editor`（target=edit）触发编译，再调 `get_compilation_errors` 查错。
  - 运行一次菜单脚本。
  - 模块验收时进 Play 模式，截一两张图看效果，必要时模拟几次点击。
- 不轮询，不连续截图。截图一律用 `save_to_file` 存成文件，用小分辨率（如 480×270），确实需要看画面时再读取图片。
- **超时规则**：MCP 连续超过 3 分钟没有响应，就跳过 Play 验收，只要求编译通过。编译结果也读不到时，就读 `%LOCALAPPDATA%\Unity\Editor\Editor.log` 里的编译错误。跳过的验收项要写进第 9 节对应模块的状态里，等用户回来手动补验。

### 模块完成时的收尾清单

1. 确认 Unity 编译没有错误，并按模块的验收标准实际运行一遍。
2. 更新本文件第 9 节中这个模块的状态：写明实际的类名、文件名和接口；和计划有出入的地方也写进去。
3. 提交代码和文档，推送 `dev/auto`。
4. 在对话里报告：做了什么、验收结果、已知问题、下一个模块是什么。
