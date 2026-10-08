# SubtitleExtractslator Beta NativeAOT 包索引

预发布运行时集来自 `development` 分支。Skill 从该分支安装；Beta Release 不发布 skill ZIP。

## 版本规则

<!-- package-version-block:start -->
- per-RID NativeAOT Beta 包集合尚未首次发布；首个候选版本沿用共享数值 high-water 基线递增。
- Development 会在所有 stable/Beta RID 包的最高数值版本上递增 patch，并且只追加精确后缀 `-beta`；不使用 alpha、preview 或 RC。
<!-- package-version-block:end -->

## 恢复本机运行时

Skill 每次启动都会检查此通道最新的预发布版本；仅当本机 RID 包缺失或缓存过期时才下载：

```bash
python3 assets/bootstrap/restore_runtime.py --channel beta
```

Windows 如没有 `python3`，请使用 `py -3`。Bootstrap 需要 Python 3 和访问 NuGet 的 HTTPS 网络，会验证 SHA-512 sidecar，并输出绝对可执行文件路径。

## 运行时包

每个 NuGet 包只包含 `tools/<rid>/` 下对应平台的 NativeAOT 可执行文件：

- `SubtitleExtractslator.Cli.win-x64`
- `SubtitleExtractslator.Cli.win-arm64`
- `SubtitleExtractslator.Cli.linux-x64`
- `SubtitleExtractslator.Cli.linux-arm64`
- `SubtitleExtractslator.Cli.linux-musl-x64`
- `SubtitleExtractslator.Cli.linux-musl-arm64`
- `SubtitleExtractslator.Cli.osx-x64`
- `SubtitleExtractslator.Cli.osx-arm64`

同一个 Beta 发布集中的 RID 包使用相同预发布版本。`linux-arm`（32 位）尚未通过 NativeAOT 支持验证，因此不包含在发布集中。

## 安装 Skill

```bash
git clone --branch development https://github.com/waynebaby/SubtitleExtractslator.git
npx skills add ./SubtitleExtractslator/.github/skills/subtitle-extractslator
```

## SO 增强后的 Skill 约定

1. 真正的执行依据：`.github/skills/subtitle-extractslator/assets/so-workflow/so-template.json`
2. planner 输入文件：`.github/skills/subtitle-extractslator/assets/so-workflow/skill-plan.md`
3. 审计产物目录：`.github/skills/subtitle-extractslator/assets/so-workflow/audit/`

## GitHub 回退

仅在 NuGet 不可用时使用 Beta 通道移动回退 Release；其中包含精确版本的各 RID `.nupkg` 及对应 `.nupkg.sha512` sidecar：

<https://github.com/waynebaby/SubtitleExtractslator/releases/tag/nuget-beta-latest>
