// UiKit.cs — shared visual language: colours, fonts and the few custom controls
// the form is built from. Hand-drawn rather than designer-generated so the whole
// app stays a handful of readable source files with no .resx to keep in sync.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace AudioSwitch
{
    internal static class Theme
    {
        /// <summary>
        /// Layout scale factor (1.0 at 96 DPI). Fonts created from point sizes are
        /// already DPI-correct, so pixel geometry has to be scaled by the same
        /// factor to keep the two in step. SwitchForm sets this once at startup
        /// before any control is created; AutoScaleMode is deliberately None so
        /// WinForms never scales anything behind our back.
        /// </summary>
        public static float Scale = 1F;

        /// <summary>Scale a design-time (96 DPI) pixel value.</summary>
        public static int S(float value)
        {
            return (int)Math.Round(value * Scale);
        }

        /// <summary>
        /// The opaque colour painted behind a control — its parent's background,
        /// walking up past any transparent ancestors.
        ///
        /// Anything that draws a rounded shape must fill its corners with this
        /// rather than with its own background colour. Filling the whole control
        /// rectangle with the control's own colour leaves opaque square corners
        /// sticking out past the rounded outline, which reads as "the rounded
        /// corners stopped working".
        /// </summary>
        public static Color BackdropOf(Control control, Color fallback)
        {
            Control parent = control == null ? null : control.Parent;
            while (parent != null)
            {
                Color colour = parent.BackColor;
                if (colour.A == 255) return colour;
                parent = parent.Parent;
            }
            return fallback;
        }

        public static readonly Color Background = Color.FromArgb(0xF4, 0xF6, 0xF9);
        public static readonly Color CardBackground = Color.White;
        public static readonly Color CardBorder = Color.FromArgb(0xE3, 0xE7, 0xEC);
        public static readonly Color HeaderTop = Color.FromArgb(0x1E, 0x29, 0x3B);
        public static readonly Color HeaderBottom = Color.FromArgb(0x37, 0x47, 0x5F);
        public static readonly Color Accent = Color.FromArgb(0x25, 0x63, 0xEB);
        public static readonly Color AccentDark = Color.FromArgb(0x1D, 0x4E, 0xD8);
        public static readonly Color AccentSoft = Color.FromArgb(0xEE, 0xF4, 0xFF);
        public static readonly Color TextPrimary = Color.FromArgb(0x11, 0x18, 0x27);
        public static readonly Color TextSecondary = Color.FromArgb(0x6B, 0x72, 0x80);
        public static readonly Color TextMuted = Color.FromArgb(0x9C, 0xA3, 0xAF);
        public static readonly Color Success = Color.FromArgb(0x04, 0x78, 0x57);
        public static readonly Color Danger = Color.FromArgb(0xDC, 0x26, 0x26);
        public static readonly Color RowHover = Color.FromArgb(0xFA, 0xFB, 0xFD);
        public static readonly Color Separator = Color.FromArgb(0xF1, 0xF3, 0xF6);

        private static readonly string FamilyName = PickFamily();

        public static readonly Font Title = MakeFont(15F, FontStyle.Bold);
        public static readonly Font Subtitle = MakeFont(8.5F, FontStyle.Regular);
        public static readonly Font CardTitle = MakeFont(10F, FontStyle.Bold);
        public static readonly Font Body = MakeFont(9F, FontStyle.Regular);
        public static readonly Font BodyBold = MakeFont(9.75F, FontStyle.Bold);
        public static readonly Font Small = MakeFont(8F, FontStyle.Regular);
        public static readonly Font SmallBold = MakeFont(8F, FontStyle.Bold);
        public static readonly Font Button = MakeFont(9.5F, FontStyle.Regular);
        public static readonly Font ButtonBold = MakeFont(9.75F, FontStyle.Bold);

        private static Font MakeFont(float size, FontStyle style)
        {
            try { return new Font(FamilyName, size, style); }
            catch (Exception) { return new Font(FontFamily.GenericSansSerif, size, style); }
        }

        /// <summary>
        /// Pick the first installed family from a preference list. Chinese Windows
        /// has "Microsoft YaHei UI"; an English SKU without the CJK fonts falls back
        /// to Segoe UI so the app still starts rather than throwing in the Font ctor.
        /// </summary>
        private static string PickFamily()
        {
            string[] candidates = { "Microsoft YaHei UI", "Microsoft YaHei", "Segoe UI", "Tahoma" };
            List<string> installed = new List<string>();
            try
            {
                foreach (FontFamily family in FontFamily.Families) installed.Add(family.Name);
            }
            catch (Exception)
            {
            }

            foreach (string candidate in candidates)
            {
                foreach (string name in installed)
                {
                    if (string.Equals(name, candidate, StringComparison.OrdinalIgnoreCase))
                        return name;
                }
            }
            return FontFamily.GenericSansSerif.Name;
        }

        public static GraphicsPath RoundedRect(Rectangle bounds, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            if (radius <= 0)
            {
                path.AddRectangle(bounds);
                return path;
            }

            int d = Math.Min(radius * 2, Math.Min(bounds.Width, bounds.Height));
            path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
            path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
            path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    /// <summary>A white card with a 1px border and an optional section title.</summary>
    internal class CardPanel : Panel
    {
        private readonly string _title;

        public CardPanel(string title)
        {
            _title = title;
            BackColor = Theme.CardBackground;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                     | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // Behind the card, so the corners outside the rounded outline show the
            // form background instead of an opaque white square.
            g.Clear(Theme.BackdropOf(this, Theme.Background));

            Rectangle bounds = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath path = Theme.RoundedRect(bounds, Theme.S(8)))
            {
                using (SolidBrush brush = new SolidBrush(Theme.CardBackground))
                    g.FillPath(brush, path);
                using (Pen pen = new Pen(Theme.CardBorder))
                    g.DrawPath(pen, path);
            }

            if (!string.IsNullOrEmpty(_title))
            {
                TextRenderer.DrawText(g, _title, Theme.CardTitle,
                    new Point(Theme.S(16), Theme.S(13)), Theme.TextPrimary,
                    TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
            }
        }
    }

    internal enum ButtonKind
    {
        Primary,
        Secondary,
        Ghost
    }

    /// <summary>Flat, rounded button with hand-rolled hover/press states.</summary>
    internal class FlatButton : Button
    {
        private bool _hover;
        private bool _pressed;

        public ButtonKind Kind = ButtonKind.Secondary;
        public int CornerRadius = Theme.S(6);

        public FlatButton()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                     | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            Font = Theme.Button;
            BackColor = Theme.CardBackground;
            ForeColor = Theme.TextPrimary;
            Cursor = Cursors.Hand;
            UseVisualStyleBackColor = false;
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            _hover = true;
            Invalidate();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _hover = false;
            _pressed = false;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            _pressed = true;
            Invalidate();
            base.OnMouseDown(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            _pressed = false;
            Invalidate();
            base.OnMouseUp(e);
        }

        protected override void OnEnabledChanged(EventArgs e)
        {
            Invalidate();
            base.OnEnabledChanged(e);
        }

        private Color BackColorFor()
        {
            if (!Enabled)
            {
                return Kind == ButtonKind.Primary
                    ? Color.FromArgb(0xBF, 0xCF, 0xF5)
                    : Color.FromArgb(0xF3, 0xF4, 0xF6);
            }

            switch (Kind)
            {
                case ButtonKind.Primary:
                    if (_pressed) return Color.FromArgb(0x18, 0x40, 0xB0);
                    return _hover ? Theme.AccentDark : Theme.Accent;
                case ButtonKind.Ghost:
                    return _pressed ? Color.FromArgb(0xE8, 0xEC, 0xF2)
                         : (_hover ? Color.FromArgb(0xF1, 0xF4, 0xF8) : Theme.CardBackground);
                default:
                    return _pressed ? Color.FromArgb(0xE4, 0xE8, 0xEE)
                         : (_hover ? Color.FromArgb(0xF3, 0xF6, 0xFA) : Color.FromArgb(0xFA, 0xFB, 0xFC));
            }
        }

        private Color ForeColorFor()
        {
            if (!Enabled) return Theme.TextMuted;
            return Kind == ButtonKind.Primary ? Color.White : Theme.TextPrimary;
        }

        private Color BorderColorFor()
        {
            if (Kind == ButtonKind.Primary) return BackColorFor();
            if (!Enabled) return Color.FromArgb(0xEC, 0xEF, 0xF3);
            return _hover ? Color.FromArgb(0xD2, 0xD9, 0xE2) : Theme.CardBorder;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // Corner pixels belong to whatever is behind the button, so the rounded
            // shape reads correctly on a white card and on the grey form alike.
            g.Clear(Theme.BackdropOf(this, Theme.CardBackground));

            Color back = BackColorFor();
            Rectangle bounds = new Rectangle(0, 0, Width - 1, Height - 1);

            using (GraphicsPath path = Theme.RoundedRect(bounds, CornerRadius))
            {
                using (SolidBrush brush = new SolidBrush(back)) g.FillPath(brush, path);
                using (Pen pen = new Pen(BorderColorFor())) g.DrawPath(pen, path);
            }

            TextRenderer.DrawText(g, Text, Font, bounds, ForeColorFor(),
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter
                | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
        }
    }

    /// <summary>
    /// Owner-drawn device list: a bold device name over a muted adapter line,
    /// plus a "默认" pill for the endpoint Windows is currently using.
    /// </summary>
    internal class DeviceListBox : ListBox
    {
        /// <summary>Row height at 96 DPI; re-scaled by the form on load.</summary>
        public const int DesignRowHeight = 50;

        private readonly int _padLeft = Theme.S(14);
        private readonly HashSet<string> _defaultIds =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private int _hoverIndex = -1;

        public DeviceListBox()
        {
            DrawMode = DrawMode.OwnerDrawFixed;
            ItemHeight = Theme.S(DesignRowHeight);
            BorderStyle = BorderStyle.None;
            IntegralHeight = false;
            BackColor = Theme.CardBackground;
            Font = Theme.Body;
            SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
        }

        public void SetDefaultIds(IEnumerable<string> ids)
        {
            _defaultIds.Clear();
            if (ids != null)
            {
                foreach (string id in ids)
                {
                    if (!string.IsNullOrEmpty(id)) _defaultIds.Add(id);
                }
            }
            Invalidate();
        }

        public AudioEndpoint SelectedEndpoint
        {
            get
            {
                if (SelectedIndex < 0 || SelectedIndex >= Items.Count) return null;
                return Items[SelectedIndex] as AudioEndpoint;
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            int index = IndexFromPoint(e.Location);
            if (index != _hoverIndex)
            {
                _hoverIndex = index;
                Invalidate();
            }
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            if (_hoverIndex != -1)
            {
                _hoverIndex = -1;
                Invalidate();
            }
            base.OnMouseLeave(e);
        }

        protected override void OnDrawItem(DrawItemEventArgs e)
        {
            Graphics g = e.Graphics;
            if (e.Index < 0 || e.Index >= Items.Count) return;

            AudioEndpoint endpoint = Items[e.Index] as AudioEndpoint;
            if (endpoint == null) return;

            bool selected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
            Rectangle row = new Rectangle(0, e.Bounds.Y, Width, ItemHeight);

            Color background = selected ? Theme.AccentSoft
                             : (e.Index == _hoverIndex ? Theme.RowHover : Theme.CardBackground);

            using (SolidBrush brush = new SolidBrush(background)) g.FillRectangle(brush, row);

            if (selected)
            {
                using (SolidBrush brush = new SolidBrush(Theme.Accent))
                    g.FillRectangle(brush, new Rectangle(row.X, row.Y, 3, row.Height));
            }

            Color nameColor = endpoint.IsActive
                ? (selected ? Theme.AccentDark : Theme.TextPrimary)
                : Theme.TextSecondary;

            // Proportions of the row height, so the two text lines stay balanced at
            // any DPI without needing separate absolute measurements.
            int nameTop = row.Y + (int)Math.Round(ItemHeight * 0.18);
            int detailTop = row.Y + (int)Math.Round(ItemHeight * 0.60);
            int textWidth = row.Width - _padLeft - Theme.S(96);

            Rectangle nameRect = new Rectangle(row.X + _padLeft, nameTop, textWidth,
                (int)Math.Round(ItemHeight * 0.40));
            TextRenderer.DrawText(g, endpoint.BestName,
                selected ? Theme.BodyBold : Theme.Body, nameRect, nameColor,
                TextFormatFlags.Left | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis
                | TextFormatFlags.NoPadding);

            string detail = endpoint.InterfaceName;
            if (string.IsNullOrEmpty(detail)) detail = endpoint.Id;
            if (!endpoint.IsActive) detail = detail + "  ·  " + endpoint.StateText;

            Rectangle detailRect = new Rectangle(row.X + _padLeft, detailTop, textWidth,
                (int)Math.Round(ItemHeight * 0.36));
            TextRenderer.DrawText(g, detail, Theme.Small, detailRect, Theme.TextMuted,
                TextFormatFlags.Left | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis
                | TextFormatFlags.NoPadding);

            if (_defaultIds.Contains(endpoint.Id))
            {
                const string label = "默认";
                Size textSize = TextRenderer.MeasureText(g, label, Theme.SmallBold,
                    new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding);
                int pillHeight = Theme.S(20);
                Rectangle pill = new Rectangle(
                    row.Right - Theme.S(14) - textSize.Width - Theme.S(16),
                    row.Y + (ItemHeight - pillHeight) / 2,
                    textSize.Width + Theme.S(16), pillHeight);

                using (GraphicsPath path = Theme.RoundedRect(pill, pillHeight / 2))
                using (SolidBrush brush = new SolidBrush(Theme.Accent))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.FillPath(brush, path);
                }
                TextRenderer.DrawText(g, label, Theme.SmallBold, pill, Color.White,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter
                    | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
            }

            using (Pen pen = new Pen(Theme.Separator))
                g.DrawLine(pen, row.X + _padLeft, row.Bottom - 1, row.Right - _padLeft, row.Bottom - 1);
        }
    }

    internal static class IconLoader
    {
        private static readonly byte[] PngSignature =
        {
            0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A
        };

        /// <summary>Extensions accepted as a shortcut icon source.</summary>
        public static bool IsSupportedIconPath(string path, out string error)
        {
            error = null;
            if (string.IsNullOrEmpty(path)) return true;

            if (!File.Exists(path))
            {
                error = "图标文件不存在：" + path;
                return false;
            }

            string extension = Path.GetExtension(path).ToLowerInvariant();
            if (extension != ".ico" && extension != ".exe" && extension != ".dll")
            {
                error = "图标格式不受支持，请使用 .ico、.exe 或 .dll 文件。";
                return false;
            }

            if (extension == ".ico" && !LooksLikeIco(path))
            {
                error = "这不是一个有效的 .ico 文件。";
                return false;
            }
            return true;
        }

        private static bool LooksLikeIco(string path)
        {
            try
            {
                using (FileStream stream = File.OpenRead(path))
                {
                    byte[] header = new byte[6];
                    if (stream.Read(header, 0, 6) != 6) return false;
                    return BitConverter.ToUInt16(header, 0) == 0     // reserved
                        && BitConverter.ToUInt16(header, 2) == 1     // type: icon
                        && BitConverter.ToUInt16(header, 4) > 0;     // image count
                }
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Render a square preview bitmap, or return null with a message.
        /// Never throws — the caller shows the message instead of crashing.
        ///
        /// Deliberately does NOT use Icon.ToBitmap(): on .NET Framework that throws
        /// ArgumentOutOfRangeException ("请求的范围扩展超过了数组的结尾。") for any
        /// .ico containing a PNG-compressed entry, which is how most modern icons
        /// store their 256x256 image. The container is parsed here instead.
        /// </summary>
        public static Bitmap LoadPreview(string path, int size, out string error)
        {
            error = null;
            try
            {
                if (string.IsNullOrEmpty(path))
                {
                    error = "未指定图标文件。";
                    return null;
                }
                if (!File.Exists(path))
                {
                    error = "图标文件不存在：" + path;
                    return null;
                }

                string extension = Path.GetExtension(path).ToLowerInvariant();
                if (extension == ".ico") return LoadIcoPreview(path, size, out error);

                if (extension == ".exe" || extension == ".dll")
                {
                    using (Icon extracted = Icon.ExtractAssociatedIcon(path))
                    {
                        if (extracted == null)
                        {
                            error = "无法从该文件提取图标。";
                            return null;
                        }
                        using (Bitmap raw = extracted.ToBitmap()) return ScaleToSquare(raw, size);
                    }
                }

                error = "不支持的图标格式，请选择 .ico、.exe 或 .dll 文件。";
                return null;
            }
            catch (Exception ex)
            {
                error = "读取图标失败：" + ex.Message;
                return null;
            }
        }

        private static Bitmap LoadIcoPreview(string path, int size, out string error)
        {
            error = null;

            byte[] data = File.ReadAllBytes(path);
            if (data.Length < 6
                || BitConverter.ToUInt16(data, 0) != 0
                || BitConverter.ToUInt16(data, 2) != 1)
            {
                error = "这不是一个有效的 .ico 文件。";
                return null;
            }

            int count = BitConverter.ToUInt16(data, 4);
            int bestWidth = 0, bestHeight = 0, bestLength = 0, bestOffset = 0;
            int bestScore = int.MaxValue;

            for (int i = 0; i < count; i++)
            {
                int entry = 6 + i * 16;
                if (entry + 16 > data.Length) break;

                int width = data[entry]; if (width == 0) width = 256;
                int height = data[entry + 1]; if (height == 0) height = 256;
                int length = BitConverter.ToInt32(data, entry + 8);
                int offset = BitConverter.ToInt32(data, entry + 12);

                if (length <= 0 || offset < 0 || offset + length > data.Length) continue;

                // Smallest image that still covers the requested size; else the largest.
                int score = width >= size ? width - size : 1000 + (size - width);
                if (score < bestScore)
                {
                    bestScore = score;
                    bestWidth = width;
                    bestHeight = height;
                    bestLength = length;
                    bestOffset = offset;
                }
            }

            if (bestScore == int.MaxValue)
            {
                error = "图标文件中没有可用的图像。";
                return null;
            }

            byte[] image = new byte[bestLength];
            Buffer.BlockCopy(data, bestOffset, image, 0, bestLength);

            if (StartsWith(image, PngSignature))
            {
                using (MemoryStream stream = new MemoryStream(image, false))
                using (Image decoded = Image.FromStream(stream))
                    return ScaleToSquare(decoded, size);
            }

            Bitmap dib = DecodeDib(image, bestWidth, bestHeight);
            if (dib != null)
            {
                using (dib) return ScaleToSquare(dib, size);
            }

            // Exotic bit depth: let System.Drawing try, but never let it escape.
            try
            {
                using (Icon icon = new Icon(path, bestWidth, bestHeight))
                using (Bitmap bitmap = icon.ToBitmap())
                    return ScaleToSquare(bitmap, size);
            }
            catch (Exception)
            {
                error = "无法预览此图标，但仍可用于生成快捷方式。";
                return null;
            }
        }

        /// <summary>
        /// Decode an uncompressed DIB icon entry (BITMAPINFOHEADER + bottom-up BGRA
        /// rows). Returns null for layouts we do not handle so the caller can fall back.
        /// </summary>
        private static Bitmap DecodeDib(byte[] entry, int width, int height)
        {
            if (entry.Length < 40 || width <= 0 || height <= 0) return null;

            int headerSize = BitConverter.ToInt32(entry, 0);
            if (headerSize < 40) return null;

            int bitCount = BitConverter.ToUInt16(entry, 14);
            int compression = BitConverter.ToInt32(entry, 16);
            if (compression != 0) return null;               // BI_RGB only
            if (bitCount != 32 && bitCount != 24) return null;

            int stride = ((width * bitCount + 31) / 32) * 4;
            if (entry.Length < headerSize + stride * height) return null;

            int bytesPerPixel = bitCount / 8;
            Bitmap bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            BitmapData target = bitmap.LockBits(
                new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly,
                PixelFormat.Format32bppArgb);

            try
            {
                byte[] row = new byte[target.Stride];
                for (int y = 0; y < height; y++)
                {
                    int source = headerSize + (height - 1 - y) * stride;
                    for (int x = 0; x < width; x++)
                    {
                        int s = source + x * bytesPerPixel;
                        row[x * 4 + 0] = entry[s];                     // B
                        row[x * 4 + 1] = entry[s + 1];                 // G
                        row[x * 4 + 2] = entry[s + 2];                 // R
                        row[x * 4 + 3] = bitCount == 32 ? entry[s + 3] : (byte)255;
                    }
                    Marshal.Copy(row, 0, (IntPtr)((long)target.Scan0 + y * target.Stride),
                        target.Stride);
                }
            }
            finally
            {
                bitmap.UnlockBits(target);
            }
            return bitmap;
        }

        private static bool StartsWith(byte[] data, byte[] prefix)
        {
            if (data.Length < prefix.Length) return false;
            for (int i = 0; i < prefix.Length; i++)
            {
                if (data[i] != prefix[i]) return false;
            }
            return true;
        }

        private static Bitmap ScaleToSquare(Image source, int size)
        {
            if (size <= 0) size = 48;

            Bitmap target = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            using (Graphics graphics = Graphics.FromImage(target))
            {
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.Clear(Color.Transparent);

                float scale = Math.Min((float)size / source.Width, (float)size / source.Height);
                int width = Math.Max(1, (int)Math.Round(source.Width * scale));
                int height = Math.Max(1, (int)Math.Round(source.Height * scale));
                graphics.DrawImage(source,
                    new Rectangle((size - width) / 2, (size - height) / 2, width, height));
            }
            return target;
        }

        /// <summary>The icon compiled into this executable.</summary>
        public static Icon LoadApplicationIcon(int size)
        {
            try
            {
                Icon extracted = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
                if (extracted != null)
                {
                    if (size <= 0) return extracted;
                    return new Icon(extracted, new Size(size, size));
                }
            }
            catch (Exception)
            {
            }
            return SystemIcons.Application;
        }

        /// <summary>Safe preview of this program's own icon (null if unavailable).</summary>
        public static Bitmap LoadApplicationPreview(int size)
        {
            try
            {
                string ignored;
                return LoadPreview(Application.ExecutablePath, size, out ignored);
            }
            catch (Exception)
            {
            }
            return null;
        }
    }
}
