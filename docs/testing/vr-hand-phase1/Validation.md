# Phase 1 validation — 2026-10-16

Environment: macOS, Unity 6000.3.25f1, XRI 3.3.2, Input System 1.20.0, Adaptive Hands 1.0.0.

The live project was open in Unity. Validation ran in a temporary copy of its Assets, Packages and ProjectSettings, with a cloned Library cache; the live Editor was not closed. Delivered module source and generated assets were byte-compared with the tested copy.

- Unity batch compilation and `Ghost.VRHandInteraction.Editor.HandInteractionAssets.Build`: exit 0.
- Original hand FBX import inspection: 0 skinned renderers, 0 bones, 0 blendshapes, 0 animation clips.
- EditMode.xml: 4 passed, 0 failed.
- PlayMode.xml: 15 passed, 0 failed, including generated-scene synthetic keyboard interaction, synthetic XR Trigger versus Grip, actual Animator bone motion and existing Main scene startup.
- Second asset-generation pass: all existing generated file hashes unchanged.
- Asset audit: no missing `.meta`, no unresolved non-engine GUID references.
- File audit: no changes to original Scenes, Scripts, 3D_Objects or ProjectSettings. Existing user changes to AGENTS.md and package files were preserved.
- `git diff --check`: passed.

The first integration run exposed a destroyed-candidate interface reference; the runtime now checks Unity object lifetime before accessing it. The headless scene test required temporary in-memory Input System focus settings for synthetic keyboard events; those settings are restored in teardown and no project settings are saved.

Not Tested: real Quest controller tracking/input/alignment, headset visual quality/performance, Windows Editor, Android/ARM64 build/device execution, and complete manual non-VR gameplay regression. Main startup smoke is not full gameplay acceptance. User Phase 1 acceptance is pending; Phase 2 has not started.

Reproduction: open the supplied test scene, or run the two module test assemblies with Unity Test Runner. For CLI use `-batchmode -nographics -runTests -testPlatform EditMode` (or `PlayMode`), `-assemblyNames Ghost.VRHandInteraction.EditModeTests` (or `Ghost.VRHandInteraction.PlayModeTests`), and `-testResults <report.xml>`. Asset generation uses `-executeMethod Ghost.VRHandInteraction.Editor.HandInteractionAssets.Build -quit`. Do not launch a second Editor against an already-open project.
