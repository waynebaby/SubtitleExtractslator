# SubtitleExtractslator 稳定通道包索引

该通道是从 `main` 发布的稳定生产线。

这个页面是 binary-free `.github/skills/subtitle-extractslator/` skill 包获取运行时的权威入口。

## 安装命令

```bash
dotnet add package SubtitleExtractslator.Cli --version <stable-version>
```

## 运行前先看 guide

在还原或解包 `.nupkg` 后，先定位绝对 DLL 路径，再执行：

```bash
dotnet "<absolute-path>/SubtitleExtractslator.Cli.dll" --guide
```

## SO 增强说明

SO 增强后的 workflow template 目前仅在 Beta 文档路径中维护。当前 `so-template.json` 合同说明请使用 `packages.beta.md`。

## GitHub 回退下载

仅在包管理源不可用时使用回退通道。

- 稳定通道最新回退 Release: <https://github.com/waynebaby/SubtitleExtractslator/releases/tag/nuget-stable-latest>
- 稳定通道 `.latest.nupkg`: <https://github.com/waynebaby/SubtitleExtractslator/releases/download/nuget-stable-latest/SubtitleExtractslator.Cli.latest.nupkg>
