# AGENTS.md

给在本仓库工作的 AI 编码助手（以及人类队友）的项目约定。动手写代码或改场景前请先读完。

## 项目背景

- 游戏：《A Gift for Ghost》，Unity 项目位于 `A_Gift_for_Ghost/`。
- 短期目标：纯 PC（键鼠）demo，参加比赛。
- 长期目标：在**同一个项目**上迁移为 Meta Quest 2 VR 游戏。
- 所以每个实现决定都要问一句：**"换成 VR 时这里要不要重写？"** 能用同样成本做成"VR 友好"的方案，就选它。

## 固定环境（不要改）

- Unity 编辑器：**6000.3.25f1**，全队一致。不要升级或降级，不要提交改动了 `ProjectSettings/ProjectVersion.txt` 的变更。
- 渲染管线：**URP**。不要引入 HDRP 或 Built-in 管线的 Shader/材质/插件。
- 渲染路径：Forward / Forward+（Quest 要求）。不要切到 Deferred。
- 输入：**新版 Input System**（Active Input Handling = Input System Package）。不要使用旧版 `UnityEngine.Input`（`Input.GetKey`、`Input.GetMouseButton`、`Input.GetAxis` 等）。
- 平台：PC 阶段可以只打 Windows 包，但不要引入只能在 Windows/桌面运行的原生插件或 API。

## 1. 输入：按"动作"抽象，不绑设备

- 所有输入都走 `Assets/InputSystem_Actions.inputactions` 中的 Action（如 `Move`、`Look`、`Interact`、`Grab`），代码只读取 Action，不直接判断按键或鼠标按钮。
- 游戏逻辑不能知道输入来自键鼠还是手柄。迁移 VR 时只给同一 Action 加 XR 控制器绑定。
- 新增操作时：先在 `.inputactions` 里加 Action 和键鼠绑定，再写代码订阅。

## 2. 交互：组件化，交互发起方与被交互物解耦

- 可交互物体实现统一接口/组件（如 `IInteractable`、`Grabbable`），挂在物体本身上，暴露 `OnInteract`、`OnGrab`、`OnRelease` 等事件。
- "谁发起交互"单独实现：PC 端是相机中心射线 / 鼠标射线；VR 端以后替换为手柄射线或直接抓取（XR Interaction Toolkit）。被交互物的代码不应改动。
- 不要在物体脚本里写死 `Camera.main` 射线检测或鼠标坐标。
- 抓取物体使用 Rigidbody + Collider，碰撞体大小贴合实际外形（VR 里玩家会用手直接碰）。

## 3. 玩家与相机

- 玩家控制器分层：根节点（身体/移动）→ 相机节点（头部视角）。移动逻辑作用在根节点，视角旋转作用在相机节点。以后整体替换为 XR Origin。
- 相机高度按真人眼高（约 1.6–1.7 m）。
- **不要强制控制玩家镜头**：避免镜头震动、强制转向、镜头摇晃/走路晃动（head bob）、FOV 动画、过场动画抢夺相机。需要引导视线时用场景中的灯光、声音、物体动画。
- 移动速度适中（步行约 1.5–3 m/s），避免高速冲刺、急加速、旋转加速度，这些在 VR 中致晕。
- 过场/叙事优先用"玩家可自由看"的场景内演出，而不是切镜头。

## 4. 尺度与场景

- **1 Unity 单位 = 1 米**，所有模型按现实尺寸制作和导入（门约 2 m 高，桌面约 0.75 m，可拿起的物品是手能握住的大小）。
- 美术导入 FBX 时检查缩放（Scale Factor / Convert Units），不要靠在场景里缩放物体凑尺寸。
- 交互物体放在站立或坐着伸手可及的高度（约 0.5–1.6 m）。

## 5. UI

- 游戏内 UI 优先用 **World Space Canvas**（挂在场景中或跟随玩家前方的面板），而不是 Screen Space Overlay 的 HUD。
- 主菜单、暂停菜单也尽量做成场景内的 3D 面板。
- 不要依赖鼠标悬停（hover）作为唯一的信息展示方式；不要把关键信息放在屏幕边缘。
- 文字字号保持在较远距离也能读清（VR 分辨率低）。
- 准星 / 交互提示可以暂时做在屏幕中心，但做成独立组件，方便以后替换为手柄射线指示。

## 6. 性能预算（按 Quest 2 定）

PC demo 可以在此基础上提高画质，但**核心资源必须能降回这个预算**：

- 同屏三角面：约 50 万–100 万以内；Draw Call / SetPass：约 100–200 以内。
- 光照：以**烘焙光照**为主，实时光源尽量少，实时阴影只给少数关键光源。
- 后处理：少用或可关闭，尤其 SSAO、景深、动态模糊、体积雾、屏幕空间反射。动态模糊和景深在 VR 中必须关闭。
- 贴图：一般 1K–2K，避免大量 4K；尽量使用图集、共享材质，便于合批。
- Shader：使用 URP Lit / Simple Lit / Unlit 或 Shader Graph（URP 目标）；避免多 Pass、大量透明叠加（overdraw）、高开销全屏特效。
- 透明物体和粒子数量要克制。
- 画质差异通过 URP 的 Quality 等级 / `PC_RPAsset` 与 `Mobile_RPAsset` 切换，而不是给 PC 单独做一套资源。

## 7. 代码与资源组织

- 脚本放在 `Assets/Scripts/`，按功能分子目录（如 `Player/`、`Interaction/`、`UI/`、`Core/`）。
- 平台相关代码（PC 输入/相机 vs 以后 VR）放在独立目录或用独立组件，不要在通用逻辑里写 `if (isVR)` 分支散落各处。
- 不要依赖帧率写逻辑：移动用 `Time.deltaTime`，物理放 `FixedUpdate`。Quest 目标帧率为 72/90 Hz。
- 第三方插件引入前确认：支持 URP、支持 Android（ARM64）。

## 8. Git 协作

- 仓库根目录是 `D:\2026_Tripothon`（即 GitHub 仓库根），Unity 项目在子目录 `A_Gift_for_Ghost/`。
- 已配置 `.gitignore`（忽略 `Library/`、`Temp/`、`Logs/`、`UserSettings/`、IDE 文件等）和 `.gitattributes`（二进制资源走 Git LFS，场景/Prefab 用 UnityYAMLMerge）。新增二进制格式时，先在 `.gitattributes` 中加 LFS 规则再提交。
- 每个资源都必须和它的 `.meta` 文件一起提交、一起移动、一起删除。在 Unity 编辑器内移动/重命名资源，不要在文件管理器里直接操作。
- 不要提交空文件夹的 `.meta`（Git 不跟踪空文件夹，会导致队友那边 `.meta` 被删除并报警告）。
- 避免多人同时编辑同一个场景或 Prefab。把功能拆成 Prefab，各自负责；主场景改动先在群里说一声。
- 不要提交个人文件夹（如根目录下的 `美术参考-Xie/`）和打包产物。
- 只在用户明确要求时提交和推送；推送前先 `git pull`。

## 迁移到 VR 时的预期改动（供参考）

如果以上约定都遵守了，迁移时只需要：

1. 安装 XR Plugin Management + OpenXR + Meta XR / XR Interaction Toolkit，打 Android 平台。
2. 用 XR Origin 替换 PC 玩家控制器的相机部分。
3. 给现有 Input Action 增加 XR 控制器绑定，用 XR 交互器替换 PC 的射线交互发起方。
4. 切换到移动端画质等级，做性能调优。

任何会让上面这份清单变长的实现，都应该先和团队确认。
