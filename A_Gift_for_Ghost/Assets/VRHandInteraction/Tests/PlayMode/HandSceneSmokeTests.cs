#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Ghost.VRHandInteraction.Tests
{
    public class HandSceneSmokeTests
    {
        Scene scene;
        Keyboard keyboard;
        InputSettings.BackgroundBehavior previousBackground;
        InputSettings.EditorInputBehaviorInPlayMode previousEditorInput;
        [SetUp] public void ConfigureSyntheticInput()
        {
            // Batch mode has no focused Game View. Restore both in-memory values after each test.
            previousBackground = InputSystem.settings.backgroundBehavior;
            previousEditorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
            if (keyboard != null) InputSystem.RemoveDevice(keyboard);
            keyboard = null;
            InputSystem.settings.backgroundBehavior = previousBackground;
            InputSystem.settings.editorInputBehaviorInPlayMode = previousEditorInput;
        }
        [UnityTest] public IEnumerator GeneratedSceneSimulationMovesRotatesGraspsAndReleases()
        {
            const string path = "Assets/VRHandInteraction/Generated/HandInteractionTest.unity";
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(path, new LoadSceneParameters(LoadSceneMode.Additive));
            scene = SceneManager.GetSceneByPath(path);
            var roots = scene.GetRootGameObjects();
            var sim = roots.Select(r => r.GetComponent<EditorHandSimulator>()).First(c => c != null);
            var hand = roots.Select(r => r.GetComponent<TimedDirectInteractor>()).First(c => c != null);
            var cube = roots.First(r => r.name == "TestCube1");
            yield return null;
            var visual = hand.GetComponent<HandPoseFollower>().handVisualTransform;
            Assert.That(Vector3.Distance(visual.GetChild(0).position, hand.transform.position), Is.LessThan(0.001f),
                "Package sample placement must not offset the hand from its grab collider.");
            var camera = roots.Select(r => r.GetComponent<Camera>()).First(c => c != null);
            var planes = GeometryUtility.CalculateFrustumPlanes(camera);
            Assert.True(visual.GetComponentsInChildren<Renderer>().Any(r => r.enabled && GeometryUtility.TestPlanesAABB(planes, r.bounds)),
                "At least one enabled hand renderer must be inside the test camera frustum.");
            keyboard = InputSystem.AddDevice<Keyboard>();
            Vector3 initial = sim.poseSource.position;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
            yield return new WaitForSeconds(0.2f);
            Assert.Greater(sim.poseSource.position.z, initial.z + 0.03f, $"sim={sim.isActiveAndEnabled}, action={sim.move.action.enabled}, controls={sim.move.action.controls.Count}, value={sim.move.action.ReadValue<Vector3>()}, key={keyboard.wKey.isPressed}, device={keyboard.enabled}, dt={Time.deltaTime}");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
            sim.poseSource.position = cube.transform.position;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space));
            yield return new WaitForSeconds(0.45f);
            Assert.True(hand.hasSelection);
            Quaternion original = hand.transform.rotation;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space, Key.RightArrow));
            yield return new WaitForSeconds(0.2f);
            Assert.Greater(Quaternion.Angle(original, hand.transform.rotation), 5);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null; yield return null;
            Assert.False(hand.hasSelection);
        }
        [UnityTest] public IEnumerator ExistingMainSceneStartsWithoutHandModuleInjection()
        {
            const string path = "Assets/Scenes/Main.unity";
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(path, new LoadSceneParameters(LoadSceneMode.Additive));
            scene = SceneManager.GetSceneByPath(path);
            yield return new WaitForSeconds(0.5f);
            var roots = scene.GetRootGameObjects();
            Assert.That(roots.SelectMany(r => r.GetComponentsInChildren<Camera>(true)).Count(), Is.GreaterThan(0));
            Assert.That(roots.SelectMany(r => r.GetComponentsInChildren<TimedDirectInteractor>(true)), Is.Empty);
            LogAssert.NoUnexpectedReceived();
        }
    }
}
#endif
