# SO Guide（Beta 通道本地镜像）

这个本地镜像文件用于满足当前 Beta SO 运行时的 `dotnet so.dll --guide --lang zh-cn` 启动契约查找。

当前权威 Beta 参考：
- packages index: https://github.com/waynebaby/Techne-Loom/blob/development/packages.beta.md
- guide (zh-CN): https://github.com/waynebaby/Techne-Loom/blob/development/docs/zh-cn/reference/products/so-guide.md
- guide (en): https://github.com/waynebaby/Techne-Loom/blob/development/docs/en/reference/products/so-guide.md

当前运行时锁定：
- channel: beta
- resolved version: 0.2.91-beta
- lock file: .github/skills/subtitle-extractslator/assets/so-workflow/so-package-lock.json

执行权提醒：
- 官方技能运行只承认 `dotnet so.dll run` 和 `dotnet so.dll resume`。
- 直接 CLI 与直接 MCP 只是运行时原语，不计入官方技能执行历史。