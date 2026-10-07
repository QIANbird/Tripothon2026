# AGENTS.md

本文件只记录所有 AI 编码助手都必须遵守的**全局、不易变化的规则**。  
项目现状、架构细节和具体工作流程不要写在这里，按下面的文档路由读取。

## Project

- Unity project: `A_Gift_for_Ghost/`
- Unity version: **6000.3.25f1**
- Render pipeline: **URP**
- Current development target is PC, but the same project will later target **Meta Quest 2 VR**.
- Prefer implementations that keep gameplay logic independent from the input device.

## Non-negotiable engineering rules

- Do not upgrade/downgrade Unity or change `ProjectSettings/ProjectVersion.txt`.
- Use the **new Input System**. Do not use legacy `UnityEngine.Input`.
- Gameplay logic must not depend directly on keyboard, mouse, camera raycasts, or XR controllers.
- Keep platform-specific PC / XR code separate from shared gameplay logic.
- Do not introduce Windows-only APIs or plugins into shared systems.
- Preserve Unity `.meta` files when moving, adding, or deleting assets.
- Do not make unrelated refactors while implementing or debugging a scoped task.
- Do not commit or push unless the user explicitly asks.

## Context routing

Read only the documents relevant to the current task:

- Current implemented state → `docs/CURRENT_STATE.md`
- System ownership, interfaces and data flow → `docs/ARCHITECTURE.md`
- VR-migration rules (camera, scale, UI, Quest 2 performance budget) → `docs/VR_GUIDELINES.md` — read before adding scenes, cameras, UI, art assets, shaders or lighting
- `docs/PROJECT_SUMMARY.md` is a human-facing design document. Agents do not maintain it.

Workflow protocols:
- Debugging → `docs/protocols/DEBUG.md`
- Git / checkpoints / branching → `docs/protocols/VERSIONING.md`
- End-of-task state maintenance → `docs/protocols/CLOSEOUT.md`


For a scoped feature or bug, read its file under `docs/tasks/` before editing.

## Working rule

Do not read every project document by default.

Start from the current task and the relevant documents above.  
Search outward only when the required information is missing.

Prefer existing working implementations in this repository over introducing a new pattern.

When a task is complete, follow `CLOSEOUT.md` to determine whether `CURRENT_STATE.md`, `ARCHITECTURE.md`needs updating.