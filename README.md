# 音频输出切换 · 快捷方式生成器

> 一个 Windows 桌面应用：选择音频输出设备和图标，一键生成可切换默认音频输出的快捷方式。

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![Platform](https://img.shields.io/badge/Platform-Windows-blue.svg)]()
[![.NET](https://img.shields.io/badge/.NET%20Framework-4.x-purple.svg)]()

---

## 目录

- [简介](#简介)
- [为什么不用 NirCmd](#为什么不用-nircmd)
- [快速开始](#快速开始)
- [使用说明](#使用说明)
- [命令行接口](#命令行接口)
- [工作原理](#工作原理)
- [开发指南](#开发指南)
- [常见问题](#常见问题)
- [许可证](#许可证)

---

## 简介

**音频输出切换 · 快捷方式生成器** 是一个单文件 Windows 桌面程序，用于解决一个很具体的日常问题：*在扬声器、耳机、显示器音箱、蓝牙音箱之间频繁切换，每次都要点开声音设置。*

它把「切换到某个输出设备」这件事做成一个可以放在桌面/任务栏/开始菜单的快捷方式，因此：

- 双击一次即可切换，无需打开任何设置界面；
- 图标由你自选（`.ico` 文件），可以与设备本身对应；
- 可选绑定全局快捷键（如 `Ctrl+Alt+1`），彻底不用鼠标。

整个程序**只有一个 exe，没有任何外部依赖**：不需要 Node.js，不需要安装 .NET 运行时，也不需要随附 NirCmd。

### 功能一览

| 功能 | 说明 |
|------|------|
| 🎧 设备列表 | 自动枚举所有音频输出设备，标注当前默认设备，区分「已启用 / 已禁用 / 未连接」 |
| 🖼 自定义图标 | 选择 `.ico` / `.exe` / `.dll` 作为快捷方式图标，界面内实时预览；不选则使用程序自带图标 |
| ⚡ 一键生成 | 在指定目录（默认桌面）生成 `.lnk`，可选复制程序到固定目录以保证快捷方式长期有效 |
| ⌨️ 全局快捷键 | 可为快捷方式绑定 `Ctrl+Alt+1` ~ `Ctrl+Alt+9` |
| 🔔 切换通知 | 切换成功后弹出系统托盘通知 |
| 🎭 角色选择 | 可只切换「控制台 / 多媒体」，保留「通讯」（语音通话）设备不变 |
| 🧪 立即测试 | 生成之前可以先试一下切换效果 |

---

## 为什么不用 NirCmd

姊妹项目 [sound-device-switcher](https://github.com/chancoki/sound-device-switcher) 基于 NirCmd 的 `setdefaultsounddevice`，思路很轻巧，但在图形化场景下有两个硬伤，本项目的实现方式正是为了绕开它们：

1. **NirCmd 按设备名匹配。** 而 Windows 里同名设备非常常见 —— 本机就有 5 个设备都叫「扬声器」，分别属于 Realtek、USB 声卡、Steam 串流、网易虚拟声卡等。`setdefaultsounddevice "扬声器"` 到底切到哪一个是不确定的。

   本项目改用 Windows Core Audio 的 **端点 ID**（`{0.0.0.00000000}.{guid}`）精确定位，同名设备也能准确区分。

2. **`nircmd.exe syslistaudio` 并不是一个真实存在的命令。** 它在 NirCmd 2.87 上静默失败、不输出任何内容，因此无法用它来枚举设备。

   本项目直接调用 `IMMDeviceEnumerator` 枚举，拿到的是 Windows 自己使用的设备信息（友好名、适配器名、状态）。

> 顺带一提：Windows 从未提供过公开的「设置默认音频设备」API。NirCmd、SoundVolumeView、AudioDeviceCmdlets 等工具实际上都在调用一个未公开的 COM 接口 `IPolicyConfig`，本项目也一样 —— 区别只在于匹配设备的方式。

---

## 快速开始

### 前置条件

- Windows 8 及以上（已在 Windows 11 26100 上验证；Windows 7 需要自行安装 .NET Framework 4.x）
- 无需安装任何运行时或依赖

### 构建

```powershell
powershell -ExecutionPolicy Bypass -File build.ps1
```

构建产物为 `dist\AudioSwitch.exe`（约 158 KB）。构建过程只使用 Windows 自带的 C# 编译器（`%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe`），**不联网、不下载、不需要 SDK**。

首次构建时还会自动生成应用图标 `src\app.ico`（由 `tools\IconGen.cs` 绘制并打包成 8 种尺寸的 ico）。之后可用 `-SkipIcon` 跳过：

```powershell
powershell -ExecutionPolicy Bypass -File build.ps1 -SkipIcon
```

### 运行

```powershell
dist\AudioSwitch.exe
```

---

## 使用说明

界面分三步，对应窗口里的三张卡片：

**1 选择音频输出设备**

列出所有输出设备。名称取自 Windows 的友好名（`耳机 (Realtek High Definition Audio)`），下方灰字是驱动/适配器名，用于区分同名设备。右侧蓝色「默认」标签表示该设备正是当前默认输出设备。未启用的设备会标注状态，选中时会提示切换可能无效。

**2 选择图标（可选）**

点「浏览图标…」选择 `.ico` 文件（也支持从 `.exe` / `.dll` 中提取图标），左侧方框会实时预览。点「使用默认图标」可清除选择，此时快捷方式使用本程序自带的图标。

> 支持含 PNG 压缩条目的现代 `.ico`（256×256 那张通常是 PNG 格式）。万一某个图标无法预览，界面会给出提示，但**仍可正常生成快捷方式** —— 资源管理器读取图标的兼容性比程序内的预览更广。图标文件损坏或格式不对时会直接报错。

**3 快捷方式设置**

| 项 | 说明 |
|----|------|
| 快捷方式名称 | 默认随所选设备自动填写；改成自己的名字后，该设备会记住它（切走再切回来仍是你的名字）。清空输入框即可恢复自动填写 |
| 快捷键 | 可选 `Ctrl+Alt+1` ~ `Ctrl+Alt+9`。**快捷方式需位于桌面或开始菜单，全局快捷键才会生效** |
| 保存位置 | 默认桌面，可改成任意目录 |
| 切换后显示通知 | 切换成功弹出托盘通知 |
| 同时设置全部角色 | 勾选：同时切换「控制台 / 多媒体 / 通讯」；取消：只切换「控制台 / 多媒体」，保留语音通话设备不变 |
| 复制程序到固定目录 | 把程序复制到 `%LOCALAPPDATA%\AudioSwitch\` 并让快捷方式指向该副本，这样即使你移动或删除了下载来的程序，快捷方式依然有效（**推荐**） |

底部三个按钮：

- **立即测试切换** —— 立刻切换一次，验证设备是否可用（同时刷新列表里的「默认」标签）；
- **打开所在文件夹** —— 打开生成结果所在目录，并选中刚生成的快捷方式；
- **一键生成快捷方式** —— 生成 `.lnk`。

---

## 命令行接口

同一个 exe 也提供命令行模式，方便脚本化或排查问题。在终端中运行时，输出会自动附加到当前控制台（程序本身是 GUI 子系统，不会额外弹出黑窗）。

```text
AudioSwitch.exe                                打开图形界面
AudioSwitch.exe --list [--all]                 列出输出设备（--all 含未连接设备）
AudioSwitch.exe --default                      显示当前默认输出设备
AudioSwitch.exe --switch <设备ID> [--label <名称>] [--notify] [--roles <角色>]
AudioSwitch.exe --make-shortcut --device <设备ID> [--name <名称>] [--dir <目录>]
                                [--icon <图标>] [--hotkey <快捷键>] [--target <程序>]
                                [--notify] [--roles <角色>]
AudioSwitch.exe --help
```

- `--roles`：`all`（默认）或 `console,multimedia,communications` 的任意组合
- `--hotkey`：如 `Ctrl+Alt+1`
- `--notify`：切换后弹出托盘通知

**退出码**：`0` 成功 · `1` 切换/创建失败 · `2` 找不到设备 · `3` 设备未启用

示例：

```powershell
# 看看有哪些设备（* 表示当前默认）
.\dist\AudioSwitch.exe --list

# 在当前目录生成一个快捷方式，使用指定图标和快捷键
.\dist\AudioSwitch.exe --make-shortcut `
  --device "{0.0.0.00000000}.{088294fd-63b0-4259-a828-c9a52f0a622d}" `
  --name "切到耳机" --dir "D:\Shortcuts" --icon "D:\icons\headset.ico" --hotkey "Ctrl+Alt+1"
```

生成的快捷方式实际执行的命令形如：

```text
AudioSwitch.exe --switch "{0.0.0.00000000}.{088294fd-...}" --label "耳机 (Realtek High Definition Audio)" --roles all --notify
```

`--label` 是为了容错：如果设备被拔掉再插上，Windows 可能分配新的端点 ID，此时程序会回退到按名称匹配。

---

## 工作原理

```text
枚举设备        IMMDeviceEnumerator::EnumAudioEndpoints(eRender, stateMask)
                └─ IMMDevice::GetId()            → 端点 ID
                └─ IMMDevice::OpenPropertyStore() → 友好名 / 设备描述 / 适配器名
                └─ IMMDevice::GetState()          → 已启用 / 已禁用 / 未连接

读取当前默认    IMMDeviceEnumerator::GetDefaultAudioEndpoint(eRender, role)   × 3 个角色

切换默认设备    IPolicyConfig::SetDefaultEndpoint(端点ID, role)                × 选中的角色

生成快捷方式    IShellLinkW  (SetPath / SetArguments / SetIconLocation / SetHotkey / SetWorkingDirectory)
                └─ IPersistFile::Save(.lnk)

切换通知        NotifyIcon::ShowBalloonTip + 一段消息循环
```

几个值得注意的实现细节：

- **`IPolicyConfig` 是未公开接口**，其 vtable 顺序不能改动。`src/CoreAudioInterop.cs` 中的声明顺序即是 ABI 契约。
- **`SetDefaultEndpoint` 的参数是端点 ID，不是名称**，尽管它叫 `pszDeviceName`。
- **角色有三个**：控制台（系统声音）、多媒体（播放）、通讯（VoIP）。Windows 分别记录，所以「切换输出」通常要三个都设。
- **`--switch` 不创建任何窗口**，因此快捷方式启动时不会闪黑框；通知靠一个隐藏的 `NotifyIcon` 完成。
- **图标预览不使用 `Icon.ToBitmap()`**：.NET Framework 遇到含 PNG 压缩条目的 `.ico` 会抛异常，程序改为自行解析 ICO 容器（详见 `knowledge.md`）。
- **未处理异常有兜底**：`Application.ThreadException` 会被接管，界面不会弹出 .NET 原始崩溃对话框，而是给出可读提示并写入 `AudioSwitch-error.log`。
- **DPI 缩放自己实现**（见下）。

### 关于 DPI

WinForms 自带的 `AutoScaleMode.Dpi` 在本项目里被刻意**关闭**了，原因值得记一笔：

设置 `AutoScaleDimensions` 时，WinForms 会把它归一化成当前 DPI（在 125% 缩放的屏幕上，(96,96) 会变成 (120,120)），于是它自己的缩放比值恒为 1；但它**仍然**会按原始比值缩放所有子控件。结果是子控件都被放大了 1.25 倍，而顶层窗口保持设计尺寸 —— 界面右侧被裁掉。

因此本项目改为：`AutoScaleMode = None`，由 `SwitchForm.ApplyScale()` 在构造时读取 DPI、统一缩放窗口和每个控件（`Theme.Scale` / `Theme.S()`），自定义绘制的圆角、内边距等也全部走 `Theme.S()`。点单位的字体本来就会随 DPI 自动放大，两边因此始终一致。

---

## 开发指南

### 项目结构

```text
sound-device-switcher-desktop/
├── build.ps1                  # 构建脚本（调用系统自带 csc.exe）
├── src/
│   ├── Program.cs             # 入口：GUI / CLI 分发，控制台附加
│   ├── SwitchForm.cs          # 主窗口与全部布局
│   ├── UiKit.cs               # 配色、字体、自定义控件（卡片/按钮/设备列表）
│   ├── CoreAudioInterop.cs    # Core Audio + IPolicyConfig COM 声明
│   ├── AudioManager.cs        # 设备枚举、当前默认设备、切换默认设备
│   ├── Shortcut.cs            # IShellLink 创建 .lnk
│   ├── CommandLine.cs         # 参数解析 + 快捷方式参数生成
│   ├── Notifier.cs            # 托盘通知
│   ├── AssemblyInfo.cs        # 版本信息
│   ├── app.manifest           # DPI 感知、视觉样式、兼容性声明
│   └── app.ico                # 应用图标（构建时生成）
├── tools/
│   ├── IconGen.cs             # 图标生成器（构建时工具）
│   └── capture-window.ps1     # 窗口截图工具（DPI 感知，用于界面核对）
├── dist/
│   └── AudioSwitch.exe        # 构建产物
└── README.md
```

### 语言版本限制

构建使用 .NET Framework 自带的 `csc.exe`，它只支持 **C# 5**。因此源码中不能使用：

- 字符串插值 `$"..."`（用 `string.Format` 或 `+` 拼接）
- 表达式体成员 `=>`、`nameof`、`?.`、`out var`、自动属性初始化器

### 中文与编码

`csc.exe` 在没有 BOM 时会按系统 ANSI 代码页读取源文件，会把中文字符串读乱。因此 `build.ps1` 显式传入 **`/codepage:65001`**，源文件统一使用 UTF-8（无 BOM）。

`build.ps1` 自身带 UTF-8 BOM，以便 Windows PowerShell 5.1 正确解析其中的中文输出。

### 手动编译

```powershell
$csc = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
& $csc /nologo /target:winexe /platform:anycpu /optimize+ /codepage:65001 `
  /out:dist\AudioSwitch.exe `
  /reference:System.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll `
  /win32icon:src\app.ico /win32manifest:src\app.manifest `
  src\*.cs
```

### 界面核对

`tools\capture-window.ps1` 可以给正在运行的窗口截图，用于核对布局：

```powershell
powershell -ExecutionPolicy Bypass -File tools\capture-window.ps1 `
  -ProcessName AudioSwitch -Out shot.png
```

它会以 per-monitor DPI 感知的上下文测量窗口 —— 这一点很关键：PowerShell 本身是 DPI 不感知的，直接调用 `GetWindowRect` 会被系统按缩放比例虚拟化（125% 下 975px 宽的窗口会报成 780px），据此截图会缺一块。

程序自身也保留了布局自检：设置环境变量 `AUDIOSWITCH_DIAG=<文件路径>` 启动，会把缩放比、窗口尺寸、各控件位置写入该文件后退出。

---

## 常见问题

### Q: 双击快捷方式没反应？

切换过程不显示任何窗口，如果同时关闭了通知，表现就是「没有任何反馈」。可以在生成快捷方式时勾选「切换后显示通知」，或在终端里运行 `AudioSwitch.exe --switch "<设备ID>"` 观察输出和退出码。

### Q: 切换了但声音还是从原来的设备出来？

- 该设备可能处于「未连接」状态（列表中有标注），Windows 会拒绝切换；
- 部分应用（尤其是独占音频的播放器、游戏）需要重启才能跟随新的默认设备；
- 若只勾选了部分角色，某些应用读取的可能是另一个角色对应的设备。

### Q: 快捷方式的全局快捷键不起作用？

`.lnk` 的快捷键只在快捷方式位于**桌面**或**开始菜单**时生效，放在普通文件夹里不行。另外若快捷键与其它程序冲突，也不会生效。

### Q: 移动了 exe，快捷方式失效了？

快捷方式记录的是绝对路径。生成时勾选「复制程序到固定目录（推荐）」，让快捷方式指向 `%LOCALAPPDATA%\AudioSwitch\AudioSwitch.exe`，就不会因为挪动下载目录而失效。

### Q: 生成的快捷方式图标不显示我选的 ico？

资源管理器会缓存图标。若图标文件被替换过内容，可能仍显示旧图；换个文件名或重启资源管理器即可。

### Q: 程序弹出「操作未能完成」提示怎么办？

说明某一步操作失败了，但程序不会崩溃退出。提示里会给出日志文件的位置（exe 同目录的 `AudioSwitch-error.log`），把它发出来即可定位。图标无法预览、磁盘无写入权限等都走这条提示，而不是弹出 .NET 的原始崩溃对话框。

### Q: 之前选择 ico 时闪退，现在还会吗？

不会了。原因已定位并修复：.NET Framework 的 `Icon.ToBitmap()` 无法解码 `.ico` 里 **PNG 压缩**的条目（现代图标普遍用它存 256×256 那张），会抛「请求的范围扩展超过了数组的结尾」。现在程序自己解析 `.ico` 容器，PNG 条目交给 GDI+、未压缩条目手工还原，并且所有图标操作都有兜底，不再可能出现未处理异常。

### Q: 支持 Windows 7 吗？

代码路径上没有使用 Windows 8+ 专有 API，`app.manifest` 也声明了 Windows 7 兼容。但 Windows 7 需自行安装 .NET Framework 4.x，且未在本机验证。

---

## 许可证

本项目基于 [MIT License](LICENSE) 开源。

版权所有 © 2025 [chancoki](https://github.com/chancoki)

---

## 致谢

- [sound-device-switcher](https://github.com/chancoki/sound-device-switcher) —— 本项目的起点
- [NirSoft](https://www.nirsoft.net/) —— NirCmd 展示了 `IPolicyConfig` 这条路径的可行性
