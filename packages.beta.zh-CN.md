# SubtitleExtractslator Beta 通道包索引

该通道是从 `development` 发布的预发布通道。

这个页面是 binary-free `.github/skills/subtitle-extractslator/` skill 包获取运行时的权威入口。

## 安装命令

```bash
dotnet add package SubtitleExtractslator.Cli --version <beta-version>
```

## 运行前先看 guide

在还原或解包 `.nupkg` 后，先定位绝对 DLL 路径，再执行：

```bash
dotnet "<absolute-path>/SubtitleExtractslator.Cli.dll" --guide
```

## SO 增强后的 Skill 约定

1. 真正的执行依据：`.github/skills/subtitle-extractslator/assets/so-workflow/so-template.json`
2. planner 输入文件：`.github/skills/subtitle-extractslator/assets/so-workflow/skill-plan.md`
3. 审计产物目录：`.github/skills/subtitle-extractslator/assets/so-workflow/audit/`

## GitHub 回退下载

仅在包管理源不可用时使用回退通道。

- Beta 通道最新回退 Release: <https://github.com/waynebaby/SubtitleExtractslator/releases/tag/nuget-beta-latest>
- Beta 通道 `.latest.nupkg`: <https://github.com/waynebaby/SubtitleExtractslator/releases/download/nuget-beta-latest/SubtitleExtractslator.Cli.latest.nupkg>
