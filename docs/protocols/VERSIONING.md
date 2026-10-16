# Versioning

## Rules
- Do not commit, push, merge, reset, or rebase without user approval.
- Check `git status` and `git diff` before committing.
- Keep Unity assets and `.meta` files together.
- Do not include unrelated changes.
- Never commit a machine-local `file:` path in `A_Gift_for_Ghost/Packages/manifest.json` or `packages-lock.json` (Windows `C:/Users/...`, macOS `/Users/...`, Desktop, Downloads). Optional tools such as Tripo Unity Bridge stay on the machine that uses them.

## macOS / Windows collaboration

- All developers use Unity **6000.3.25f1** and open `A_Gift_for_Ghost/` as the Unity project.
- Install Git LFS once with `git lfs install`. After cloning or pulling binary assets, run `git lfs pull` before opening Unity.
- Pull the latest target branch before starting work. Develop scoped work on a feature branch; do not mix unrelated files into the same commit.
- Shared package dependencies must resolve on both macOS and Windows. Install them from the Unity registry, a stable Git URL, or a package committed inside the repository. Optional machine-local tools must not become required project dependencies.
- Commit `Assets/`, `Packages/`, `ProjectSettings/`, and every required Unity `.meta` file. Never commit `Library/`, `Temp/`, `Logs/`, `Obj/`, or `UserSettings/`.
- Coordinate ownership before editing the same scene, prefab, material, or ScriptableObject.
- `Assets/Scenes/Main.unity` is generated. Change `Assets/Scripts/Core/Editor/MainSceneMenu.cs`, then run **Ghost → Core → Build Main Scene**.
- After pulling or merging, wait for Unity package resolution and script compilation, then check the Console and run the relevant Play Mode verification.
- Platform-specific behavior stays isolated from shared gameplay code.

## Checkpoints
Create a clean checkpoint before:
- risky features;
- large refactors;
- scene/prefab changes;
- gameplay experiments.

Useful commit types:
- `feat: ...`
- `fix: ...`
- `refactor: ...`
- `checkpoint: ...`

## Debugging
For regressions:
1. Find the last working commit.
2. Inspect the recent diff.
3. Search changed files first.
4. Expand scope only if needed.

See `DEBUG.md` for the full debug workflow.