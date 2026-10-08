# Goal

右键查看节点详情时，标题用策划表里每个阶段对应的节点名称：S2 用项目名（如“能源输入系统”），S3 用物理描述名（如“浅褐色丝状物”），S4 及以后用真实部位名（如“根”）。正文用 `docs/script/03_node_text.csv` 的最新文案。

# Existing reference

- `Assets/Scripts/Narrative/NodeDetailTable.cs`：部位 × 深度 → 正文，已经是数据驱动，名称照这个结构加字段。
- `Assets/Scripts/Stages/S2CircuitStage.cs` 的 `SystemName()`、`Assets/Scripts/Stages/BugStageUtil.cs` 的 `PhysicalTitle()`：现在名称写死在代码里，改成读表。
- `Assets/Scripts/Narrative/Editor/NarrativeAssets.cs`：编辑器菜单生成资产的写法。

# Files likely involved

- `Assets/Scripts/Narrative/NodeDetailTable.cs`：`Entry` 新增 `projectName` / `physicalName` / `realName` 三个字段，加 `GetName(organ, depth)`。
- `Assets/Scripts/Stages/S2CircuitStage.cs`：`PopupTitle` 改为读表。
- `Assets/Scripts/Stages/BugStageUtil.cs`：`PhysicalTitle` 拆成 S3 用的物理名和 S4 用的真实名。
- `Assets/Scripts/Stages/S3NetworkStage.cs`、`Assets/Scripts/Stages/S4GeometricStage.cs`：分别调用对应的名称。
- 新建 `Assets/Scripts/Narrative/Editor/NodeTextCsvImporter.cs`：菜单 `Ghost/Narrative/Import Node Text CSV`，按列名读取 `docs/script/03_node_text.csv`，写入 `Assets/Data/Narrative/NodeDetails.asset`。
- `Assets/Data/Narrative/NodeDetails.asset`：导入后更新。

# CSV 列 → 字段

| CSV 列 | 字段 | 用在 |
|---|---|---|
| S2 名称 | projectName | S2 标题 |
| S2 缺水（待补充资源） | project | S2 正文（缺水 / 未修复时） |
| S3名称 | physicalName | S3 标题 |
| S3+ physical | physical | S3、S4 正文 |
| （无列）| realName | S4 标题；CSV 说明写的是“S4 名称改成真正的部位名称”，先用部位名（根、茎、叶片、花苞、果实、小虫、土壤）当默认值 |

“（说明）”这一行和“待确认”列导入时跳过。部位按第一列括号里的英文名对应 `Organ` 枚举。

# Acceptance criteria

- 执行导入菜单后，`NodeDetails.asset` 里 7 个部位的名称和正文都和 CSV 一致，`[占位]` 前缀消失。
- S2 右键根部：标题是“能源输入系统”。
- S3 右键根部：标题是“浅褐色丝状物”，正文是 CSV 的 physical 文本；右键虫子时，“已标记 x / y”这一行仍在。
- S4 右键根部：标题是“根”。
- 某个名称留空时，回退到旧的硬编码名称，不显示空标题。
- CSV 改了之后重新导入就能生效，不用改代码。
- 已有阶段的交互仍正常。

# Open decision（实现前确认，默认值见括号）

- 标题里还保留 `#id` 吗？（默认：S2 保留，因为是系统视角；S3/S4 去掉，这两关是“用自己的眼睛看”，编号会出戏。）
- S2 正文里“运作正常 / 缺水”的切换：CSV 写的是“不缺水 = 运作正常，缺水 = 待补充资源 + 描述”，现在的代码是固定显示描述再加一行状态。（默认：本任务只换文案、不改逻辑，正文切换等策划答复哪些节点缺水后另开任务。）

# Out of scope

- S1 status 文案（CSV 里还是“待确认”）。
- 台词表 `02_dialogue.csv` 的导入。
- 修改哪些节点缺水的规则。
