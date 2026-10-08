# Knowledge

## What is this?

**音频输出切换 · 快捷方式生成器** — 一个单文件 Windows 桌面应用，用于生成「切换默认音频输出设备」的快捷方式。

- 纯原生 C# / WinForms，构建产物只有一个 `dist\AudioSwitch.exe`（约 158 KB）
- 无需 Node.js、无需安装 .NET 运行时、不依赖 NirCmd
- 与 CLI 姊妹项目 `sound-device-switcher`（基于 NirCmd）解决同一个问题，但改用端点 ID 精确定位设备

## Key Commands

| 命令 | 说明 |
|------|------|
| `powershell -ExecutionPolicy Bypass -File build.ps1` | 构建（含图标生成） |
| `powershell -ExecutionPolicy Bypass -File build.ps1 -SkipIcon` | 只编译，复用已有 `src\app.ico` |
| `.\dist\AudioSwitch.exe` | 打开图形界面 |
| `.\dist\AudioSwitch.exe --list [--all]` | 列出输出设备（`*` 标记当前默认） |
| `.\dist\AudioSwitch.exe --default` | 显示三个角色的当前默认设备 |
| `.\dist\AudioSwitch.exe --switch <ID> [--label <名>] [--notify] [--roles <角色>]` | 切换默认设备（快捷方式使用的命令） |
| `.\dist\AudioSwitch.exe --make-shortcut --device <ID> [--name] [--dir] [--icon] [--hotkey] [--target]` | 生成快捷方式（与 GUI 按钮同一条代码路径） |
| `tools\capture-window.ps1 -ProcessName AudioSwitch -Out shot.png` | 给运行中的窗口截图（DPI 感知） |

## Dependencies

- **运行依赖：** 无。使用系统自带的 .NET Framework 4.x（Windows 8+ 内置）
- **构建依赖：** `%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe`（Windows 自带，仅支持 **C# 5**）
- **开发依赖：** 无，不需要网络

## Architecture

| 文件 | 职责 |
|------|------|
| `Program.cs` | 入口；GUI/CLI 分发；`EnsureConsole()` 让 GUI 子系统程序在终端里也能输出 |
| `SwitchForm.cs` | 主窗口、全部布局坐标、DPI 缩放、业务动作 |
| `UiKit.cs` | `Theme`（配色/字体/缩放）、`CardPanel`、`FlatButton`、`DeviceListBox`、`IconLoader` |
| `CoreAudioInterop.cs` | Core Audio 公开接口 + 未公开 `IPolicyConfig` 的 COM 声明 |
| `AudioManager.cs` | `AudioEndpoint` 模型、枚举、读默认设备、切换默认设备 |
| `Shortcut.cs` | `IShellLinkW` / `IPersistFile`，创建 `.lnk`；文件名清洗；快捷键解析 |
| `CommandLine.cs` | 参数解析；生成快捷方式的参数串 |
| `Notifier.cs` | 托盘气泡通知（带消息循环） |

## Conventions & Gotchas

### 验证 GUI 时的坑（血泪教训）

- **跨进程读控件文本，`GetWindowText` 会返回过期缓存值，必须用 `WM_GETTEXT`。** 用 `GetWindowText` 读另一个进程里的 TextBox，拿到的是窗口管理器缓存的老值 —— 明明名称框已经更新了，读出来还是旧的，会让人误判成「功能失效」。`SendMessage(hwnd, WM_GETTEXT, capacity, buffer)` 才会真正向控件查询。同理，读 ListBox 的项要用 `LB_GETTEXT`，读索引用 `LB_GETCURSEL`。
- **WinForms 控件的类名是 `WindowsForms10.EDIT.app.0.xxx` 这种形式**，不是 `Edit`/`ListBox`。匹配时要 `-match 'EDIT'`，用 `-eq` 会全部落空。
- 想确认「当前焦点在哪个控件」，用 `GetGUIThreadInfo(GetWindowThreadProcessId(hwnd))` 取 `hwndFocus`，再比对句柄。比反复按 Tab 猜要可靠得多。
- 用 `SendKeys` 做 Tab 导航时必须**每步校验焦点**；否则一旦 Tab 次数差一个，文本就会输入到别的控件里（本项目中曾把路径粘进了「保存位置」）。
- 用 PowerShell 临时脚本做这类探针时**不要写中文**：文件无 BOM 时可能被按 ANSI 解析而语法报错（即使 pwsh 7 也会因环境而异）。纯 ASCII 最省事。

### 与自绘控件相关

- **画圆角时必须用「父控件背景色」清屏，不能用控件自身的背景色。** `CardPanel` 原来写的是 `g.Clear(Theme.CardBackground)`（白色），它会把整个矩形控件填成白色，而圆角路径只画了边框 —— 结果四个角是白的方块，圆角线缩在里面，看起来就是「圆角失效」。正确做法：`g.Clear(Theme.BackdropOf(this, 回退色))` 再用圆角路径 `FillPath` 铺底色。
- `Theme.BackdropOf()` 会沿 `Parent` 向上找到第一个不透明背景色，因此同一个按钮放在白色卡片上和放在灰色窗体上都能正确显示圆角。
- 这个缺陷从第一版就存在，且**逐像素比对过用户早期截图与修复前的构建，两者完全一致** —— 排查这类问题时要先测像素，不要凭印象判断是不是「最近改坏了」。
- 圆角半径等一切尺寸都走 `Theme.S()`，否则高 DPI 下半径不会随控件一起放大。

### 与图标相关（重要）

- **绝对不要用 `System.Drawing.Icon.ToBitmap()` 渲染预览。** .NET Framework 在解析含 **PNG 压缩条目**的 `.ico` 时会抛 `ArgumentOutOfRangeException`（「请求的范围扩展超过了数组的结尾。」）。而现代图标普遍用 PNG 存放 256×256 那一张 —— 本项目测试用的 5 个 `.ico` 里有 4 个会崩。更糟的是 `new Icon(path, 48, 48)` 并不会报错，它会忽略请求尺寸并返回 256×256（即那个无法解码的条目），所以错误直到 `ToBitmap()` 才爆发。
- 因此 `IconLoader` **自己解析 ICO 容器**（`ICONDIR` + `ICONDIRENTRY`）：PNG 条目交给 `Image.FromStream`（GDI+ 解码 PNG 没问题），未压缩的 DIB 条目由 `DecodeDib()` 手工还原（支持 32bpp/24bpp，自下而上的 BGRA 行）。其余位深回退到 `new Icon(path)`，失败也只是预览失败。
- **预览失败绝不应阻断生成快捷方式。** 资源管理器自己会读 `.ico`，能处理的格式比 .NET 多。`CreateShortcut` 不依赖预览是否成功。
- 选择条目的策略：优先取「不小于预览尺寸的最小条目」，否则取最大的。
- `UiKit.cs` 里的 `IconLoader.IsSupportedIconPath()` 同时被预览和 `ShortcutFactory` 使用，避免两处判断不一致。
- 程序图标 `src\app.ico` 只有 256 那一张是 PNG，其余尺寸是 DIB，因此 `Icon.ExtractAssociatedIcon` 也能正常读取它。

### 与异常处理相关

- `Program.InstallExceptionHandlers()` 接管了 `Application.ThreadException` 和 `AppDomain.UnhandledException`，避免再出现 .NET 原始的「应用程序中发生了未经处理的异常」对话框。UI 线程异常会写日志（exe 同目录的 `AudioSwitch-error.log`）并提示用户，然后继续运行。
- 凡是有外部输入参与的操作（读图标、建快捷方式、切设备），函数本身都应捕获异常并返回可读的错误字符串，而不是让异常逃逸到消息循环。

### 与 Windows 音频相关

- **Windows 没有公开的「设置默认音频设备」API。** 必须调用未公开的 `IPolicyConfig`（CLSID `{870af99c-171d-4f9e-af0d-e63df40c2bc9}`）。已在 Windows 11 26100 上验证可用。
- **`IPolicyConfig` 的 vtable 顺序是 ABI 契约**，`CoreAudioInterop.cs` 中方法的声明顺序不能调整、不能删减。
- **`SetDefaultEndpoint` 接受的是端点 ID**（`{0.0.0.00000000}.{guid}`），不是设备名 —— 尽管形参叫 `pszDeviceName`。
- **角色有三个**：`eConsole`（控制台）、`eMultimedia`（多媒体）、`eCommunications`（通讯）。要完整切换「默认输出」通常三个都要设；GUI 取消勾选「同时设置全部角色」时只设前两个。
- **设备名会重名。** 本机有 5 个设备都叫「扬声器」，因此一切匹配都必须基于端点 ID；`--label` 仅作为 ID 失效时的回退。
- **`nircmd.exe syslistaudio` 不是真实命令**，静默失败且无输出（NirCmd 2.87）。姊妹项目的 README 里这条示例是错的。
- `DeviceStateFlags`：`1` 已启用、`2` 已禁用、`4` 未连接、`8` 已拔出。`ALL = 0x0F`。

### 与 WinForms / DPI 相关

- **不要重新打开 `AutoScaleMode`。** 详情见 README「关于 DPI」：设置 `AutoScaleDimensions` 会让 WinForms 把它归一化成当前 DPI，导致它只缩放子控件、不缩放顶层窗口，界面右侧被裁掉。当前实现是 `AutoScaleMode.None` + `SwitchForm.ApplyScale()` 手工统一缩放。
- `Theme.Scale` 是静态字段，必须在**创建任何控件之前**赋值（`SwitchForm` 构造函数第一步），因为 `DeviceListBox._padLeft`、`FlatButton.CornerRadius` 等都是在字段初始化器里用它算出来的。
- 自定义绘制（卡片圆角、内边距、列表行文字位置）一律走 `Theme.S()`，否则高 DPI 下会与缩放后的控件对不齐。
- `DeviceListBox` 行内文字位置按 `ItemHeight` 的比例计算，而不是写死像素。
- 窗口在 `Screen.PrimaryScreen.WorkingArea` 放不下时会自动启用 `AutoScroll`，避免按钮够不着。

### 与 PowerShell / 工具链相关

- **`csc.exe` 无 BOM 时按系统 ANSI 代码页读源文件**，会把中文字面量读乱。`build.ps1` 必须传 `/codepage:65001`，源文件保持 UTF-8（无 BOM）。
- **`build.ps1` 自身要带 UTF-8 BOM**，否则 Windows PowerShell 5.1 会按 ANSI 解析，中文字符串会破坏语法。注意：用编辑工具改过 `build.ps1` 后 BOM 可能丢失，需重新写入。
- `build.ps1` 的参数不能叫 `-Debug`，与 `[CmdletBinding()]` 注入的公共参数冲突，故用 `-DebugBuild`。
- **PowerShell 是 DPI 不感知的**：直接对别的窗口调用 `GetWindowRect` 会拿到被虚拟化的坐标（125% 下 975px 报成 780px）。测量/截图必须先 `SetThreadDpiAwarenessContext(-4)`。
- **本沙箱不投递合成鼠标事件**：`SetCursorPos` + `mouse_event` 能把光标移到正确位置，但应用收不到点击。因此本环境**无法**验证需要点击的按钮（刷新列表、浏览图标…、生成快捷方式等）。键盘输入可用（焦点控件能收到 `SendKeys` / `Ctrl+V`），据此验证了图标输入框 → 预览这条链路。按钮调用的底层函数另行通过 CLI 与无头测试覆盖。
- **中文输入法会改写 `SendKeys` 的内容**：本机拼音输入法把 `\` 变成了 `、`，`mk3` 变成了 `口`。用 `SendKeys` 输入含反斜杠的路径时务必先关输入法，或改用剪贴板 `Ctrl+V`。
- **PowerShell 不会等待 GUI 子系统程序**，且 `Invoke-Item`／`Start-Process` 打开 `.lnk` 会阻塞到子进程结束。要在脚本里安全地跑 GUI exe，用 `Start-Process -Wait -RedirectStandardOutput <文件>`（重定向后不会继承并占用调用方的管道）。
- 程序是 `/target:winexe`（保证快捷方式启动不闪黑框），因此 `Console.WriteLine` 需要一个附加控制台的过程：`EnsureConsole()` 会先 `AttachConsole(ATTACH_PARENT_PROCESS)`；若标准句柄本身有效则直接写入（管道/重定向时用 UTF-8 保证编码稳定，真实控制台则用其代码页），否则打开 `CONOUT$`。没有任何控制台时保持沉默。

### 环境相关

- 本机的 DSH 沙箱会限制**由 agent 启动的子进程**的写入范围：子进程只有在工作区内才能写文件，写桌面 / `%TEMP%` / `%LOCALAPPDATA%` 均返回 `E_ACCESSDENIED`。这是环境限制，不是程序缺陷 —— 用户正常双击运行时不受影响。若在沙箱内验证，请把 `--dir` 指向工作区内的目录。

## Verified behaviour

在本机 Windows 11（26100）实测通过：

- 设备枚举：20 个输出端点（8 个已启用），友好名/适配器名/状态均正确
- 切换默认设备：三个角色全部切换成功，并已还原为原值
  （控制台/多媒体 = `耳机 (Realtek High Definition Audio)`，通讯 = `扬声器 (YTL Audio )`）
- 快捷方式：目标、参数、图标、快捷键（`Alt+Ctrl+1`）、工作目录、描述均正确写入
- 通过 shell 启动生成的 `.lnk`：参数正确传入，程序按参数执行
- `--switch` 无 `--notify` 时 0.4 秒退出；带 `--notify` 时 4.9 秒（气泡显示完即退出），不残留进程
- 界面在 125% 缩放下完整显示：缩放比 1.25，客户区 975×962，无裁切
- 图标预览：8 个样本（含 4 个曾令 `ToBitmap()` 崩溃的 PNG 条目 ico）全部解码成功、透明通道正确
- 图形界面内输入不存在的图标路径：显示红色友好提示且**不崩溃**；输入有效 `.ico`：预览正确显示并提示「已选择」

### 未能在本环境验证

沙箱不投递合成鼠标事件，以下需要点击的路径未能实测，它们调用的函数已另行覆盖：

| 未实测 | 其底层函数已被验证的方式 |
|--------|--------------------------|
| 刷新列表 | 同一 `LoadDevices()` 在启动时正确渲染「默认」标签 |
| 浏览图标… | `IconLoader` 无头测试 + 输入框链路实测 |
| 立即测试切换 | `AudioManager.SetDefaultDevice` 经 CLI 实测 |
| 一键生成快捷方式 | `ShortcutFactory.Create` 经 CLI 实测 |
| 保存位置 / 图标路径写入桌面 | 沙箱禁止子进程写工作区外目录（见下） |

### 环境限制

- 沙箱会限制**由 agent 启动的子进程**的写入范围：子进程只有在工作区内才能写文件，写桌面 / `%TEMP%` / `%LOCALAPPDATA%` 均返回 E_ACCESSDENIED。用户正常双击运行时不受影响。
- 沙箱不投递合成鼠标事件，且中文输入法会改写 `SendKeys` 内容。
