using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Ghost.Core.EditorTools
{
    // Game 视图打开 Gizmos 时，世界原点的 Y 轴会画成一条贯穿画面的竖线（相机在 x=0 水平正视）。
    // 进 Play 时关掉，避免当成场景内容。Scene 视图的 Gizmos 不受影响。
    [InitializeOnLoad]
    static class GameViewGizmosOff
    {
        static GameViewGizmosOff()
        {
            EditorApplication.playModeStateChanged += OnPlayMode;
        }

        static void OnPlayMode(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode) SetGameViewGizmos(false);
        }

        static void SetGameViewGizmos(bool on)
        {
            var type = typeof(Editor).Assembly.GetType("UnityEditor.GameView");
            if (type == null) return;
            var draw = type.GetProperty("drawGizmos", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (draw == null || !draw.CanWrite) return;
            foreach (var view in Resources.FindObjectsOfTypeAll(type))
                draw.SetValue(view, on);
        }
    }
}
