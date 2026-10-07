# VR Guidelines

当前开发目标是 PC，但同一个项目之后要迁移到 Meta Quest 2。写代码、搭场景、做 UI 和美术资源时按本文件执行，降低迁移成本。全局硬性规则见 [AGENTS.md](../AGENTS.md)，输入抽象和交互发起方的具体实现见 [ARCHITECTURE.md](ARCHITECTURE.md)。

判断标准：每个实现都问一句"换成 VR 时这里要不要重写？"。成本相同时，选 VR 友好的方案。

## 1. 渲染

- 只用 URP。不引入 HDRP 或 Built-in 管线的 Shader、材质、插件。
- 渲染路径用 Forward / Forward+（Quest 要求），不要切到 Deferred。
- PC 和 Quest 的画质差异通过 Quality 等级（`PC_RPAsset` / `Mobile_RPAsset`）切换，不给 PC 单独做一套资源。

## 2. 输入与交互

- 输入只读 Input Action，游戏逻辑不知道输入来自键鼠还是手柄。新增操作时先在 `.inputactions` 里加 Action 和键鼠绑定，再写代码订阅。
- 交互发起方（PC：`PointerInput` 鼠标射线；VR：手柄射线或直接抓取）和被交互物分开。被交互物实现 `IInteractable` 或通过 `NodePicker` 被选中，自己不做射线检测，不读 `Camera.main` 或鼠标坐标。
- 鼠标悬停（hover）只能用来高亮，不能作为唯一的信息展示方式。
- 以后可以用手抓的物体：Rigidbody + 贴合外形的 Collider。

## 3. 相机与舒适度

- 玩家结构分层：根节点（身体/移动）→ 相机节点（头）。迁移时整体替换为 XR Origin。
- 相机高度按真人眼高（约 1.6–1.7 m）。
- 不强制控制玩家镜头：不做镜头震动、强制转向、走路晃动（head bob）、FOV 动画，不让过场动画接管相机。需要引导视线时用灯光、声音、物体动画。
- 移动速度适中（步行约 1.5–3 m/s），避免高速冲刺、急加速和旋转加速度。
- 叙事演出优先放在场景里，让玩家可以自由看，不切镜头。
- 当前项目的相机固定不动，构图靠 `PlantFit` 移动和缩放植株，这和 VR 兼容：迁移后由头显决定视角，植株仍在世界空间里。

## 4. 尺度与场景

- 1 Unity 单位 = 1 米。模型按现实尺寸制作和导入（门约 2 m，桌面约 0.75 m，手持物品按手掌大小）。
- 导入 FBX 时检查 Scale Factor / Convert Units，不要靠在场景里缩放物体凑尺寸。Tripo 生成的模型是归一化尺寸，导入后要按真实尺寸校正。
- 交互物放在站立或坐着伸手可及的高度（约 0.5–1.6 m）。

## 5. UI

- 游戏内 UI 优先用 World Space Canvas，主菜单和暂停菜单也尽量做成场景内的 3D 面板。
- 不把关键信息放在屏幕边缘；字号要保证稍远距离也看得清（VR 分辨率低）。
- 准星、交互提示可以暂时放在屏幕中心，但要做成独立组件，以后替换为手柄射线指示。
- 【比赛期技术债】当前 HUD 是 Screen Space Overlay，赛后改回 World Space。新增 UI 不要再加深对 Screen Space 的依赖（例如不要写依赖屏幕像素坐标的布局逻辑）。

## 6. 性能预算（按 Quest 2）

PC demo 可以在此基础上提高画质，但核心资源必须能降回这个预算：

- 同屏三角面约 50 万–100 万以内；Draw Call / SetPass 约 100–200 以内。
- 光照以烘焙为主，实时光源尽量少，实时阴影只给少数关键光源。
- 后处理少用或做成可关闭，尤其 SSAO、景深、动态模糊、体积雾、屏幕空间反射。动态模糊和景深在 VR 中必须关闭。
- 贴图一般 1K–2K，避免大量 4K；尽量用图集、共享材质，方便合批。节点渲染已用 GPU instancing，新增大量重复物体也应走 instancing。
- Shader 用 URP Lit / Simple Lit / Unlit 或 URP 目标的 Shader Graph，避免多 Pass、大面积透明叠加（overdraw）和高开销全屏特效。
- 透明物体和粒子数量要克制。

## 7. 代码

- PC 专用代码和以后的 XR 代码放在独立组件或目录，不在通用逻辑里散落 `if (isVR)` 分支。
- 不依赖帧率：移动用 `Time.deltaTime`，物理放 `FixedUpdate`。Quest 目标帧率 72/90 Hz。
- 引入第三方插件前确认：支持 URP，支持 Android（ARM64）。不在共享系统里用 Windows 专用 API。

## 8. 迁移时的预期改动

如果以上规则都遵守了，迁移时只需要：

1. 安装 XR Plugin Management + OpenXR + Meta XR / XR Interaction Toolkit，切到 Android 平台。
2. 用 XR Origin 替换相机。
3. 给现有 Input Action 增加 XR 控制器绑定；用手柄射线发起方替换 `PointerInput`，保持事件签名不变。
4. HUD 改为 World Space。
5. 切到移动端画质等级，做性能调优。

任何会让这份清单变长的实现，都应该先和团队确认。
