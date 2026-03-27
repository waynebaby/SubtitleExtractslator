# Subtitle Extractslator（中文）

[English README](README.md)

SubtitleExtractslator 是一个以 skill 为主体的字幕翻译项目。

这个仓库的核心交付物是 skill 包（提示词、策略、运行资产与使用约定）。CLI 与 MCP 服务端是 skill 的运行时实现层，用于在本地脚本和 agent 环境中稳定执行该 skill。

仓库中的运行形态：
- CLI（本地命令行自动化）
- MCP stdio 服务器（供 agent / MCP 客户端调用）

它覆盖从字幕探测到最终 SRT 输出的完整链路：探测已有字幕、检索候选、提取源字幕、按上下文分组翻译、合并并输出结果，同时尽量保持时间轴和结构稳定。

## 下载

<!-- release-links:start -->
- Release 总入口：[Releases](https://github.com/waynebaby/SubtitleExtractslator/releases)
- Windows x64 包（v0.1.4）：[subtitle-extractslator-v0.1.4-win-x64.zip](https://github.com/waynebaby/SubtitleExtractslator/releases/download/v0.1.4/subtitle-extractslator-v0.1.4-win-x64.zip)
- Windows ARM64 包（v0.1.4）：[subtitle-extractslator-v0.1.4-win-arm64.zip](https://github.com/waynebaby/SubtitleExtractslator/releases/download/v0.1.4/subtitle-extractslator-v0.1.4-win-arm64.zip)
- Linux x64 包（v0.1.4）：[subtitle-extractslator-v0.1.4-linux-x64.zip](https://github.com/waynebaby/SubtitleExtractslator/releases/download/v0.1.4/subtitle-extractslator-v0.1.4-linux-x64.zip)
- Linux musl x64 包（v0.1.4）：[subtitle-extractslator-v0.1.4-linux-musl-x64.zip](https://github.com/waynebaby/SubtitleExtractslator/releases/download/v0.1.4/subtitle-extractslator-v0.1.4-linux-musl-x64.zip)
- Linux ARM64 包（v0.1.4）：[subtitle-extractslator-v0.1.4-linux-arm64.zip](https://github.com/waynebaby/SubtitleExtractslator/releases/download/v0.1.4/subtitle-extractslator-v0.1.4-linux-arm64.zip)
- Linux musl ARM64 包（v0.1.4）：[subtitle-extractslator-v0.1.4-linux-musl-arm64.zip](https://github.com/waynebaby/SubtitleExtractslator/releases/download/v0.1.4/subtitle-extractslator-v0.1.4-linux-musl-arm64.zip)
- Linux ARM 包（v0.1.4）：[subtitle-extractslator-v0.1.4-linux-arm.zip](https://github.com/waynebaby/SubtitleExtractslator/releases/download/v0.1.4/subtitle-extractslator-v0.1.4-linux-arm.zip)
- macOS ARM64 包（v0.1.4）：[subtitle-extractslator-v0.1.4-osx-arm64.zip](https://github.com/waynebaby/SubtitleExtractslator/releases/download/v0.1.4/subtitle-extractslator-v0.1.4-osx-arm64.zip)
- macOS x64 包（v0.1.4）：[subtitle-extractslator-v0.1.4-osx-x64.zip](https://github.com/waynebaby/SubtitleExtractslator/releases/download/v0.1.4/subtitle-extractslator-v0.1.4-osx-x64.zip)
<!-- release-links:end -->

## 这个 Skill 解决什么问题

- 提供可复用的字幕工作流 skill 约定（probe/search/extract/translate/merge）。
- 在翻译过程中保持 cue 顺序与时间戳稳定。
- 通过 MCP tools 标准化 agent 侧调用方式。
- 通过 CLI 暴露分组、批量、重试、模型与端点等运行参数。

## 当前实现范围

运行模式：
- CLI（默认）
- MCP stdio（`--mode mcp`）

工作流步骤：
1. 探测媒体文件中的字幕轨道。
2. 查询 OpenSubtitles 候选（配置后走真实 API，未配置可走 mock）。
3. 本地提取字幕（优先英文，失败时做确定性回退）。
4. 按时间线规则分组 cues。
5. 构建滚动场景摘要与历史上下文。
6. 按模式策略执行翻译。
7. 合并并输出 SRT。
8. 可选：将生成的 AI 字幕回封装进原视频，作为新语言字幕轨道。

翻译策略：
- MCP 模式：仅 sampling（`sampling/createMessage`），sampling 失败直接报错。
- CLI 模式：仅 external provider（包含自定义 endpoint 访问）。

## 构建

```powershell
dotnet build SubtitleExtractslator.sln
```

## 项目结构

- `subtitle-extractslator/`：skill 包（主体）
- `SubtitleExtractslator.Cli/`：skill 运行时宿主（CLI + MCP tools + workflow 核心）
- `docs/`：安装与运维文档
- `samples/`：示例字幕与 trace 文件

## CLI 用法

```powershell
dotnet run --project SubtitleExtractslator.Cli -- --mode cli probe --input "movie.mkv" --lang zh

dotnet run --project SubtitleExtractslator.Cli -- --mode cli opensubtitles-search --input "movie.mkv" --lang zh --search-query-primary "movie" --search-query-normalized "movie s00e00" --opensubtitles-api-key "<key>"

dotnet run --project SubtitleExtractslator.Cli -- --mode cli extract --input "movie.mkv" --out "movie.en.srt" --prefer en

dotnet run --project SubtitleExtractslator.Cli -- --mode cli translate --input "movie.en.srt" --lang zh --output "movie.zh.srt"

dotnet run --project SubtitleExtractslator.Cli -- --mode cli translate-batch --input-list ".\\inputs.txt" --lang zh --output-dir ".\\out" --output-suffix ".zh.srt"
```

批量输入文件格式（`--input-list`）：
- UTF-8 文本文件。
- 每行一个媒体/字幕文件路径。
- 空行和以 `#` 开头的行会被忽略。

批量模式仅在 CLI 提供。MCP 模式不提供批量工作流，以避免 MCP 客户端常见的超时问题。

CLI 通用参数：
- `--env "KEY=VALUE;KEY2=VALUE2"`：仅对当前命令注入临时环境变量覆盖。
- `--help`：打印完整命令帮助。

## MCP stdio 模式

```powershell
dotnet run --project SubtitleExtractslator.Cli -- --mode mcp
```

MCP 传输与工具注册使用官方 `ModelContextProtocol` NuGet 包（`AddMcpServer().WithStdioServerTransport().WithTools<...>()`）。

MCP tools：
- `probe`
- `opensubtitles_search`
- `extract`
- `translate`

MCP 工具返回约定：

- 工具返回结构化对象：`ok`、`data`、`error`。
- 成功时：`ok=true`，`data` 为工具结果。
- 失败时：`ok=false`，`error` 包含 `code`、`message`、可选 `snapshotPath`、`timeUtc`。

## 翻译提供者说明

- MCP sampling 使用官方 `sampling/createMessage`。
- MCP sampling 重试次数遵循 `LLM_RETRY_COUNT`（或覆盖值）。
- 当响应过大时，下一次重试会注入“简化思考”提示以降低过度思考输出。
- MCP 模式翻译失败时不会回退到 external。
- external / 自定义 endpoint 访问仅走 CLI 路线。

## OpenSubtitles

- 配置 `OPENSUBTITLES_API_KEY` 后可使用真实 OpenSubtitles 访问器。
- 建议同时配置 `OPENSUBTITLES_USERNAME` 与 `OPENSUBTITLES_PASSWORD` 以支持下载配额与会话。
- 仍保留 `OPENSUBTITLES_MOCK=1` 的离线测试分支。
- 真实 API 集成建议拆分到独立 provider 模块，并补充鉴权与限流处理。

## 单文件发布示例

```powershell
dotnet publish SubtitleExtractslator.Cli -c Release -r win-x64 -p:PublishSingleFile=true -p:SelfContained=true

dotnet publish SubtitleExtractslator.Cli -c Release -r linux-x64 -p:PublishSingleFile=true -p:SelfContained=true

dotnet publish SubtitleExtractslator.Cli -c Release -r osx-arm64 -p:PublishSingleFile=true -p:SelfContained=true
```





