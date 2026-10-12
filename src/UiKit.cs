// UiKit.cs — the shared visual language: palette, typography and the handful of
// owner-drawn controls the window is built from.
//
// The look follows the Windows 11 design language (Fluent): a light/dark palette
// picked from the user's app theme, 8px rounded surfaces, subtle 1px borders
// instead of heavy frames, and the system accent colour for primary actions.
// Everything is hand-drawn so the app stays a few readable source files with no
// .resx or designer file to keep in sync.

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
    /// <summary>
    /// Every colour for one theme. Windows 11 ships a light and a dark variant of
    /// each surface, so the palette is chosen once at startup and read through
    /// <see cref="Theme"/> from then on.
    /// </summary>
    internal sealed class Palette
    {
        public Color Window;
        public Color Card;
        public Color CardBorder;
        public Color Shadow;
        public Color Layer;
        public Color LayerHover;
        public Color LayerPressed;
        public Color Accent;
        public Color AccentHover;
        public Color AccentPressed;
        public Color AccentSoft;
        public Color OnAccent;
        public Color TextPrimary;
        public Color TextSecondary;
        public Color TextMuted;
        public Color Success;
        public Color Warning;
        public Color Danger;
        public Color Separator;
        public Color RowHover;
        public Color DisabledFill;
        public Color DisabledText;

        public static Palette Create(bool dark, Color accent)
        {
            Palette p = new Palette();

            if (dark)
            {
                p.Window = Color.FromArgb(0x20, 0x20, 0x20);
                p.Card = Color.FromArgb(0x2B, 0x2B, 0x2B);
                p.CardBorder = Color.FromArgb(0x3A, 0x3A, 0x3A);
                p.Shadow = Color.FromArgb(0x40, 0x00, 0x00, 0x00);
                p.Layer = Color.FromArgb(0x32, 0x32, 0x32);
                p.LayerHover = Color.FromArgb(0x3D, 0x3D, 0x3D);
                p.LayerPressed = Color.FromArgb(0x2A, 0x2A, 0x2A);
                p.Accent = accent;
                p.AccentHover = Theme.Blend(accent, Color.White, 0.14F);
                p.AccentPressed = Theme.Blend(accent, Color.Black, 0.16F);
                p.AccentSoft = Theme.Blend(accent, p.Card, 0.78F);
                p.OnAccent = Theme.ReadableOn(accent);
                p.TextPrimary = Color.FromArgb(0xFF, 0xFF, 0xFF);
                p.TextSecondary = Color.FromArgb(0xC8, 0xC8, 0xC8);
                p.TextMuted = Color.FromArgb(0x99, 0x99, 0x99);
                p.Success = Color.FromArgb(0x6C, 0xCB, 0x5A);
                p.Warning = Color.FromArgb(0xFD, 0xB9, 0x00);
                p.Danger = Color.FromArgb(0xFF, 0x99, 0xA4);
                p.Separator = Color.FromArgb(0x3A, 0x3A, 0x3A);
                p.RowHover = Color.FromArgb(0x35, 0x35, 0x35);
                p.DisabledFill = Color.FromArgb(0x2E, 0x2E, 0x2E);
                p.DisabledText = Color.FromArgb(0x6E, 0x6E, 0x6E);
            }
            else
            {
                p.Window = Color.FromArgb(0xF3, 0xF3, 0xF3);
                p.Card = Color.FromArgb(0xFF, 0xFF, 0xFF);
                p.CardBorder = Color.FromArgb(0xE5, 0xE5, 0xE5);
                p.Shadow = Color.FromArgb(0x1A, 0x00, 0x00, 0x00);
                p.Layer = Color.FromArgb(0xFF, 0xFF, 0xFF);
                p.LayerHover = Color.FromArgb(0xF7, 0xF7, 0xF7);
                p.LayerPressed = Color.FromArgb(0xED, 0xED, 0xED);
                p.Accent = accent;
                p.AccentHover = Theme.Blend(accent, Color.Black, 0.10F);
                p.AccentPressed = Theme.Blend(accent, Color.Black, 0.22F);
                p.AccentSoft = Theme.Blend(accent, p.Card, 0.88F);
                p.OnAccent = Theme.ReadableOn(accent);
                p.TextPrimary = Color.FromArgb(0x1A, 0x1A, 0x1A);
                p.TextSecondary = Color.FromArgb(0x5D, 0x5D, 0x5D);
                p.TextMuted = Color.FromArgb(0x8A, 0x8A, 0x8A);
                p.Success = Color.FromArgb(0x0F, 0x7B, 0x0F);
                p.Warning = Color.FromArgb(0x9D, 0x5D, 0x00);
                p.Danger = Color.FromArgb(0xC4, 0x2B, 0x1C);
                p.Separator = Color.FromArgb(0xEC, 0xEC, 0xEC);
                p.RowHover = Color.FromArgb(0xF7, 0xF7, 0xF7);
                p.DisabledFill = Color.FromArgb(0xF0, 0xF0, 0xF0);
                p.DisabledText = Color.FromArgb(0xA0, 0xA0, 0xA0);
            }

            return p;
        }
    }

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

        private static Palette _palette = Palette.Create(false, Color.FromArgb(0x00, 0x67, 0xC0));
        private static bool _dark;

        /// <summary>Pick the light or dark palette. Must run before any control exists.</summary>
        public static void Initialize(bool dark, Color accent)
        {
            _dark = dark;
            _palette = Palette.Create(dark, accent);
        }

        public static bool IsDark { get { return _dark; } }

        /// <summary>Scale a design-time (96 DPI) pixel value.</summary>
        public static int S(float value)
        {
            return (int)Math.Round(value * Scale);
        }

        // --- palette accessors -------------------------------------------
        public static Color Window { get { return _palette.Window; } }
        public static Color Card { get { return _palette.Card; } }
        public static Color CardBorder { get { return _palette.CardBorder; } }
        public static Color Shadow { get { return _palette.Shadow; } }
        public static Color Layer { get { return _palette.Layer; } }
        public static Color LayerHover { get { return _palette.LayerHover; } }
        public static Color LayerPressed { get { return _palette.LayerPressed; } }
        public static Color Accent { get { return _palette.Accent; } }
        public static Color AccentHover { get { return _palette.AccentHover; } }
        public static Color AccentPressed { get { return _palette.AccentPressed; } }
        public static Color AccentSoft { get { return _palette.AccentSoft; } }
        public static Color OnAccent { get { return _palette.OnAccent; } }
        public static Color TextPrimary { get { return _palette.TextPrimary; } }
        public static Color TextSecondary { get { return _palette.TextSecondary; } }
        public static Color TextMuted { get { return _palette.TextMuted; } }
        public static Color Success { get { return _palette.Success; } }
        public static Color Warning { get { return _palette.Warning; } }
        public static Color Danger { get { return _palette.Danger; } }
        public static Color Separator { get { return _palette.Separator; } }
        public static Color RowHover { get { return _palette.RowHover; } }
        public static Color DisabledFill { get { return _palette.DisabledFill; } }
        public static Color DisabledText { get { return _palette.DisabledText; } }

        /// <summary>Linear blend from <paramref name="from"/> to <paramref name="to"/> (t = 0..1).</summary>
        public static Color Blend(Color from, Color to, float t)
        {
            if (t < 0F) t = 0F;
            if (t > 1F) t = 1F;
            return Color.FromArgb(
                (int)Math.Round(from.A + (to.A - from.A) * t),
                (int)Math.Round(from.R + (to.R - from.R) * t),
                (int)Math.Round(from.G + (to.G - from.G) * t),
                (int)Math.Round(from.B + (to.B - from.B) * t));
        }

        /// <summary>
        /// Black or white, whichever stays readable on the given fill. Used for
        /// button captions so a pale system accent does not produce white-on-yellow.
        /// </summary>
        public static Color ReadableOn(Color background)
        {
            double luminance = (0.299 * background.R + 0.587 * background.G
                                + 0.114 * background.B) / 255.0;
            return luminance > 0.6 ? Color.FromArgb(0x1A, 0x1A, 0x1A) : Color.White;
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

        private static readonly string FamilyName = PickFamily();

        public static readonly Font Title = MakeFont(16F, FontStyle.Bold);
        public static readonly Font Subtitle = MakeFont(9F, FontStyle.Regular);
        public static readonly Font CardTitle = MakeFont(10.5F, FontStyle.Bold);
        public static readonly Font Body = MakeFont(9.5F, FontStyle.Regular);
        public static readonly Font BodyBold = MakeFont(10F, FontStyle.Bold);
        public static readonly Font Small = MakeFont(8.5F, FontStyle.Regular);
        public static readonly Font SmallBold = MakeFont(8.5F, FontStyle.Bold);
        public static readonly Font Button = MakeFont(9.5F, FontStyle.Regular);
        public static readonly Font ButtonBold = MakeFont(10F, FontStyle.Bold);

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
            string[] candidates =
            {
                "Microsoft YaHei UI", "Microsoft YaHei",
                "Segoe UI Variable Text", "Segoe UI", "Tahoma"
            };
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

        /// <summary>Corner radius for cards and other large surfaces.</summary>
        public const int CardRadius = 8;
        /// <summary>Corner radius for buttons, inputs and list rows.</summary>
        public const int ControlRadius = 6;

        public static GraphicsPath RoundedRect(Rectangle bounds, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            if (radius <= 0)
            {
                path.AddRectangle(bounds);
                return path;
            }

            int d = Math.Min(radius * 2, Math.Min(bounds.Width, bounds.Height));
            if (d <= 0)
            {
                path.AddRectangle(bounds);
                return path;
            }

            path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
            path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
            path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        /// <summary>Fill a rounded rectangle in one call.</summary>
        public static void FillRounded(Graphics g, Rectangle bounds, int radius, Color colour)
        {
            using (GraphicsPath path = RoundedRect(bounds, radius))
            using (SolidBrush brush = new SolidBrush(colour))
                g.FillPath(brush, path);
        }

        /// <summary>Outline a rounded rectangle in one call.</summary>
        public static void DrawRounded(Graphics g, Rectangle bounds, int radius, Color colour)
        {
            using (GraphicsPath path = RoundedRect(bounds, radius))
            using (Pen pen = new Pen(colour))
                g.DrawPath(pen, path);
        }
    }

    /// <summary>A card surface: rounded, hairline border, a hint of depth.</summary>
    internal class CardPanel : Panel
    {
        private readonly string _title;
        private readonly string _step;

        /// <param name="step">Leading number badge, e.g. "1". Empty for no badge.</param>
        /// <param name="title">Section heading.</param>
        public CardPanel(string step, string title)
        {
            _step = step;
            _title = title;
            BackColor = Theme.Card;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                     | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // Behind the card, so the corners outside the rounded outline show the
            // form background instead of an opaque square.
            g.Clear(Theme.BackdropOf(this, Theme.Window));

            int radius = Theme.S(Theme.CardRadius);

            // One pixel of soft shadow along the bottom edge gives the surface a
            // little depth without needing a margin reserved around the control.
            Rectangle shadow = new Rectangle(0, 1, Width - 1, Height - 1);
            Theme.FillRounded(g, shadow, radius, Theme.Shadow);

            Rectangle bounds = new Rectangle(0, 0, Width - 1, Height - 1);
            Theme.FillRounded(g, bounds, radius, Theme.Card);
            Theme.DrawRounded(g, bounds, radius, Theme.CardBorder);

            if (!string.IsNullOrEmpty(_title))
            {
                int x = Theme.S(18);
                int y = Theme.S(14);

                if (!string.IsNullOrEmpty(_step))
                {
                    // Number badge, sized to the glyph so it stays circular.
                    int badge = Theme.S(20);
                    Rectangle circle = new Rectangle(x, y - Theme.S(1), badge, badge);
                    Theme.FillRounded(g, circle, badge / 2, Theme.Accent);
                    TextRenderer.DrawText(g, _step, Theme.SmallBold, circle, Theme.OnAccent,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter
                        | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
                    x += badge + Theme.S(9);
                }

                TextRenderer.DrawText(g, _title, Theme.CardTitle,
                    new Point(x, y), Theme.TextPrimary,
                    TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
            }
        }
    }

    internal enum ButtonKind
    {
        /// <summary>Accent-filled: the one main action on the surface.</summary>
        Primary,
        /// <summary>Neutral filled: ordinary actions.</summary>
        Secondary,
        /// <summary>Borderless: low-emphasis actions.</summary>
        Ghost
    }

    /// <summary>Flat, rounded button with hand-rolled hover/press states.</summary>
    internal class FlatButton : Button
    {
        private bool _hover;
        private bool _pressed;

        public ButtonKind Kind = ButtonKind.Secondary;
        public int CornerRadius = Theme.S(Theme.ControlRadius);

        public FlatButton()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                     | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            Font = Theme.Button;
            BackColor = Theme.Card;
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
            if (!Enabled) return Theme.DisabledFill;

            switch (Kind)
            {
                case ButtonKind.Primary:
                    if (_pressed) return Theme.AccentPressed;
                    return _hover ? Theme.AccentHover : Theme.Accent;
                case ButtonKind.Ghost:
                    if (_pressed) return Theme.LayerPressed;
                    return _hover ? Theme.LayerHover : Color.Transparent;
                default:
                    if (_pressed) return Theme.LayerPressed;
                    return _hover ? Theme.LayerHover : Theme.Layer;
            }
        }

        private Color ForeColorFor()
        {
            if (!Enabled) return Theme.DisabledText;
            if (Kind == ButtonKind.Primary) return Theme.OnAccent;
            return Kind == ButtonKind.Ghost ? Theme.TextSecondary : Theme.TextPrimary;
        }

        private Color BorderColorFor()
        {
            if (Kind == ButtonKind.Primary) return BackColorFor();
            if (Kind == ButtonKind.Ghost) return Color.Transparent;
            if (!Enabled) return Theme.CardBorder;
            return _hover ? Theme.Blend(Theme.CardBorder, Theme.TextMuted, 0.45F) : Theme.CardBorder;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // Corner pixels belong to whatever is behind the button, so the rounded
            // shape reads correctly on a white card and on the grey form alike.
            g.Clear(Theme.BackdropOf(this, Theme.Card));

            Rectangle bounds = new Rectangle(0, 0, Width - 1, Height - 1);
            Theme.FillRounded(g, bounds, CornerRadius, BackColorFor());

            Color border = BorderColorFor();
            if (border != Color.Transparent) Theme.DrawRounded(g, bounds, CornerRadius, border);

            TextRenderer.DrawText(g, Text, Font, bounds, ForeColorFor(),
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter
                | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
        }
    }

    /// <summary>
    /// A rounded single-line text input: the borderless TextBox is hosted inside a
    /// panel that paints the surface, so the field matches the card language
    /// instead of falling back to the square Win32 edit box.
    /// </summary>
    internal class RoundedField : Panel
    {
        private readonly TextBox _box;
        private bool _focused;

        public RoundedField()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                     | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Layer;

            _box = new TextBox();
            _box.BorderStyle = BorderStyle.None;
            _box.Font = Theme.Body;
            _box.BackColor = Theme.Layer;
            _box.ForeColor = Theme.TextPrimary;
            _box.GotFocus += delegate { _focused = true; Invalidate(); };
            _box.LostFocus += delegate { _focused = false; Invalidate(); };
            _box.TextChanged += delegate
            {
                if (TextChangedHandler != null) TextChangedHandler(this, EventArgs.Empty);
            };
            _box.HandleCreated += delegate { LayoutInner(); };
            Controls.Add(_box);
        }

        /// <summary>Raised when the inner text changes, so callers can bind like a TextBox.</summary>
        public event EventHandler TextChangedHandler;

        public TextBox Inner { get { return _box; } }

        public override string Text
        {
            get { return _box.Text; }
            set { _box.Text = value; }
        }

        /// <summary>Placeholder drawn while the field is empty and unfocused.</summary>
        public string Placeholder = "";

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            LayoutInner();
        }

        private void LayoutInner()
        {
            if (_box == null) return;

            int pad = Theme.S(11);
            int height = _box.PreferredHeight;
            _box.SetBounds(pad, Math.Max(Theme.S(2), (Height - height) / 2),
                Math.Max(Theme.S(10), Width - pad * 2), height);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Theme.BackdropOf(this, Theme.Card));

            Rectangle bounds = new Rectangle(0, 0, Width - 1, Height - 1);
            Theme.FillRounded(g, bounds, Theme.S(Theme.ControlRadius), Theme.Layer);

            // The focus ring is the accent border Windows 11 uses on inputs.
            Color border = _focused
                ? Theme.Accent
                : Theme.Blend(Theme.CardBorder, Theme.TextMuted, 0.35F);
            Theme.DrawRounded(g, bounds, Theme.S(Theme.ControlRadius), border);

            if (_box.TextLength == 0 && !_focused && !string.IsNullOrEmpty(Placeholder))
            {
                int pad = Theme.S(11);
                Rectangle text = new Rectangle(pad, 0, Width - pad * 2, Height);
                TextRenderer.DrawText(g, Placeholder, Theme.Body, text, Theme.TextMuted,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter
                    | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
            }
        }
    }

    /// <summary>
    /// A drop-down selector drawn like <see cref="RoundedField"/>: the standard
    /// ComboBox keeps its classic drop-down button and cannot be themed, so the
    /// value is shown here and the list opens as a themed menu.
    /// </summary>
    internal class DropdownField : Panel
    {
        private readonly string[] _items;
        private int _selected;
        private bool _hover;

        public DropdownField(string[] items)
        {
            _items = items ?? new string[0];
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                     | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Layer;
            Cursor = Cursors.Hand;
        }

        public event EventHandler SelectionChanged;

        public int SelectedIndex
        {
            get { return _selected; }
            set
            {
                if (value < 0 || value >= _items.Length || value == _selected) return;
                _selected = value;
                Invalidate();
                if (SelectionChanged != null) SelectionChanged(this, EventArgs.Empty);
            }
        }

        public string SelectedItem
        {
            get { return _selected >= 0 && _selected < _items.Length ? _items[_selected] : ""; }
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
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            ShowMenu();
        }

        private void EnsureMenu()
        {
            if (_menu != null) return;

            _menu = new ContextMenuStrip();
            _menu.Renderer = new ThemedMenuRenderer();
            _menu.ShowImageMargin = false;
            _menu.Font = Theme.Body;
            _menu.BackColor = Theme.Card;
            _menu.ForeColor = Theme.TextPrimary;

            // Only clear the "open" flag here. Disposing the drop-down inside its own
            // Closed event is fatal: ToolStripManager's modal message filter still
            // holds a reference to it as the active drop-down and touches it again
            // while unwinding, which throws ObjectDisposedException
            // ("无法访问已释放的对象。对象名:"ContextMenuStrip"") and takes the whole
            // app down. The menu is disposed with the control instead.
            _menu.Closed += delegate { _menuOpen = false; };
        }

        private void ShowMenu()
        {
            if (_menuOpen) return;

            EnsureMenu();

            // Rebuild the items so the current value is the one marked as selected.
            // This also disposes the previous items, which is safe here because the
            // menu is closed (guarded above) rather than mid-close.
            _menu.Items.Clear();
            for (int i = 0; i < _items.Length; i++)
            {
                int index = i;
                ToolStripMenuItem item = new ToolStripMenuItem(_items[i]);
                item.ForeColor = Theme.TextPrimary;
                item.Tag = index == _selected ? "current" : null;
                item.Click += delegate { SelectedIndex = index; };
                _menu.Items.Add(item);
            }

            _menuOpen = true;
            _menu.Show(this, new Point(0, Height + Theme.S(4)));
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _menu != null)
            {
                _menu.Dispose();
                _menu = null;
            }
            base.Dispose(disposing);
        }

        private ContextMenuStrip _menu;
        private bool _menuOpen;

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Theme.BackdropOf(this, Theme.Card));

            Rectangle bounds = new Rectangle(0, 0, Width - 1, Height - 1);
            Theme.FillRounded(g, bounds, Theme.S(Theme.ControlRadius),
                _hover ? Theme.LayerHover : Theme.Layer);
            Theme.DrawRounded(g, bounds, Theme.S(Theme.ControlRadius),
                _hover ? Theme.Blend(Theme.CardBorder, Theme.TextMuted, 0.45F) : Theme.CardBorder);

            int pad = Theme.S(11);
            int chevron = Theme.S(10);
            Rectangle text = new Rectangle(pad, 0,
                Width - pad * 2 - chevron - Theme.S(6), Height);
            TextRenderer.DrawText(g, SelectedItem, Theme.Body, text, Theme.TextPrimary,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter
                | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);

            // Chevron glyph, drawn as two strokes so it scales with the control.
            int cx = Width - pad - chevron / 2;
            int cy = Height / 2;
            using (Pen pen = new Pen(Theme.TextSecondary, Math.Max(1.4F, Theme.S(1.4F))))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                g.DrawLine(pen, cx - chevron / 2, cy - chevron / 4,
                                cx, cy + chevron / 4);
                g.DrawLine(pen, cx, cy + chevron / 4,
                                cx + chevron / 2, cy - chevron / 4);
            }
        }
    }

    /// <summary>Flat, palette-aware renderer for the themed drop-down menus.</summary>
    internal class ThemedMenuRenderer : ToolStripRenderer
    {
        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
        {
            e.Graphics.Clear(Theme.Card);
        }

        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
            Rectangle bounds = new Rectangle(0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Theme.DrawRounded(e.Graphics, bounds, Theme.S(Theme.ControlRadius), Theme.CardBorder);
        }

        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            if (!e.Item.Selected && e.Item.Tag == null) return;

            Rectangle bounds = new Rectangle(Theme.S(4), 0,
                e.Item.Width - Theme.S(8), e.Item.Height);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            Color fill = e.Item.Selected ? Theme.LayerHover
                       : Theme.Blend(Theme.AccentSoft, Theme.Card, 0.35F);
            Theme.FillRounded(e.Graphics, bounds, Theme.S(4), fill);
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = Theme.TextPrimary;
            base.OnRenderItemText(e);
        }
    }

    /// <summary>
    /// A thin, rounded scroll indicator in the Windows 11 style. The device list
    /// keeps its native scrolling (wheel, keys, page keys) and reports every change
    /// through <c>ScrollChanged</c>; this control mirrors that state and can also be
    /// dragged or clicked to drive the list.
    /// </summary>
    internal class ThinScrollBar : Panel
    {
        private DeviceListBox _target;
        private bool _dragging;
        private int _dragOffset;

        public ThinScrollBar()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                     | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Card;
            Cursor = Cursors.Hand;
            Width = Theme.S(6);
        }

        public void Attach(DeviceListBox target)
        {
            _target = target;
        }

        /// <summary>Recompute whether a thumb is needed; returns false when everything fits.</summary>
        public bool RefreshState()
        {
            int top, height;
            bool needed = Geometry(out top, out height);
            if (Visible != needed) Visible = needed;
            Invalidate();
            return needed;
        }

        private bool Geometry(out int thumbTop, out int thumbHeight)
        {
            thumbTop = 0;
            thumbHeight = 0;
            if (_target == null || Height <= 0) return false;

            int count = _target.Items.Count;
            int visible = _target.VisibleRowCount;
            if (count <= 0 || count <= visible) return false;

            int track = Height;
            int minThumb = Theme.S(30);
            thumbHeight = Math.Max(minThumb, (int)Math.Round((double)track * visible / count));
            if (thumbHeight > track) thumbHeight = track;

            int maxTop = track - thumbHeight;
            int maxFirst = _target.MaxTopIndex;
            int first = _target.TopIndex;
            if (first < 0) first = 0;
            if (first > maxFirst) first = maxFirst;

            thumbTop = maxFirst > 0 ? (int)Math.Round((double)maxTop * first / maxFirst) : 0;
            return true;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Theme.BackdropOf(this, Theme.Card));

            int top, height;
            if (!Geometry(out top, out height)) return;

            Rectangle thumb = new Rectangle(0, top, Width - 1, height);
            Color fill = _dragging
                ? Theme.Blend(Theme.TextMuted, Theme.TextPrimary, 0.45F)
                : Theme.Blend(Theme.TextMuted, Theme.Card, 0.55F);
            Theme.FillRounded(g, thumb, Math.Max(2, (Width - 1) / 2), fill);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (_target == null) return;

            int top, height;
            if (!Geometry(out top, out height)) return;

            if (e.Y >= top && e.Y <= top + height)
            {
                _dragging = true;
                _dragOffset = e.Y - top;
            }
            else
            {
                // Clicking the track pages towards the click.
                _target.ScrollByRows(e.Y < top ? -_target.VisibleRowCount : _target.VisibleRowCount);
            }
            Invalidate();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (!_dragging || _target == null) return;

            int top, height;
            if (!Geometry(out top, out height)) return;

            int maxTop = Height - height;
            if (maxTop <= 0) return;

            int desired = e.Y - _dragOffset;
            if (desired < 0) desired = 0;
            if (desired > maxTop) desired = maxTop;

            int maxFirst = _target.MaxTopIndex;
            if (maxFirst <= 0) return;

            // Proportional mapping, so dragging the thumb to either end reaches the
            // first/last row exactly instead of stalling one row short.
            _target.ScrollToRow((int)Math.Round((double)desired / maxTop * maxFirst));
            Invalidate();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            _dragging = false;
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (!_dragging) Invalidate();
        }
    }

    /// <summary>
    /// Owner-drawn device list: a bold device name over a muted adapter line, plus
    /// a "默认" pill for the endpoint Windows is currently using. Selection is a
    /// rounded inset highlight, matching the Windows 11 list style.
    /// </summary>
    internal class DeviceListBox : ListBox
    {
        /// <summary>Row height at 96 DPI; re-scaled by the form on load.</summary>
        public const int DesignRowHeight = 54;

        private const int GWL_STYLE = -16;
        private const int GWL_EXSTYLE = -20;
        private const int WS_VSCROLL = 0x00200000;
        private const int WS_EX_COMPOSITED = 0x02000000;

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        private readonly int _padLeft = Theme.S(14);
        private readonly HashSet<string> _defaultIds =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private int _hoverIndex = -1;

        /// <summary>
        /// Width the rows should be drawn at. A native ListBox keeps a non-client
        /// scrollbar gutter that is painted with the system colour and cannot be
        /// themed, so the list is deliberately made wider than the viewport that
        /// clips it and rows are laid out to the visible width instead.
        /// </summary>
        public int VisibleWidth
        {
            get { return _visibleWidth > 0 ? _visibleWidth : ClientSize.Width; }
            set
            {
                if (value == _visibleWidth) return;
                _visibleWidth = value;
                Invalidate();
            }
        }

        private int _visibleWidth;

        public DeviceListBox()
        {
            DrawMode = DrawMode.OwnerDrawFixed;
            ItemHeight = Theme.S(DesignRowHeight);
            BorderStyle = BorderStyle.None;
            IntegralHeight = false;
            BackColor = Theme.Card;
            Font = Theme.Body;
            // OptimizedDoubleBuffer is the safe anti-flicker option for a native
            // ListBox. Do NOT add ControlStyles.UserPaint here: that asks WinForms
            // to paint the control itself instead of the native control, which
            // suppresses the owner-draw notifications the rows depend on.
            SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
        }

        /// <summary>Diagnostic: the list's live window style and scrollbar state.</summary>
        public string DescribeScrollState()
        {
            if (!IsHandleCreated) return "no handle";
            int style = GetWindowLong(Handle, GWL_STYLE);
            return "style=0x" + style.ToString("X8")
                 + " WS_VSCROLL=" + ((style & WS_VSCROLL) != 0)
                 + " width=" + Width
                 + " clientW=" + ClientSize.Width
                 + " gutter=" + (Width - ClientSize.Width)
                 + " items=" + Items.Count
                 + " topIndex=" + TopIndex
                 + " itemH=" + ItemHeight;
        }

        /// <summary>Raised whenever the visible range may have moved, so the thin scrollbar can follow.</summary>
        public event EventHandler ScrollChanged;

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);

            // WS_VSCROLL must STAY: the ListBox needs it for wheel, keyboard and
            // page scrolling. Removing it (as an earlier revision did) silently
            // kills the wheel entirely, because the control only handles
            // WM_MOUSEWHEEL while it believes it has a scrollbar to move.
            //
            // The gutter it creates is painted with the system colour and cannot be
            // themed, so it is hidden by geometry instead: the list is made wider
            // than the viewport that clips it (see VisibleWidth), which puts the
            // gutter outside the visible area while scrolling keeps working.
            //
            // WS_EX_COMPOSITED double-buffers the whole control including the
            // non-client gutter, so hovering no longer flashes the selection row.
            int exStyle = GetWindowLong(Handle, GWL_EXSTYLE);
            SetWindowLong(Handle, GWL_EXSTYLE, exStyle | WS_EX_COMPOSITED);
        }

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);

            const int WM_VSCROLL = 0x0115;
            const int WM_MOUSEWHEEL = 0x020A;
            const int WM_KEYDOWN = 0x0100;
            const int WM_LBUTTONDOWN = 0x0201;
            const int WM_LBUTTONUP = 0x0202;

            if (m.Msg == WM_VSCROLL || m.Msg == WM_MOUSEWHEEL || m.Msg == WM_KEYDOWN
                || m.Msg == WM_LBUTTONDOWN || m.Msg == WM_LBUTTONUP)
            {
                // Scrolling moves the rows under a stationary pointer, so the hovered
                // row must be recomputed — no MouseMove arrives in that case.
                if (m.Msg != WM_LBUTTONDOWN && m.Msg != WM_LBUTTONUP) ResyncHover();

                if (ScrollChanged != null) ScrollChanged(this, EventArgs.Empty);
            }
        }

        /// <summary>Recompute which row is under the cursor after the view moved.</summary>
        private void ResyncHover()
        {
            int index = -1;
            try
            {
                Point client = PointToClient(Cursor.Position);
                if (ClientRectangle.Contains(client)) index = IndexFromPoint(client);
            }
            catch (Exception)
            {
            }

            if (index == _hoverIndex) return;

            int previous = _hoverIndex;
            _hoverIndex = index;
            InvalidateRow(previous);
            InvalidateRow(_hoverIndex);
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
                int previous = _hoverIndex;
                _hoverIndex = index;

                // Repaint only the two rows whose appearance changed. A whole-control
                // Invalidate() here redraws every visible row, which makes the
                // selected row visibly flash while the pointer moves down the list.
                InvalidateRow(previous);
                InvalidateRow(_hoverIndex);
            }
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            if (_hoverIndex != -1)
            {
                int previous = _hoverIndex;
                _hoverIndex = -1;
                InvalidateRow(previous);
            }
            base.OnMouseLeave(e);
        }

        /// <summary>
        /// Repaint a single row. Indices outside the item range mean "no row", which
        /// needs no repaint at all — repainting everything there would reintroduce the
        /// flash on the very first hover (previous index is -1).
        /// </summary>
        private void InvalidateRow(int index)
        {
            if (index < 0 || index >= Items.Count) return;
            if (ItemHeight <= 0) return;

            int top = (index - TopIndex) * ItemHeight;
            // Off-screen rows need no repaint at all.
            if (top + ItemHeight <= 0 || top >= ClientSize.Height) return;

            Invalidate(new Rectangle(0, top, Math.Max(1, VisibleWidth), ItemHeight));
        }

        /// <summary>Number of whole rows that fit in the viewport.</summary>
        public int VisibleRowCount
        {
            get
            {
                if (ItemHeight <= 0) return 1;
                int rows = ClientSize.Height / ItemHeight;
                return rows < 1 ? 1 : rows;
            }
        }

        /// <summary>Scroll by whole rows, clamped. Used by the thin scrollbar.</summary>
        public void ScrollByRows(int rows)
        {
            int max = Math.Max(0, Items.Count - VisibleRowCount);
            int target = TopIndex + rows;
            if (target < 0) target = 0;
            if (target > max) target = max;
            if (target == TopIndex) return;

            TopIndex = target;
            Invalidate();
            if (ScrollChanged != null) ScrollChanged(this, EventArgs.Empty);
        }

        /// <summary>Move the visible window to an absolute first row, clamped.</summary>
        public void ScrollToRow(int first)
        {
            int max = Math.Max(0, Items.Count - VisibleRowCount);
            if (first < 0) first = 0;
            if (first > max) first = max;
            if (first == TopIndex) return;

            TopIndex = first;
            Invalidate();
            if (ScrollChanged != null) ScrollChanged(this, EventArgs.Empty);
        }

        /// <summary>Largest valid first-row index (0 when everything fits).</summary>
        public int MaxTopIndex
        {
            get { return Math.Max(0, Items.Count - VisibleRowCount); }
        }

        protected override void OnDrawItem(DrawItemEventArgs e)
        {
            Graphics g = e.Graphics;
            if (e.Index < 0 || e.Index >= Items.Count) return;

            AudioEndpoint endpoint = Items[e.Index] as AudioEndpoint;
            if (endpoint == null) return;

            bool selected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // ClientSize excludes any border/scrollbar, so the highlight and the
            // "默认" pill never slide underneath the scroll indicator. VisibleWidth
            // narrows it further to the clipping viewport the form provides.
            int rowWidth = VisibleWidth;

            // Full-width clear first, then the rounded highlight inset inside it.
            using (SolidBrush brush = new SolidBrush(Theme.Card))
                g.FillRectangle(brush, new Rectangle(0, e.Bounds.Y, rowWidth, ItemHeight));

            Rectangle row = new Rectangle(Theme.S(4), e.Bounds.Y + Theme.S(1),
                rowWidth - Theme.S(8), ItemHeight - Theme.S(3));

            if (selected) Theme.FillRounded(g, row, Theme.S(6), Theme.AccentSoft);
            else if (e.Index == _hoverIndex) Theme.FillRounded(g, row, Theme.S(6), Theme.RowHover);

            Color nameColor = endpoint.IsActive
                ? (selected ? Theme.Accent : Theme.TextPrimary)
                : Theme.TextSecondary;

            // Proportions of the row height, so the two text lines stay balanced at
            // any DPI without needing separate absolute measurements.
            int nameTop = row.Y + (int)Math.Round(ItemHeight * 0.17);
            int detailTop = row.Y + (int)Math.Round(ItemHeight * 0.58);
            int textWidth = row.Width - _padLeft - Theme.S(104);

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
                    row.Right - Theme.S(12) - textSize.Width - Theme.S(14),
                    row.Y + (ItemHeight - pillHeight) / 2,
                    textSize.Width + Theme.S(14), pillHeight);

                Theme.FillRounded(g, pill, pillHeight / 2, Theme.Accent);
                TextRenderer.DrawText(g, label, Theme.SmallBold, pill, Theme.OnAccent,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter
                    | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
            }

            // Hairline between rows, kept clear of the rounded highlight.
            if (e.Index < Items.Count - 1)
            {
                using (Pen pen = new Pen(Theme.Separator))
                    g.DrawLine(pen, row.X + _padLeft, row.Bottom + Theme.S(1),
                                    row.Right - _padLeft, row.Bottom + Theme.S(1));
            }
        }
    }

    /// <summary>
    /// Owner-drawn checkbox: a rounded box with an accent fill and a stroked tick,
    /// so the three options read as part of the same design language as the fields
    /// instead of falling back to the classic Win32 box.
    /// </summary>
    internal class FlatCheckBox : CheckBox
    {
        private bool _hover;

        public FlatCheckBox()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                     | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Card;
            ForeColor = Theme.TextPrimary;
            Font = Theme.Body;
            Cursor = Cursors.Hand;
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
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnCheckedChanged(EventArgs e)
        {
            Invalidate();
            base.OnCheckedChanged(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Theme.BackdropOf(this, Theme.Card));

            int box = Theme.S(18);
            int top = (Height - box) / 2;
            Rectangle square = new Rectangle(0, top, box, box);
            int radius = Theme.S(4);

            if (Checked)
            {
                Theme.FillRounded(g, square, radius, Enabled ? Theme.Accent : Theme.DisabledText);
            }
            else
            {
                Theme.FillRounded(g, square, radius, Theme.Layer);
                Theme.DrawRounded(g, square, radius,
                    _hover ? Theme.Blend(Theme.CardBorder, Theme.TextMuted, 0.70F)
                           : Theme.Blend(Theme.CardBorder, Theme.TextMuted, 0.35F));
            }

            if (Checked)
            {
                float w = Math.Max(1.6F, Theme.S(1.6F));
                using (Pen pen = new Pen(Theme.OnAccent, w))
                {
                    pen.StartCap = LineCap.Round;
                    pen.EndCap = LineCap.Round;
                    pen.LineJoin = LineJoin.Round;
                    g.DrawLines(pen, new PointF[]
                    {
                        new PointF(square.X + box * 0.26F, square.Y + box * 0.53F),
                        new PointF(square.X + box * 0.43F, square.Y + box * 0.70F),
                        new PointF(square.X + box * 0.75F, square.Y + box * 0.31F)
                    });
                }
            }

            int gap = Theme.S(9);
            Rectangle text = new Rectangle(box + gap, 0, Math.Max(1, Width - box - gap), Height);
            TextRenderer.DrawText(g, Text, Font, text,
                Enabled ? ForeColor : Theme.DisabledText,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter
                | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
        }
    }

    /// <summary>
    /// A rounded, bordered tile that hosts the icon preview, so the image sits on
    /// the same surface language as the fields instead of a hard FixedSingle frame.
    /// </summary>
    internal class PreviewTile : Panel
    {
        private Image _image;

        public PreviewTile()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                     | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Layer;
        }

        /// <summary>The image to show. The tile owns it and disposes the previous one.</summary>
        public Image Preview
        {
            get { return _image; }
            set
            {
                if (_image != null) _image.Dispose();
                _image = value;
                Invalidate();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Theme.BackdropOf(this, Theme.Card));

            Rectangle bounds = new Rectangle(0, 0, Width - 1, Height - 1);
            int radius = Theme.S(Theme.ControlRadius);
            Theme.FillRounded(g, bounds, radius, Theme.Layer);
            Theme.DrawRounded(g, bounds, radius, Theme.CardBorder);

            if (_image == null) return;

            int inset = Theme.S(6);
            int available = Math.Min(Width, Height) - inset * 2;
            if (available <= 0) return;

            float ratio = Math.Min((float)available / _image.Width,
                                   (float)available / _image.Height);
            int w = Math.Max(1, (int)Math.Round(_image.Width * ratio));
            int h = Math.Max(1, (int)Math.Round(_image.Height * ratio));

            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.DrawImage(_image, new Rectangle((Width - w) / 2, (Height - h) / 2, w, h));
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
