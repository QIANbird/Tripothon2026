# VR Runtime Asset Table and Future Stage Notes

本文记录当前分支已经接入的 VR 交互原型、现有资产可用性、Quest 3 连接电脑体验路径，以及后续「点云」和「四维空间甜椒」方案评估。

## 1. 当前可体验范围

目标是让 Quest 3 通过 Link / Air Link 连电脑时，能直接体验同一套阶段交互逻辑：

- S1 / Tutorial / S1：默认 ray。
- S2：ray 末端显示导水器，沿路径导水。
- S3：默认是镊子 / 工具；按住 Grasp 后显示半透明手指过渡态。
- S4：显示半透明虚拟手；摘虫需要 `Grasp + Select`。
- Pick：显示更稳定的手部代理。

当前实现是「运行时原型」，不依赖绑定骨骼的手模型。它使用 `LineRenderer`、cube、sphere 等集合块即时生成导水器、镊子和手指。

## 2. Quest 3 输入映射

| 操作 | Quest 3 / PCVR | PC fallback | 当前用途 |
|---|---|---|---|
| Aim | 右手 controller pose ray | 鼠标屏幕 ray | 指向节点 / 虫子 |
| Select / Actuate | 右手 trigger | 鼠标左键 | S2 导水、S4 抓取执行 |
| Grasp | 右手 grip | `G` 键 | S3/S4 手部准备和摘虫条件 |
| Inspect | 右手 primary button | 鼠标右键 | 查看节点详情 |

实现位置：

- `Assets/Scripts/Interaction/PointerInput.cs`
- `Assets/Scripts/Interaction/InteractionAvatarVisual.cs`
- `Assets/Scripts/Interaction/XRInputStatusHud.cs`
- `Assets/Scripts/Stages/S4GeometricStage.cs`

## 3. 资产表

| 资产 | 路径 | 当前状态 | 本次用法 | 后续建议 |
|---|---|---|---|---|
| 静态 hand FBX | `Assets/3D_Objects/hand/tripo_convert_07d6ebfa-74e6-4ae4-a1a9-7c87fa196291.fbx` | 有 mesh，但未确认有可用骨骼/绑定 | 暂不直接使用，避免手指无法动 | 后续作为 GhostHand 外形参考，或重新绑定骨骼 |
| hand basecolor | `Assets/3D_Objects/hand/.../hand_3d_model_basecolor.jpg` | 静态贴图 | 暂不使用 | 可用于半透明手材质的颜色参考 |
| bug FBX | `Assets/3D_Objects/bug/tripo_convert_cbdef70f-0cb3-4f3b-8ed2-3e0794432c86.fbx` | 已用于 S4 真实虫子模型 | 继续沿用 | 后续给 bug 增加抓取点 / collider |
| pepper plant FBX | `Assets/3D_Objects/pepper2/pepper_plant.fbx` | 写实植株来源 | 不改 | 可用于点云 / 四维空间候选实例的来源 |
| Node mesh | 运行时 cube / icosphere | 已用于节点变形 | 用作手部代理集合块 | 继续作为低成本 VR 视觉块 |
| XRI settings | `Assets/XRI/Settings/...` | 项目已有 XRI 配置 | 不依赖具体 prefab | 后续接 XR Origin / XR Interaction Toolkit |
| Input Actions | `Assets/InputSystem_Actions.inputactions` | 已有键鼠和部分 XR 绑定 | 这次用 XR InputDevices 直接读取 controller pose | 后续可把 XR action 正式写进 Gameplay map |

## 4. 当前占位视觉策略

由于 hand FBX 暂未绑定骨骼，本阶段不使用它做手指动画。当前策略：

- 导水器：cube + sphere，ray 末端发光。
- 镊子：两条细 cube 作为夹片。
- ProtoFinger：两条更圆润的块 + 两个指尖 sphere + 淡掌心。
- GhostHand：sphere 掌心 + 指尖 / 指节代理，半透明材质。
- StableHand：同样结构，但抖动更小、透明度更高。

优点：

- 不等美术绑定，可以马上体验交互。
- Quest 端负担低，不引入复杂 skinned mesh。
- 后续替换成真实手模型时，交互状态机不用改。

## 5. 当前限制

- 还没有真正 XR Origin 场景结构；当前是 PC scene + XR controller ray 的兼容原型。
- controller ray 能驱动指向和 trigger/grip，但玩家空间位置、手柄模型、正式 haptics 还没接。
- S3 目前仍是识别虫子，Grasp 后只表现为半手指过渡态；真正的 S3 抓虫状态机还未替换旧 tap 逻辑。
- S4 已要求 `Grasp + Select` 才能摘虫，但成功/失败音效和手柄震动还需要后续接入。

## 6. Quest 3 体验建议

当前代码已经能在 Unity 运行时读取 XR controller pose / trigger / grip；但仓库当前 `Packages/manifest.json` 里还没有 `com.unity.xr.openxr` 和 `com.unity.xr.management`。如果你的本机 Unity Editor 里 Quest Link 没有输入，需要先在 Unity Package Manager / Project Settings 中启用 OpenXR。由于本机 `Packages` 里还有 Tripo 本地路径的临时差异，本次提交先不改包依赖，避免把无关路径一起提交。

1. 用 Unity 6000.3.25f1 打开项目。
2. 确保 Quest 3 通过 Link / Air Link 连接电脑，并且 Unity Play 时能看到 XR controller 输入。
3. 打开 `Assets/Scenes/Main.unity`。
4. 运行后用调试快捷键跳到 S2/S3/S4。
5. 在 S4 中按住右手 grip，再按 trigger 摘虫。

运行时左上角会自动出现 `Quest / VR Input` 状态面板：

| 状态 | 含义 |
|---|---|
| `Right controller: seen` | Unity 已经看到 Quest 右手柄 |
| `Pose: ready` | 右手柄位置和旋转可读 |
| `XR ray: active` | 当前交互已经走 Quest 手柄射线 |
| `mouse fallback` | 还没有读到 XR pose，当前仍在用鼠标逻辑 |
| `Trigger / Grip` | 当前按压轴值，方便确认按键是否被 Unity 收到 |

如果面板一直显示 `Right controller: not seen` 或 `mouse fallback`，优先检查 Quest Link / Air Link、Meta Quest Link 桌面端、Unity OpenXR loader、以及 Project Settings 里的 XR Plug-in Management。`F8` 可以隐藏或显示这个面板。

如果没有头显输入，PC fallback：

- 鼠标指向。
- 左键 Select。
- `G` 按住作为 Grasp。
- S4 用 `G + 左键` 摘虫。

## 7. Unity 内桌面模拟热键

为了在没有 Quest 3 的情况下先看 VR 手部效果，`InteractionAvatarVisual` 内置了桌面模拟热键：

| 热键 | 效果 |
|---|---|
| `H` | 开关强制预览模式。开启后不需要进入对应关卡也能看手部外观 |
| `1` | RayOnly |
| `2` | WaterConduit / 导水器 |
| `3` | Tweezer / 镊子 |
| `4` | ProtoFinger / 半手指 |
| `5` | GhostHand / 半透明虚拟手 |
| `6` | StableHand / 稳定手 |
| `G` 按住 | Grasp，夹子/手指进入预备夹合 |
| `Space` | Actuate，做一次快速夹紧 |
| `F` | 触发失败抖动反馈 |

正常游玩时不需要开 `H`：视觉会自动按当前 Stage 切换。调试时可以直接按 `H` + 数字键检查外观。

## 8. 点云阶段评估

「最后阶段转点云」可以做，但不建议作为当前切片立刻实现。

可行路线：

- 从 `pepper_plant.fbx` 或已有 `PlantNodeSet` 采样点。
- 用 GPU instancing 渲染点 sprite / 小球。
- 点云密度随真实度提升：节点植株 -> 稀疏点云 -> 高密点云 -> 写实 mesh。
- 可用噪声让点云轻微漂浮，表现「真实正在解码」。

风险：

- Quest 2/3 上大量透明点会有 overdraw 压力。
- 点云如果点太少会像破碎模型；点太多会吃 fill rate。
- 与现有 RealModelHandoff 的 mesh reveal 需要明确先后关系，否则视觉语言会冲突。

建议：

- 先做一个单独 `PointCloudHandoff` 原型场景，不直接接主流程。
- 第一版用 2k-6k 个点，URP Unlit，关闭阴影。
- 与最后 Pick 前的「真实交接」结合，而不是替代整个最终关卡。

## 9. 四维空间甜椒方案

用户提出的方向：最后一关不是单株写实彩椒，而是进入类似四维空间 / 矩阵空间。玩家看到很多相似但细微不同的甜椒植株，需要找到特殊的一株，或者把属于同一植株的不同时间段按顺序找出来。

这个方向很适合放在最终阶段或 Pick 前的过渡阶段。它可以把「拿回真实」从单次抓取扩展为「在多重可能性里识别连续自我」。

### 方案 A：特殊甜椒植株

玩家进入一个矩阵空间，看到多株甜椒：

- 每株都是同一基础模型的变体。
- 差异很小：果实颜色、叶片虫咬痕、根部水痕、枝条角度、某个节点颜色。
- Agent 可以给出置信度建议，但不能直接替玩家选。
- 玩家需要靠前面关卡学到的线索找到「真实的那一株」。

优点：

- 目标清晰，适合比赛 demo。
- 视觉上容易做成强场景。
- 和「特殊果实是钥匙」叙事兼容。

实现方式：

- 复制 6-12 个 Plant root。
- 每个实例共享 mesh / material，只改少量参数。
- 特殊植株保留前面 S2/S3/S4 产生过的痕迹。
- 选择错误时给低置信度反馈，不失败，允许继续观察。

### 方案 B：同一植株的时间顺序

玩家看到同一株彩椒在不同时间段的状态：

- 缺水前。
- 缺水中。
- 接通水路后。
- 虫害暴露后。
- 除虫后。
- 果实成熟。

玩家需要按顺序找出属于同一株的时间段。

优点：

- 更符合「四维空间」概念：不是空间里找一株，而是在时间切片里重建连续性。
- 主题更强：玩家重新获得对因果和时间的感知。

风险：

- 规则比方案 A 更抽象，需要更强引导。
- 需要做清楚 UI / 交互，否则玩家可能不知道是在排序。

实现方式：

- 6 个时间切片排成环或漂浮矩阵。
- 每个切片有可观察线索：水球、虫子、叶片颜色、果实状态。
- 玩家用 ray / hand 抓取切片，按顺序放到时间轨道上。
- 正确排序后，所有切片合并成最终真实植株。

### 推荐

短期 demo 推荐方案 A，原因是实现快、可读性高。

中期版本可以把方案 A 和 B 合并：

1. 先从矩阵空间里找到「真实的那一株」。
2. 再把它的几个时间切片按顺序排列。
3. 排列完成后，四维空间坍缩为当前真实植株，进入 Pick。

这样既有空间识别，也有时间重建。

## 10. 后续实现建议

优先级建议：

1. 正式 XR Origin / OpenXR 配置，确认 Quest 3 Link 输入。
2. 把 XR 输入从 `InputDevices` 迁入 `InputSystem_Actions` 的 Gameplay map。
3. 给 `InteractionAvatarVisual` 增加 haptics 和音效事件。
4. S3 增加「是否提高精度」UI 选择，并把选择结果持久化到手部视觉模式。
5. S3 真正改为 Grasp + Actuate 捉虫状态机。
6. 评估手 FBX 是否可重绑；若不可行，用新手模型替换。
7. 单独原型化点云交接。
8. 单独原型化四维甜椒矩阵关卡。
