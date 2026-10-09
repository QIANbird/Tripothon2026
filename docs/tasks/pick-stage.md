# Goal

Pick 阶段：写实植株固定在玩家前方 2–3 m，不能转。玩家用键鼠第一人称走过去、蹲下、对准果实，用胶囊手臂伸手摘下；按 E 把果实举到嘴边（画面下方），一口一口吃掉。每咬一口，果实就更虚幻一些，最后碎成原来的节点粒子消散，然后进入 Outro。交互用 XRI 实现，以后迁到 VR 时只换交互发起方。

# Status

- 第 1–3 步已完成（10-08）：装包、`Player` 表、第一人称移动和蹲下、植株固定。见 `docs/CURRENT_STATE.md`。
- `Player` 表里已经有 `Grab`（鼠标左键）和 `Eat`（E）两个 Action，第 4–5 步直接订阅，不用再改 `.inputactions`。
- 第 4 步 + 5a 已完成（10-09，Play 自测通过）：`Scripts/Pick/`（`PickableFruit`、`HandReach`、`EatSequence`）、`PickStage` 接线、`PcCrosshairRayHover`。按 E 举到嘴边，之后每次 E 咬一口，果实缩小（3 口：1 → 0.35），发 `EatSequence.BiteTaken(index, total)`，最后一口发 `Finished`。还没有"缺口"粒子（并入 5b 的碎片）。
- 与原计划的差异：手臂是胶囊上臂 + Tripo 前臂/手模型（`3D_Objects/hand`，静态网格，挂在 Elbow 下）；植株挡板改到 Ignore Raycast 层，射线穿过它打到果实；可摘范围 = 眼睛到果实中心 ≤ 0.7 m（站着够不到，要蹲下走近）。
- 剩余：5b、5c、第 6 步（下一个对话，订阅 `BiteTaken` / `Finished`）。`Finished` 目前没人订阅，吃完停在嘴边，按 N 进 Outro。

# Existing reference

- `Assets/Scripts/Stages/S4GeometricStage.cs`、`TutorialStage.cs`：Stage 的生命周期写法（Enter 订阅 + `ctx.ResetShared()`，Exit 还原）。
- `Assets/Scripts/Player/PlayerRig.cs`、`FirstPersonMotor.cs`：已完成的玩家结构，手臂和 Interactor 挂在它的相机节点下面。
- `Assets/Scripts/Player/PC/PcCrosshair.cs`：准星，第 4 步给它加"可摘"状态。
- `Assets/Scripts/Morph/RealModelHandoff.cs`：写实模型实例 `ModelInstance`（Plant/RealModel 下面）。果实网格已经在模型里分离，按名字找（`pepper_picked` 之类，名字做成字段）。材质替换和溶解的写法也参考这里。
- `Assets/Art/Morph/RevealLit.shader`：clip 方块噪声溶解，保持不透明、支持单通道立体渲染。果实的虚幻效果照这个写。
- `Assets/Scripts/Morph/NodeShapes.cs`（`GetMesh(Shape.Icosphere)`）、`OrganPalette.cs`（`Organ.Fruit` 颜色）：碎片粒子用和节点一样的网格和颜色。
- `Assets/Scripts/Morph/NodeMorpher.cs` + `pepper_plant.asset`：果实节点（`Organ.Fruit`）在 Real 形态下的位置，作为碎片的发射点。
- 外部：XRI 3.x 的 `XRRayInteractor` / `XRGrabInteractable` / Input Action 绑定；Animation Rigging 的 `TwoBoneIKConstraint`；Unity 自带的 ParticleSystem（Mesh 渲染模式 + GPU instancing）。

# Files likely involved

- 新建 `Assets/Scripts/Pick/`：
  - `PickableFruit.cs`：挂在果实上，配合 XRI Interactable，负责 Collider、高亮、从植株上脱离，离开阶段时还原。
  - `HandReach.cs`：驱动胶囊手臂的 IK 目标，在三个姿态之间插值：待机位、果实位、嘴边位。摘下后把果实挂到手上。
  - `EatSequence.cs`：吃的流程。订阅 `Eat`：第一次把手举到嘴边，之后每次 `Bite()` 咬一口。事件 `BiteTaken(int index, int total)`、`Finished`。`Bite()` 是公开方法，以后正式动画可以通过 Animation Event 调用。
  - `FruitGhostVisual.cs`：只管表现。`SetUnreal(0..1)` 改材质参数；`EmitFragments(fraction)` 从果实上放出一批节点碎片；`Dissipate()` 把剩下的部分全部碎掉、飘散。
- 新建 `Assets/Art/Pick/FruitGhost.shader`：基于 RevealLit 的写法，`_Unreal` 控制三样东西：边缘发光（fresnel）、往节点颜色偏移、噪声 clip 镂空比例。仍然是不透明 + clip，不用透明混合。
- 新建 `Assets/Art/Pick/FruitFragment.mat`：URP Particles/Unlit，开 GPU instancing。
- 新建 `Assets/Scripts/Stages/PickStage.cs`：替换占位阶段。如果第 1–3 步已经建了，就在原有基础上扩展。
- `Assets/Scripts/Player/PC/PcCrosshair.cs`：可摘时准星变化。
- `Assets/Scripts/Core/Editor/MainSceneMenu.cs`：胶囊手臂 + IK、XR Interaction Manager + Ray Interactor、碎片粒子系统、嘴边锚点（相机子物体），填好 PickStage 的引用。
- `Assets/Scripts/Narrative/StageAssets.cs`（或对应的生成脚本）：Pick 开场、吃完两段 `[占位]` 对白。
- `docs/CURRENT_STATE.md`、`docs/ARCHITECTURE.md`：收尾时更新，补上 Pick 系统和事件。

# Implementation phases（每个阶段单独可以验收）

1. ~~**装包和输入**~~（已完成）
2. ~~**移动**~~（已完成）
3. ~~**固定植株**~~（已完成）
4. **摘**：
   - 从 `ModelInstance` 里按名字找到果实，加 Collider 和 Interactable。
   - XRI Ray Interactor 挂在相机上，从屏幕中心发射线，距离限制在伸手可及范围内。
   - 范围内高亮，准星变化。
   - 左键时胶囊手臂通过 IK 伸过去，果实脱离植株，挂到手上，手回到待机位。
5. **吃和消散**：
   - 5a：按 E，手把果实举到嘴边锚点：相机前方约 0.25 m、偏下，大约在画面下方三分之一的位置，过渡约 0.4 s。之后每按一次 E 咬一口：手前后小幅动一下，果实缩小一点，有一个"缺口"粒子爆发。
   - 5b：虚幻化。第 k 口后 `_Unreal = k / N`：边缘发光越来越强，颜色往节点红偏，表面出现方块镂空，镂空处飘出节点碎片。
   - 5c：最后一口时，剩余的果实全部碎成节点碎片。碎片从原来的果实节点位置发出，向上、向外慢慢飘，大约 1.5–2 s 内缩小消失。手回到待机位，然后播吃完的对白，进入 Outro。
6. **收尾**：按 CLOSEOUT 更新文档，跳关回归测试。

# Fragment source（碎片的发射点）

摘下时，取 `pepper_plant` 里离这颗果实最近的那组 `Organ.Fruit` 节点（判断标准：Real 形态下的位置落在果实的包围盒内），记下它们相对果实的局部坐标。

碎片用 `ParticleSystem.Emit(EmitParams)` 从这些点发出。网格用 `NodeShapes` 的 icosphere，颜色用 `OrganPalette` 的 Fruit 颜色，大小参考节点在 Real 形态下的缩放。

每一口按比例放出一部分点，最后一口放出剩下的全部。如果这组节点太少（< 20）或者找不到，就改成从果实网格表面发射（ParticleSystem Shape = MeshRenderer）。

只读节点数据，不改 `NodeMorpher` 的状态，也不改节点 id。

# Acceptance criteria

- 准星对准果实，并且离果实在伸手范围内（约 0.7 m）时，果实高亮、准星变化；范围外不高亮，也摘不了。
- 按左键时，胶囊手臂伸过去，果实脱离植株，跟着手走。植株的其他部分不受影响。
- 没摘到果实时按 E 没有任何反应。拿着果实时按 E，果实举到画面下方的嘴边位置，并且不挡住画面中心。
- 举到嘴边后，每按一次 E 咬一口，一共 N 口。每一口果实都明显变小、更虚幻（发光更强、镂空更多），同时放出一批碎片。
- 最后一口时，果实完全碎成节点形状的红色碎片，飘散后消失，不留下残余物体。之后播对白，进入 Outro。
- 吃的过程中玩家可以照常移动、转头，相机不被接管，没有镜头震动。
- 碎片同屏总数 ≤ 300，果实材质不用透明混合（Quest 预算，见 `docs/VR_GUIDELINES.md` 第 6 节）。
- 离开 Pick 阶段时（N 或跳关，包括吃到一半时离开），果实还原到植株上，材质恢复，碎片清空，手回到待机位，玩家关闭，光标解锁。再进 Pick 可以重新摘、重新吃。
- `EatSequence.Bite()` 可以在不读输入的情况下被外部调用（为以后的 Animation Event 和 VR 做准备）。
- 共享代码里不出现 `UnityEngine.Input` 和设备 API；PC 专用代码只放在 `Player/PC/`。`PickStage`、`PickableFruit`、`EatSequence`、`FruitGhostVisual` 不读 `Camera.main`，也不读鼠标坐标。嘴边位置从玩家相机节点的子物体取。
- 已有阶段的交互仍正常：S1–S4 的点击、拖拽、旋转、右键详情、对白推进，以及第 1–3 步的移动和蹲下都不受影响。

# Open decision（实现前确认，默认值见括号）

- 果实数量：1 个，已经在模型里分离，单独命名为 `pepper_picked` 之类的名字。
- 吃的触发：键盘按 E。VR 以后再调整（初步打算：手把果实靠近头部就算一口）。
- 每一口怎么触发：（默认：每按一次 E 咬一口。备选：按一次 E 自动连咬到底）
- 一共几口：（默认：N = 3）
- 每一口之间有没有最短间隔：（默认：0.5 s，避免连按时动画重叠）
- 吃到一半时放下：（默认：不支持，举到嘴边之后只能吃完）
- 对白：（默认：用 `[占位]` 文本，Enter 时一段，吃完后一段，经 `StageAssets` 生成）
- XRI 在 PC 上不用 XR Origin，只用 Interaction Manager + Ray Interactor 挂在相机上。
- 对白播放时还能移动：能，不锁玩家。

# Out of scope

- 真实手臂模型、正式的摘和吃动画、音效（由用户制作，之后替换胶囊和脚本插值）。
- VR 手柄绑定、XR Origin、Near-Far Interactor、靠近头部触发吃。
- 网格层面的真实"咬痕"（布尔切割或换模型）。占位阶段只用缩小 + 镂空来表现。
- 用 VFX Graph 做碎片（Quest 上的成本不好控制，用 ParticleSystem）。
- 跳跃、奔跑、动画化的蹲下姿态。
- 重新采样或修改 `pepper_plant`（节点 id 不能变）。
- Outro 阶段的内容。

# Session split（评估）

第 1–3 步已经在一个对话里做完。剩下的工作建议分 **2 个对话**：

1. **第 4 步 + 5a**：找果实、XRI 射线、高亮、IK 手臂、摘下挂到手上、按 E 举到嘴边，以及"咬一口"的基本节奏（先只做缩小）。风险在 XRI 不连头显时的行为，以及 IK 和胶囊的搭建，这些都属于交互链路。
2. **5b + 5c + 第 6 步**：FruitGhost 着色器、找碎片发射点、粒子、最后一口消散、对白和进入 Outro、跳关还原、文档收尾。这一段主要是着色器和视觉调参，和交互链路无关，需要反复在 Play 里看效果。

理由：吃和消散加进来以后，表现部分的工作量（着色器 + 粒子 + 调参）和交互部分差不多。两块放在一个对话里，上下文会被着色器和粒子的调参挤满，交互出 bug 时不好查。分界点放在 5a 之后：那时 `EatSequence.BiteTaken` 事件已经能发出来，第 2 个对话只需要订阅它做表现。
