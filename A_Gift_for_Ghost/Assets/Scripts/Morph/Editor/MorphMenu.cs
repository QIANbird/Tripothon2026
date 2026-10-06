using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace Ghost.Morph.EditorTools
{
    public static class MorphMenu
    {
        const string FakePlantPath = "Assets/Data/Morph/FakePlant.asset";
        const string SampledPlantPath = "Assets/Data/Morph/pepper_plant.asset";

        // 生成（或覆盖）假植物的节点数据资产
        [MenuItem("Ghost/Morph/Generate Fake Plant")]
        public static PlantNodeSet GenerateFakePlant()
        {
            var set = AssetDatabase.LoadAssetAtPath<PlantNodeSet>(FakePlantPath);
            if (set == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FakePlantPath));
                set = ScriptableObject.CreateInstance<PlantNodeSet>();
                AssetDatabase.CreateAsset(set, FakePlantPath);
            }

            set.nodes = FakePlantGenerator.Generate(new FakePlantGenerator.Settings());
            LayoutGenerator.BuildAll(set.nodes, new LayoutGenerator.Settings());
            string error = set.Validate();
            if (error != null) Debug.LogError($"[Morph] 假植物数据无效：{error}");

            EditorUtility.SetDirty(set);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Morph] 生成假植物：{set.Count} 个节点 → {FakePlantPath}");
            Selection.activeObject = set;
            return set;
        }

        // 只重算现有节点集的 Network 布局（读 Real 姿态，不重新采样），节点 id、部位、父子关系都不变，
        // S1 / S2 写死的节点 id 继续有效。改了 LayoutGenerator.Settings 的 network* 参数后用它
        [MenuItem("Ghost/Morph/Rebuild Network Layout (Keep Ids)")]
        public static void RebuildNetworkLayout()
        {
            foreach (var path in new[] { SampledPlantPath, FakePlantPath })
            {
                var set = AssetDatabase.LoadAssetAtPath<PlantNodeSet>(path);
                if (set == null) continue;
                LayoutGenerator.BuildNetwork(set.nodes, new LayoutGenerator.Settings());
                string error = set.Validate();
                if (error != null) Debug.LogError($"[Morph] {path} 数据无效：{error}");
                EditorUtility.SetDirty(set);
                Debug.Log($"[Morph] 重算 Network 布局：{set.Count} 个节点 → {path}");
            }
            AssetDatabase.SaveAssets();
        }

        // 从选中的模型（场景里的实例或 Project 里的 FBX）采样节点，生成 Assets/Data/Morph/<模型名>.asset
        [MenuItem("Ghost/Morph/Sample Plant From Selected Model")]
        public static void SampleSelectedModel()
        {
            var model = Selection.activeGameObject;
            if (model == null)
            {
                Debug.LogError("[Morph] 先在 Hierarchy 或 Project 里选中拆好件的植物模型");
                return;
            }
            // 选中子物体时按它所在的 Prefab 命名
            var root = EditorUtility.IsPersistent(model) ? model.transform.root.gameObject : PrefabUtility.GetNearestPrefabInstanceRoot(model);
            if (root == null) root = model;
            SamplePlant(model, $"Assets/Data/Morph/{root.name}.asset");
        }

        public static PlantNodeSet SamplePlant(GameObject model, string assetPath)
        {
            // 场景实例换成离它最近的一层 Prefab 资产（Prefab Variant 里加的虫子才不会丢），节点集里记录的是资产
            if (!EditorUtility.IsPersistent(model))
            {
                var instanceRoot = PrefabUtility.GetNearestPrefabInstanceRoot(model);
                if (instanceRoot == null)
                {
                    Debug.LogError($"[Morph] {model.name} 不是 Prefab 实例。先把它做成 Prefab（或 Prefab Variant）再采样");
                    return null;
                }
                if (PrefabUtility.GetAddedGameObjects(instanceRoot).Count > 0 || PrefabUtility.HasPrefabInstanceAnyOverrides(instanceRoot, false))
                {
                    Debug.LogError($"[Morph] {instanceRoot.name} 有未应用的改动（比如刚拖进去的虫子）。先 Overrides → Apply All，或者做成 Prefab Variant 再采样");
                    return null;
                }
                model = PrefabUtility.GetCorrespondingObjectFromSource(instanceRoot);
            }

            var set = AssetDatabase.LoadAssetAtPath<PlantNodeSet>(assetPath);
            if (set == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(assetPath));
                set = ScriptableObject.CreateInstance<PlantNodeSet>();
                AssetDatabase.CreateAsset(set, assetPath);
            }

            MeshAnchorSampler.Sample(model, set, new MeshAnchorSampler.Settings());
            LayoutGenerator.BuildAll(set.nodes, new LayoutGenerator.Settings());
            string error = set.Validate();
            if (error != null) Debug.LogError($"[Morph] 采样数据无效：{error}");

            EditorUtility.SetDirty(set);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Morph] 从 {model.name} 采样 {set.Count} 个节点 → {assetPath}");
            Selection.activeObject = set;
            return set;
        }

        const string NodeMaterialPath = "Assets/Art/Morph/MorphNode.mat";

        // 节点材质：Ghost/MorphNode 着色器 + GPU Instancing。已存在则直接返回
        [MenuItem("Ghost/Morph/Create Node Material")]
        public static Material EnsureNodeMaterial()
        {
            return EnsureMaterial(NodeMaterialPath, "Ghost/MorphNode");
        }

        const string LineMaterialPath = "Assets/Art/Morph/MorphLine.mat";

        // 连线材质：Ghost/MorphLine 着色器（顶点色 + 透明）
        [MenuItem("Ghost/Morph/Create Line Material")]
        public static Material EnsureLineMaterial()
        {
            return EnsureMaterial(LineMaterialPath, "Ghost/MorphLine");
        }

        static Material EnsureMaterial(string path, string shaderName)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;

            var shader = Shader.Find(shaderName);
            if (shader == null)
            {
                Debug.LogError($"[Morph] 找不到着色器 {shaderName}");
                return null;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            mat = new Material(shader) { enableInstancing = true };
            AssetDatabase.CreateAsset(mat, path);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Morph] 创建材质 → {path}");
            return mat;
        }

        const string PrototypeScenePath = "Assets/Scenes/MorphPrototype.unity";
        const string InputActionsPath = "Assets/InputSystem_Actions.inputactions";

        // 一键生成变形原型场景：浅灰背景、固定相机（眼高 1.6 m）、NodeMorpher + 数字键切换。已存在则覆盖
        [MenuItem("Ghost/Morph/Build Prototype Scene")]
        public static void BuildPrototypeScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            BuildPrototypeScene(NewSceneMode.Single);
        }

        // Additive 模式不会关闭当前打开的场景（自动化检查时用）
        public static UnityEngine.SceneManagement.Scene BuildPrototypeScene(NewSceneMode mode)
        {

            // 优先用从模型采样的节点集，没有时用程序生成的假植物
            var set = AssetDatabase.LoadAssetAtPath<PlantNodeSet>(SampledPlantPath);
            if (set == null) set = AssetDatabase.LoadAssetAtPath<PlantNodeSet>(FakePlantPath);
            if (set == null) set = GenerateFakePlant();
            var material = EnsureNodeMaterial();
            var actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
            if (material == null || actions == null)
            {
                Debug.LogError("[Morph] 缺少节点材质或 InputSystem_Actions，场景未生成");
                return default;
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, mode);
            // RenderSettings 作用于活动场景
            UnityEngine.SceneManagement.SceneManager.SetActiveScene(scene);
            var background = new Color(0.85f, 0.85f, 0.86f);
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = background;

            // 相机放在真人眼高，略微俯视植株；植株放在一个约 0.75 m 的台面高度上
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
            morpher.material = material;
            morpher.mesh = BuiltinCube();
            morpher.startForm = MorphForm.Matrix;

            var links = plantGo.AddComponent<NodeLinkRenderer>();
            links.material = EnsureLineMaterial();

            // 节点集来自模型采样时，写实形态交接给真模型。着色器显式引用，打包时才会被包含
            var handoff = plantGo.AddComponent<RealModelHandoff>();
            handoff.revealShader = Shader.Find("Ghost/RevealLit");

            var switcher = plantGo.AddComponent<MorphDebugSwitcher>();
            switcher.morpher = morpher;
            switcher.actions = actions;

            // 所有形态的中心 (0, 0.32, 0) 落在画面中央
            Vector3 focus = plantGo.transform.TransformPoint(new LayoutGenerator.Settings().center);
            cameraGo.transform.LookAt(focus);

            EditorSceneManager.SaveScene(scene, PrototypeScenePath);
            Debug.Log($"[Morph] 生成原型场景 → {PrototypeScenePath}。进入 Play Mode 后按 1–5 切换形态");
            return scene;
        }

        // Unity 内置立方体网格，节点默认形状
        public static Mesh BuiltinCube()
        {
            return Resources.GetBuiltinResource<Mesh>("Cube.fbx");
        }
    }
}
