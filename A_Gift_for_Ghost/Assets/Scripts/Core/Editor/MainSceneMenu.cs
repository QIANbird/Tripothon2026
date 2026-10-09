using System.Collections.Generic;
using Ghost.Agent;
using Ghost.Gameplay;
using Ghost.Interaction;
using Ghost.Morph;
using Ghost.Morph.EditorTools;
using Ghost.Narrative.EditorTools;
using Ghost.Pick;
using Ghost.Player;
using Ghost.Player.PC;
using Ghost.Stages;
using Ghost.Stages.EditorTools;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Animations.Rigging;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace Ghost.Core.EditorTools
{
    // 一键生成主场景 Assets/Scenes/Main.unity：相机和台面与 MorphPrototype 一致，
    // 加上 GameFlow、全部占位阶段、World Space 阶段面板和调试跳关。已存在则覆盖。
    public static class MainSceneMenu
    {
        const string MainScenePath = "Assets/Scenes/Main.unity";
        const string SampledPlantPath = "Assets/Data/Morph/pepper_plant.asset";
        const string FakePlantPath = "Assets/Data/Morph/FakePlant.asset";
        const string InputActionsPath = "Assets/InputSystem_Actions.inputactions";
        // G9：S4 叶背上的真实虫子模型（Tripo 生成）
        const string BugModelPath = "Assets/3D_Objects/bug/tripo_convert_cbdef70f-0cb3-4f3b-8ed2-3e0794432c86.fbx";
        // Pick：前臂 + 手的模型（Tripo 生成，静态网格，没有骨骼）
        const string ArmModelPath = "Assets/3D_Objects/hand/tripo_convert_07d6ebfa-74e6-4ae4-a1a9-7c87fa196291.fbx";

        // 阶段配置：名字、是否变形、形态、占位文字。加阶段时在这里加一行，再执行菜单
        struct StageSpec
        {
            public string name;
            public bool changesForm;
            public MorphForm form;
            public string message;

            public StageSpec(string name, bool changesForm, MorphForm form, string message)
            {
                this.name = name;
                this.changesForm = changesForm;
                this.form = form;
                this.message = message;
            }
        }

        static readonly StageSpec[] Stages =
        {
            // Intro 不变形，停在植株的初始形态（Matrix）；以后开场做黑屏时再处理显示
            new StageSpec("Intro", false, MorphForm.Matrix, "开场剧情：按 N 继续"),
            // G6：Tutorial 和 S1 是真实阶段（TutorialStage / S1MatrixStage），占位文字不显示
            new StageSpec("Tutorial", true, MorphForm.Matrix, ""),
            new StageSpec("S1", true, MorphForm.Matrix, ""),
            // G7：S2 是真实阶段（S2CircuitStage）
            new StageSpec("S2", true, MorphForm.Circuit, ""),
            // G8：S3 是真实阶段（S3NetworkStage）
            new StageSpec("S3", true, MorphForm.Network, ""),
            // G9：S4 是真实阶段（S4GeometricStage）
            new StageSpec("S4", true, MorphForm.Geometric, ""),
            new StageSpec("Transition", true, MorphForm.Real, "过渡：按 N 继续"),
            // Pick：第一人称走到写实植株前（PickStage，docs/tasks/pick-stage.md）
            new StageSpec("Pick", true, MorphForm.Real, ""),
            new StageSpec("Outro", true, MorphForm.Real, "结局剧情（最后一关）"),
        };

        [MenuItem("Ghost/Core/Build Main Scene")]
        public static void BuildMainScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            BuildMainScene(NewSceneMode.Single);
        }

        public static UnityEngine.SceneManagement.Scene BuildMainScene(NewSceneMode mode)
        {
            // 和原型场景一样：优先用彩椒采样的节点集，没有时用假植物
            var set = AssetDatabase.LoadAssetAtPath<PlantNodeSet>(SampledPlantPath);
            if (set == null) set = AssetDatabase.LoadAssetAtPath<PlantNodeSet>(FakePlantPath);
            if (set == null) set = MorphMenu.GenerateFakePlant();
            var material = MorphMenu.EnsureNodeMaterial();
            var actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
            if (material == null || actions == null)
            {
                Debug.LogError("[Flow] 缺少节点材质或 InputSystem_Actions，场景未生成");
                return default;
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, mode);
            UnityEngine.SceneManagement.SceneManager.SetActiveScene(scene);
            var background = new Color(0.85f, 0.85f, 0.86f);
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = background;

            // 固定相机：眼高 1.6 m，水平正视 +Z（不俯仰，docs/VR_GUIDELINES.md 第 3 节）。植株的大小和位置由 PlantFit 按形态适配
            var cameraGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var camera = cameraGo.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = background;
            camera.fieldOfView = 60f;
            camera.nearClipPlane = 0.05f;
            cameraGo.AddComponent<AudioListener>();
            cameraGo.transform.position = new Vector3(0f, 1.6f, -1.9f);
            cameraGo.transform.rotation = Quaternion.identity;

            // PlantFit：植株的适配根（缩放 + 平移），Plant 是它的子物体、本地变换为单位变换
            var fitGo = new GameObject("PlantFit");
            var plantGo = new GameObject("Plant");
            plantGo.transform.SetParent(fitGo.transform, false);
            var morpher = plantGo.AddComponent<NodeMorpher>();
            morpher.nodeSet = set;
            morpher.material = material;
            morpher.mesh = MorphMenu.BuiltinCube();
            morpher.startForm = MorphForm.Matrix;

            var links = plantGo.AddComponent<NodeLinkRenderer>();
            links.material = MorphMenu.EnsureLineMaterial();
            var halos = plantGo.AddComponent<NodeHaloRenderer>();
            halos.material = links.material;

            // G3：节点问题系统（闪烁 / 尝试 / 解决），和 NodeMorpher 挂在同一物体上
            var issues = plantGo.AddComponent<NodeIssueSystem>();
            issues.morpher = morpher;

            var handoff = plantGo.AddComponent<RealModelHandoff>();
            handoff.revealShader = Shader.Find("Ghost/RevealLit");
            // 注意：不加 MorphDebugSwitcher。主场景的形态只由 GameFlow 控制

            var fit = fitGo.AddComponent<PlantFit>();
            fit.morpher = morpher;
            fit.viewCamera = camera;
            // 植株居中；立体形态用包围球，S3 旋转后仍全部入画。Play 时 Start 会按实际屏幕比例再算一次
            fit.hudLeftFraction = 0f;
            fit.rightMarginFraction = 0f;
            fit.trimLow = 0f;
            fit.trimHigh = 1f;
            fit.ClearCache();
            fitGo.transform.localScale = Vector3.one;

            // G2：指针输入（节点拾取 + IInteractable 射线）和光标
            var pointer = BuildPointer(camera, morpher, actions);
            // G8：旋转植株（S3 / S4 共用），绕布局中心转，挂在 Plant 上
            var rotator = plantGo.AddComponent<TargetRotator>();
            rotator.target = plantGo.transform;
            rotator.pointer = pointer;
            rotator.viewCamera = camera;
            rotator.localPivot = new LayoutGenerator.Settings().center;
            // G5：对白播放器和字幕 / Agent 剧情弹窗（先建：详情弹窗要排在 Agent 剧情弹窗下方）
            var dialogue = NarrativeSceneBuilder.EnsureDialogue(cameraGo.transform);
            // 对白推进：点击空白处或空格 / 回车（节点优先）
            var advance = dialogue.gameObject.AddComponent<DialogueAdvanceInput>();
            advance.pointer = pointer;
            advance.subtitles = Object.FindAnyObjectByType<Ghost.Narrative.SubtitlePanel>(FindObjectsInactive.Include);
            advance.actions = actions;
            // G4：Agent 任务面板、节点详情弹窗、AI 询问框（HUD），以及 EventSystem
            var agentUI = AgentUIBuilder.BuildAll(null, camera);

            // 流程：每个阶段一个子物体
            var flowGo = new GameObject("GameFlow");
            var flow = flowGo.AddComponent<GameFlow>();
            flow.morpher = morpher;

            // G6：阶段共用的引用
            var ctx = flowGo.AddComponent<StageContext>();
            ctx.flow = flow;
            ctx.morpher = morpher;
            ctx.links = links;
            ctx.halos = halos;
            ctx.issues = issues;
            ctx.pointer = pointer;
            ctx.picker = pointer.picker;
            ctx.taskPanel = agentUI.taskPanel;
            ctx.detailPopup = agentUI.detailPopup;
            ctx.query = agentUI.query;
            ctx.dialogue = dialogue;
            ctx.detailTable = NarrativeAssets.EnsureNodeDetails();
            ctx.rotator = rotator;
            // 任务面板在进入真实阶段时才显示
            agentUI.taskPanel.Hide();
            // 旧对白资产里"点击查看"的占位台词改成右键（不覆盖策划改过的句子）
            StageAssets.MigrateRightClickInspect();
            var stageList = new List<Stage>();
            PickStage pickStage = null;
            foreach (var spec in Stages)
            {
                var stageGo = new GameObject(spec.name);
                stageGo.transform.SetParent(flowGo.transform, false);
                Stage stage;
                if (spec.name == "Intro")
                {
                    // G5：开场用 IntroStage（黑屏 + 对白字幕，播完自动进入教学）
                    stage = NarrativeSceneBuilder.AddIntroStage(stageGo, cameraGo.transform);
                }
                else if (spec.name == "Tutorial")
                {
                    stage = BuildTutorialStage(stageGo, ctx, set);
                }
                else if (spec.name == "S1")
                {
                    var s1 = stageGo.AddComponent<S1MatrixStage>();
                    s1.ctx = ctx;
                    stage = s1;
                }
                else if (spec.name == "S2")
                {
                    var s2 = stageGo.AddComponent<S2CircuitStage>();
                    s2.ctx = ctx;
                    s2.introSequence = StageAssets.EnsureS2Intro();
                    s2.wrongStartSequence = StageAssets.EnsureS2WrongStart();
                    s2.waterSolvedSequence = StageAssets.EnsureS2WaterSolved();
                    stage = s2;
                }
                else if (spec.name == "S3")
                {
                    var s3 = stageGo.AddComponent<S3NetworkStage>();
                    s3.ctx = ctx;
                    s3.rotator = rotator;
                    s3.introSequence = StageAssets.EnsureS3Intro();
                    s3.allFoundSequence = StageAssets.EnsureS3AllFound();
                    stage = s3;
                }
                else if (spec.name == "S4")
                {
                    var s4 = stageGo.AddComponent<S4GeometricStage>();
                    s4.ctx = ctx;
                    s4.rotator = rotator;
                    s4.introSequence = StageAssets.EnsureS4Intro();
                    s4.firstRemovedSequence = StageAssets.EnsureS4FirstRemoved();
                    // 虫子模型：组件挂在 S4 物体上，实例生成在 Plant 下面（跟着旋转）
                    var bugPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BugModelPath);
                    if (bugPrefab != null)
                    {
                        var bugModels = stageGo.AddComponent<BugModelInstances>();
                        bugModels.morpher = morpher;
                        bugModels.bugPrefab = bugPrefab;
                        s4.bugModels = bugModels;
                    }
                    else Debug.LogWarning($"[Flow] 找不到虫子模型 {BugModelPath}，S4 只显示虫子节点");
                    stage = s4;
                }
                else if (spec.name == "Pick")
                {
                    pickStage = BuildPickStage(stageGo, ctx, fit, rotator, camera, actions);
                    stage = pickStage;
                }
                else
                {
                    var placeholder = stageGo.AddComponent<PlaceholderStage>();
                    placeholder.message = spec.message;
                    stage = placeholder;
                }
                stage.stageName = spec.name;
                stage.changesForm = spec.changesForm;
                stage.form = spec.form;
                stageList.Add(stage);
            }
            flow.stages = stageList.ToArray();

            var debug = flowGo.AddComponent<GameFlowDebug>();
            debug.flow = flow;
            debug.actions = actions;

            BuildStagePanel(flow, cameraGo.transform);
            // 场景保存时玩家根物体是停用的，Pick 阶段 Enter 时才打开
            if (pickStage != null) pickStage.player.gameObject.SetActive(false);

            EditorSceneManager.SaveScene(scene, MainScenePath);
            Debug.Log($"[Flow] 生成主场景 → {MainScenePath}。Play 后按 N 进入下一关，Shift + 1–9 跳关");
            return scene;
        }

        // 教学阶段（docs/tasks/tutorial-single-cube.md）：一个演示方块，取矩阵中部的普通节点（不用果实，
        // S1 的困难问题在果实上）。对白来自台词表 TUT 段导入的 Script/TUT_*.asset
        static TutorialStage BuildTutorialStage(GameObject stageGo, StageContext ctx, PlantNodeSet set)
        {
            var stage = stageGo.AddComponent<TutorialStage>();
            stage.ctx = ctx;
            stage.introSequence = StageAssets.LoadScript(StageAssets.TutorialIntroKey);
            stage.afterEasySequence = StageAssets.LoadScript(StageAssets.TutorialAfterEasyKey);
            stage.afterHardSequence = StageAssets.LoadScript(StageAssets.TutorialAfterHardKey);
            stage.inspectSequence = StageAssets.LoadScript(StageAssets.TutorialInspectKey);
            stage.afterInspectSequence = StageAssets.LoadScript(StageAssets.TutorialAfterInspectKey);

            var used = new HashSet<int>();
            stage.demoNode = NearestInMatrix(set, new Vector2(0.5f, 0.55f), n => n.organ != Organ.Fruit && n.organ != Organ.Bug, used);
            if (stage.demoNode < 0) stage.demoNode = NearestInMatrix(set, new Vector2(0.5f, 0.55f), null, used);
            return stage;
        }

        // 和 StageContext.NearestNodeInLayout 相同的规则，但在编辑器里直接读节点集
        static int NearestInMatrix(PlantNodeSet set, Vector2 normalized, System.Predicate<PlantNode> filter, HashSet<int> used)
        {
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue), max = -min;
            foreach (var n in set.nodes)
            {
                Vector3 p = n.GetPose(MorphForm.Matrix).position;
                min = Vector2.Min(min, p);
                max = Vector2.Max(max, p);
            }
            Vector2 target = new Vector2(Mathf.Lerp(min.x, max.x, normalized.x), Mathf.Lerp(min.y, max.y, normalized.y));
            int best = -1;
            float bestDist = float.MaxValue;
            foreach (var n in set.nodes)
            {
                if (used.Contains(n.id) || (filter != null && !filter(n))) continue;
                float d = ((Vector2)n.GetPose(MorphForm.Matrix).position - target).sqrMagnitude;
                if (d < bestDist) { bestDist = d; best = n.id; }
            }
            if (best >= 0) used.Add(best);
            return best;
        }

        // G2：Pointer 物体挂 NodePicker、PointerInput、PointerCursor（只用系统光标）和 PointerDebugLogger。
        // G6 起日志默认关闭（PointerDebugLogger.logEvents = false），调试时在 Inspector 里勾上
        static PointerInput BuildPointer(Camera camera, NodeMorpher morpher, InputActionAsset actions)
        {
            var pointerGo = new GameObject("Pointer");
            var picker = pointerGo.AddComponent<NodePicker>();
            picker.morpher = morpher;

            // 先停用物体再加 PointerInput，保证 OnEnable 时引用已经填好
            pointerGo.SetActive(false);
            var pointer = pointerGo.AddComponent<PointerInput>();
            pointer.rayCamera = camera;
            pointer.picker = picker;
            pointer.actions = actions;

            var cursor = pointerGo.AddComponent<PointerCursor>();
            cursor.pointer = pointer;

            var logger = pointerGo.AddComponent<PointerDebugLogger>();
            logger.pointer = pointer;
            logger.picker = picker;
            logger.logEvents = false;
            pointerGo.SetActive(true);
            return pointer;
        }

        // ---- Pick 阶段 ----
        // 场地：固定相机在 (0, 1.6, -1.9) 面朝 +Z，植株放在前方约 2.5 m 的矮台上
        // （台面 0.45 m，植株约 0.7 m 高，果实在蹲下伸手的高度）
        const string PickArtFolder = "Assets/Art/Pick";
        static readonly Vector3 PickPlantAnchor = new Vector3(0f, 0.45f, 0.6f);
        static readonly Vector3 PickStandSize = new Vector3(0.5f, 0.45f, 0.5f);
        // 可走范围（x / z 的最小、最大值），包含固定机位
        static readonly Vector2 PickAreaX = new Vector2(-3f, 3f);
        static readonly Vector2 PickAreaZ = new Vector2(-4f, 3f);
        const float PickWallHeight = 3f;
        const float PickWallThickness = 0.2f;
        // 植株挡板半径（米）：玩家胶囊不能进到这个圆柱里
        const float PickPlantBlockerRadius = 0.3f;

        static PickStage BuildPickStage(GameObject stageGo, StageContext ctx, PlantFit fit, TargetRotator rotator,
            Camera camera, InputActionAsset actions)
        {
            var stage = stageGo.AddComponent<PickStage>();
            stage.ctx = ctx;
            stage.plantFit = fit;
            stage.rotator = rotator;
            stage.area = BuildPickArea(out stage.plantAnchor);
            stage.player = BuildPlayer(camera, actions);

            // XRI：Interaction Manager 常驻；交互发起方是头上的 Ray Interactor（PC 从屏幕中心射出，左键 = Grab）
            var managerGo = new GameObject("XR Interaction Manager");
            stage.interactionManager = managerGo.AddComponent<XRInteractionManager>();
            Transform head = stage.player.motor.cameraPivot;
            var ray = BuildPickRay(head, stage.interactionManager, actions);

            var pc = stage.player.transform.Find("PC");
            var hover = pc.gameObject.AddComponent<PcCrosshairRayHover>();
            hover.ray = ray;
            hover.crosshair = pc.GetComponentInChildren<PcCrosshair>(true);

            stage.hand = BuildArm(head);
            var eat = stage.hand.gameObject.AddComponent<EatSequence>();
            eat.hand = stage.hand;
            eat.actions = actions;
            stage.eat = eat;
            stage.reachDistance = PickReachDistance;
            return stage;
        }

        // 伸手范围：眼睛到果实中心的距离（米）。站着够不到台上的果实，要蹲下
        const float PickReachDistance = 0.7f;

        static XRRayInteractor BuildPickRay(Transform head, XRInteractionManager manager, InputActionAsset actions)
        {
            var go = new GameObject("PickRay");
            go.transform.SetParent(head, false);
            var ray = go.AddComponent<XRRayInteractor>();
            ray.interactionManager = manager;
            ray.lineType = XRRayInteractor.LineType.StraightLine;
            // 射线比伸手距离稍长（果实碰撞球的半径），距离判断以 PickStage.reachDistance 为准
            ray.maxRaycastDistance = PickReachDistance + 0.15f;
            // 植株挡板在 Ignore Raycast 层，射线穿过它打到果实
            ray.raycastMask = ~(1 << 2);
            ray.raycastTriggerInteraction = QueryTriggerInteraction.Ignore;
            ray.enableUIInteraction = false;
            ray.hoverToSelect = false;
            ray.useForceGrab = false;
            ray.manipulateAttachTransform = false;
            ray.selectActionTrigger = XRBaseInputInteractor.InputTriggerType.State;

            var grab = FindActionReference(actions, "Player", "Grab");
            if (grab == null) Debug.LogError("[Flow] 找不到 Player/Grab 的 InputActionReference");
            ray.selectInput.inputSourceMode = XRInputButtonReader.InputSourceMode.InputActionReference;
            ray.selectInput.inputActionReferencePerformed = grab;
            ray.selectInput.inputActionReferenceValue = grab;
            ray.activateInput.inputSourceMode = XRInputButtonReader.InputSourceMode.Unused;
            ray.uiPressInput.inputSourceMode = XRInputButtonReader.InputSourceMode.Unused;
            return ray;
        }

        static InputActionReference FindActionReference(InputActionAsset actions, string map, string action)
        {
            string path = AssetDatabase.GetAssetPath(actions);
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (o is InputActionReference r && r.action != null && r.action.name == action && r.action.actionMap != null &&
                    r.action.actionMap.name == map)
                    return r;
            }
            return null;
        }

        // ---- 手臂（占位）----
        // 头（CameraPivot）下：Arm（Animator + RigBuilder，原点在眼睛）→ Shoulder → Elbow → Wrist 三节骨骼，Two Bone IK 驱动。
        // 上臂是胶囊；前臂和手用 Tripo 模型（静态网格），挂在 Elbow 下的 ForearmVisual 里，
        // 同一个 ForearmVisual 下还有 Palm（掌心）→ FruitHolder（果实挂点），由 ArmVisualPlacement 按设置资产摆放。
        // 位置都是相对眼睛（米）：x 右、y 上、z 前。待机 / 嘴边的位置在设置资产里（ArmVisualSettings）
        static readonly Vector3 ArmShoulder = new Vector3(0.17f, -0.24f, 0.02f);
        static readonly Vector3 ArmHint = new Vector3(0.45f, -0.55f, 0.05f);
        const float ArmUpperLength = 0.30f;
        const float ArmForeLength = 0.27f;
        // 前臂模型：肘端到指尖的长度（米）
        const float ArmModelLength = 0.44f;
        // 掌心在对齐后的模型里的位置（肘端在原点、手指朝 +Z、掌心朝 +Y、大拇指在 +X），按 10-09 的网格截面量出
        static readonly Vector3 ArmPalmInModel = new Vector3(0f, 0.05f, 0.30f);

        static HandReach BuildArm(Transform head)
        {
            var armMat = EnsureLitMaterial(PickArtFolder + "/PickArm.mat", new Color(0.86f, 0.72f, 0.64f));
            var settings = EnsureArmVisualSettings();

            var arm = new GameObject("Arm");
            arm.transform.SetParent(head, false);
            var animator = arm.AddComponent<Animator>();
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            var builder = arm.AddComponent<RigBuilder>();

            // 初始姿态：肩膀伸向待机位，肘部往 hint 方向弯（运行时 IK 会再对齐）
            Vector3 idle = settings.idlePalm;
            Vector3 dir = (idle - ArmShoulder).normalized;
            Vector3 bend = Vector3.ProjectOnPlane(ArmHint - ArmShoulder, dir).normalized;
            float c = Mathf.Min((idle - ArmShoulder).magnitude, ArmUpperLength + ArmForeLength - 0.01f);
            // 三角形：上臂 a、前臂 b、肩到腕 c，肘在 c 上的投影长度 x
            float a = ArmUpperLength, b = ArmForeLength;
            float x = (a * a - b * b + c * c) / (2f * c);
            float h = Mathf.Sqrt(Mathf.Max(0f, a * a - x * x));
            Vector3 elbowPos = ArmShoulder + dir * x + bend * h;
            Vector3 wristPos = ArmShoulder + dir * c;

            var shoulder = NewChild("Shoulder", arm.transform, ArmShoulder);
            shoulder.localRotation = Quaternion.LookRotation(elbowPos - ArmShoulder, Vector3.up);
            var elbow = NewChild("Elbow", shoulder, Vector3.forward * ArmUpperLength);
            elbow.rotation = arm.transform.rotation * Quaternion.LookRotation(wristPos - elbowPos, Vector3.up);
            var wrist = NewChild("Wrist", elbow, Vector3.forward * ArmForeLength);

            // 上臂胶囊（不要 Collider）
            var upper = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            upper.name = "UpperArmVisual";
            Object.DestroyImmediate(upper.GetComponent<Collider>());
            upper.transform.SetParent(shoulder, false);
            upper.transform.localPosition = Vector3.forward * (ArmUpperLength * 0.5f);
            upper.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            upper.transform.localScale = new Vector3(0.085f, ArmUpperLength * 0.5f + 0.03f, 0.085f);
            var upperR = upper.GetComponent<Renderer>();
            upperR.sharedMaterial = armMat;
            upperR.shadowCastingMode = ShadowCastingMode.Off;

            var placement = BuildForearmVisual(elbow, armMat, settings);

            // Rig：Two Bone IK，目标和 hint 都是 Arm 的直接子物体（HandReach 改 target 的 localPosition）
            var rigGo = new GameObject("Rig");
            rigGo.transform.SetParent(arm.transform, false);
            var rig = rigGo.AddComponent<Rig>();
            var ikGo = new GameObject("ArmIK");
            ikGo.transform.SetParent(rigGo.transform, false);
            var ik = ikGo.AddComponent<TwoBoneIKConstraint>();
            var target = NewChild("IKTarget", arm.transform, wristPos);
            var hint = NewChild("IKHint", arm.transform, ArmHint);
            ik.data.root = shoulder;
            ik.data.mid = elbow;
            ik.data.tip = wrist;
            ik.data.target = target;
            ik.data.hint = hint;
            ik.data.targetPositionWeight = 1f;
            ik.data.targetRotationWeight = 0f;
            ik.data.hintWeight = 1f;
            builder.layers.Clear();
            builder.layers.Add(new RigLayer(rig, true));

            var idleAnchor = NewChild("HandIdle", head, settings.idlePalm);
            var mouthAnchor = NewChild("MouthAnchor", head, settings.mouthPalm);

            var handReach = arm.AddComponent<HandReach>();
            handReach.armRoot = arm.transform;
            handReach.ikTarget = target;
            handReach.elbow = elbow;
            handReach.idleAnchor = idleAnchor;
            handReach.mouthAnchor = mouthAnchor;
            handReach.placement = placement;
            handReach.settings = settings;
            return handReach;
        }

        // 前臂 + 手的模型和掌心挂点，都放在 Elbow 下的 ForearmVisual 里（本地 = 肘部骨骼：原点在肘，+Z 沿前臂）。
        // 模型自动对齐：肘端到原点、肘→指尖对齐 +Z；之后的方向 / 位置微调交给 ArmVisualPlacement + 设置资产。
        // 找不到模型时用胶囊代替（掌心挂点照样有）
        static ArmVisualPlacement BuildForearmVisual(Transform elbow, Material fallback, ArmVisualSettings settings)
        {
            var visual = new GameObject("ForearmVisual").transform;
            visual.SetParent(elbow, false);
            var placement = visual.gameObject.AddComponent<ArmVisualPlacement>();
            placement.settings = settings;

            var palm = NewChild("Palm", visual, Vector3.zero);
            var holder = NewChild("FruitHolder", palm, Vector3.zero);
            placement.palm = palm;
            placement.fruitHolder = holder;
            placement.palmBasePosition = ArmPalmInModel;
            placement.palmBaseRotation = Quaternion.identity;

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ArmModelPath);
            if (prefab == null)
            {
                Debug.LogWarning($"[Flow] 找不到手臂模型 {ArmModelPath}，前臂用胶囊");
                var fore = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                fore.name = "ArmModel";
                Object.DestroyImmediate(fore.GetComponent<Collider>());
                fore.transform.SetParent(visual, false);
                fore.GetComponent<Renderer>().sharedMaterial = fallback;
                placement.model = fore.transform;
                placement.modelBasePosition = Vector3.forward * (ArmModelLength * 0.5f);
                placement.modelBaseRotation = Quaternion.Euler(90f, 0f, 0f);
                placement.modelBaseScale = new Vector3(0.07f, ArmModelLength * 0.5f, 0.07f);
                placement.Apply();
                return placement;
            }

            var model = (GameObject)PrefabUtility.InstantiatePrefab(prefab, visual);
            model.name = "ArmModel";
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            model.transform.localScale = Vector3.one;

            // 在 visual 空间里找前臂主轴（顶点协方差的主方向）；两端里顶点多的是手（手指），少的是肘部断面
            var points = new List<Vector3>();
            foreach (var mf in model.GetComponentsInChildren<MeshFilter>())
            {
                if (mf.sharedMesh == null) continue;
                Matrix4x4 m = visual.worldToLocalMatrix * mf.transform.localToWorldMatrix;
                foreach (var v in mf.sharedMesh.vertices) points.Add(m.MultiplyPoint3x4(v));
            }
            if (points.Count > 0)
            {
                Vector3 mean = Vector3.zero;
                foreach (var p in points) mean += p;
                mean /= points.Count;
                Vector3 axis = PrincipalAxis(points, mean);
                float tMin = float.MaxValue, tMax = float.MinValue;
                foreach (var p in points)
                {
                    float t = Vector3.Dot(p - mean, axis);
                    tMin = Mathf.Min(tMin, t);
                    tMax = Mathf.Max(tMax, t);
                }
                float band = (tMax - tMin) * 0.1f;
                int nMin = 0, nMax = 0;
                foreach (var p in points)
                {
                    float t = Vector3.Dot(p - mean, axis);
                    if (t < tMin + band) nMin++;
                    if (t > tMax - band) nMax++;
                }
                // 让 axis 从肘指向手
                if (nMax < nMin)
                {
                    axis = -axis;
                    float tmp = tMin;
                    tMin = -tMax;
                    tMax = -tmp;
                }
                Vector3 elbowEnd = Vector3.zero;
                int count = 0;
                foreach (var p in points)
                {
                    if (Vector3.Dot(p - mean, axis) < tMin + band)
                    {
                        elbowEnd += p;
                        count++;
                    }
                }
                elbowEnd /= Mathf.Max(1, count);

                float scale = ArmModelLength / Mathf.Max(1e-4f, tMax - tMin);
                Quaternion align = Quaternion.FromToRotation(axis, Vector3.forward);
                // 先缩放、旋转，再平移让肘端落在原点
                placement.modelBaseScale = Vector3.one * scale;
                placement.modelBaseRotation = align;
                placement.modelBasePosition = -(align * (elbowEnd * scale));
            }
            placement.model = model.transform;
            placement.Apply();

            foreach (var r in model.GetComponentsInChildren<Renderer>())
                r.shadowCastingMode = ShadowCastingMode.Off;
            return placement;
        }

        const string ArmSettingsPath = "Assets/Data/Pick/ArmVisualSettings.asset";

        static ArmVisualSettings EnsureArmVisualSettings()
        {
            var settings = AssetDatabase.LoadAssetAtPath<ArmVisualSettings>(ArmSettingsPath);
            if (settings != null) return settings;
            if (!AssetDatabase.IsValidFolder("Assets/Data")) AssetDatabase.CreateFolder("Assets", "Data");
            if (!AssetDatabase.IsValidFolder("Assets/Data/Pick")) AssetDatabase.CreateFolder("Assets/Data", "Pick");
            settings = ScriptableObject.CreateInstance<ArmVisualSettings>();
            AssetDatabase.CreateAsset(settings, ArmSettingsPath);
            return settings;
        }

        static Vector3 PrincipalAxis(List<Vector3> points, Vector3 mean)
        {
            float xx = 0, xy = 0, xz = 0, yy = 0, yz = 0, zz = 0;
            foreach (var p in points)
            {
                Vector3 d = p - mean;
                xx += d.x * d.x; xy += d.x * d.y; xz += d.x * d.z;
                yy += d.y * d.y; yz += d.y * d.z; zz += d.z * d.z;
            }
            Vector3 a = new Vector3(1f, 0.31f, 0.17f).normalized;
            for (int i = 0; i < 64; i++)
                a = new Vector3(xx * a.x + xy * a.y + xz * a.z, xy * a.x + yy * a.y + yz * a.z, xz * a.x + yz * a.y + zz * a.z).normalized;
            return a;
        }

        static Transform NewChild(string name, Transform parent, Vector3 localPosition)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = localPosition;
            return t;
        }

        // 地面、四面边界（不可见）、矮台和植株挡板。默认停用，PickStage 打开
        static GameObject BuildPickArea(out Transform plantAnchor)
        {
            var area = new GameObject("PickArea");
            var floorMat = EnsureLitMaterial(PickArtFolder + "/PickFloor.mat", new Color(0.72f, 0.72f, 0.70f));
            var standMat = EnsureLitMaterial(PickArtFolder + "/PickStand.mat", new Color(0.45f, 0.38f, 0.32f));

            float cx = (PickAreaX.x + PickAreaX.y) * 0.5f, cz = (PickAreaZ.x + PickAreaZ.y) * 0.5f;
            float sx = PickAreaX.y - PickAreaX.x, sz = PickAreaZ.y - PickAreaZ.x;
            // 地面：比可走范围大一圈，顶面 y = 0
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Floor";
            floor.transform.SetParent(area.transform, false);
            floor.transform.localPosition = new Vector3(cx, -0.05f, cz);
            floor.transform.localScale = new Vector3(sx + 4f, 0.1f, sz + 4f);
            floor.GetComponent<Renderer>().sharedMaterial = floorMat;

            var walls = new GameObject("Bounds").transform;
            walls.SetParent(area.transform, false);
            float h = PickWallHeight, t = PickWallThickness;
            AddWall(walls, "Left", new Vector3(PickAreaX.x - t * 0.5f, h * 0.5f, cz), new Vector3(t, h, sz + 2f * t));
            AddWall(walls, "Right", new Vector3(PickAreaX.y + t * 0.5f, h * 0.5f, cz), new Vector3(t, h, sz + 2f * t));
            AddWall(walls, "Back", new Vector3(cx, h * 0.5f, PickAreaZ.x - t * 0.5f), new Vector3(sx, h, t));
            AddWall(walls, "Front", new Vector3(cx, h * 0.5f, PickAreaZ.y + t * 0.5f), new Vector3(sx, h, t));

            // 矮台：顶面就是植株锚点
            var stand = GameObject.CreatePrimitive(PrimitiveType.Cube);
            stand.name = "Stand";
            stand.transform.SetParent(area.transform, false);
            stand.transform.localPosition = new Vector3(PickPlantAnchor.x, PickPlantAnchor.y - PickStandSize.y * 0.5f, PickPlantAnchor.z);
            stand.transform.localScale = PickStandSize;
            stand.GetComponent<Renderer>().sharedMaterial = standMat;

            // 植株挡板：不可见的竖直胶囊，玩家走不进植株（植株节点本身没有 Collider）
            var blocker = new GameObject("PlantBlocker");
            blocker.transform.SetParent(area.transform, false);
            blocker.transform.localPosition = new Vector3(PickPlantAnchor.x, 1f, PickPlantAnchor.z);
            // Ignore Raycast 层：只挡玩家胶囊，不挡摘果实的射线（果实在挡板圆柱里面）
            blocker.layer = 2;
            var capsule = blocker.AddComponent<CapsuleCollider>();
            capsule.radius = PickPlantBlockerRadius;
            capsule.height = 2f;

            var anchor = new GameObject("PlantAnchor").transform;
            anchor.SetParent(area.transform, false);
            anchor.localPosition = PickPlantAnchor;
            plantAnchor = anchor;

            area.SetActive(false);
            return area;
        }

        static void AddWall(Transform parent, string name, Vector3 position, Vector3 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.AddComponent<BoxCollider>().size = size;
        }

        // 玩家：PlayerRoot（CharacterController + 移动）→ CameraPivot（头，主相机在 Pick 阶段挂到这里）；
        // PC 专用的光标锁定和准星挂在根物体下的 PC 子物体上，跟着玩家开关
        static PlayerRig BuildPlayer(Camera camera, InputActionAsset actions)
        {
            var root = new GameObject("PlayerRoot");
            var controller = root.AddComponent<CharacterController>();
            controller.radius = 0.25f;
            controller.height = 1.72f;
            controller.center = Vector3.up * 0.86f;
            controller.stepOffset = 0.25f;
            controller.skinWidth = 0.02f;

            var pivot = new GameObject("CameraPivot").transform;
            pivot.SetParent(root.transform, false);
            pivot.localPosition = Vector3.up * 1.6f;

            var motor = root.AddComponent<FirstPersonMotor>();
            motor.cameraPivot = pivot;
            motor.actions = actions;

            var rig = root.AddComponent<PlayerRig>();
            rig.motor = motor;
            rig.viewCamera = camera.transform;

            var pc = new GameObject("PC");
            pc.transform.SetParent(root.transform, false);
            pc.AddComponent<PcCursorLock>();
            BuildCrosshair(pc.transform);
            return rig;
        }

        // 屏幕中心准星（PC 专用，HUD）
        static PcCrosshair BuildCrosshair(Transform parent)
        {
            var canvas = Ghost.Agent.AgentUIStyle.CreateHudCanvas("Crosshair", parent, 160);
            Object.DestroyImmediate(canvas.GetComponent<GraphicRaycaster>());
            var dotRect = Ghost.Agent.AgentUIStyle.CreateAnchored("Dot", canvas.transform, new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(10f, 10f));
            var dot = Ghost.Agent.AgentUIStyle.AddImage(dotRect, Color.white);
            dot.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            // 浅色描边：深色背景（叶子）上也看得清
            var outline = dot.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(1f, 1f, 1f, 0.7f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);
            var crosshair = canvas.gameObject.AddComponent<PcCrosshair>();
            crosshair.dot = dot;
            return crosshair;
        }

        static Material EnsureLitMaterial(string path, Color color)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;
            if (!AssetDatabase.IsValidFolder(PickArtFolder)) AssetDatabase.CreateFolder("Assets/Art", "Pick");
            mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Smoothness", 0.1f);
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        // 阶段面板（占位阶段的提示文字）。【技术债】比赛期间和其他界面一样是 Screen Space HUD，屏幕顶部居中
        static void BuildStagePanel(GameFlow flow, Transform cameraTransform)
        {
            var canvas = Ghost.Agent.AgentUIStyle.CreateHudCanvas("StagePanel", null, 150);
            var canvasGo = canvas.gameObject;
            Object.DestroyImmediate(canvasGo.GetComponent<GraphicRaycaster>());
            var holder = Ghost.Agent.AgentUIStyle.CreateAnchored("Holder", canvasGo.transform, new Vector2(0.5f, 1f),
                new Vector2(0f, -40f), new Vector2(1000f, 80f));

            var textGo = new GameObject("Label");
            textGo.transform.SetParent(holder, false);
            var textRect = textGo.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            var label = textGo.AddComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 34;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = new Color(0.15f, 0.17f, 0.2f);
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.raycastTarget = false;
            label.text = "Stage";

            var panel = canvasGo.AddComponent<StagePanel>();
            panel.flow = flow;
            panel.label = label;
        }
    }
}
