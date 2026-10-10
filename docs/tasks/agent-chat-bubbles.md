# Goal

Agent 弹窗从“左上角一块白底面板”改成社交软件式的气泡聊天：每句 Agent 台词是一条新消息，从左栏底部滚动进入，向下堆叠，最上面那条到时间后淡出、其余上移。台词表 `通道` 列决定消息样式（三种）。Yes 询问框换成同一套气泡风格，仍单独在屏幕右侧，不进滚动栏。播放、推进、询问的逻辑都不变。

# 三种样式（台词表 `通道` 列）

| 通道值 | 样式 | 结构 |
|---|---|---|
| `Agent弹窗_02`（以及旧的 `Agent弹窗`） | 聊天气泡 | 圆形头像 + 半透明灰色圆角气泡 + 白字。不显示署名“没有温度的声音” |
| `Agent弹窗_01` | Caution 标签卡 | 有颜色的圆角卡片，第一行小号粗体 `Caution`（前面一个小圆标），第二行正文。不显示署名 |
| `Agent弹窗_01_query` | Caution 询问卡（右侧） | Caution 标签卡 + 正文下方 `YES` 按钮。只在屏幕右侧中部单独出现，不进滚动栏 |

参考配色（取自 10-10 参考图，Inspector 可改，常量放在 `AgentUIStyle`）：

| 用途 | 颜色 |
|---|---|
| 聊天气泡底 | `#8E9196`，alpha 0.72 |
| 气泡正文 | `#F5F6F7` |
| Agent 头像圆底 | 品红 `#E6337A`，中间白色图标 |
| Caution 卡底 | 青色 `#3FC1C9`，alpha 0.88 |
| Caution 标题 / 正文 | `#1E2126` / `#1E2126` |
| Caution 小圆标 | 黄 `#F2E14C`，中间黑色图标 |
| YES 按钮 | 底 `#1E2126`，悬停 `#E6337A`，字 `#F5F6F7` |

黑屏（Intro）和浅灰场景两种背景下都要看得清：灰气泡是半透明中灰 + 白字，两边都成立；Caution 卡不透明度高，不依赖背景。

# 消息滚动规则

- 每次 `DialoguePlayer.LineStarted` 且通道是 Agent（01 / 02）时，在左栏底部追加一条消息，打字机效果只作用在最新这条。
- 消息条目垂直排列，间距 12 px，从左栏顶部（现在 AgentMessage 的位置）往下排；最新的在最下面。
- 新消息进入：从下方 24 px 处上移到位，同时 alpha 0→1，0.2 s（ease-out）。
- 台词播完（`LineFinished`）后消息不立刻消失，留在栏里当聊天记录。
- 最上面的那条在“它的台词播完满 3 s”后淡出（0.25 s），下面的条目平滑上移（0.2 s）。只有最上面那条会被移除，一次一条；下一条成为最上面后，按它自己的播完时间重新计 3 s（已经超时的就立刻接着淡出）。
- 同时最多 8 条，超出时立刻淡出最上面一条（防止长段 Agent 台词把栏撑到屏幕底）。
- `DialoguePlayer.Stopped`（跳关、被 Play 打断）：整栏立刻清空，不做动画。
- 字幕通道的台词不影响滚动栏。
- 固定消息（`Pin`，S4_005 进度）：作为一条不会过期的聊天气泡固定在栏的最下面，`Pin` 再次调用只改文字；有新 Agent 台词时，新消息插在它上面。`Unpin` 时淡出。

位置和尺寸都用 Canvas 内的锚点坐标，不读屏幕像素（`docs/VR_GUIDELINES.md` 第 5 节），赛后改 World Space 时整栏换个 Canvas 就能用。

# 最快实现方式

1. 不用 LayoutGroup，自己写一个小的条目列表：每条记目标 y，`Update` 里 `Mathf.Lerp` 到位。这样上移 / 淡出动画好控制，也避免 ContentSizeFitter 一帧延迟导致的跳动。
2. 条目高度沿用现在 `ResizeAgentPopup` 的做法：按整句（不是打字机进度）量 `preferredHeight`，打字时气泡不变高。
3. 条目直接用代码搭（和 `NarrativeSceneBuilder` / `AgentUIBuilder` 一样），预制体模板不做；运行时 `Instantiate` 一个隐藏的模板子物体，或直接按样式现搭。消息最多 8 条，不需要对象池。
4. 圆角和圆形用三张小图（Sliced / Simple），用代码画出来，不等美术：
   - `Assets/Art/UI/AgentChat/bubble_round.png`：64×64 白色圆角矩形，半径 24，Sprite Border 24，Image Type = Sliced，颜色用 Image.color 染。
   - `Assets/Art/UI/AgentChat/circle.png`：64×64 白色圆（头像底、Caution 小圆标共用）。
   - `Assets/Art/UI/AgentChat/icon_agent.png`：64×64 白色图标（头像中间）。先放一个占位几何图标；美术给正式图标后同路径覆盖即可，导入设置不变。
   找不到这几张图时退回无圆角的纯色 Image，并打一条警告，不报错。
5. 询问框只改 `AgentUIBuilder.BuildQueryDialog` 的外观（Caution 卡 + Yes），`AgentQueryDialog` 的接口和逻辑不动，`Resize` 跟着新布局改。

# 导入器要改的地方

- `ParseChannel`：`Agent弹窗_02` / `Agent弹窗` → `LineChannel.AgentPopup`（聊天气泡）；`Agent弹窗_01` → 新增的 `LineChannel.AgentCaution`。枚举值追加在末尾，已有资产的序列化值不变。
- `IsCue`：目前只认 `Agent询问`，现在表里 S1_005、S3_011 写的是 `Agent弹窗_01_query`，会被当成普通 Agent 台词并进前一段，`S1_005` / `S3_011` 段就不存在了（S1、S3 结尾的询问会失效）。改成：以 `_query` 结尾的通道值也算询问行。这是当前 CSV 下已经存在的问题，本任务顺带修。
- 导入后的分段应和 `docs/tasks/final-script-integration.md` 的分段表一致。

# Existing reference

- `Narrative/SubtitlePanel.cs`：现在的 Agent 弹窗显示、打字机、`Pin/Unpin`、`IsShowingLine`、`ResizeAgentPopup`（按整句量高度）。
- `Narrative/Editor/NarrativeSceneBuilder.cs:83`：现在 AgentMessage 的搭法和位置（左上，宽 `HudLeftColumnWidth`，边距 `HudMargin`）。
- `Agent/AgentUIBuilder.cs:146`：询问框的搭法；`AgentQueryDialog.Resize` 的布局计算。
- `Agent/AgentUIStyle.cs`：配色常量和 UGUI 小工具（`CreateAnchored`、`AddImage`、`AddText`）。
- `Agent/NodeDetailPopup.cs:110`：`stackBelow`，详情弹窗放在 Agent 弹窗下方。改成指向滚动栏容器，高度 = 当前所有条目的总高度。
- `Narrative/Editor/DialogueCsvImporter.cs:200`：`IsCue`、`ParseChannel`。

# Files likely involved

- `Narrative/DialogueLine.cs`（`LineChannel` 追加 `AgentCaution`）
- `Narrative/Editor/DialogueCsvImporter.cs`
- 新建 `Narrative/AgentChatFeed.cs`（滚动栏：追加 / 打字机目标 / 过期 / 上移 / 清空 / Pin）
- `Narrative/SubtitlePanel.cs`（Agent 通道改为转给 `AgentChatFeed`；字幕部分不动；`IsShowingLine`、`Advance` 语义不变）
- `Narrative/Editor/NarrativeSceneBuilder.cs`（搭滚动栏容器）
- `Agent/AgentUIStyle.cs`（新配色、加载三张 Sprite）
- `Agent/AgentUIBuilder.cs`（询问框新外观；`FindAgentMessageRect` 改找滚动栏容器）
- `Agent/AgentQueryDialog.cs`（只改 `Resize` 和颜色默认值）
- `Agent/NodeDetailPopup.cs`（如果 `stackBelow` 的高度要从容器算）
- 新建 `Assets/Art/UI/AgentChat/*.png` + `.meta`
- 改完用场景生成菜单（`Core/Editor/MainSceneMenu.cs`）重建主场景

# Acceptance criteria

- 导入台词表没有警告；`S1_005`、`S3_011` 是单独的询问段，S1、S3 结尾仍弹出询问，点 Yes 进下一关。
- Intro：INTRO_002 显示为 Caution 卡（第一行 `Caution`），INTRO_003、004 显示为灰色聊天气泡，依次堆在下面；黑屏上三条都看得清；没有“没有温度的声音”署名。
- 连续多句 Agent 台词时，新消息从底部滑入，旧消息保留；最上面那条在它播完 3 s 后淡出，其余平滑上移；栏里最多 8 条。
- 打字机只在最新一条上进行；点击空白 / 空格时，打字中先补全，打完再跳句（和现在一样）。
- 字幕台词出现时，滚动栏里已有的消息照常按时间消失，不被清掉。
- Shift+数字跳关时滚动栏立刻清空，不把上一关的消息带进下一关。
- S4：每摘一只虫子，栏底的进度消息原地更新文字（1/3、2/3），不重复追加；摘完后淡出。
- 右键详情弹窗出现在滚动栏下方，不和消息重叠；栏变短后详情跟着上移。
- 询问框在屏幕右侧中部，Caution 卡 + 正文 + YES，长文本（S3_011）完整显示不溢出；悬停 YES 变品红。
- 删除三张 PNG 后进 Play 不报错，只有一条缺图警告，退回直角纯色块。
- 共享代码里不出现输入设备 API，布局不依赖屏幕像素坐标。
- 已有阶段的交互仍正常。

# Out of scope

- 不改台词文本、不改分段和播放时序、不改推进方式。
- 不加时间戳、已读、发送者名字等聊天装饰。
- 不改字幕样式。
- 不改任务面板（`AgentTaskPanel`，当前不显示）。
- 不迁 World Space（赛后技术债）。
- 不换 TextMeshPro。

# 美术分工

- 我来：三张 PNG 用脚本画（圆角气泡、圆、占位图标），先跑通。
- 用户（可选）：Agent 头像图标、Caution 小圆标图标，64×64 或 128×128 白色单色 PNG、透明底，放到 `Assets/Art/UI/AgentChat/` 覆盖同名文件（`icon_agent.png`；Caution 图标如果要不同的，命名 `icon_caution.png`，没有就复用 `icon_agent.png`）。不需要切气泡图。

# Decisions（策划已确认 10-10）

- 询问框用 Caution 卡 + YES（不是灰色气泡）。
- “最顶上的过 3s 消失”按“它的台词播完后 3 s”计时。
- 同时最多 8 条。
- 图标先用占位几何图形，正式图标由策划稍后覆盖同路径。
