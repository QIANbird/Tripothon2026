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

- 现实锚点是一株番茄。Agent 口中的"核心项目"就是这株番茄，"成员 A"就是母亲。
- 结局不是摧毁 Agent，而是把它调成 **Assistive（辅助）** 模式。Agent 可以举灯、给建议、稳住动作，但不能替人决定看什么、怎么理解别人、什么值得珍惜。
- 立场不反 AI。重点是校准信任：可逆、低风险的事可以交给 Agent；模糊、不可逆、涉及价值判断的事要查证据，或者自己来做。

## 3. 视觉核心："Encoded States"

全游戏只有**同一组节点**（约 150–300 个方块或碎片），在几套布局之间变形。它们始终代表那株番茄，只是抽象程度在降低：

方块矩阵 → 直角回路 → 3D 网络 → 几何植株 → 写实番茄

- 美术风格：浅灰背景，黑、灰、蓝灰色的方块和细线，写实阶段才出现绿色和红色。参考策划给的 6 张 "ENCODED STATES" 概念图。
- 材质用 Unlit 或 Simple Lit，配 GPU Instancing，保持 Quest 的性能预算。

## 4. 四个阶段（已从 6 个合并而来）

| 阶段 | 形态 | Agent 的说法 | 异常或矛盾 | 玩家动作 | 取回的东西 |
|---|---|---|---|---|---|
| S1 矩阵 | 打乱的方块网格 | "提高增长 / 解决异常 / 维持关系……" | 有一个方块反复亮起："执行完成。异常仍然存在。" | 轻点：批准并熄灭。长按反复亮起的那个方块，听到隐藏的滴水声，方块展开成连线 | 怀疑 |
| S2 回路 | 直角回路，中央有计划体块 | "立即浇水"（依据：增长低于目标，距上次浇水已 24 小时，进度落后） | 角落里一个很浅的节点："Soil signal: Unverified"；计划中有一步"移除低增长分支"没有任何依据 | 把浅色证据拖进 Context，Agent 的建议随之改变；把那一步错误计划拖出去删掉 | 决定 Agent 看什么、做什么 |
| S3 网络到几何植株 | 3D 网络，收拢成几何植株 | "异常：Noise，建议忽略"；"94% 坏死" | 异常点都集中在叶背；"坏死"的卷叶底下其实有花苞 | 旋转结构找到并标记虫子；否决这条高置信度标注 | 判断权 |
| S4 真番茄 | 写实番茄（Tripo 生成） | "果实质量 72%，形状一致性低于目标" | 不需要异常，番茄本来就不完美 | 伸手、抓住、旋转、摘下，有阻力感 | 亲手行动 |

- **开场**：黑屏，Agent 开机问候，显示"Full Proxy 模式运行中"，然后进入 S1。
- **结局**：番茄摘下后，场景剥落成治疗舱，玩家亲手把 Agent 调到 Assistive 模式。主角最后说："我知道。但这一次，我知道它为什么长成这样。"
- **三条线**：母亲的语音、同事的消息、项目提交记录，作为证据分散在 S2 和 S3，每条线 2–3 条消息。
- **砍掉的内容**：依赖度统计、记忆碎片的颜色分类、剪刀摇杆、多结局。

设计原则：每个阶段只保留一句 Agent 台词、一个异常、一个动作。AI 原理放在答辩 PPT 里讲，不在游戏里讲。

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
StageDirector        // 阶段状态机：进入阶段 → 目标完成 → 过渡演出 → 下一阶段
```

布局倒着生成，从写实番茄往回推：

1. **写实形态**：在番茄模型上采样锚点。原型阶段先用程序生成一株假植物（递归分枝）。
2. **几何植株**：节点留在原位，各自换成三角片或长方块。
3. **3D 网络**：保留父子连接，把位置拉开、打散。
4. **回路**：投影到一个平面，连线改成直角折线（LineRenderer）。
5. **矩阵**：把节点顺序打乱，排成网格，让人看不出植物轮廓。

形态和阶段的对应：S1 = Matrix，S2 = Circuit，S3 从 Network 收拢到 Geometric，S4 = Real。S3 需要两套布局，所以形态是 5 种，不是 4 种。

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
Assets/Scripts/Core/          StageDirector、事件、数据定义
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
- 参考模型：`Assets/TripoModels/pepper_plant_3d_model/`，是一株甜椒，不是番茄，只作形态参考。约 5.1 万三角面，2K 贴图；高约 1 m（Tripo 归一化尺寸，未按真实尺寸校正）。S4 仍需番茄模型。
- 变形原型进度：M1 数据结构已完成（`Assets/Scripts/Morph/PlantNode.cs`、`PlantNodeSet.cs`）。M2 程序化假植物已完成（`FakePlantGenerator.cs`，菜单 Ghost → Morph → Generate Fake Plant，生成 `Assets/Data/Morph/FakePlant.asset`，206 个节点，约 0.67 × 0.64 × 0.56 m）。叶片由两个节点组成（内侧宽、外侧窄），虫子贴在叶背，以那片叶为父节点。

## 6. 除变形和交互外还要做的内容

- **对话系统**：播放器、字幕、排队与打断、摘要和原文的对照呈现。
- **开场与结局**：开机演出、治疗舱剥落效果、调到 Assistive 模式的交互。
- **交互引导**：每个新动作第一次出现时给提示，用场景里的光和声音引导视线。
- **音频**：每个阶段的环境音乐（从电子逐渐过渡到自然）；关键音效（滴水声、连线、摘番茄的撕裂声）；配音（Agent 用合成腔，母亲最好真人录）。
- **资产**：写实番茄、虫子、治疗舱、玩家的手（Tripo 生成）。
- **调试跳关**：一键跳到任意阶段，联调和答辩现场都会用到。
- **提交材料**：Windows 包、1–2 分钟演示视频、答辩 PPT、操作说明。

## 7. 分工

| 角色 | 负责 |
|---|---|
| A 程序 | 交互框架、StageDirector、调试跳关、对话系统、S1–S4 玩法逻辑 |
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

## 9. 下一步：变形原型

1. 在 `Scripts/Morph/` 下写 `PlantNode`、程序化假植物生成器、5 套布局生成器和 `NodeMorpher`。
2. 新建测试场景 `Assets/Scenes/MorphPrototype.unity`：浅灰背景，固定相机，用 Input Action 绑定数字键 1–5 切换形态。
3. 验收：节点在 5 种形态间平滑过渡，每种形态都接近对应的概念图；200 个节点时帧率稳定。
