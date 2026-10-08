// Shortcut.cs — create a .lnk via IShellLink (no PowerShell / WScript.Shell needed).

using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace AudioSwitch
{
    [ComImport, Guid("00021401-0000-0000-C000-000000000046")]
    internal class ShellLinkCoClass
    {
    }

    [Guid("000214F9-0000-0000-C000-000000000046")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IShellLinkW
    {
        void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder file, int maxPath, IntPtr data, int flags);
        void GetIDList(out IntPtr itemIdList);
        void SetIDList(IntPtr itemIdList);
        void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder name, int maxName);
        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string name);
        void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder dir, int maxPath);
        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string dir);
        void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder args, int maxPath);
        void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string args);
        void GetHotkey(out short hotkey);
        void SetHotkey(short hotkey);
        void GetShowCmd(out int showCmd);
        void SetShowCmd(int showCmd);
        void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder iconPath, int maxPath, out int iconIndex);
        void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string iconPath, int iconIndex);
        void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pathRel, int reserved);
        void Resolve(IntPtr hwnd, int flags);
        void SetPath([MarshalAs(UnmanagedType.LPWStr)] string path);
    }

    [Guid("0000010b-0000-0000-C000-000000000046")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IPersistFile
    {
        void GetClassID(out Guid classId);
        [PreserveSig] int IsDirty();
        void Load([MarshalAs(UnmanagedType.LPWStr)] string fileName, int mode);
        void Save([MarshalAs(UnmanagedType.LPWStr)] string fileName, [MarshalAs(UnmanagedType.Bool)] bool remember);
        void SaveCompleted([MarshalAs(UnmanagedType.LPWStr)] string fileName);
        void GetCurFile([MarshalAs(UnmanagedType.LPWStr)] out string fileName);
    }

    internal sealed class ShortcutOptions
    {
        /// <summary>Where the .lnk is written (a directory).</summary>
        public string Directory = "";

        /// <summary>The .lnk file name without extension.</summary>
        public string Name = "";

        /// <summary>Executable the shortcut launches, normally this program.</summary>
        public string TargetPath = "";

        public string Arguments = "";
        public string Description = "";

        /// <summary>.ico (or .exe/.dll) used for the shortcut's icon; empty = use the target's own icon.</summary>
        public string IconPath = "";

        /// <summary>Optional global hotkey, e.g. "Ctrl+Alt+1". Empty for none.</summary>
        public string Hotkey = "";

        public string FullPath
        {
            get { return Path.Combine(Directory, Name + ".lnk"); }
        }
    }

    internal static class ShortcutFactory
    {
        // Modifier flags accepted by IShellLink::SetHotkey (the HOTKEYF_* values).
        private const int HOTKEYF_SHIFT = 0x01;
        private const int HOTKEYF_CONTROL = 0x02;
        private const int HOTKEYF_ALT = 0x04;

        private static readonly string[] InvalidNameChars = new string[]
        {
            "\\", "/", ":", "*", "?", "\"", "<", ">", "|"
        };

        /// <summary>Strip characters Windows forbids in a file name.</summary>
        public static string SanitizeFileName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "";
            string result = name;
            foreach (string bad in InvalidNameChars) result = result.Replace(bad, "");
            result = result.Trim().TrimEnd('.');
            return result;
        }

        public static bool IsValidIconFile(string path, out string error)
        {
            // Shares the icon checks with the preview so the two can never disagree.
            return IconLoader.IsSupportedIconPath(path, out error);
        }

        /// <summary>
        /// Parse a hotkey string such as "Ctrl+Alt+3" into the packed WORD that
        /// IShellLink::SetHotkey expects: modifiers in the high byte, virtual key low.
        /// </summary>
        public static short ParseHotkey(string text)
        {
            if (string.IsNullOrEmpty(text)) return 0;

            string normalized = text.Replace(" ", "").ToLowerInvariant();
            if (normalized == "none" || normalized == "无" || normalized == "0") return 0;

            int modifiers = 0;
            int virtualKey = 0;

            string[] parts = normalized.Split('+');
            foreach (string rawPart in parts)
            {
                string part = rawPart.Trim();
                if (part.Length == 0) continue;

                if (part == "ctrl" || part == "control") { modifiers |= HOTKEYF_CONTROL; continue; }
                if (part == "alt") { modifiers |= HOTKEYF_ALT; continue; }
                if (part == "shift") { modifiers |= HOTKEYF_SHIFT; continue; }

                if (part.Length == 1)
                {
                    char c = part[0];
                    if (c >= '0' && c <= '9') virtualKey = 0x30 + (c - '0');
                    else if (c >= 'a' && c <= 'z') virtualKey = 0x41 + (c - 'a');
                    else if (c >= 'A' && c <= 'Z') virtualKey = 0x41 + (c - 'A');
                }
                else if (part.Length >= 2 && part[0] == 'f')
                {
                    int number;
                    if (int.TryParse(part.Substring(1), out number) && number >= 1 && number <= 24)
                        virtualKey = 0x70 + (number - 1);
                }
            }

            if (virtualKey == 0) return 0;
            return (short)((modifiers << 8) | virtualKey);
        }

        /// <summary>Human-readable form of a parsed hotkey, or "" when unset.</summary>
        public static string DescribeHotkey(short hotkey)
        {
            if (hotkey == 0) return "";

            int modifiers = (hotkey >> 8) & 0xFF;
            int virtualKey = hotkey & 0xFF;

            StringBuilder text = new StringBuilder();
            if ((modifiers & HOTKEYF_CONTROL) != 0) text.Append("Ctrl+");
            if ((modifiers & HOTKEYF_ALT) != 0) text.Append("Alt+");
            if ((modifiers & HOTKEYF_SHIFT) != 0) text.Append("Shift+");

            if (virtualKey >= 0x30 && virtualKey <= 0x39)
                text.Append((char)virtualKey);
            else if (virtualKey >= 0x41 && virtualKey <= 0x5A)
                text.Append((char)virtualKey);
            else if (virtualKey >= 0x70 && virtualKey <= 0x87)
                text.Append("F").Append(virtualKey - 0x70 + 1);

            return text.ToString();
        }

        /// <summary>
        /// Write the .lnk. Returns null on success or a human-readable error message.
        /// </summary>
        public static string Create(ShortcutOptions options)
        {
            if (options == null) return "缺少快捷方式参数。";

            string name = SanitizeFileName(options.Name);
            if (string.IsNullOrEmpty(name)) return "请填写快捷方式名称。";
            if (string.IsNullOrEmpty(options.Directory)) return "请选择快捷方式的保存位置。";
            if (string.IsNullOrEmpty(options.TargetPath) || !File.Exists(options.TargetPath))
                return "找不到要启动的程序：" + options.TargetPath;

            string iconError;
            if (!IsValidIconFile(options.IconPath, out iconError)) return iconError;

            string linkPath = Path.Combine(options.Directory, name + ".lnk");

            IShellLinkW link = null;
            IPersistFile persist = null;
            try
            {
                link = (IShellLinkW)(new ShellLinkCoClass());

                link.SetPath(options.TargetPath);
                link.SetArguments(options.Arguments ?? "");
                link.SetWorkingDirectory(Path.GetDirectoryName(options.TargetPath) ?? "");
                link.SetDescription(string.IsNullOrEmpty(options.Description)
                    ? "切换默认音频输出设备" : options.Description);
                link.SetShowCmd(1); // SW_SHOWNORMAL

                if (!string.IsNullOrEmpty(options.IconPath))
                    link.SetIconLocation(options.IconPath, 0);
                else
                    link.SetIconLocation(options.TargetPath, 0);

                short hotkey = ParseHotkey(options.Hotkey);
                if (hotkey != 0) link.SetHotkey(hotkey);

                persist = (IPersistFile)link;
                persist.Save(linkPath, true);
            }
            catch (Exception ex)
            {
                return "创建快捷方式失败：" + ex.Message;
            }
            finally
            {
                AudioManager.ReleaseComObject(persist);
                AudioManager.ReleaseComObject(link);
            }

            if (!File.Exists(linkPath)) return "快捷方式创建后未找到文件：" + linkPath;
            return null;
        }
    }
}
