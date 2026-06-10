# SubtitleExtractslator 稳定通道包索引

该通道是从 `main` 发布的稳定生产线。

## 安装命令

```bash
dotnet add package SubtitleExtractslator.Cli --version <stable-version>
```

## 运行前先看 guide

在还原并拿到运行输出后，先执行：

```bash
dotnet SubtitleExtractslator.Cli.dll --guide
```

## GitHub 回退下载

仅在包管理源不可用时使用回退通道。

- 稳定通道最新回退 Release: https://github.com/waynebaby/SubtitleExtractslator/releases/tag/nuget-stable-latest
- 稳定通道 `.latest.nupkg`: https://github.com/waynebaby/SubtitleExtractslator/releases/download/nuget-stable-latest/SubtitleExtractslator.Cli.latest.nupkg
