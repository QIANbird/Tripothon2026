using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace Ghost.VRHandInteraction.Tests
{
    public class HandIntegrationTests
    {
        readonly List<GameObject> objects = new List<GameObject>();
        XRInteractionManager manager;
        TimedDirectInteractor hand;
        Transform source;
        XRGrabInteractable cube;
        static readonly WaitForSeconds Contact = new WaitForSeconds(0.12f);
        static readonly WaitForSeconds Hold = new WaitForSeconds(0.38f);
        GameObject New(string name) { var go = new GameObject(name); objects.Add(go); return go; }
        GameObject Primitive(string name, Vector3 position, Vector3 scale)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); objects.Add(go);
            go.name = name; go.transform.position = position; go.transform.localScale = scale; return go;
        }
        TimedDirectInteractor MakeHand(string name, Vector3 position, out Transform pose)
        {
            pose = New(name + "Pose").transform; pose.position = position;
            var go = New(name); go.SetActive(false); go.transform.position = position;
            var follower = go.AddComponent<HandPoseFollower>(); follower.controllerPoseSource = pose;
            var input = go.AddComponent<HandTriggerInput>(); input.useInjectedValue = true;
            var result = go.AddComponent<TimedDirectInteractor>();
            result.poseFollower = follower; result.triggerInput = input; result.interactionManager = manager;
            result.attachTransform = go.transform;
            go.SetActive(true); return result;
        }
        XRGrabInteractable MakeCube(Vector3 position)
        {
            var go = Primitive("Cube", position, Vector3.one * 0.1f);
            go.AddComponent<Rigidbody>().useGravity = false;
            var grab = go.AddComponent<XRGrabInteractable>();
            grab.interactionManager = manager;
            grab.movementType = XRBaseInteractable.MovementType.Kinematic;
            grab.useDynamicAttach = true; grab.throwOnDetach = false;
            grab.selectMode = InteractableSelectMode.Single;
            return grab;
        }
        [UnitySetUp] public IEnumerator SetUp()
        {
            manager = New("Manager").AddComponent<XRInteractionManager>();
            hand = MakeHand("Right", new Vector3(0, 3, 0), out source);
            cube = MakeCube(source.position);
            yield return Contact;
        }
        [UnityTearDown] public IEnumerator TearDown()
        {
            for (int i = objects.Count - 1; i >= 0; i--) if (objects[i] != null) Object.Destroy(objects[i]);
            objects.Clear(); yield return null;
        }
        void Press(float value = 1) => hand.triggerInput.SetInjectedValue(value);

        [UnityTest] public IEnumerator ShortPressDoesNotSelectOrMoveObject()
        {
            var original = cube.transform.position; var parent = cube.transform.parent;
            int selected = 0; cube.selectEntered.AddListener(_ => selected++);
            Press(); yield return new WaitForSeconds(0.18f);
            Assert.False(cube.isSelected); Assert.False(cube.GetComponent<Rigidbody>().isKinematic);
            Assert.That(Vector3.Distance(original, cube.transform.position), Is.LessThan(0.0001f));
            Assert.AreSame(parent, cube.transform.parent);
            Press(0); yield return Hold;
            Assert.False(hand.hasSelection); Assert.AreEqual(0, selected);
        }
        [UnityTest] public IEnumerator LongPressFollowsPositionRotationAndReleasesWithGravity()
        {
            Press(); yield return Hold;
            Assert.True(hand.hasSelection); Assert.True(cube.isSelected);
            source.position += Vector3.right * 0.4f;
            source.rotation = Quaternion.Euler(20, 50, 10);
            yield return Contact;
            Assert.That(Vector3.Distance(source.position, cube.transform.position), Is.LessThan(0.03f));
            Assert.That(Quaternion.Angle(source.rotation, cube.transform.rotation), Is.LessThan(2));
            // Original useGravity=false is restored, then exercise dynamic gravity after release.
            Press(0); yield return null; yield return null;
            Assert.False(hand.hasSelection); Assert.False(cube.isSelected);
            var body = cube.GetComponent<Rigidbody>(); Assert.False(body.isKinematic); Assert.False(body.useGravity);
            body.useGravity = true; float y = body.position.y;
            yield return new WaitForSeconds(0.2f);
            Assert.Less(body.position.y, y - 0.05f);
        }
        [UnityTest] public IEnumerator OutOfRangeCannotSelectAndEntryStartsNewTimer()
        {
            source.position += Vector3.right;
            Press(); yield return Hold; Assert.False(hand.hasSelection);
            source.position = cube.transform.position;
            yield return new WaitForSeconds(0.18f); Assert.False(hand.hasSelection);
            yield return Hold; Assert.True(hand.hasSelection);
        }
        [UnityTest] public IEnumerator ExitAndReentryCancelPendingAuthorization()
        {
            Press(); yield return new WaitForSeconds(0.18f);
            source.position += Vector3.right; yield return Contact;
            source.position = cube.transform.position; yield return new WaitForSeconds(0.18f);
            Assert.False(hand.hasSelection); yield return Hold; Assert.True(hand.hasSelection);
        }
        [UnityTest] public IEnumerator MultipleCollidersDoNotResetContactOrAllowTwoSelections()
        {
            var extra = new GameObject("ExtraCollider"); extra.transform.SetParent(cube.transform, false);
            var collider = extra.AddComponent<BoxCollider>(); collider.size = Vector3.one * 0.8f;
            cube.colliders.Add(collider);
            var other = MakeCube(source.position + Vector3.right * 0.1f);
            Press(); yield return Hold;
            Assert.AreEqual(1, hand.interactablesSelected.Count);
            Assert.True(cube.isSelected ^ other.isSelected);
            Press(0); yield return Contact;
            Assert.False(cube.isSelected); Assert.False(other.isSelected);
        }
        [UnityTest] public IEnumerator TrackingLossAndDisableReleaseWithoutAnimationDelay()
        {
            Press(); yield return Hold; Assert.True(hand.hasSelection);
            hand.poseFollower.SetTrackingValid(false); yield return null; yield return null;
            Assert.False(hand.hasSelection); Assert.False(cube.GetComponent<Rigidbody>().isKinematic);
            hand.poseFollower.SetTrackingValid(true); yield return Hold; Assert.True(hand.hasSelection);
            hand.enabled = false; yield return null;
            Assert.False(cube.isSelected); Assert.False(cube.GetComponent<Rigidbody>().isKinematic);
        }
        [UnityTest] public IEnumerator DestroyedCandidateCancelsAndDestroyedHeldObjectCleansUp()
        {
            Press(); yield return new WaitForSeconds(0.15f);
            Object.Destroy(cube.gameObject); yield return Contact; Assert.False(hand.IsAuthorized);
            cube = MakeCube(source.position); yield return Hold; Assert.True(hand.hasSelection);
            Object.Destroy(cube.gameObject); yield return Contact; Assert.False(hand.hasSelection);
        }
        [UnityTest] public IEnumerator TwoHandsHaveIndependentTimersAndSingleOwnership()
        {
            var left = MakeHand("Left", source.position, out var leftPose);
            yield return Contact; Press(); left.triggerInput.SetInjectedValue(1);
            yield return Hold;
            Assert.True(hand.hasSelection ^ left.hasSelection);
            Assert.AreEqual(1, cube.interactorsSelecting.Count);
            hand.triggerInput.SetInjectedValue(0); left.triggerInput.SetInjectedValue(0);
            yield return Contact;
            leftPose.position += Vector3.left;
            var second = MakeCube(leftPose.position);
            left.triggerInput.SetInjectedValue(1); yield return Hold;
            Assert.True(second.isSelected); Assert.False(hand.hasSelection); Assert.False(cube.isSelected);
        }
        [UnityTest] public IEnumerator LayerMismatchRejectsSelection()
        {
            hand.interactionLayerMask = 2; Press(); yield return Hold;
            Assert.False(hand.hasSelection);
            hand.interactionLayerMask = 1; hand.contactPhysicsLayers = 0; yield return Hold;
            Assert.False(hand.hasSelection);
        }
        [UnityTest] public IEnumerator VisualClosesAndOpensIndependentlyOfGrabbing()
        {
            var visual = hand.gameObject.AddComponent<HandGraspVisual>(); visual.input = hand.triggerInput;
            source.position += Vector3.right;
            Press(); yield return new WaitForSeconds(0.2f);
            Assert.That(visual.Amount, Is.EqualTo(1).Within(0.01f)); Assert.False(hand.hasSelection);
            Press(0); yield return new WaitForSeconds(0.2f);
            Assert.That(visual.Amount, Is.EqualTo(0).Within(0.01f));
        }
        [UnityTest] public IEnumerator ReleaseRestoresOriginallyEnabledGravity()
        {
            Primitive("Support", new Vector3(0, 2.9f, 0), new Vector3(1, 0.1f, 1));
            var body = cube.GetComponent<Rigidbody>(); body.useGravity = true;
            yield return Contact; Press(); yield return Hold;
            Assert.True(hand.hasSelection);
            source.position += Vector3.up * 0.4f; yield return Contact;
            Press(0); yield return null; yield return null;
            Assert.False(body.isKinematic); Assert.True(body.useGravity);
            float y = body.position.y; yield return new WaitForSeconds(0.2f);
            Assert.Less(body.position.y, y - 0.05f);
        }
#if UNITY_EDITOR
        [UnityTest] public IEnumerator InputSystemTriggerSelectsButGripDoesNot()
        {
            UnityEngine.InputSystem.InputSystem.RegisterLayout(@"{
                ""name"":""Phase1TestController"", ""extend"":""XRController"",
                ""controls"":[{""name"":""trigger"",""layout"":""Axis"",""format"":""FLT""},
                              {""name"":""grip"",""layout"":""Axis"",""format"":""FLT""}]
            }");
            var device = UnityEngine.InputSystem.InputSystem.AddDevice("Phase1TestController");
            UnityEngine.InputSystem.InputSystem.SetDeviceUsage(device, UnityEngine.InputSystem.CommonUsages.RightHand);
            try
            {
                var input = hand.triggerInput; input.enabled = false;
                input.triggerAction = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.InputSystem.InputActionReference>(
                    "Assets/VRHandInteraction/Generated/Hand_RightTrigger.asset");
                input.useInjectedValue = false; input.enabled = true;
                UnityEngine.InputSystem.InputSystem.QueueDeltaStateEvent((UnityEngine.InputSystem.Controls.AxisControl)device["grip"], 1f);
                yield return Hold;
                Assert.False(input.Pressed); Assert.False(hand.hasSelection);
                UnityEngine.InputSystem.InputSystem.QueueDeltaStateEvent((UnityEngine.InputSystem.Controls.AxisControl)device["trigger"], 1f);
                yield return Hold; Assert.True(input.Pressed); Assert.True(hand.hasSelection);
                UnityEngine.InputSystem.InputSystem.QueueDeltaStateEvent((UnityEngine.InputSystem.Controls.AxisControl)device["trigger"], 0f);
                yield return null; yield return null;
                Assert.False(hand.hasSelection);
            }
            finally
            {
                UnityEngine.InputSystem.InputSystem.RemoveDevice(device);
                UnityEngine.InputSystem.InputSystem.RemoveLayout("Phase1TestController");
            }
        }
#endif
#if UNITY_EDITOR
        [UnityTest] public IEnumerator GeneratedPrefabAnimatesFingerBonesFromTrigger()
        {
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/VRHandInteraction/Generated/RightHand.prefab");
            Assert.NotNull(prefab);
            var instance = Object.Instantiate(prefab); objects.Add(instance);
            var follower = instance.GetComponent<HandPoseFollower>(); follower.controllerPoseSource = source;
            var input = instance.GetComponent<HandTriggerInput>(); input.useInjectedValue = true;
            var visual = instance.GetComponent<HandGraspVisual>();
            Assert.NotNull(visual.animator, "Expected installed Adaptive Hands Animator, not fallback.");
            yield return new WaitForSeconds(0.2f);
            var bones = visual.animator.GetComponentsInChildren<Transform>();
            var open = new Quaternion[bones.Length];
            for (int i = 0; i < bones.Length; i++) open[i] = bones[i].localRotation;
            input.SetInjectedValue(1); yield return new WaitForSeconds(0.3f);
            int changed = 0;
            for (int i = 0; i < bones.Length; i++) if (Quaternion.Angle(open[i], bones[i].localRotation) > 5) changed++;
            Assert.GreaterOrEqual(changed, 5, "At least five finger bones must visibly bend.");
            input.SetInjectedValue(0); yield return new WaitForSeconds(0.3f);
            for (int i = 0; i < bones.Length; i++)
                Assert.That(Quaternion.Angle(open[i], bones[i].localRotation), Is.LessThan(1), bones[i].name);
        }
#endif
    }
}
