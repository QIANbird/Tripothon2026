using Ghost.Morph;
using Ghost.Morph.EditorTools;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Ghost.Gameplay.EditorTools
{
    // G3 验收场景：Matrix 形态的彩椒节点 + NodeIssueSystem + G3TestDriver（开局挂三种难度的问题，鼠标点节点）
    public static class G3TestMenu
    {
        const string ScenePath = "Assets/Scenes/G3Test.unity";
        const string NodeSetPath = "Assets/Data/Morph/pepper_plant.asset";
        const string InputActionsPath = "Assets/InputSystem_Actions.inputactions";

        [MenuItem("Ghost/Gameplay/Build G3 Test Scene")]
        public static void Build()
        {
            // 用 Additive 新建、保存后关掉，不打断当前打开的场景
            var scene = BuildScene();
            if (scene.IsValid()) EditorSceneManager.CloseScene(scene, true);
        }

        public static Scene BuildScene()
        {
            var set = AssetDatabase.LoadAssetAtPath<PlantNodeSet>(NodeSetPath);
            var actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
            var nodeMaterial = MorphMenu.EnsureNodeMaterial();
            var lineMaterial = MorphMenu.EnsureLineMaterial();
            if (set == null || actions == null || nodeMaterial == null || lineMaterial == null)
            {
                Debug.LogError("[G3] 缺少 pepper_plant 节点集、InputSystem_Actions 或节点材质，测试场景未生成");
                return default;
            }

            var previousActive = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);

            var background = new Color(0.85f, 0.85f, 0.86f);
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = background;

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

            var switcher = plantGo.AddComponent<MorphDebugSwitcher>();
            switcher.morpher = morpher;
            switcher.actions = actions;

            var issues = plantGo.AddComponent<NodeIssueSystem>();
            issues.morpher = morpher;

            var driver = plantGo.AddComponent<G3TestDriver>();
            driver.morpher = morpher;
            driver.issues = issues;
            driver.links = links;
            driver.actions = actions;

            Vector3 focus = plantGo.transform.TransformPoint(new LayoutGenerator.Settings().center);
            cameraGo.transform.LookAt(focus);

            EditorSceneManager.SaveScene(scene, ScenePath);
            if (previousActive.IsValid() && previousActive.isLoaded) SceneManager.SetActiveScene(previousActive);
            Debug.Log($"[G3] 生成测试场景 → {ScenePath}");
            return scene;
        }
    }
}
