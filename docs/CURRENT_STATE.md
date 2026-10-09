# Current State

快照日期：2026-10-08 · `dev/auto` 已合并到 `main`

## Current stage

第一阶段（比赛 demo 主流程 G0–G9）开发完成。S1–S4 可以玩通；Transition / Pick / Outro 还是占位阶段（按 N 继续）。
最新一轮 UX 调整（HUD、右键详情、PlantFit 适配、Gizmos 竖线）已通过 Play 验收（10-08）。

## Features currently working

- 流程：Intro（黑屏 + 字幕）→ Tutorial → S1 Matrix → S2 Circuit → S3 Network → S4 Geometric → 占位阶段。进关和变形同时发生。
- 调试：N 下一关，Shift+1–9 跳关（两个方向都能干净进出）。
- 节点问题：Easy / Medium / Hard 三档；Medium 两次点击之间隔 5 s 再闪；Hard 永远解决不了。
- 左键点节点：授权 Agent 处理一次，同时触发亮度脉冲反馈。
- 右键按住：左侧 HUD 弹出详情，松开 2.5 s 后淡出；详情深度随阶段变化（Status → Project → Physical）。标题按阶段取名：S2 项目名、S3 外观名、S4 真实部位名（不带编号），来自 `NodeDetails.asset`，可用菜单 Ghost/Narrative/Import Node Text CSV 从 `docs/script/03_node_text.csv` 导入。
- S2：脉冲引路（`docs/tasks/S2_PULSE_ROUTE.md`）。脉冲源蓝色闪烁 + 外圈呼吸发光，段内节点外圈依次亮起并慢慢衰减（`NodeHaloRenderer`），每段最多 5 个节点；拖拽时一条蓝线连着最后连上的节点和指针；连线进关时是直角折线，每结束一次拖拽按连通比例往两点直连过渡，全部连通时变成树形直线（S3 变形从这里开始）；从脉冲源按住沿脉冲拖过这一段连上（线和节点变蓝，缺水恢复），终点成为下一个脉冲源，自动指向最近的缺水节点；中途松开不回退。彩椒上共 16 段。果实不在线路上；右键查看果实是通关条件之一。已通过 Play 验收（10-09）；intro 占位对白仍写"从亮着的根部按住"。
- S3：拖空白处旋转网络，左键标记 3 个虫子节点，弹出 AI 询问，选 Yes 进下一关。
- S4：旋转找叶背虫子并点击摘除（含虫子 FBX 实例），每摘一只写实度提高一档。
- 对白分流：亲切的声音 = 底部字幕（浅色背景黑字、黑屏时白字）；没有温度的声音 = 左侧弹窗。
- 台词表：`docs/script/02_dialogue.csv` 在编辑器里自动导入（也可用菜单 Ghost/Narrative/Import Dialogue CSV），生成 `Assets/Data/Narrative/Script/*.asset` 和 `DialogueLibrary.asset`；台词可以单独指定通道（字幕 / Agent 弹窗）。阶段脚本还没改用这些资产，仍引用旧的占位对白。
- 对白推进：左键点空白处或按空格 / 回车，打字中先补全整句，再点进入下一句；点中节点时只算点节点（全黑屏时除外）。
- PlantFit：相机固定（眼高 1.6 m、水平正视），按形态缩放、居中植株。

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

- 相机永远不动。构图靠 PlantFit 缩放和平移植株（`docs/VR_GUIDELINES.md` 第 3 节）。
- 输入只读 Input Action（`Gameplay` / `Debug` 表），不读设备。
- 节点不是 GameObject：拾取走 `NodePicker`，显示走 `NodeMorpher` 的状态接口，不要给节点加 Collider。
- 场景由代码生成，不要手动改 `Main.unity`；改场景就改 `MainSceneMenu` 再跑菜单。
- 阶段 Enter 时订阅、Exit 时退订，并调用 `ctx.ResetShared()`，保证跳关可逆。
- 不重新采样 `pepper_plant`，除非同时重查 S1/S2 的节点 id。
- 【比赛期技术债】UI 是 Screen Space Overlay HUD，赛后改回 World Space（`docs/VR_GUIDELINES.md` 第 5 节）。
- 不要提交 `Packages/manifest.json`、`packages-lock.json` 里的 `com.tripo3d.unitybridge` 一行（本机路径）。`runInBackground: 1` 已提交（10-08），让编辑器失焦时 Play 模式和 MCP 照常运行。

## Next planned milestone

