# Goal

关卡画面四周套一圈"流动的压暗边"（参考图：小红书截图，四周一圈不规则、慢慢蠕动的暗边，带一点模糊）。暗边颜色跟着画面明暗反着来：亮背景上压黑，黑背景上（Intro 黑屏等）微微发白。

# Existing reference

- 渲染器：`Assets/Settings/PC_Renderer.asset`、`Assets/Settings/Mobile_Renderer.asset`（已有一个 Renderer Feature，新增的加在后面）
- 着色器写法参考 `Assets/Art/Morph/MorphLine.shader`（URP、无光照、支持单通道立体渲染）
- URP 自带 `Full Screen Pass Renderer Feature`：挂一个全屏材质即可，不用自己写 ScriptableRendererFeature
- 不用 Volume 自带的 Vignette：它只能压固定颜色、边缘是规则椭圆，做不到"流动"和"按亮度反色"。`SampleSceneProfile` 里已有的 Vignette（intensity 0.2）保持原样或关掉，二选一，别叠两层

# Files likely involved

- `Assets/Art/PostFX/EdgeVignette.shader`（新增：全屏着色器，HLSL，`Blit.hlsl` 的 `_BlitTexture` 采样画面）
- `Assets/Art/PostFX/EdgeVignette.mat`（新增）
- `PC_Renderer.asset` / `Mobile_Renderer.asset`（加 Full Screen Pass，Injection Point = After Rendering Post Processing，Requirements = Color）
- 可选：`Assets/Scripts/Visual/EdgeVignetteControl.cs`（新增：运行时改强度 / 开关，例如某一关想加重）

# Shader 思路

1. 遮罩：以屏幕中心为原点算到边缘的距离（按画面宽高比修正，做成圆角矩形 / 椭圆），`smoothstep(inner, outer, d)` 得到 0（中心）→ 1（边缘）。
2. 流动：用低频噪声扰动边界——按极角 `atan2` 采样 2 层 value noise / 程序化噪声（不用贴图也行），随 `_Time.y * flowSpeed` 漂移，扰动量叠到 `d` 上。边缘因此不规则且缓慢蠕动，不是整体缩放呼吸。
3. 反色：取当前像素亮度 `L = dot(color, (0.2126, 0.7152, 0.0722))`。目标色 = `L > pivot ? darkColor : lightColor`，用 `smoothstep` 做软过渡避免分界处闪。黑色背景上目标色是灰白（不是纯白，强度上限单独控制），白色背景上是近黑。
4. 混合：`lerp(color, target, mask * intensity)`；黑底时用 `lightIntensity`（默认较小，只"一点点发白"），亮底时用 `darkIntensity`。
5. 模糊（可选，默认 PC 开、Mobile 关）：边缘区按 mask 做少量偏移采样（4–8 tap）模拟参考图的边缘虚化。中心区 mask=0 时跳过，省带宽。

# 材质参数（建议默认值，Play 里调）

- `_Inner` 0.55 / `_Outer` 1.05：暗边开始和最深的位置
- `_NoiseScale` 3、`_NoiseAmount` 0.08、`_FlowSpeed` 0.15：边缘起伏程度和流速（要慢，像雾在边上爬）
- `_DarkColor` (0.05,0.05,0.05)、`_DarkIntensity` 0.85
- `_LightColor` (0.85,0.85,0.85)、`_LightIntensity` 0.25
- `_Pivot` 0.5、`_PivotSoftness` 0.2
- `_BlurAmount` 0（Mobile）/ 小值（PC）

# Acceptance criteria

- S1–S4、Tutorial、Pick 画面四周都有暗边，中心区域（植株主体、节点）完全不受影响。
- 边缘形状不规则、随时间缓慢流动，看不到明显的循环跳变或整体缩放。
- 浅色背景处边缘压暗；Intro 黑屏 / 暗背景处边缘微微发白，不抢字幕。同一帧画面一侧亮一侧暗时，两侧各自正确反色，分界处不闪。
- 改窗口宽高比（16:9、21:9、4:3）暗边仍贴合四边，不变形成拉伸的椭圆。
- 底部字幕、左侧弹窗、准星等 Screen Space Overlay UI 不被压暗（Overlay 在后处理之后绘制，应该天然满足；确认一下）。
- 可以整体关闭：在 Renderer 上取消勾选该 Feature 即恢复原画面，没有其他依赖。
- VR 兼容：着色器支持单通道立体渲染（`UNITY_VERTEX_OUTPUT_STEREO` / XR 宏，参考 MorphLine）；遮罩按每只眼各自的屏幕坐标算。Mobile_Renderer 上关闭模糊，单 Pass、无额外 RT。
- 已有阶段的交互、对白和过场正常，帧率没有明显下降。

# Out of scope

- 不做随关卡切换的不同暗边风格（需要再加 `EdgeVignetteControl` 另开任务）。
- 不改场景光照、背景颜色和其他后处理（Bloom、Tonemapping 等）。
- 不改 UI 的 Screen Space Overlay → World Space 迁移。
- 不做 VR 舒适度用的移动暗角（那是另一种功能，迁移时再定）。
