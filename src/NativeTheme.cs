// NativeTheme.cs — Windows 11 window chrome.
//
// Three best-effort touches that make the window look like a native Windows 11
// app rather than a plain Win32 frame:
//
//   * rounded window corners (DWM_WINDOW_CORNER_PREFERENCE)
//   * an immersive dark title bar when the user runs the dark app theme
//   * the user's own accent colour, so buttons match the rest of the shell
//
// Every call is best effort. On Windows 10 and older the DWM attributes are
// simply rejected, and the registry may be unreadable; the app then keeps its
// light palette and plain square corners instead of failing.

using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

namespace AudioSwitch
{
    internal static class NativeTheme
    {
        private const int DwmwaUseImmersiveDarkMode = 20;      // Windows 11 22000+
        private const int DwmwaUseImmersiveDarkModeLegacy = 19; // 1809..21H2
        private const int DwmwaWindowCornerPreference = 33;
        private const int DwmwcpRound = 2;

        [DllImport("dwmapi.dll", PreserveSig = true)]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute,
                                                        ref int value, int size);

        /// <summary>
        /// True when the user runs apps in the dark theme.
        /// Set AUDIOSWITCH_THEME=dark or =light to override, which keeps the two
        /// palettes verifiable without changing the user's Windows settings.
        /// </summary>
        public static bool IsDarkMode()
        {
            string forced = null;
            try { forced = Environment.GetEnvironmentVariable("AUDIOSWITCH_THEME"); }
            catch (Exception) { }

            if (!string.IsNullOrEmpty(forced))
            {
                string value = forced.Trim().ToLowerInvariant();
                if (value == "dark") return true;
                if (value == "light") return false;
            }

            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                {
                    if (key != null)
                    {
                        object value = key.GetValue("AppsUseLightTheme");
                        if (value is int) return (int)value == 0;
                    }
                }
            }
            catch (Exception)
            {
            }
            return false;
        }

        /// <summary>
        /// The user's accent colour, or the Windows 11 default blue when it cannot
        /// be read. The DWM stores AccentColor as 0xAABBGGRR.
        /// </summary>
        public static Color AccentColor()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\DWM"))
                {
                    if (key != null)
                    {
                        object value = key.GetValue("AccentColor");
                        if (value is int)
                        {
                            int raw = (int)value;
                            int r = raw & 0xFF;
                            int g = (raw >> 8) & 0xFF;
                            int b = (raw >> 16) & 0xFF;

                            // Reject near-black and near-white accents: as a button
                            // fill they would read as broken rather than as a colour.
                            int sum = r + g + b;
                            if (sum > 90 && sum < 700) return Color.FromArgb(r, g, b);
                        }
                    }
                }
            }
            catch (Exception)
            {
            }
            return Color.FromArgb(0x00, 0x67, 0xC0);
        }

        /// <summary>Round the window corners and match the title bar to the palette.</summary>
        public static void ApplyWindowChrome(Form form, bool dark)
        {
            if (form == null || !form.IsHandleCreated) return;

            try
            {
                IntPtr hwnd = form.Handle;
                int on = dark ? 1 : 0;

                if (DwmSetWindowAttribute(hwnd, DwmwaUseImmersiveDarkMode, ref on, 4) != 0)
                    DwmSetWindowAttribute(hwnd, DwmwaUseImmersiveDarkModeLegacy, ref on, 4);

                int preference = DwmwcpRound;
                DwmSetWindowAttribute(hwnd, DwmwaWindowCornerPreference, ref preference, 4);
            }
            catch (Exception)
            {
                // Older Windows: keep the plain frame.
            }
        }
    }

    /// <summary>
    /// Resolving the user's shell folders.
    ///
    /// <see cref="Environment.GetFolderPath"/> is the normal route, but it returns
    /// an empty string when the profile's folder registry values are redirected or
    /// unavailable — which leaves the save-location box blank and the user with no
    /// default. The registry value the shell itself uses is read as a fallback.
    /// </summary>
    internal static class ShellFolders
    {
        public static string Desktop()
        {
            string path = SafeGet(Environment.SpecialFolder.DesktopDirectory);
            if (path.Length > 0) return path;

            path = SafeGet(Environment.SpecialFolder.Desktop);
            if (path.Length > 0) return path;

            return FromRegistry("Desktop");
        }

        public static string LocalAppData()
        {
            string path = SafeGet(Environment.SpecialFolder.LocalApplicationData);
            if (path.Length > 0) return path;

            return FromRegistry("Local AppData");
        }

        private static string SafeGet(Environment.SpecialFolder folder)
        {
            try { return Environment.GetFolderPath(folder) ?? ""; }
            catch (Exception) { return ""; }
        }

        /// <summary>Read the shell's own "Shell Folders" value, expanding %USERPROFILE%.</summary>
        private static string FromRegistry(string name)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Explorer\Shell Folders"))
                {
                    if (key != null)
                    {
                        string value = key.GetValue(name) as string;
                        if (!string.IsNullOrEmpty(value))
                            return Environment.ExpandEnvironmentVariables(value);
                    }
                }
            }
            catch (Exception)
            {
            }
            return "";
        }
    }
}
