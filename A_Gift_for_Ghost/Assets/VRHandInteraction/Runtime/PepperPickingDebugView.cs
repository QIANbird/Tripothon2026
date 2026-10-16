using UnityEngine;

namespace Ghost.VRHandInteraction
{
    public sealed class PepperPickingDebugView : MonoBehaviour
    {
        public PepperPickingManager manager; public HandTriggerInput trigger; public bool show = true;
        GUIStyle style;
        void OnGUI()
        {
            if (!show || manager == null) return;
            style ??= new GUIStyle(GUI.skin.label) { fontSize = 16, normal = { textColor = Color.white } };
            var held = manager.CurrentHeldPepper;
            GUI.Label(new Rect(16, 16, 600, 30), $"Pepper: {held?.name ?? "None"}  State: {held?.State.ToString() ?? "-"}", style);
            GUI.Label(new Rect(16, 40, 900, 30), $"Trigger: {trigger?.Pressed}  Picked: {manager.PickedPepperCount}/{manager.TotalPepperCount}", style);
            if (!string.IsNullOrEmpty(manager.CurrentMessage)) GUI.Label(new Rect(16, 70, 1000, 36), manager.CurrentMessage, style);
        }
    }
}
