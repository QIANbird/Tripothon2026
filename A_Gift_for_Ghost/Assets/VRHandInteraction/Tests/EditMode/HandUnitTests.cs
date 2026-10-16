using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEditor;

namespace Ghost.VRHandInteraction.Tests
{
    public class HandUnitTests
    {
        [Test] public void TimingRequiresBothConditionsAndResetsOnReleaseOrExit()
        {
            var gate = new HoldAuthorization(); var a = new object();
            Assert.False(gate.Tick(null, true, 0, 0.3f));
            Assert.False(gate.Tick(a, false, 2, 0.3f));
            Assert.False(gate.Tick(a, true, 4, 0.3f));
            Assert.False(gate.Tick(a, true, 4.299, 0.3f));
            Assert.True(gate.Tick(a, true, 4 + (double)0.3f, 0.3f));
            Assert.False(gate.Tick(a, false, 5, 0.3f));
            Assert.False(gate.Tick(a, true, 6, 0.3f));
            Assert.False(gate.Tick(null, true, 6.2, 0.3f));
            Assert.False(gate.Tick(a, true, 8, 0.3f));
            Assert.False(gate.Tick(a, true, 8.2, 0.3f));
        }
        [Test] public void SwitchingCandidateNeverTransfersElapsedTime()
        {
            var gate = new HoldAuthorization(); var a = new object(); var b = new object();
            gate.Tick(a, true, 0, 0.3f);
            Assert.False(gate.Tick(b, true, 0.29, 0.3f));
            Assert.False(gate.Tick(b, true, 0.31, 0.3f));
            Assert.True(gate.Tick(b, true, 0.6, 0.3f));
        }
        [Test] public void OffsetUsesControllerRotationButNotItsScale()
        {
            var go = new GameObject("pose");
            try
            {
                go.transform.SetPositionAndRotation(new Vector3(1, 2, 3), Quaternion.Euler(0, 90, 0));
                go.transform.localScale = Vector3.one * 4;
                var pose = HandPoseFollower.MapPose(go.transform, Vector3.forward * 0.1f, new Vector3(20, 0, 0));
                Assert.That(Vector3.Distance(pose.position, new Vector3(1.1f, 2, 3)), Is.LessThan(0.00001f));
                Assert.That(Quaternion.Angle(pose.rotation, go.transform.rotation * Quaternion.Euler(20, 0, 0)), Is.LessThan(0.001f));
            }
            finally { Object.DestroyImmediate(go); }
        }
        [Test] public void ControllerBindingsUseIndexTriggerForBothHands()
        {
            var actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/VRHandInteraction/Generated/HandInteraction.inputactions");
            Assert.NotNull(actions);
            Assert.AreEqual("<XRController>{RightHand}/trigger", actions.FindAction("Hand/RightTrigger", true).bindings[0].path);
            Assert.AreEqual("<XRController>{LeftHand}/trigger", actions.FindAction("Hand/LeftTrigger", true).bindings[0].path);
        }
    }
}
