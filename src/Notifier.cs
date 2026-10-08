// Notifier.cs — the tray balloon shown after a silent device switch.
//
// A .lnk launched from Explorer has no console and no window, so the only
// feedback the user gets is this balloon. It borrows the NotifyIcon approach
// the NirCmd-based original used ("trayballoon") but without the extra binary.

using System;
using System.Drawing;
using System.Windows.Forms;

namespace AudioSwitch
{
    internal static class Notifier
    {
        public const int DefaultTimeoutMs = 4000;

        /// <summary>
        /// Show a balloon and pump messages until it has had time to display.
        /// Blocks for roughly timeoutMs + 750ms, then returns.
        /// </summary>
        public static void ShowBalloon(string title, string text, int timeoutMs)
        {
            if (timeoutMs <= 0) timeoutMs = DefaultTimeoutMs;

            try
            {
                Application.EnableVisualStyles();

                NotifyIcon trayIcon = new NotifyIcon();
                Icon icon = IconLoader.LoadApplicationIcon(32);

                trayIcon.Icon = icon;
                // NotifyIcon.Text is capped at 63 characters by the shell.
                trayIcon.Text = Trim(title + " - " + text, 63);
                trayIcon.Visible = true;
                trayIcon.BalloonTipTitle = title;
                trayIcon.BalloonTipText = text;
                trayIcon.BalloonTipIcon = ToolTipIcon.Info;
                trayIcon.ShowBalloonTip(timeoutMs);

                Timer timer = new Timer();
                timer.Interval = timeoutMs + 750;
                timer.Tick += delegate(object sender, EventArgs e)
                {
                    timer.Stop();
                    timer.Dispose();
                    trayIcon.Visible = false;
                    trayIcon.Dispose();
                    Application.ExitThread();
                };
                timer.Start();

                // A message loop is required for the balloon to appear at all.
                // ApplicationContext with no MainForm keeps running until ExitThread.
                Application.Run(new ApplicationContext());
            }
            catch (Exception)
            {
                // Never let a failed notification turn a successful switch into an error.
            }
        }

        private static string Trim(string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value)) return "";
            if (value.Length <= maxLength) return value;
            return value.Substring(0, maxLength - 1) + "…";
        }
    }
}
