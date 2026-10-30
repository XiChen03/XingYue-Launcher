# XingYue Launcher

包含 XingYue 的大部分源代码，包括 UI 库、动画模块、下载模块、Minecraft 启动模块、联机模块与开服器模块等。

本仓库为自维护分支：不包含上游的下载页、功能投票页、帮助文档库链接，也不内置任何第三方 API Key。<br/>
非常感谢社区的大力支持！

## 目录结构

| 目录 | 说明 |
| --- | --- |
| `XingYue/` | 启动器主程序（VB.NET / WPF，`net48`） |
| `XingYueCS/` | 启动器的 C# 侧组件 |
| `MeloongCore/` | 公共库（`Shared` 通用工具 + `Wpf` 控件），以源码形式随仓库提供 |
| `RelayTool/` | 联机中转节点（`XingYue联机中转节点.exe`） |
| `tools/` | 构建与辅助脚本 |
| `patches/` | 历史补丁 |

## 构建

需要 Windows 与 [.NET SDK 8](https://dotnet.microsoft.com/download)（不需要安装 Visual Studio）：

```powershell
powershell -ExecutionPolicy Bypass -File tools\build.ps1 -Configuration Release
```

产物位于 `XingYue\bin\XingYue Launcher.exe`。

构建脚本会自动处理三件事：从 NuGet 获取 .NET Framework 4.8 引用程序集（本机无需安装开发包）、把语言版本放宽到 SDK 自带的最新版本、关闭标记编译期的包引用检查（旧式项目没有 `PackageReference`）。

## 运行期配置（不含任何内置密钥）

- **CurseForge 官方源**：仓库不内置 API Key。需要使用时请自行申请，然后二选一：
  - 设置环境变量 `XINGYUE_CURSEFORGE_API_KEY`
  - 在 `设置 → 其他 → 下载源` 里填写

  没有配置 Key 时，社区资源默认走镜像源（镜像源不需要 Key，加载失败时也会自动回退到官方源）。
- **微软登录**：默认使用公共 ClientId；若你自建了 Azure 应用，可设置环境变量 `XINGYUE_MS_CLIENT_ID` 改用 `v2.0` 端点。
- **樱花映射**：首次使用时会自动下载 `frpc` 到 `XingYue\SakuraFrp\`，需要用户自己的访问密钥。
- **EasyTier 联机**：内核已内置于程序资源中，运行时解压到 `XingYue\EasyTier\`。

程序产生的配置、日志与缓存都写在启动器同目录的 `XingYue\` 文件夹里，不会写入 `%AppData%`。

## 许可

见 [LICENCE](LICENCE)。
