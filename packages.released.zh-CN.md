# SubtitleExtractslator 稳定版 NativeAOT 包索引

稳定版发布集来自 `main`。Skill 从仓库安装；稳定版 Release 不发布 skill ZIP。

## 版本规则

<!-- package-version-block:start -->
- per-RID NativeAOT 稳定包集合尚未首次发布；首个候选版本沿用共享数值 high-water 基线递增。
- Stable 会在所有 stable/Beta RID 包的最高数值版本上递增 patch；`main` 不追加 prerelease 后缀。
<!-- package-version-block:end -->

## 恢复本机运行时

Skill 每次启动都会检查此通道的 NuGet 最新版本；仅当本机 RID 包缺失或缓存版本过期时才下载：

```bash
python3 assets/bootstrap/restore_runtime.py --channel stable
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

同一个稳定版发布集中的 RID 包使用相同版本。`linux-arm`（32 位）尚未通过 NativeAOT 支持验证，因此不包含在发布集中。

## 安装 Skill

从 GitHub 仓库安装 Skill，不使用 Release 压缩包：

```bash
npx skills add waynebaby/SubtitleExtractslator --skill subtitle-extractslator
```

## SO 执行

- 正式运行：`dotnet so.dll run --workflow-file <skill-path>/assets/so-workflow/so-template.json`
- 正式恢复：`dotnet so.dll resume --workflow-file <runtime-workflow-copy>.json --result-file <external-result>.json`
- 直接 CLI/MCP 只作为运行时基础能力，不属于正式 skill 执行历史。

## GitHub 回退

仅在 NuGet 不可用时使用稳定通道移动回退 Release；其中包含精确版本的各 RID `.nupkg` 及对应 `.nupkg.sha512` sidecar：

<https://github.com/waynebaby/SubtitleExtractslator/releases/tag/nuget-stable-latest>
