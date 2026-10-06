using Ghost.Interaction;
using Ghost.Morph;
using Ghost.Morph.EditorTools;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Ghost.Agent.EditorTools
{
    // G4 验收场景 Assets/Scenes/G4Test.unity：相机和植株与 Main 一致，加三个 Agent 界面、
    // Pointer（同 MainSceneMenu.BuildPointer）和 AgentUITestDriver（假数据）。已存在则覆盖。
    public static class AgentUITestMenu
    {
        const string ScenePath = "Assets/Scenes/G4Test.unity";
        const string SampledPlantPath = "Assets/Data/Morph/pepper_plant.asset";
        const string FakePlantPath = "Assets/Data/Morph/FakePlant.asset";
        const string InputActionsPath = "Assets/InputSystem_Actions.inputactions";

        [MenuItem("Ghost/Agent/Build Agent UI Test Scene")]
        public static void Build()
        {
            // 用 Additive 新建、保存后关掉，不打断当前打开的场景（其他模块可能正在用编辑器）
            var scene = BuildScene();
            if (scene.IsValid()) EditorSceneManager.CloseScene(scene, true);
        }

        public static Scene BuildScene()
        {
            var set = AssetDatabase.LoadAssetAtPath<PlantNodeSet>(SampledPlantPath);
            if (set == null) set = AssetDatabase.LoadAssetAtPath<PlantNodeSet>(FakePlantPath);
            var actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
            var nodeMaterial = MorphMenu.EnsureNodeMaterial();
            var lineMaterial = MorphMenu.EnsureLineMaterial();
            if (set == null || actions == null || nodeMaterial == null || lineMaterial == null)
            {
                Debug.LogError("[G4] 缺少节点集、InputSystem_Actions 或节点材质，测试场景未生成");
                return default;
            }

            var previousActive = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);

            var background = new Color(0.85f, 0.85f, 0.86f);
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = background;

            // 固定相机：眼高 1.6 m，离植株 1.9 m（同 Main）
            var cameraGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var camera = cameraGo.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = background;
            camera.fieldOfView = 60f;
            camera.nearClipPlane = 0.05f;
            cameraGo.AddComponent<AudioListener>();
            cameraGo.transform.position = new Vector3(0f, 1.6f, -1.9f);

            var plantGo = new GameObject("Plant");
            plantGo.transform.position = new Vector3(0f, 0.75f, 0f);
            var morpher = plantGo.AddComponent<NodeMorpher>();
            morpher.nodeSet = set;
            morpher.material = nodeMaterial;
            morpher.mesh = MorphMenu.BuiltinCube();
            morpher.startForm = MorphForm.Matrix;
            var links = plantGo.AddComponent<NodeLinkRenderer>();
            links.material = lineMaterial;

            Vector3 focus = plantGo.transform.TransformPoint(new LayoutGenerator.Settings().center);
            cameraGo.transform.LookAt(focus);

            // Pointer：和 MainSceneMenu.BuildPointer 相同（先停用再加 PointerInput，保证 OnEnable 时引用已填）
            var pointerGo = new GameObject("Pointer");
            var picker = pointerGo.AddComponent<NodePicker>();
            picker.morpher = morpher;
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
            pointerGo.SetActive(true);

            // Agent 界面（G6 在 Main 里也是这一行）
            var ui = AgentUIBuilder.BuildAll(null, camera);

            var driverGo = new GameObject("AgentUITestDriver");
            var driver = driverGo.AddComponent<AgentUITestDriver>();
            driver.taskPanel = ui.taskPanel;
            driver.detailPopup = ui.detailPopup;
            driver.query = ui.query;
            driver.pointer = pointer;
            driver.picker = picker;

            EditorSceneManager.SaveScene(scene, ScenePath);
            if (previousActive.IsValid() && previousActive.isLoaded) SceneManager.SetActiveScene(previousActive);
            Debug.Log($"[G4] 生成测试场景 → {ScenePath}");
            return scene;
        }
    }
}
