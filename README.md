# ScreenDock

轻量 Windows 10/11 x64 多显示器管理工具，使用 C#、.NET Framework 和 WinForms 开发，采用 MIT 许可证。

## 功能

- 自动识别连接的显示器，查看布局与编号。
- 按屏调节硬件亮度、内置色彩模式与暖色护眼模式（需要显示器支持 DDC/CI）。
- 首页拖动屏幕位置，选择分辨率、刷新率、主显示器和屏幕方向。
- 停用、重新启用副屏；布局修改提供倒计时确认及恢复尝试。
- 保存布局、亮度与护眼预设，支持从托盘后台切换。
- 系统托盘快捷操作、当前用户开机启动、显示器连接变化后的自动刷新。
- 中文安装向导、开始菜单入口与卸载功能，保留本地用户配置。

## 使用

安装包发布后可在仓库 Releases 下载。也可以按下文从源码构建并运行 `ScreenDock.exe`。

具体操作见 [使用说明](使用说明.md)。亮度和内置模式取决于显示器、连接方式、驱动与固件，不保证所有型号都支持。护眼功能使用显示器公开的暖色色温档位，不是系统全局滤镜。

## 构建

在 Windows 10/11 x64 的 PowerShell 中运行：

```powershell
.\build.ps1
.\test.ps1
```

编译使用系统 .NET Framework 的 `Framework64\v4.0.30319\csc.exe`，应用不依赖第三方运行库。测试使用示例数据和回调验证，不会切换真实屏幕参数；部分界面测试会短暂显示测试窗口。

制作安装包需要 Inno Setup 6：

```powershell
.\build-installer.ps1 -Compiler 'C:\Program Files (x86)\Inno Setup 6\ISCC.exe'
```

安装脚本位于 `installer/ScreenDock.iss`。安装包输出到项目的上一级目录。中文语言文件来自 [Inno Setup 官方源码仓库](https://github.com/jrsoftware/issrc/blob/main/Files/Languages/ChineseSimplified.isl)，保留原文件作者信息。

## 配置与恢复

用户预设、显示器恢复备份等保存在 `%LOCALAPPDATA%\ScreenDock`，不属于源码，不随安装包分发。卸载默认保留这些文件。应用仅在检查和下载更新时访问 GitHub；不会上传本地设置或设备信息。

屏幕布局修改取消或超时会尝试恢复；驱动异常、程序退出或崩溃时不能保证回滚。暖色模式关闭后恢复开启前可读取的设置，无法恢复显示器未公开的私有参数。

旧版预设可能没有护眼开关记录；缺失时保持当前状态，需要重新保存才能明确记录开关。预设目前要求连接的显示器组合和主屏身份匹配。

## 显示器适配

从 Windows 读取显示器型号，基于 DDC/CI capabilities 中明确公开且可读取的 VCP 0x14 / 0xDC 值提供模式，不猜测厂商私有指令。

已在 Philips 275M1RZN 上验证部分色温切换；其他型号与模式仍需要实机反馈。更多信息见 [适配指南](profiles/README.md)。提交问题时请提供系统版本、型号、连接方式和复现步骤，不要提交序列号或本地恢复备份。

## 源码结构

- `src/ScreenDock.cs`：主窗口、显示器连接和 Win32 接口。
- `src/DisplayInventory.cs`、`src/LayoutEditor.cs`、`src/InlineDisplaySettings.cs`：显示器枚举与布局编辑。
- `src/Brightness.cs`、`src/MonitorModes.cs`、`src/EyeComfort.cs`：硬件控制。
- `src/Presets.cs`、`src/TrayFeatures.cs`：预设与托盘。
- `tests/`：解析、预设、布局和界面回归测试。

## 许可证

项目代码使用 [MIT License](LICENSE)。第三方语言文件保留其原有版权与许可要求。

## 下载和自动发布

- [最新版安装包](https://github.com/penghanrose-coder/ScreenDock/releases/latest/download/ScreenDock-Setup-x64.exe)
- [全部版本和便携包](https://github.com/penghanrose-coder/ScreenDock/releases)

推送 main 后自动构建和测试，Actions 中保留 14 天测试产物。发布正式版时创建新的版本标签（例如 `v1.0.1`）并推送：

```powershell
git tag v1.0.1
git push origin v1.0.1
```

构建和测试成功后自动发布对应版本的安装包、便携包和 SHA256 校验文件。安装包版本跟随标签，最新版下载链接自动指向最新正式发布。不要移动已发布标签；修改后应使用新版本号。

这套流程自动更新 GitHub 的发布包，不会自动修改用户电脑上的软件。软件支持后台自动检查更新，并在用户确认后自动下载、校验、安装和重启。升级前从托盘退出 ScreenDock，再安装新版即可保留用户配置。安装包目前没有商业代码签名。

## 软件内更新

启动后约 15 秒检查 GitHub 最新正式版，运行期间每 6 小时再检查。设置菜单可关闭自动检查；帮助菜单和托盘提供手动检查入口。发现新版时先通知，用户确认后才下载安装。更新使用 HTTPS，并核对发布资产的大小与 SHA-256；失败时保留当前版本。便携版需先安装安装版才能自动安装更新。更新前请完成屏幕调节，并应用或重置未保存的布局。

版本号由 VERSION 文件管理；打标签发布时，程序与安装包版本统一使用标签中的版本。
