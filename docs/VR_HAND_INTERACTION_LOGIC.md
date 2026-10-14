# VR Hand Interaction Logic

本文描述 PC 原型到 VR 版本时，交互表现如何从「射线工具」逐步过渡到「半透明虚拟手」。目标不是替换 Unity / XR 默认输入，而是在默认 ray、controller action 和现有 Stage 逻辑之上，增加一层随关卡进化的手部视觉和动作反馈。

相关原则见 [VR_GUIDELINES.md](VR_GUIDELINES.md)。可交互 HTML 原型见 [prototypes/vr_hand_interaction_sim.html](prototypes/vr_hand_interaction_sim.html)。

## 1. 设计目标

- 让玩家的主体感逐步增强：早期只有远程 ray，之后先出现工具，再出现半成形手指，最后变成半透明虚拟手。
- 不让玩法逻辑依赖具体设备：鼠标、手柄 ray、XR Hands 都应该走同一组高层事件。
- S2 保持「导水 / 接通」的工具感，S3/S4 逐渐进入「亲手抓取」。
- 捉虫动作需要组合输入和可见动作，不再只是点一下。

## 2. 阶段表现

| 阶段 | 输入外观 | 操作含义 | 视觉关键词 |
|---|---|---|---|
| S1 | RayOnly | 远程授权 / 选择 | 细 ray、准星、无手 |
| S2 | WaterConduit | 导水器接通供水路径 | 喷嘴、导水线、水脉冲、蓝绿反馈 |
| S3 | Tweezer / ProtoFinger | 用镊子或半手指夹取 bug | 夹合、轻抖、锁定、半透明指尖 |
| S4 | GhostHand | 半透明手指直接抓虫 | pinch、指尖发光、手掌轮廓 |
| Pick | BodyHand | 真实行动 / 摘果实 / 送入口中 | 更稳定、更实体的手 |

## 3. 输入抽象

新增一层交互视觉与动作适配器，建议命名为 `InteractionAvatarVisual`。它只负责表现，不直接决定玩法成功失败。

```text
PointerInput / XR Ray / XR Hands
        ↓
InteractionIntent
        ↓
Stage gameplay logic
        ↓
InteractionAvatarVisual
```

建议抽象出的高层输入：

| Intent | PC 临时绑定 | VR 绑定建议 | 用途 |
|---|---|---|---|
| Aim | 鼠标位置 | controller ray / hand ray | 指向节点、虫子、果实 |
| Select | 左键 | trigger | S1 授权、S2 导水开始、普通确认 |
| Grasp | 键盘键 / 右键暂代 | grip | S3/S4 进入夹取准备 |
| Actuate | 左键 / trigger | trigger | 与 Grasp 组合，执行夹合 |
| Inspect | 右键按住 | secondary button | 查看详情 |

S3/S4 捉虫的核心组合是：

```text
Grasp held + Actuate pressed = 执行夹取判定
```

这样能避免玩家只是扫过 bug 就触发，也能让抓取动作有明显仪式感。

## 4. 手部外观状态

`InteractionAvatarVisual` 至少支持四种模式：

```csharp
public enum InteractionAvatarMode
{
    RayOnly,
    WaterConduit,
    Tweezer,
    ProtoFinger,
    GhostHand,
}
```

Stage 进入时设置模式：

```text
S1 -> RayOnly
S2 -> WaterConduit
S3 early -> Tweezer
S3 late -> ProtoFinger
S4 -> GhostHand
Pick -> GhostHand / BodyHand
```

## 5. 精度切换选择

S3 可以把「手部出现」做成一个明确的选择，而不是自动升级。触发点来自剧情和操作检测：Agent 或治疗中心检测到当前执行硬件精度不足，继续用默认代理工具可能无法稳定清除虫子。

建议弹出一次选择：

```text
检测到当前操作精度不足。

选项 A：提高操作精度
- 切换到半透明真手的过渡状态。
- 玩家需要承担更多自主人工操作。
- 捉虫改为 Grasp + Actuate 的组合动作。
- 成功后真实感与手部清晰度提高。

选项 B：维持现状
- 保持当前 ray / 工具代理模式。
- 操作更省力，但精度和反馈有限。
- 可继续观察，但关键移除动作可能受限。
```

推荐让游戏文案避免说「更好 / 更差」，而是表达两种代价：

- 提高精度：更多身体参与、更高自主性、更明确的成功反馈。
- 维持现状：更依赖代理、更少动作负担、但难以完成精细干预。

这个选择可以成为玩家从「代理替我操作」转向「我亲自操作」的关键节点。选择提高精度后，手部表现不应立刻变成完整真实手，而是进入 `ProtoFinger` 或 `GhostHand` 的过渡态：半透明、轻微抖动、指尖清晰但掌心仍不稳定。

实现上先只作为设计文档和剧情规划，不立刻改现有 S3 逻辑。

### RayOnly

- 使用默认 ray，不额外画手。
- 只显示命中点、准星或轻微 hover 高亮。
- 表达玩家仍在远程授权，身体感很弱。

### WaterConduit

S2 的鼠标 / VR ray 末端表现为导水器，而不是手。

- ray 末端是一个小喷嘴 / 接口。
- 按住开始导水后，喷嘴到当前节点之间出现水线。
- 正确连上缺水节点：喷嘴亮蓝，节点充水，播放短促水声。
- 连错或连到不需要水的节点：喷嘴暗一下，播放低沉沙沙声。
- 与现有 S2 逻辑对接：`TraceStarted`、`NodeLinkedDetailed`、`WrongNodeLinked`。

### Tweezer

S3 初期建议使用镊子或夹子，不直接出现完整手。

- 两片细长半透明夹片，默认张开。
- 按住 `Grasp` 时进入准备状态，夹片轻微靠拢。
- `Grasp + Actuate` 时快速夹合。
- 瞄准 bug 时夹尖发光，抖动变小，像锁定目标。
- 未命中时短促抖动并回弹。

### ProtoFinger

S3 中后期从工具过渡到手。

- 镊子两片变圆润，像拇指和食指的末端。
- 指尖仍由光点、短线、半透明几何体组成。
- 手掌只显示一个很淡的掌心圆片或骨架线，不做完整模型。
- 夹取动作仍然是 pinch，但视觉上已经开始像手指。

### GhostHand

S4 开始使用半透明虚拟手。

- 有完整手掌轮廓和拇指 / 食指。
- 平时手指自然张开。
- 按住 `Grasp` 时拇指和食指预收拢。
- `Grasp + Actuate` 时执行 pinch。
- 成功抓虫：指尖短亮，虫子被移除或被夹起后消散。
- 失败抓取：手指短抖，目标无变化。

## 6. 捉虫状态机

建议使用一个独立状态机，服务 S3/S4，不写死在某个输入设备里。

```text
Idle
  ↓ aim hits bug
AimingBug
  ↓ Grasp held
GraspHeld
  ↓ Actuate pressed while Grasp held
PinchAttempt
  ↓ hit valid bug
PinchSuccess
  ↓ miss / invalid target
PinchFail
  ↓ animation finished or Grasp released
Idle
```

### 状态说明

| 状态 | 条件 | 视觉 | 反馈 |
|---|---|---|---|
| Idle | 未瞄准 bug | 工具 / 手自然状态 | 无 |
| AimingBug | ray 命中 bug 或进入抓取半径 | 尖端微亮 | 可选轻提示音 |
| GraspHeld | 按住 Grasp | 夹子半合，轻微手抖 | 手柄轻震或低频 hum |
| PinchAttempt | Grasp + Actuate | 快速夹合 | 短音效 |
| PinchSuccess | 判定命中 | 指尖亮、bug 被夹走 | 成功音、短震 |
| PinchFail | 未命中或目标不合法 | 工具抖动回弹 | 失败音、短低频震 |

## 7. 抓取判定

S3/S4 不建议只用 ray 命中判定。更稳妥的是 ray 命中 + 尖端半径。

成功条件建议：

```text
Grasp is held
Actuate pressed this frame
aim target is bug
bug is visible / active
distance from pinch tip to bug <= grabRadius
angle between ray direction and target normal <= maxGrabAngle
```

第一版可以只实现前三条，后续再加距离和角度。

失败条件：

- 未瞄准 bug。
- 目标是叶片 / 节点 / 空白。
- bug 已经被移除。
- 抓取时机不满足组合输入。

## 8. 抖动与震动

抖动要表达「手还不稳定」，但不能干扰瞄准。

建议参数：

| 模式 | 常态抖动 | Grasp 按住 | 命中锁定 | 失败反馈 |
|---|---:|---:|---:|---:|
| Tweezer | 0.4-0.8 mm | 0.8-1.5 mm | 0.2-0.4 mm | 2-4 mm 短抖 |
| ProtoFinger | 0.3-0.6 mm | 0.5-1.0 mm | 0.1-0.3 mm | 1.5-3 mm 短抖 |
| GhostHand | 0.1-0.3 mm | 0.2-0.5 mm | 0-0.2 mm | 1-2 mm 短抖 |

实现方式：

```csharp
Vector3 tremor = noiseDirection * tremorAmplitude;
visualRoot.localPosition = basePosition + tremor + failKickOffset;
```

VR 手柄震动建议：

- 进入 GraspHeld：极轻震，持续 0.05-0.08 秒。
- 成功抓取：短促中等震动，0.08-0.12 秒。
- 失败抓取：低频短震，0.06-0.1 秒。

## 9. 与现有关卡的对接

### S2

保留已有逻辑：

- `TraceStarted` 开始导水底声和导水器激活。
- `NodeLinkedDetailed(nodeId, indexInSegment, needsWater)` 判断是充水声还是沙沙声。
- `WrongNodeLinked(nodeId)` 触发导水器失败反馈。

新增视觉：

- 导水器喷嘴跟随 ray 命中点或鼠标命中点。
- 拖拽时显示从喷嘴到当前 anchor 节点的细水线。
- 正确连线时喷嘴短亮。

### S3

S3 当前是旋转结构和识别虫子。后续接入捉虫时：

- 进入 S3 初期使用 `Tweezer` 或维持默认 ray / 工具代理。
- 当系统检测到精细操作需求时，弹出「是否提高精度」选择。
- 选择提高精度后进入 `ProtoFinger` 或半透明 `GhostHand` 过渡态。
- 选择维持现状时保留工具代理表现，允许继续观察，但捉虫成功窗口更窄或需要后续再次确认。
- 玩家先旋转观察，找到 bug 后按住 `Grasp`，再按 `Actuate` 夹取。
- 成功移除 bug 后，局部真实感或手部清晰度上升。
- 当移除一定数量 bug 后，从 `Tweezer` 过渡到 `ProtoFinger`。

### S4

S4 使用 `GhostHand`：

- 仍然可以沿用 S3 的捉虫状态机。
- 视觉从夹子变为拇指和食指 pinch。
- 成功抓虫后，植物真实度提升，手也更稳定。

## 10. 最小实现切片

第一版建议只做以下内容：

1. `InteractionAvatarVisual`：支持 `WaterConduit`、`Tweezer`、`ProtoFinger`、`GhostHand` 四种外观。
2. `BugGraspController`：维护捉虫状态机，只读高层输入和当前 aim target。
3. `TweezerVisual`：两个 capsule / mesh 片，根据状态开合。
4. 抖动：用 Perlin noise 或简单 sin noise 叠加局部偏移。
5. 成功 / 失败事件：接到现有音效和 bug 移除逻辑。

第二版再加：

- 真正的半透明手模型。
- XR Hands 骨骼驱动。
- 更准确的 pinch tip 距离和角度判定。
- 线条、指尖和 bug 的空间音效。

## 11. 验收标准

- 不接 VR 时，PC 鼠标也能看到 WaterConduit / Tweezer / ProtoFinger / GhostHand 的模拟表现。
- S3 捉虫必须需要 `Grasp + Actuate` 组合，不允许单纯 hover 或单击误触发。
- 成功和失败都有不同视觉、音效和震动反馈。
- 手部清晰度随阶段提升：工具 -> 半手指 -> 半透明手。
- S3 精度切换选择只改变交互负担与自主程度，不应写成简单的好坏选项。
- 玩法逻辑不直接读取具体设备；设备差异停留在输入适配层。
