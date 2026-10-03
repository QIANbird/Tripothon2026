# SETUP.md

队友（或其 AI 助手）从 GitHub 拉取本仓库后，在 Windows 上完成本地初始化的步骤。按顺序执行，每一步都有验证命令。开发约定见 [AGENTS.md](AGENTS.md)。

> 给 AI 助手：涉及下载安装（编辑器、模块）或需要用户登录/授权（GitHub、Unity 账号、UAC 弹窗）的步骤，先告知用户再执行。不要修改 `.gitignore`、`.gitattributes`、`ProjectSettings/ProjectVersion.txt` 来"解决"环境问题。

## 关键信息

| 项目 | 值 |
|---|---|
| 仓库 | `https://github.com/QIANbird/Tripothon2026.git`，分支 `main` |
| Unity 项目目录 | 仓库根下的 `A_Gift_for_Ghost/` |
| 编辑器版本 | **6000.3.25f1**（必须完全一致） |
| 渲染管线 | URP 17.3 |
| 必装模块 | Android Build Support、Android SDK & NDK Tools、OpenJDK（为后续 Quest 打包准备） |

## 1. 前置工具

需要：Git、Git LFS、Unity Hub、Unity CLI（`unity` 命令，可选但推荐）。

```bash
git --version
git lfs version
unity --version
```

- 缺 Git LFS：安装 Git for Windows 时默认已包含；否则从 https://git-lfs.com 安装。
- 缺 Unity CLI：PowerShell 中运行 `irm https://unity.com/install.ps1 | iex`，然后**重开终端**。默认安装在 `%LOCALAPPDATA%\Unity\bin\unity.exe`；若当前 shell 找不到 `unity`，用完整路径调用。

## 2. 克隆仓库（必须先启用 LFS）

```bash
git lfs install
git clone https://github.com/QIANbird/Tripothon2026.git
cd Tripothon2026
git lfs pull
```

验证 LFS 文件是真实内容而不是指针文件：

```bash
git lfs ls-files
git lfs status
```

如果图片/模型在 Unity 里显示为损坏或大小只有约 130 字节，说明 LFS 没拉下来，重新执行 `git lfs pull`。

## 3. 本机 Git 配置

提交者身份（用自己的 GitHub 账号绑定邮箱）：

```bash
git config user.name "你的名字"
git config user.email "你的GitHub邮箱"
```

配置 UnityYAMLMerge（场景/Prefab 的智能合并，`.gitattributes` 已声明，但驱动需每台机器单独配置）。先找到编辑器路径：

```bash
unity editors --format json
```

取 6000.3.25f1 的 `location`（如 `D:\APP\Unity\6000.3.25f1\Editor\Unity.exe`），把 `Unity.exe` 替换为 `Data/Tools/UnityYAMLMerge.exe`，用正斜杠写入：

```bash
git config merge.unityyamlmerge.name "Unity SmartMerge"
git config merge.unityyamlmerge.driver "'<编辑器目录>/Editor/Data/Tools/UnityYAMLMerge.exe' merge -p %O %B %A %A"
git config merge.unityyamlmerge.recursive binary
```

验证：

```bash
git config --get merge.unityyamlmerge.driver
```

并确认该路径下的 `UnityYAMLMerge.exe` 文件存在。

## 4. 安装 Unity 编辑器和模块

已安装 6000.3.25f1 的先检查模块：

```bash
unity editors --format json
```

未安装编辑器：

```bash
unity install 6000.3.25f1 -m android android-sdk-ndk-tools android-open-jdk-17.0.18+8 --accept-eula
```

已安装编辑器但缺模块：

```bash
unity install-modules -e 6000.3.25f1 -m android android-sdk-ndk-tools android-open-jdk-17.0.18+8 --accept-eula
```

模块 ID 以 `unity install 6000.3.25f1 --list-modules` 的输出为准（OpenJDK 的 ID 带版本号，可能变化）。安装过程约 3–4 GB 下载，可能弹出 UAC 授权。

没有 CLI 时：Unity Hub → Installs → Install Editor → 选 6000.3.25f1 → 勾选上述三个 Android 相关模块。

## 5. 打开项目

```bash
unity open A_Gift_for_Ghost
```

或在 Unity Hub → Projects → Add → 选择 `Tripothon2026/A_Gift_for_Ghost` 文件夹。

- 首次打开会重建 `Library/`，需要数分钟，属正常现象。
- 如果 Unity 提示版本不一致要求升级/降级：**取消**，回到第 4 步安装 6000.3.25f1。
- 如果提示进入 Safe Mode：有脚本编译错误，先看 Console 报错，通常是拉取不完整或包未解析，不要随意删包。

## 6. 打开后核对项目设置

这些设置已提交在 `ProjectSettings/` 中，正常情况下无需修改，只需核对。如有不同，说明拉取有问题或本地被改动，不要提交这类改动，先和团队确认。

| 位置 | 应为 |
|---|---|
| Edit → Project Settings → Editor → Version Control → Mode | Visible Meta Files |
| Edit → Project Settings → Editor → Asset Serialization → Mode | Force Text |
| Edit → Project Settings → Player → Other Settings → Active Input Handling | Input System Package (New) |
| Edit → Project Settings → Graphics → Default Render Pipeline | `PC_RPAsset` |
| Edit → Project Settings → Quality | PC / Mobile 两档，分别使用 `PC_RPAsset` / `Mobile_RPAsset` |
| Window → Package Manager | 包能全部解析，无红色报错 |

对应文件检查（可由 AI 助手直接读取）：

```bash
grep m_SerializationMode A_Gift_for_Ghost/ProjectSettings/EditorSettings.asset
grep activeInputHandler A_Gift_for_Ghost/ProjectSettings/ProjectSettings.asset
cat A_Gift_for_Ghost/ProjectSettings/ProjectVersion.txt
```

期望值：`m_SerializationMode: 2`（Force Text）、`activeInputHandler: 1`（仅新版 Input System）、`m_EditorVersion: 6000.3.25f1`。

本机个人设置（存于 `UserSettings/`，不会同步，按需自行设置）：

- Edit → Preferences → External Tools → External Script Editor：选择 Visual Studio / Rider / VS Code。
- 打开主场景：`Assets/Scenes/` 下对应场景。

## 7. 可选：让 AI 助手直接操作 Unity 编辑器

项目已包含 `com.unity.pipeline` 包。安装 Unity CLI 并打开项目后：

```bash
unity status
```

状态为 `ready` 时，AI 助手可通过 `unity command ...` 在编辑器中创建对象、进入 Play 模式、读取 Console。详细用法：`unity skill show`。

## 8. 完成检查

```bash
git status
git lfs status
```

- 打开项目后 `git status` 应基本干净。如果出现大量 `ProjectSettings/` 或 `.meta` 改动，通常是编辑器版本不一致或设置被改动，先不要提交，找出原因。
- `Library/`、`Temp/`、`Logs/`、`UserSettings/`、`.vscode/`、`*.csproj` 不应出现在 `git status` 中（已被忽略）。

## 日常同步

```bash
git pull
```

拉取前先在 Unity 中保存场景（Ctrl+S）。拉取后若 Unity 正在运行，它会自动重新导入改动。提交时资源和 `.meta` 必须一起提交，详见 [AGENTS.md](AGENTS.md) 的 Git 协作部分。
