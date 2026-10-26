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

用户预设、显示器恢复备份等保存在 `%LOCALAPPDATA%\ScreenDock`，不属于源码，不随安装包分发。卸载默认保留这些文件。应用不联网，尚未实现自动更新。

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
