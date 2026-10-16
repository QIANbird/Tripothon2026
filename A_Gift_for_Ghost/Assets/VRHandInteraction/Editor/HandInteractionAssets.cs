using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace Ghost.VRHandInteraction.Editor
{
    public static class HandInteractionAssets
    {
        public const string Root = "Assets/VRHandInteraction/Generated";
        [MenuItem("Ghost/VR Hand Interaction/Create Missing Test Assets")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            if (!Application.isBatchMode && string.IsNullOrEmpty(SceneManager.GetActiveScene().path))
                throw new InvalidOperationException("Save the current untitled scene before generating test assets. No scene will be replaced.");
            Directory.CreateDirectory(Root);
            AssetDatabase.Refresh();
            var actions = CreateActions();
            var handMaterial = Material("Hand", new Color(0.65f, 0.82f, 0.95f));
            var objectMaterial = Material("TestObject", new Color(0.95f, 0.4f, 0.12f));
            var hand = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/RightHand.prefab");
            if (hand == null) hand = CreateHand(actions, handMaterial);
            var cube = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/GrabbableCube.prefab");
            if (cube == null)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = "GrabbableCube";
                go.transform.localScale = Vector3.one * 0.09f;
                go.GetComponent<Renderer>().sharedMaterial = objectMaterial;
                go.AddComponent<Rigidbody>().mass = 0.15f;
                var grab = go.AddComponent<XRGrabInteractable>();
                grab.selectMode = InteractableSelectMode.Single;
                grab.movementType = XRBaseInteractable.MovementType.Kinematic;
                grab.useDynamicAttach = true;
                grab.throwOnDetach = false;
                cube = PrefabUtility.SaveAsPrefabAsset(go, Root + "/GrabbableCube.prefab");
                UnityEngine.Object.DestroyImmediate(go);
            }
            if (!File.Exists(Root + "/HandInteractionTest.unity")) CreateScene(hand, cube, actions);
            InspectOriginalModel();
            AssetDatabase.SaveAssets();
            Debug.Log("VR Hand Phase 1 assets ready: " + Root + "/HandInteractionTest.unity");
        }

        static InputActionAsset CreateActions()
        {
            string path = Root + "/HandInteraction.inputactions";
            var existing = AssetDatabase.LoadAssetAtPath<InputActionAsset>(path);
            if (existing != null) return existing;
            var asset = ScriptableObject.CreateInstance<InputActionAsset>();
            var xr = asset.AddActionMap("Hand");
            xr.AddAction("RightTrigger", InputActionType.Value, "<XRController>{RightHand}/trigger", expectedControlLayout: "Axis");
            xr.AddAction("LeftTrigger", InputActionType.Value, "<XRController>{LeftHand}/trigger", expectedControlLayout: "Axis");
            var sim = asset.AddActionMap("Simulation");
            sim.AddAction("Trigger", InputActionType.Value, "<Keyboard>/space", expectedControlLayout: "Axis");
            var move = sim.AddAction("Move", InputActionType.Value, expectedControlLayout: "Vector3");
            move.AddCompositeBinding("3DVector").With("Forward", "<Keyboard>/w").With("Backward", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d").With("Up", "<Keyboard>/e").With("Down", "<Keyboard>/q");
            var rotate = sim.AddAction("Rotate", InputActionType.Value, expectedControlLayout: "Vector3");
            rotate.AddCompositeBinding("3DVector").With("Left", "<Keyboard>/upArrow").With("Right", "<Keyboard>/downArrow")
                .With("Up", "<Keyboard>/rightArrow").With("Down", "<Keyboard>/leftArrow").With("Forward", "<Keyboard>/x").With("Backward", "<Keyboard>/z");
            File.WriteAllText(path, asset.ToJson());
            UnityEngine.Object.DestroyImmediate(asset);
            AssetDatabase.ImportAsset(path);
            return AssetDatabase.LoadAssetAtPath<InputActionAsset>(path);
        }

        static InputActionReference Reference(InputActionAsset asset, string name)
        {
            string path = Root + "/" + name.Replace('/', '_') + ".asset";
            var reference = AssetDatabase.LoadAssetAtPath<InputActionReference>(path);
            if (reference != null) return reference;
            reference = InputActionReference.Create(asset.FindAction(name, true));
            AssetDatabase.CreateAsset(reference, path);
            return reference;
        }

        static Material Material(string name, Color color)
        {
            string path = Root + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name, color = color };
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        static GameObject CreateHand(InputActionAsset actions, Material material)
        {
            var root = new GameObject("RightHand");
            root.SetActive(false);
            var pose = root.AddComponent<HandPoseFollower>();
            var input = root.AddComponent<HandTriggerInput>();
            input.triggerAction = Reference(actions, "Hand/RightTrigger");
            var visualRoot = new GameObject("HandVisual");
            visualRoot.transform.SetParent(root.transform, false);
            pose.handVisualTransform = visualRoot.transform;
            var visual = root.AddComponent<HandGraspVisual>();
            visual.input = input;
            pose.preventPenetration = true;
            pose.collisionRadius = 0.045f;
            pose.collisionLayers = 1;
            string packageHand = "Packages/com.jeremiah-stevens.adaptive-hands/Runtime/Prefabs/AdaptiveHand_Right.prefab";
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(packageHand);
            if (source != null)
            {
                var model = (GameObject)PrefabUtility.InstantiatePrefab(source, visualRoot.transform);
                // Package sample prefab includes a one-meter scene placement offset.
                model.transform.localPosition = Vector3.zero;
                model.transform.localRotation = Quaternion.identity;
                PrefabUtility.RecordPrefabInstancePropertyModifications(model.transform);
                // Retain model and animation only. Do not inherit package controller/physics behavior.
                foreach (var script in model.GetComponentsInChildren<MonoBehaviour>(true)) script.enabled = false;
                foreach (var collider in model.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
                foreach (var body in model.GetComponentsInChildren<Rigidbody>(true)) { body.isKinematic = true; body.useGravity = false; body.detectCollisions = false; }
                visual.animator = model.GetComponentInChildren<Animator>(true);
                if (visual.animator != null)
                {
                    visual.animator.enabled = true;
                    visual.animator.applyRootMotion = false;
                    visual.animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                }
                foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
                    renderer.sharedMaterials = Enumerable.Repeat(material, renderer.sharedMaterials.Length).ToArray();
                if (visual.animator == null) UnityEngine.Object.DestroyImmediate(model);
            }
            if (visual.animator == null)
            {
                Debug.LogWarning("Adaptive Hands Animator unavailable; using articulated primitive hand.");
                var palm = GameObject.CreatePrimitive(PrimitiveType.Cube);
                palm.name = "FallbackPalm"; palm.transform.SetParent(visualRoot.transform, false);
                palm.transform.localScale = new Vector3(0.075f, 0.025f, 0.09f);
                UnityEngine.Object.DestroyImmediate(palm.GetComponent<Collider>());
                palm.GetComponent<Renderer>().sharedMaterial = material;
                visual.fallbackFingers = new Transform[5];
                for (int i = 0; i < 5; i++)
                {
                    var pivot = new GameObject("FingerPivot" + i).transform;
                    pivot.SetParent(visualRoot.transform, false); pivot.localPosition = new Vector3((i - 2) * 0.017f, 0, 0.04f);
                    var finger = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    finger.transform.SetParent(pivot, false); finger.transform.localPosition = new Vector3(0, 0, 0.03f);
                    finger.transform.localScale = new Vector3(0.013f, 0.015f, 0.06f);
                    finger.GetComponent<Renderer>().sharedMaterial = material;
                    UnityEngine.Object.DestroyImmediate(finger.GetComponent<Collider>());
                    visual.fallbackFingers[i] = pivot;
                }
            }
            var collision = new GameObject("HandCollision");
            collision.transform.SetParent(root.transform, false);
            var collisionSphere = collision.AddComponent<SphereCollider>();
            collisionSphere.radius = 0.045f;
            collisionSphere.isTrigger = false;
            var attach = new GameObject("Attach").transform;
            attach.SetParent(root.transform, false);
            var interactor = root.AddComponent<TimedDirectInteractor>();
            root.GetComponent<SphereCollider>().isTrigger = true;
            root.GetComponent<SphereCollider>().radius = 0.08f;
            root.GetComponent<Rigidbody>().isKinematic = true;
            root.GetComponent<Rigidbody>().useGravity = false;
            interactor.poseFollower = pose;
            interactor.triggerInput = input;
            interactor.attachTransform = attach;
            root.SetActive(true);
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, Root + "/RightHand.prefab");
            UnityEngine.Object.DestroyImmediate(root);
            return prefab;
        }

        static void CreateScene(GameObject hand, GameObject cube, InputActionAsset actions)
        {
            var previous = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, Application.isBatchMode ? NewSceneMode.Single : NewSceneMode.Additive);
            try
            {
                SceneManager.SetActiveScene(scene);
                actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(Root + "/HandInteraction.inputactions");
                hand = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/RightHand.prefab");
                cube = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/GrabbableCube.prefab");
                var manager = new GameObject("TestInteractionManager").AddComponent<XRInteractionManager>();
                var source = new GameObject("SimulatedControllerPose").transform;
                source.position = new Vector3(0, 1, 0);
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(hand, scene);
                instance.transform.position = source.position;
                instance.GetComponent<HandPoseFollower>().controllerPoseSource = source;
                instance.GetComponent<TimedDirectInteractor>().interactionManager = manager;
                var sim = new GameObject("EditorSimulation").AddComponent<EditorHandSimulator>();
                sim.poseSource = source; sim.input = instance.GetComponent<HandTriggerInput>();
                sim.move = Reference(actions, "Simulation/Move");
                sim.rotate = Reference(actions, "Simulation/Rotate");
                sim.trigger = Reference(actions, "Simulation/Trigger");
                for (int i = 0; i < 2; i++)
                {
                    var obj = (GameObject)PrefabUtility.InstantiatePrefab(cube, scene);
                    obj.name = "TestCube" + (i + 1); obj.transform.position = new Vector3(i * 0.2f, 0.9f, 0.25f);
                    obj.GetComponent<XRGrabInteractable>().interactionManager = manager;
                }
                var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
                ground.name = "TestTable"; ground.transform.position = new Vector3(0, 0.75f, 0.2f);
                ground.transform.localScale = new Vector3(2, 0.1f, 2);
                var camera = new GameObject("TestObserverCamera").AddComponent<Camera>();
                camera.transform.position = new Vector3(0.5f, 1.6f, -1.1f);
                camera.transform.LookAt(new Vector3(0, 0.95f, 0.15f)); camera.nearClipPlane = 0.01f;
                var light = new GameObject("TestLight").AddComponent<Light>(); light.type = LightType.Directional;
                light.transform.rotation = Quaternion.Euler(45, -25, 0);
                EditorSceneManager.SaveScene(scene, Root + "/HandInteractionTest.unity");
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
                if (previous.IsValid()) SceneManager.SetActiveScene(previous);
            }
        }

        static void InspectOriginalModel()
        {
            const string path = "Assets/3D_Objects/hand/tripo_convert_07d6ebfa-74e6-4ae4-a1a9-7c87fa196291.fbx";
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (model == null) { Debug.LogWarning("Original hand FBX did not import."); return; }
            var skins = model.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            Debug.Log("Original hand inspection: skinned renderers=" + skins.Length + ", bones=" + skins.Sum(s => s.bones.Length) +
                ", blendshapes=" + skins.Sum(s => s.sharedMesh != null ? s.sharedMesh.blendShapeCount : 0) +
                ", animation clips=" + AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Count());
        }
    }
}

namespace Ghost.VRHandInteraction.Editor
{
    public static class PepperPickingSceneAssets
    {
        const string Root = "Assets/VRHandInteraction/Generated";
        [UnityEditor.MenuItem("Ghost/VR Hand Interaction/Create Pepper Picking Test Scene")]
        public static void Create()
        {
            var scene = UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene, UnityEditor.SceneManagement.NewSceneMode.Single);
            var manager = new UnityEngine.GameObject("PepperPickingManager").AddComponent<PepperPickingManager>();
            manager.interactionManager = new UnityEngine.GameObject("XRInteractionManager").AddComponent<UnityEngine.XR.Interaction.Toolkit.XRInteractionManager>();
            var source = new UnityEngine.GameObject("SimulatedControllerPose").transform; source.position = new UnityEngine.Vector3(0, 1, 0);
            var hand = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>(Root + "/RightHand.prefab");
            var handGo = (UnityEngine.GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(hand);
            handGo.GetComponent<HandPoseFollower>().controllerPoseSource = source;
            var sim = new UnityEngine.GameObject("EditorSimulation").AddComponent<EditorHandSimulator>(); sim.poseSource = source; sim.input = handGo.GetComponent<HandTriggerInput>();
            var actions = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.InputSystem.InputActionAsset>(Root + "/HandInteraction.inputactions");
            sim.move = UnityEngine.InputSystem.InputActionReference.Create(actions.FindAction("Simulation/Move", true)); sim.rotate = UnityEngine.InputSystem.InputActionReference.Create(actions.FindAction("Simulation/Rotate", true)); sim.trigger = UnityEngine.InputSystem.InputActionReference.Create(actions.FindAction("Simulation/Trigger", true));
            var tree = UnityEngine.Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>("Assets/3D_Objects/pepper2/pepper_plant.fbx")); tree.name = "PepperTree"; tree.transform.position = new UnityEngine.Vector3(0, 0.55f, 0.35f); tree.transform.localScale = Vector3.one * 0.7f; manager.treeRoot = tree.transform;
            foreach (var t in tree.GetComponentsInChildren<UnityEngine.Transform>(true)) { var n=t.name.ToLowerInvariant(); if (n.Contains("pepper") && t.GetComponent<UnityEngine.MeshRenderer>() != null) manager.explicitPeppers.Add(t); }
            if (manager.explicitPeppers.Count == 0)
            {
                var mat = new UnityEngine.Material(UnityEngine.Shader.Find("Universal Render Pipeline/Lit")) { color = new UnityEngine.Color(0.85f, 0.08f, 0.03f) };
                for (int i=0;i<3;i++) { var g=UnityEngine.GameObject.CreatePrimitive(UnityEngine.PrimitiveType.Sphere); g.name="PepperProxy_"+(i+1); g.transform.position=tree.transform.position+new UnityEngine.Vector3((i-1)*0.18f,0.25f+(i%2)*0.18f,0.08f); g.transform.localScale=new UnityEngine.Vector3(0.11f,0.16f,0.11f); g.GetComponent<UnityEngine.Renderer>().sharedMaterial=mat; manager.explicitPeppers.Add(g.transform); }
            }
            manager.autoScan = false; manager.RegisterAll();
            var floor = UnityEngine.GameObject.CreatePrimitive(UnityEngine.PrimitiveType.Cube); floor.name="DropFloor"; floor.transform.position=new UnityEngine.Vector3(0,0,0.5f); floor.transform.localScale=new UnityEngine.Vector3(3,0.1f,3);
            var cam = new UnityEngine.GameObject("TestObserverCamera").AddComponent<UnityEngine.Camera>(); cam.transform.position=new UnityEngine.Vector3(0,0.95f,-1.35f); cam.transform.LookAt(new UnityEngine.Vector3(0,0.65f,0.35f));
            var light = new UnityEngine.GameObject("TestLight").AddComponent<UnityEngine.Light>(); light.type=UnityEngine.LightType.Directional; light.transform.rotation=UnityEngine.Quaternion.Euler(45,-30,0);
            manager.handInteractor = handGo.GetComponent<TimedDirectInteractor>();
            var debug=manager.gameObject.AddComponent<PepperPickingDebugView>(); debug.manager=manager; debug.trigger=handGo.GetComponent<HandTriggerInput>(); debug.show=true;
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene, Root + "/VRPepperPicking_Test.unity");
            UnityEngine.Debug.Log($"Pepper picking test scene created. Independent candidates: {manager.explicitPeppers.Count}");
        }
    }
}
