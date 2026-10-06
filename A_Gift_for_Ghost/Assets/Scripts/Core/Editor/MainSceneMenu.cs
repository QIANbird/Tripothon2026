using System.Collections.Generic;
using Ghost.Agent;
using Ghost.Gameplay;
using Ghost.Interaction;
using Ghost.Morph;
using Ghost.Morph.EditorTools;
using Ghost.Narrative.EditorTools;
using Ghost.Stages;
using Ghost.Stages.EditorTools;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.UI;

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
            new StageSpec("S4", true, MorphForm.Geometric, "S4：按 N 完成"),
            new StageSpec("Transition", true, MorphForm.Real, "过渡：按 N 继续"),
            new StageSpec("Pick", true, MorphForm.Real, "采摘：按 N 继续"),
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

            // 固定相机：眼高 1.6 m，离植株 1.9 m
            var cameraGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var camera = cameraGo.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = background;
            camera.fieldOfView = 60f;
            camera.nearClipPlane = 0.05f;
            cameraGo.AddComponent<AudioListener>();
            cameraGo.transform.position = new Vector3(0f, 1.6f, -1.9f);

            // 植株放在约 0.75 m 的台面高度
            var plantGo = new GameObject("Plant");
            plantGo.transform.position = new Vector3(0f, 0.75f, 0f);
            var morpher = plantGo.AddComponent<NodeMorpher>();
            morpher.nodeSet = set;
            morpher.material = material;
            morpher.mesh = MorphMenu.BuiltinCube();
            morpher.startForm = MorphForm.Matrix;

            var links = plantGo.AddComponent<NodeLinkRenderer>();
            links.material = MorphMenu.EnsureLineMaterial();

            // G3：节点问题系统（闪烁 / 尝试 / 解决），和 NodeMorpher 挂在同一物体上
            var issues = plantGo.AddComponent<NodeIssueSystem>();
            issues.morpher = morpher;

            var handoff = plantGo.AddComponent<RealModelHandoff>();
            handoff.revealShader = Shader.Find("Ghost/RevealLit");
            // 注意：不加 MorphDebugSwitcher。主场景的形态只由 GameFlow 控制

            Vector3 focus = plantGo.transform.TransformPoint(new LayoutGenerator.Settings().center);
            cameraGo.transform.LookAt(focus);

            // G2：指针输入（节点拾取 + IInteractable 射线）和光标
            var pointer = BuildPointer(camera, morpher, actions);
            // G8：旋转植株（S3 / S4 共用），绕布局中心转，挂在 Plant 上
            var rotator = plantGo.AddComponent<TargetRotator>();
            rotator.target = plantGo.transform;
            rotator.pointer = pointer;
            rotator.viewCamera = camera;
            rotator.localPivot = new LayoutGenerator.Settings().center;
            // G4：Agent 任务面板、节点详情弹窗、AI 询问框
            var agentUI = AgentUIBuilder.BuildAll(null, camera);
            // G5：对白播放器和字幕
            var dialogue = NarrativeSceneBuilder.EnsureDialogue(cameraGo.transform);

            // 流程：每个阶段一个子物体
            var flowGo = new GameObject("GameFlow");
            var flow = flowGo.AddComponent<GameFlow>();
            flow.morpher = morpher;

            // G6：阶段共用的引用
            var ctx = flowGo.AddComponent<StageContext>();
            ctx.flow = flow;
            ctx.morpher = morpher;
            ctx.links = links;
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
            var stageList = new List<Stage>();
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

            EditorSceneManager.SaveScene(scene, MainScenePath);
            Debug.Log($"[Flow] 生成主场景 → {MainScenePath}。Play 后按 N 进入下一关，Shift + 1–9 跳关");
            return scene;
        }

        // G6：教学阶段。三个教学节点按 Matrix 布局挑选：画面中部偏上一排，左 / 中 / 右分开，
        // 避开左侧任务面板、右侧询问框和下方字幕。【占位】具体节点等策划的教学步骤定
        static TutorialStage BuildTutorialStage(GameObject stageGo, StageContext ctx, PlantNodeSet set)
        {
            var stage = stageGo.AddComponent<TutorialStage>();
            stage.ctx = ctx;
            stage.introSequence = StageAssets.EnsureTutorialIntro();
            stage.afterEasySequence = StageAssets.EnsureTutorialAfterEasy();
            stage.afterMediumSequence = StageAssets.EnsureTutorialAfterMedium();
            stage.afterHardSequence = StageAssets.EnsureTutorialAfterHard();

            var used = new HashSet<int>();
            // 困难问题优先放在果实上，呼应 S1 的"果实出现严重问题"
            stage.easyNode = NearestInMatrix(set, new Vector2(0.25f, 0.72f), null, used);
            stage.mediumNode = NearestInMatrix(set, new Vector2(0.5f, 0.72f), null, used);
            stage.hardNode = NearestInMatrix(set, new Vector2(0.75f, 0.72f), n => n.organ == Organ.Fruit, used);
            if (stage.hardNode < 0) stage.hardNode = NearestInMatrix(set, new Vector2(0.75f, 0.72f), null, used);
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

        // World Space 面板：放在植株上方、玩家前方约 1.5 m，正对相机
        static void BuildStagePanel(GameFlow flow, Transform cameraTransform)
        {
            var canvasGo = new GameObject("StagePanel");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = cameraTransform.GetComponent<Camera>();
            canvasGo.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 4f;

            // 1000 × 160 像素，缩放 0.001 → 1 m × 0.16 m
            var rect = canvasGo.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(1000f, 160f);
            canvasGo.transform.localScale = Vector3.one * 0.001f;
            canvasGo.transform.position = new Vector3(0f, 1.62f, -0.4f);
            canvasGo.transform.rotation = Quaternion.LookRotation(canvasGo.transform.position - cameraTransform.position);

            var textGo = new GameObject("Label");
            textGo.transform.SetParent(canvasGo.transform, false);
            var textRect = textGo.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            var label = textGo.AddComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 64;
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
