// Program.cs — entry point.
//
// Launched with no arguments this is the GUI. Launched with --switch (which is
// exactly what the generated .lnk files do) it silently changes the default
// output device and optionally pops a tray balloon.

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32.SafeHandles;

namespace AudioSwitch
{
    internal static class Program
    {
        private const int ATTACH_PARENT_PROCESS = -1;
        private const int STD_OUTPUT_HANDLE = -11;
        private const int STD_ERROR_HANDLE = -12;
        private const uint GENERIC_WRITE = 0x40000000;
        private const uint FILE_SHARE_WRITE = 0x00000002;
        private const uint OPEN_EXISTING = 3;

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool AttachConsole(int processId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetStdHandle(int stdHandle);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetConsoleMode(IntPtr consoleHandle, out uint mode);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern SafeFileHandle CreateFile(
            string fileName, uint desiredAccess, uint shareMode, IntPtr securityAttributes,
            uint creationDisposition, uint flagsAndAttributes, IntPtr templateFile);

        [STAThread]
        private static int Main(string[] args)
        {
            InstallExceptionHandlers();

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            if (args == null || args.Length == 0) return RunGui();

            string command = args[0].ToLowerInvariant();
            switch (command)
            {
                case "--switch":
                case "/switch":
                    return RunSwitch(args);

                case "--list":
                case "/list":
                    return RunList(args);

                case "--default":
                case "--get-default":
                    return RunDefault();

                case "--make-shortcut":
                    return RunMakeShortcut(args);

                case "--help":
                case "-h":
                case "/?":
                case "--?":
                    EnsureConsole();
                    PrintHelp();
                    return 0;

                default:
                    if (command.StartsWith("--") || command.StartsWith("/"))
                    {
                        EnsureConsole();
                        Console.Error.WriteLine("未知参数：" + args[0]);
                        PrintHelp();
                        return 1;
                    }
                    return RunGui();
            }
        }

        // ------------------------------------------------------------------
        // GUI
        // ------------------------------------------------------------------

        private static int RunGui()
        {
            try
            {
                Application.Run(new SwitchForm());
                return 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "程序启动失败：" + ex.Message + Environment.NewLine + Environment.NewLine + ex.StackTrace,
                    "音频输出切换", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return 1;
            }
        }

        // ------------------------------------------------------------------
        // --switch : the mode every generated shortcut uses
        // ------------------------------------------------------------------

        private static int RunSwitch(string[] args)
        {
            EnsureConsole();

            Dictionary<string, string> options = CommandLine.Parse(args);
            string deviceId = CommandLine.Get(options, "switch");
            string label = CommandLine.Get(options, "label");
            bool notify = CommandLine.Has(options, "notify");
            RoleSelection roles = CommandLine.ParseRoles(CommandLine.Get(options, "roles"));

            AudioEndpoint device = AudioManager.Resolve(deviceId, label);
            if (device == null)
            {
                string message = "找不到音频设备：" +
                    (string.IsNullOrEmpty(label) ? deviceId : label);
                Console.Error.WriteLine(message);
                if (notify) Notifier.ShowBalloon("切换失败", message, Notifier.DefaultTimeoutMs);
                return 2;
            }

            if (!device.IsActive)
            {
                string message = device.BestName + " 当前" + device.StateText + "，无法切换。";
                Console.Error.WriteLine(message);
                if (notify) Notifier.ShowBalloon("切换失败", message, Notifier.DefaultTimeoutMs);
                return 3;
            }

            string error = AudioManager.SetDefaultDevice(device.Id, roles);
            if (error != null)
            {
                Console.Error.WriteLine(error);
                if (notify) Notifier.ShowBalloon("切换失败", error, Notifier.DefaultTimeoutMs);
                return 1;
            }

            string summary = "当前输出：" + device.BestName;
            if (roles != RoleSelection.All)
                summary = summary + "（" + CommandLine.RolesToString(roles) + "）";

            Console.WriteLine("已将默认音频输出切换为 " + device.BestName);
            if (notify) Notifier.ShowBalloon("音频输出已切换", summary, Notifier.DefaultTimeoutMs);
            return 0;
        }

        // ------------------------------------------------------------------
        // --list / --default
        // ------------------------------------------------------------------

        private static int RunList(string[] args)
        {
            EnsureConsole();

            Dictionary<string, string> options = CommandLine.Parse(args);
            bool includeInactive = CommandLine.Has(options, "all");

            List<AudioEndpoint> devices = AudioManager.GetOutputDevices(
                includeInactive ? DeviceStateFlags.ALL : DeviceStateFlags.ACTIVE);

            List<string> defaultIds = new List<string>();
            foreach (ERole role in AudioManager.AllRoles)
            {
                string id = AudioManager.GetDefaultDeviceId(role);
                if (!string.IsNullOrEmpty(id) && !defaultIds.Contains(id)) defaultIds.Add(id);
            }

            foreach (AudioEndpoint device in devices)
            {
                string marker = defaultIds.Contains(device.Id) ? "* " : "  ";
                Console.WriteLine("{0}{1}  [{2}]", marker, device.BestName, device.StateText);
                Console.WriteLine("    {0}", device.Id);
            }
            Console.WriteLine();
            Console.WriteLine("* = 当前的默认输出设备（共 {0} 个）", devices.Count);
            return 0;
        }

        private static int RunDefault()
        {
            EnsureConsole();

            List<AudioEndpoint> devices = AudioManager.GetOutputDevices(DeviceStateFlags.ALL);
            foreach (ERole role in AudioManager.AllRoles)
            {
                string id = AudioManager.GetDefaultDeviceId(role);
                string name = id;
                foreach (AudioEndpoint device in devices)
                {
                    if (string.Equals(device.Id, id, StringComparison.OrdinalIgnoreCase))
                    {
                        name = device.BestName;
                        break;
                    }
                }
                Console.WriteLine("{0}: {1}", AudioManager.RoleName(role), name);
            }
            return 0;
        }

        // ------------------------------------------------------------------
        // --make-shortcut : same code path the GUI button uses, scriptable
        // ------------------------------------------------------------------

        private static int RunMakeShortcut(string[] args)
        {
            EnsureConsole();

            Dictionary<string, string> options = CommandLine.Parse(args);
            string deviceId = CommandLine.Get(options, "device");
            string label = CommandLine.Get(options, "label");

            AudioEndpoint device = AudioManager.Resolve(deviceId, label);
            if (device == null)
            {
                Console.Error.WriteLine("找不到音频设备：" +
                    (string.IsNullOrEmpty(label) ? deviceId : label));
                return 2;
            }

            ShortcutOptions shortcut = new ShortcutOptions();
            shortcut.Directory = CommandLine.Get(options, "dir");
            if (string.IsNullOrEmpty(shortcut.Directory))
                shortcut.Directory = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);

            shortcut.Name = CommandLine.Get(options, "name");
            if (string.IsNullOrEmpty(shortcut.Name)) shortcut.Name = device.BestName;

            shortcut.TargetPath = CommandLine.Get(options, "target");
            if (string.IsNullOrEmpty(shortcut.TargetPath)) shortcut.TargetPath = Application.ExecutablePath;

            shortcut.IconPath = CommandLine.Get(options, "icon");
            shortcut.Hotkey = CommandLine.Get(options, "hotkey");
            shortcut.Description = "将默认音频输出设备切换为 " + device.BestName;
            shortcut.Arguments = CommandLine.BuildSwitchArguments(
                device, CommandLine.Has(options, "notify"),
                CommandLine.ParseRoles(CommandLine.Get(options, "roles")));

            try
            {
                if (!Directory.Exists(shortcut.Directory)) Directory.CreateDirectory(shortcut.Directory);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("无法创建目录：" + ex.Message);
                return 1;
            }

            bool accessDenied;
            string error = ShortcutFactory.Create(shortcut, out accessDenied);
            if (error != null)
            {
                Console.Error.WriteLine(error);
                if (accessDenied)
                {
                    Console.Error.WriteLine(
                        "提示：目标文件夹拒绝了写入。可改用其他位置（如桌面、文档），"
                        + "或以管理员身份运行本程序重试。");
                }
                return accessDenied ? 4 : 1;
            }

            Console.WriteLine("已生成快捷方式：" + shortcut.FullPath);
            return 0;
        }

        // ------------------------------------------------------------------
        // Safety net
        // ------------------------------------------------------------------

        /// <summary>
        /// Keep an unexpected failure from surfacing as the raw .NET crash dialog
        /// ("应用程序中发生了未经处理的异常"). The window stays usable and the user
        /// gets a readable message plus a log to send along.
        /// </summary>
        private static void InstallExceptionHandlers()
        {
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);

            Application.ThreadException += delegate(object sender, ThreadExceptionEventArgs e)
            {
                ReportUnexpectedError(e.Exception, true);
            };

            AppDomain.CurrentDomain.UnhandledException +=
                delegate(object sender, UnhandledExceptionEventArgs e)
            {
                ReportUnexpectedError(e.ExceptionObject as Exception, false);
            };
        }

        private static void ReportUnexpectedError(Exception error, bool canContinue)
        {
            string detail = error == null ? "未知错误" : error.ToString();
            string logPath = TryWriteErrorLog(detail);

            StringBuilder message = new StringBuilder();
            message.AppendLine(canContinue
                ? "操作未能完成，程序会继续运行。"
                : "程序遇到了一个严重错误，即将关闭。");
            message.AppendLine();
            message.AppendLine(error == null ? "未知错误" : error.Message);
            if (!string.IsNullOrEmpty(logPath))
            {
                message.AppendLine();
                message.AppendLine("详细信息已写入：");
                message.AppendLine(logPath);
            }

            try
            {
                MessageBox.Show(message.ToString(), "音频输出切换 · 快捷方式生成器",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            catch (Exception)
            {
            }
        }

        /// <summary>Append to a log beside the executable; never throw from here.</summary>
        private static string TryWriteErrorLog(string detail)
        {
            try
            {
                string directory = Path.GetDirectoryName(Application.ExecutablePath);
                if (string.IsNullOrEmpty(directory)) return null;

                string path = Path.Combine(directory, "AudioSwitch-error.log");
                File.AppendAllText(path, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                    + Environment.NewLine + detail + Environment.NewLine
                    + new string('-', 60) + Environment.NewLine);
                return path;
            }
            catch (Exception)
            {
                return null;
            }
        }

        // ------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------

        /// <summary>
        /// Make Console.Write* land somewhere when this GUI-subsystem process is run
        /// from a terminal. Two distinct cases:
        ///
        ///  * stdout is already a valid handle (piped or redirected): write straight
        ///    to it, using UTF-8 when it is not a real console so redirection is
        ///    encoding-stable, and the console code page when it is one.
        ///  * stdout is NULL (launched from a console, but a /target:winexe process
        ///    inherits no handles): attach to the parent console and reopen CONOUT$.
        ///
        /// When there is no parent console at all (a shortcut launch from Explorer)
        /// this leaves output going nowhere, and nothing is ever printed on screen.
        /// </summary>
        private static void EnsureConsole()
        {
            try
            {
                bool attached = AttachConsole(ATTACH_PARENT_PROCESS);

                StreamWriter stdout = OpenStandardStream(STD_OUTPUT_HANDLE, attached);
                if (stdout != null) Console.SetOut(stdout);

                StreamWriter stderr = OpenStandardStream(STD_ERROR_HANDLE, attached);
                if (stderr != null) Console.SetError(stderr);
            }
            catch (Exception)
            {
            }
        }

        private static StreamWriter OpenStandardStream(int which, bool attachedToConsole)
        {
            IntPtr handle = GetStdHandle(which);
            bool haveHandle = handle != IntPtr.Zero && handle != new IntPtr(-1);

            Stream stream;
            Encoding encoding;

            if (haveHandle)
            {
                uint mode;
                stream = new FileStream(new SafeFileHandle(handle, false), FileAccess.Write);
                encoding = GetConsoleMode(handle, out mode)
                    ? Console.OutputEncoding      // a real console: use its code page
                    : new UTF8Encoding(false);    // piped/redirected: stay encoding-stable
            }
            else if (attachedToConsole)
            {
                SafeFileHandle console = CreateFile("CONOUT$", GENERIC_WRITE, FILE_SHARE_WRITE,
                    IntPtr.Zero, OPEN_EXISTING, 0, IntPtr.Zero);
                if (console.IsInvalid) return null;

                stream = new FileStream(console, FileAccess.Write);
                encoding = Console.OutputEncoding;
            }
            else
            {
                return null;
            }

            StreamWriter writer = new StreamWriter(stream, encoding);
            writer.AutoFlush = true;
            return writer;
        }

        private static void PrintHelp()
        {
            Console.WriteLine();
            Console.WriteLine("音频输出切换 · 快捷方式生成器");
            Console.WriteLine();
            Console.WriteLine("用法：");
            Console.WriteLine("  AudioSwitch.exe                              打开图形界面");
            Console.WriteLine("  AudioSwitch.exe --list [--all]               列出输出设备（--all 含未连接设备）");
            Console.WriteLine("  AudioSwitch.exe --default                    显示当前默认输出设备");
            Console.WriteLine("  AudioSwitch.exe --switch <设备ID> [--label <名称>] [--notify] [--roles <角色>]");
            Console.WriteLine("                                               切换默认输出设备（快捷方式使用此命令）");
            Console.WriteLine("  AudioSwitch.exe --make-shortcut --device <设备ID> [--name <名称>] [--dir <目录>]");
            Console.WriteLine("                                  [--icon <图标>] [--hotkey <快捷键>] [--target <程序>]");
            Console.WriteLine("                                  [--notify] [--roles <角色>]");
            Console.WriteLine();
            Console.WriteLine("  --roles   all | console,multimedia,communications   （默认 all）");
            Console.WriteLine("  --hotkey  例如 Ctrl+Alt+1（快捷方式需位于桌面或开始菜单才会生效）");
            Console.WriteLine();
            Console.WriteLine("退出码：0 成功 · 1 参数或创建失败 · 2 找不到设备 · 3 设备未启用 · 4 目标文件夹拒绝写入");
            Console.WriteLine();
        }
    }
}
