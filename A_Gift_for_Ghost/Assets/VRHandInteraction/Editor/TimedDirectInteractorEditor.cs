using UnityEditor;
using UnityEngine;

namespace Ghost.VRHandInteraction.Editor
{
    // XRI's inherited custom inspector omits subclass fields. Explicitly expose this module's settings.
    [CustomEditor(typeof(TimedDirectInteractor))]
    public sealed class TimedDirectInteractorEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox("Trigger hold authorization is controlled here. Leave XRI Select Input unbound. Pose Source/offsets are on HandPoseFollower; animation speed is on HandGraspVisual.", MessageType.Info);
            DrawDefaultInspector();
        }
    }
}
