# Versioning

## Rules
- Do not commit, push, merge, reset, or rebase without user approval.
- Check `git status` and `git diff` before committing.
- Keep Unity assets and `.meta` files together.
- Do not include unrelated changes.

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