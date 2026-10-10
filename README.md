<div align="center">

# A Gift for Ghost

**Agent 替你活了太久。一株彩椒，是你回到真实世界的钥匙。**

*An AI agent has been living your life for you. A single pepper plant is your key back to reality.*

![Unity](https://img.shields.io/badge/Unity-6000.3.25f1-black?logo=unity)
![URP](https://img.shields.io/badge/Render-URP-blue)
![Platform](https://img.shields.io/badge/Platform-PC%20%E2%86%92%20Meta%20Quest%202-6e40c9)
![Made in 48h](https://img.shields.io/badge/Tripothon%202026-48h-orange)

<img src="A_Gift_for_Ghost/Assets/Art/UI/StartScreen/start_bg.png" alt="A Gift for Ghost 开始界面" width="820">

<!-- TODO：在这里放一段 5–10 秒的玩法 GIF（推荐：S1 方块矩阵 → S4 几何植株 → 写实彩椒的连续变形） -->

<!-- TODO：上传后把下面两项换成链接，例如 [▶ 演示视频](https://...) · [⬇ 下载 Windows 版](https://github.com/QIANbird/Tripothon2026/releases) -->

▶ 演示视频（待上传） · ⬇ Windows 版（待发布） · [🛠 从源码运行](#-从源码运行)

</div>

---

## 这是一个什么游戏

> *"也许你没有意识到，你生活在这片虚无的黑暗中已经很久了。"*

你是 **Ghost**。长久以来，你让 Agent 替你处理和现实世界的一切交互，直到感官与神经机能严重退化，患上了正在大流行的**"认知贫化症"**：大脑失去了重建真实世界的能力。

你在一片黑暗里醒来。治疗中心的人正通过脑机接口和你说话，而你身上的 Agent 仍然运行在**全能代理模式**。你能看到的世界，只是它报表里的任务、进度和置信度。

你面前只有一堆灰色的方块。Agent 叫它"核心项目"，它其实是一株彩椒，是治疗中心为你植入的**"后门"**：其中有一颗果实，是退出代理模式的**"确认钥匙"**。整局游戏，这组方块会**一层一层褪去抽象**：方块矩阵 → 直角回路 → 3D 网络 → 几何植株 → 写实彩椒。最后，你亲手把那颗果实摘下来，送到嘴边。

> *"现在，请品尝真实。"*

**设计立场：** 这不是一个反 AI 的故事。玩家不是去摧毁 Agent，而是一步步校准对它的信任：可逆、低风险的事可以交给它；模糊、不可逆、涉及判断的事，要自己看证据，或者自己动手。

## 玩法：五种形态，一步步拿回主体性

全游戏只有**同一组节点**，它始终代表那株彩椒。每进一关，它就变形一次，你和它之间的"代理层"也就少一层。

| 段落 | 植株形态 | 你做什么 | 你在拿回什么 |
|---|---|---|---|
| 教学 · S1 | 方块矩阵 | 点击闪烁的方块，授权 Agent 处理问题 | **批准输出**：你只能看到进度和置信度 |
| S2 | 直角回路 | 沿着脉冲从根部拖出一条通路，修复缺水 | **检查输入**：问题第一次有了"原因" |
| S3 | 3D 网络 | 旋转结构，靠颜色认出藏起来的虫子 | **审核推理 · 接管决策**：你开始看见物体本身。Agent 警告"我的执行硬件精度可能过低"，问你"是否切换为人工操作？" |
| S4 | 几何植株 | 旋转植株，翻到叶背摘掉虫子；每摘一只，植株更真实一档 | **验证结果**：你亲手做的每一步，都让真实多显现一分 |
| Pick | 写实彩椒 | 第一人称走过去、蹲下、摘下果实，送到嘴边一口一口吃掉 | **亲自行动**："义体控制权限已被用户回收。" |

整条流程约 10 分钟，线性推进，没有失败状态。

## 亮点

- **一组节点讲完整个故事。** 191 个节点在 5 种布局之间平滑插值，每个形态都是同一株彩椒的不同"编码"。不切场景，变形本身就是进入下一关的信号。
- **Tripo 模型是一切的源头。** 写实彩椒由 Tripo3D 生成；我们在它的表面采样出全部节点，再倒推出几何植株、网络、回路和矩阵。玩家一路看到的每一种"抽象"，都来自同一个 AI 生成的模型。
- **抽象度递减就是叙事。** 节点详情会随关卡越写越具体：从"任务状态 / 置信度"，到"支撑系统 / 边缘系统"这样的项目语言，再到真实的颜色、形状、质感。
- **两种声音，两个位置。** 治疗中心"有温度的声音"出现在屏幕底部的字幕里；"没有温度的" Agent 只出现在左侧的聊天气泡和 Caution 警告卡里。玩家一眼就能分清谁在说话。
- **从点击到身体动作。** 前半程你在远处点击、授权；最后一段，玩家第一次真正拥有身体：走、蹲、伸手、吃。
- **写实交接。** 进入写实形态时，节点从下往上缩小消失，真实模型按方块噪声溶解显现，"抽象"和"真实"在同一个画面里交接。

## 操作

| 操作 | 键鼠 |
|---|---|
| 授权 / 选择节点 | 鼠标左键 |
| 查看节点详情 | 按住鼠标右键 |
| 拖出通路（S2）· 旋转植株（S3 / S4） | 按住左键拖动 |
| 推进对白 | 左键点空白处 / 空格 / 回车 |
| 移动 · 转头（Pick） | WASD · 鼠标 |
| 蹲下（Pick） | C / 左 Ctrl |
| 摘果实 · 吃（Pick） | 左键对准果实 · E |

<details>
<summary>调试快捷键（评审和录屏时用）</summary>

| 按键 | 作用 |
|---|---|
| N | 进入下一关（在开始界面等于点"开始"） |
| Shift + 1–9 | 直接跳到第 1–9 关，两个方向都能干净进出 |

</details>

## 🛠 从源码运行

**环境要求：** Unity **6000.3.25f1**（URP）· [Git LFS](https://git-lfs.com)（模型、贴图、音频和视频都放在 LFS 里）

```bash
git lfs install
git clone https://github.com/QIANbird/Tripothon2026.git
```

1. 用 Unity Hub 打开 `A_Gift_for_Ghost/` 文件夹，版本选 6000.3.25f1。
2. 菜单 **Ghost → Core → Build Main Scene**。主场景 `Assets/Scenes/Main.unity` 由代码生成，这一步保证场景和代码一致。
3. 打开 `Main.unity`，按 Play，点开始界面上的 **AWAKE**。

## 技术实现

<details open>
<summary><b>节点变形系统</b></summary>

- 节点不是 GameObject。所有节点用 `Graphics.RenderMeshInstanced` 一次画完（GPU instancing），颜色、高亮、闪烁、脉冲都是逐实例属性，为 Quest 2 的性能预算留出余量。
- 布局**倒着生成**：先在 Tripo 生成的写实彩椒模型表面采样锚点，用最小生成树从茎底部长出拓扑；再依次推出几何植株、3D 网络、直角回路（吸附网格）和打乱的方块矩阵。
- 变形按节点深度错峰：往写实方向从根部往外长，往抽象方向从枝梢先散开。

</details>

<details>
<summary><b>流程与关卡</b></summary>

- `GameFlow` 按顺序运行 `Stage`。每一关自己判断通关，结束时调用 `Complete()`；进关的同一帧开始变形。
- 每关在 `Enter` 时订阅事件、`Exit` 时退订并重置共享状态，所以调试跳关在任意方向都可逆。
- 台词表（CSV）、节点详情文本、配音、音效都由编辑器工具自动导入成 ScriptableObject，策划改表不需要改代码。

</details>

<details>
<summary><b>为 VR 而设计</b></summary>

- 游戏逻辑只读 Input Action，不读键盘鼠标；交互发起方（PC 鼠标射线）和被交互物分离，迁移到 VR 时换成手柄射线即可。
- 除 Pick 段外相机固定不动，构图靠移动、缩放植株完成，降低 VR 中的晕动风险。
- Pick 段的摘取已经基于 XR Interaction Toolkit 实现，PC 上由屏幕中心射线驱动。
- 全部尺寸按 1 单位 = 1 米制作，写实彩椒按真实大小（约 0.7 m 高）摆放。

</details>

### 用到的 AI 工具

| 工具 | 用途 |
|---|---|
| [Tripo3D](https://www.tripo3d.ai) | 写实彩椒、虫子、手部模型。彩椒模型同时是全部节点布局的"源头"：所有抽象形态都是从它的表面采样推导出来的 |
| PixVerse | 概念图与视频素材 |  <!-- TODO：确认 PixVerse 的实际用途，以及结局视频由哪个工具生成 -->

## 路线图

- [x] PC 版完整流程：开场 → 教学 → S1–S4 → 采摘 → 结局视频
- [ ] 采摘后的"虚幻化与消散"演出
- [ ] 界面改为 World Space（为 VR 做准备）
- [ ] **Meta Quest 2 版本**：XR Origin、手柄射线、用手直接摘果实、送到嘴边吃

## 项目结构

```
Tripothon2026/
├── A_Gift_for_Ghost/                 Unity 工程（用 Unity Hub 打开这个文件夹）
│   └── Assets/
│       ├── Scripts/                  全部 C# 代码，按系统分目录（见下方展开）
│       ├── Scenes/Main.unity         主场景，由菜单 Ghost → Core → Build Main Scene 生成
│       ├── Data/                     运行时只读的 ScriptableObject：节点集、台词、节点文本、音频表
│       ├── 3D_Objects/               Tripo3D 生成的模型：彩椒（pepper2）、虫子（bug）、手（hand）
│       ├── Art/                      Shader 与材质（节点、连线、写实溶解、暗角）、UI 贴图
│       ├── Audio/                    BGM / SFX / VO（配音按台词 ID 命名，自动挂到对应台词）
│       ├── Video/Outro.mp4           结局视频
│       ├── XRI/                      XR Interaction Toolkit 配置（采摘段已在用，为 VR 版准备）
│       └── InputSystem_Actions.inputactions   全部输入动作（Gameplay / Player / Debug）
└── docs/
    ├── PROJECT_SUMMARY.md            设计文档：主题、玩法规则、开发模块与验收记录
    ├── ARCHITECTURE.md               系统职责、接口、事件和数据流
    ├── CURRENT_STATE.md              当前实现状态和已知问题
    ├── VR_GUIDELINES.md              Quest 2 迁移规范（相机、尺度、UI、性能预算）
    ├── script/                       策划数据源：节拍表、台词表、节点文本、媒体与音频清单
    ├── tasks/                        单个功能的任务说明（教学、S2 脉冲引路、采摘、聊天气泡……）
    └── protocols/                    协作流程：调试、版本管理、收尾
```

<details>
<summary><b>Scripts/ 各目录详解</b>（点击展开）</summary>

| 目录 | 职责 | 关键文件 |
|---|---|---|
| `Core/` | 流程骨架：按顺序运行关卡，开始 / 结束界面 | `GameFlow`（关卡状态机）、`Stage`（关卡基类）、`StartScreen` / `EndScreen`、`GameFlowDebug`（N / Shift+数字跳关）、`Editor/MainSceneMenu`（**主场景的唯一来源**） |
| `Stages/` | 每一关的玩法和通关判定 | `TutorialStage`、`S1MatrixStage`、`S2CircuitStage`、`S3NetworkStage`、`S4GeometricStage`、`RealTransitionStage`、`PickStage`、`OutroVideoStage`；`IssueStage`（点击问题类关卡的基类）、`StageContext`（各关共用的引用和工具） |
| `Morph/` | 节点变形系统，视觉核心 | `PlantNode` / `PlantNodeSet`（节点数据）、`LayoutGenerator`（由写实布局推出 4 种抽象布局）、`NodeMorpher` + `NodeMorpherStates`（GPU instancing 绘制与逐节点状态）、`NodeLinkRenderer`（连线）、`NodeHaloRenderer`（外发光圈）、`PlantFit`（按形态构图）、`RealModelHandoff`（写实模型溶解交接）、`Editor/MeshAnchorSampler`（从 Tripo 模型采样节点） |
| `Gameplay/` | 节点问题规则 | `NodeIssueSystem`：简单 / 中等 / 困难三档问题的尝试次数、重试和解决状态 |
| `Interaction/` | 输入到玩法事件的翻译层 | `PointerInput`（PC 交互发起方：点击、拖拽、查看）、`NodePicker`（射线拾取节点，节点没有 Collider）、`TargetRotator`（旋转植株）、`IInteractable`、`DialogueAdvanceInput` |
| `Narrative/` | 对白与文本 | `DialoguePlayer`（播放、排队、跳过）、`SubtitlePanel`（字幕 / Agent 分流）、`AgentChatFeed`（左侧聊天气泡）、`ScreenBlackout`、`IntroStage`、`NodeDetailTable`；`Editor/` 下是台词表、节点文本 CSV 导入器和配音自动绑定 |
| `Agent/` | Agent 界面 | `AgentQueryDialog`（"是否……" YES 询问框）、`NodeDetailPopup`（右键详情）、`AgentUIStyle`（全部 HUD 的统一样式与工厂） |
| `Player/` | 采摘段的第一人称玩家 | `PlayerRig`（接管 / 归还主相机）、`FirstPersonMotor`（走、转头、蹲下）；`PC/` 下是 PC 专用的光标锁定、准星和操作提示 |
| `Pick/` | 摘和吃 | `PickableFruit`（基于 XRI 的可摘果实）、`HandReach`（手臂伸出去摘）、`EatSequence`（举到嘴边、一口一口吃） |
| `Audio/` | 音乐与音效 | `GameAudio`（按关卡切换 BGM / 环境音）、`GameAudioCues`（订阅玩法事件播放音效，不改玩法逻辑）、`AudioLibrary` |

</details>

<details>
<summary><b>从哪里读起</b></summary>

- **想看一局游戏是怎么串起来的**：`Core/GameFlow.cs` → `Stages/StageContext.cs` → 任意一个 `Stages/S*Stage.cs`
- **想看变形是怎么做的**：`Morph/NodeMorpher.cs` → `Morph/LayoutGenerator.cs` → `Morph/RealModelHandoff.cs`
- **想改台词或节点文本**：台词改 `docs/script/02_dialogue.csv`，编辑器会自动重新导入；节点文本改 `03_node_text.csv`，再点菜单 Ghost → Narrative → Import Node Text CSV。都不用改代码
- **想加一关**：写一个 `Stage` 子类，在 `Core/Editor/MainSceneMenu.cs` 的关卡列表里加一行，再重新生成场景
- **系统之间怎么通信**：见 [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md)

</details>

## 团队

为 **Tripothon 2026** 制作。3 人，48 小时。

<!-- TODO：填写成员姓名、分工和联系方式 -->
| 成员 | 分工 |
|---|---|
| — | 程序：交互框架、关卡流程、对话系统 |
| — | 技术美术：节点变形、Shader、场景与演出 |
| — | 策划与内容：关卡设计、台词、音频、视频 |

## 许可

<!-- TODO：选定许可证后补上 LICENSE 文件（比赛开源类别通常要求，例如 MIT） -->
许可证待定。
