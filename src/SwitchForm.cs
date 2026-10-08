// SwitchForm.cs — the whole GUI: pick an output device, pick an .ico, generate
// the shortcut. Laid out by hand with absolute coordinates inside a fixed-size
// window so no designer file is needed and the layout is fully reviewable here.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace AudioSwitch
{
    internal sealed class HeaderPanel : Panel
    {
        public HeaderPanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                     | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.HeaderTop;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            using (LinearGradientBrush brush = new LinearGradientBrush(
                new Rectangle(0, 0, Width, Height), Theme.HeaderTop, Theme.HeaderBottom,
                LinearGradientMode.Horizontal))
            {
                e.Graphics.FillRectangle(brush, ClientRectangle);
            }
        }
    }

    internal sealed class SwitchForm : Form
    {
        // Design-time geometry at 96 DPI. OnLoad scales the window to whatever
        // factor WinForms applied to the child controls.
        private const int WindowWidth = 780;
        private const int WindowHeight = 770;

        private readonly string[] _hotkeyChoices = new string[]
        {
            "无", "Ctrl+Alt+1", "Ctrl+Alt+2", "Ctrl+Alt+3", "Ctrl+Alt+4",
            "Ctrl+Alt+5", "Ctrl+Alt+6", "Ctrl+Alt+7", "Ctrl+Alt+8", "Ctrl+Alt+9"
        };

        private HeaderPanel _header;
        private DeviceListBox _deviceList;
        private FlatButton _refreshButton;

        private PictureBox _iconPreview;
        private TextBox _iconPathBox;
        private FlatButton _browseIconButton;
        private FlatButton _clearIconButton;
        private Label _iconHint;

        private TextBox _nameBox;
        private ComboBox _hotkeyBox;
        private TextBox _directoryBox;
        private FlatButton _browseDirectoryButton;
        private CheckBox _notifyCheck;
        private CheckBox _allRolesCheck;
        private CheckBox _installCheck;

        private FlatButton _testButton;
        private FlatButton _openFolderButton;
        private FlatButton _createButton;
        private Label _statusLabel;

        /// <summary>
        /// Shortcut names the user typed themselves, keyed by endpoint ID, so a
        /// renamed device keeps its name when you switch away and back.
        /// </summary>
        private readonly Dictionary<string, string> _customNames =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>The device name we last filled in automatically.</summary>
        private string _autoName = "";

        /// <summary>True while we are filling the box ourselves, to ignore our own TextChanged.</summary>
        private bool _fillingName;
        private string _lastCreatedLink;
        private string _lastOpenFolder;

        public SwitchForm()
        {
            Theme.Scale = DetectScale();

            BuildUi();
            ApplyScale();

            LoadDevices();
            UpdateIconPreview();
            UpdateRoleCheckState();
        }

        /// <summary>
        /// The DPI the point-sized fonts in Theme were created at — the same device
        /// context, so the layout scale always matches the text scale.
        /// </summary>
        private static float DetectScale()
        {
            try
            {
                using (Graphics graphics = Graphics.FromHwnd(IntPtr.Zero))
                {
                    float scale = graphics.DpiX / 96F;
                    if (scale <= 0F || float.IsNaN(scale) || float.IsInfinity(scale)) return 1F;
                    return scale;
                }
            }
            catch (Exception)
            {
                return 1F;
            }
        }

        /// <summary>
        /// Scale every control from its 96 DPI design coordinate, then size the
        /// window to match. AutoScaleMode is None on purpose: WinForms' own DPI
        /// scaling normalises AutoScaleDimensions to the current DPI (so its ratio
        /// becomes 1) while still scaling children by the raw factor, which leaves
        /// the top-level window at its design size and clips the right-hand side.
        /// Doing it here keeps the window and its contents in agreement.
        /// </summary>
        private void ApplyScale()
        {
            float scale = Theme.Scale;

            SuspendLayout();
            if (scale != 1F) ScaleControlTree(this, scale);

            Size target = new Size(
                (int)Math.Round(WindowWidth * scale),
                (int)Math.Round(WindowHeight * scale));

            // Never open a window taller than the screen: fall back to a scrollable
            // form so the buttons at the bottom stay reachable.
            Rectangle working = Screen.PrimaryScreen.WorkingArea;
            if (target.Width > working.Width - 40 || target.Height > working.Height - 60)
            {
                AutoScroll = true;
                target = new Size(
                    Math.Min(target.Width, working.Width - 60),
                    Math.Min(target.Height, working.Height - 90));
            }

            ClientSize = target;
            ResumeLayout(true);
        }

        private static void ScaleControlTree(Control parent, float scale)
        {
            foreach (Control child in parent.Controls)
            {
                child.Location = new Point(
                    (int)Math.Round(child.Left * scale),
                    (int)Math.Round(child.Top * scale));
                child.Size = new Size(
                    (int)Math.Round(child.Width * scale),
                    (int)Math.Round(child.Height * scale));
                ScaleControlTree(child, scale);
            }
        }

        // ------------------------------------------------------------------
        // Layout
        // ------------------------------------------------------------------

        private void BuildUi()
        {
            SuspendLayout();

            Text = "音频输出切换 · 快捷方式生成器";
            ClientSize = new Size(WindowWidth, WindowHeight);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Theme.Background;
            Font = Theme.Body;
            AutoScaleMode = AutoScaleMode.None;   // ApplyScale() owns all geometry
            Icon = IconLoader.LoadApplicationIcon(0);

            BuildHeader();
            BuildDeviceCard();
            BuildIconCard();
            BuildShortcutCard();
            BuildFooter();

            ResumeLayout(false);
        }

        /// <summary>
        /// Set AUDIOSWITCH_DIAG=&lt;file&gt; to dump the real layout metrics there and
        /// exit. Keeps DPI behaviour verifiable without guessing from screenshots;
        /// inert (and side-effect free) on a normal launch.
        /// </summary>
        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);

            string diagPath = Environment.GetEnvironmentVariable("AUDIOSWITCH_DIAG");
            if (string.IsNullOrEmpty(diagPath)) return;

            try
            {
                System.Text.StringBuilder text = new System.Text.StringBuilder();
                text.AppendLine("DetectedScale              = " + Theme.Scale);
                text.AppendLine("DeviceDpi                  = " + DeviceDpi);
                text.AppendLine("AutoScaleMode              = " + AutoScaleMode);
                text.AppendLine("ClientSize                 = " + ClientSize);
                text.AppendLine("WorkingArea                = " + Screen.PrimaryScreen.WorkingArea);
                text.AppendLine("AutoScroll                 = " + AutoScroll);
                text.AppendLine("header.Bounds              = " + _header.Bounds);
                text.AppendLine("deviceList.Bounds          = " + _deviceList.Bounds);
                text.AppendLine("deviceList.ItemHeight      = " + _deviceList.ItemHeight);
                text.AppendLine("createButton.Bounds        = " + _createButton.Bounds);
                text.AppendLine("statusLabel.Bounds         = " + _statusLabel.Bounds);
                text.AppendLine("Theme.Body                 = " + Theme.Body.Size
                                 + "pt height=" + Theme.Body.Height);
                text.AppendLine("Theme.Title                = " + Theme.Title.Size
                                 + "pt height=" + Theme.Title.Height);
                File.WriteAllText(diagPath, text.ToString());
            }
            catch (Exception)
            {
            }

            Close();
        }

        private void BuildHeader()
        {
            HeaderPanel header = new HeaderPanel();
            header.Location = new Point(0, 0);
            header.Size = new Size(WindowWidth, 78);
            Controls.Add(header);
            _header = header;

            Label title = new Label();
            title.Text = "音频输出切换 · 快捷方式生成器";
            title.Font = Theme.Title;
            title.ForeColor = Color.White;
            title.BackColor = Color.Transparent;
            title.AutoSize = true;
            title.Location = new Point(24, 15);
            header.Controls.Add(title);

            Label subtitle = new Label();
            subtitle.Text = "选择音频输出设备与图标，一键生成可切换 Windows 默认输出设备的桌面快捷方式";
            subtitle.Font = Theme.Subtitle;
            subtitle.ForeColor = Color.FromArgb(0x9F, 0xB0, 0xC8);
            subtitle.BackColor = Color.Transparent;
            subtitle.AutoSize = true;
            subtitle.Location = new Point(25, 46);
            header.Controls.Add(subtitle);

            PictureBox logo = new PictureBox();
            logo.Size = new Size(44, 44);
            logo.Location = new Point(WindowWidth - 24 - 44, 17);
            logo.BackColor = Color.Transparent;
            logo.SizeMode = PictureBoxSizeMode.Zoom;
            try { logo.Image = IconLoader.LoadApplicationIcon(64).ToBitmap(); }
            catch (Exception) { }
            header.Controls.Add(logo);
        }

        private void BuildDeviceCard()
        {
            CardPanel card = new CardPanel("1    选择音频输出设备");
            card.Location = new Point(20, 92);
            card.Size = new Size(740, 264);
            Controls.Add(card);

            _refreshButton = new FlatButton();
            _refreshButton.Text = "刷新列表";
            _refreshButton.Kind = ButtonKind.Ghost;
            _refreshButton.Location = new Point(620, 10);
            _refreshButton.Size = new Size(88, 28);
            _refreshButton.Font = Theme.Small;
            _refreshButton.Click += delegate { LoadDevices(); SetStatus("设备列表已刷新。", StatusKind.Info); };
            card.Controls.Add(_refreshButton);

            _deviceList = new DeviceListBox();
            _deviceList.Location = new Point(16, 48);
            _deviceList.Size = new Size(708, 200);
            _deviceList.SelectedIndexChanged += delegate { OnDeviceSelectionChanged(); };
            card.Controls.Add(_deviceList);
        }

        private void BuildIconCard()
        {
            CardPanel card = new CardPanel("2    选择图标（可选）");
            card.Location = new Point(20, 370);
            card.Size = new Size(740, 124);
            Controls.Add(card);

            _iconPreview = new PictureBox();
            _iconPreview.Location = new Point(16, 44);
            _iconPreview.Size = new Size(52, 52);
            _iconPreview.SizeMode = PictureBoxSizeMode.Zoom;
            _iconPreview.BackColor = Color.FromArgb(0xFA, 0xFB, 0xFC);
            _iconPreview.BorderStyle = BorderStyle.FixedSingle;
            card.Controls.Add(_iconPreview);

            _iconPathBox = new TextBox();
            _iconPathBox.Location = new Point(80, 56);
            _iconPathBox.Size = new Size(352, 28);
            _iconPathBox.Font = Theme.Body;
            _iconPathBox.BorderStyle = BorderStyle.FixedSingle;
            _iconPathBox.TextChanged += delegate { UpdateIconPreview(); };
            card.Controls.Add(_iconPathBox);

            _browseIconButton = new FlatButton();
            _browseIconButton.Text = "浏览图标…";
            _browseIconButton.Location = new Point(444, 54);
            _browseIconButton.Size = new Size(128, 32);
            _browseIconButton.Click += delegate { BrowseForIcon(); };
            card.Controls.Add(_browseIconButton);

            _clearIconButton = new FlatButton();
            _clearIconButton.Text = "使用默认图标";
            _clearIconButton.Location = new Point(584, 54);
            _clearIconButton.Size = new Size(124, 32);
            _clearIconButton.Click += delegate
            {
                _iconPathBox.Text = "";
                UpdateIconPreview();
            };
            card.Controls.Add(_clearIconButton);

            _iconHint = new Label();
            _iconHint.Location = new Point(80, 92);
            _iconHint.Size = new Size(628, 18);
            _iconHint.Font = Theme.Small;
            _iconHint.ForeColor = Theme.TextMuted;
            _iconHint.AutoEllipsis = true;
            card.Controls.Add(_iconHint);
        }

        private void BuildShortcutCard()
        {
            CardPanel card = new CardPanel("3    快捷方式设置");
            card.Location = new Point(20, 508);
            card.Size = new Size(740, 162);
            Controls.Add(card);

            card.Controls.Add(MakeLabel("快捷方式名称", 16, 46, 96));

            _nameBox = new TextBox();
            _nameBox.Location = new Point(116, 42);
            _nameBox.Size = new Size(344, 28);
            _nameBox.Font = Theme.Body;
            _nameBox.BorderStyle = BorderStyle.FixedSingle;
            _nameBox.TextChanged += delegate { OnShortcutNameEdited(); };
            card.Controls.Add(_nameBox);

            card.Controls.Add(MakeLabel("快捷键", 476, 46, 48));

            _hotkeyBox = new ComboBox();
            _hotkeyBox.Location = new Point(528, 42);
            _hotkeyBox.Size = new Size(180, 28);
            _hotkeyBox.DropDownStyle = ComboBoxStyle.DropDownList;
            _hotkeyBox.FlatStyle = FlatStyle.Flat;
            _hotkeyBox.Font = Theme.Body;
            _hotkeyBox.Items.AddRange(_hotkeyChoices);
            _hotkeyBox.SelectedIndex = 0;
            card.Controls.Add(_hotkeyBox);

            card.Controls.Add(MakeLabel("保存位置", 16, 84, 96));

            _directoryBox = new TextBox();
            _directoryBox.Location = new Point(116, 80);
            _directoryBox.Size = new Size(436, 28);
            _directoryBox.Font = Theme.Body;
            _directoryBox.BorderStyle = BorderStyle.FixedSingle;
            _directoryBox.Text = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            card.Controls.Add(_directoryBox);

            _browseDirectoryButton = new FlatButton();
            _browseDirectoryButton.Text = "浏览文件夹…";
            _browseDirectoryButton.Location = new Point(560, 79);
            _browseDirectoryButton.Size = new Size(148, 30);
            _browseDirectoryButton.Click += delegate { BrowseForDirectory(); };
            card.Controls.Add(_browseDirectoryButton);

            _notifyCheck = MakeCheckBox("切换后显示通知", 16, 122, 176);
            _notifyCheck.Checked = true;
            card.Controls.Add(_notifyCheck);

            _allRolesCheck = MakeCheckBox("同时设置全部角色", 200, 122, 196);
            _allRolesCheck.Checked = true;
            _allRolesCheck.CheckedChanged += delegate { UpdateRoleCheckState(); };
            card.Controls.Add(_allRolesCheck);

            _installCheck = MakeCheckBox("复制程序到固定目录（推荐）", 404, 122, 304);
            _installCheck.Checked = true;
            card.Controls.Add(_installCheck);

            ToolTip tips = new ToolTip();
            tips.SetToolTip(_allRolesCheck,
                "勾选：同时切换「控制台」「多媒体」「通讯」三个角色。\r\n" +
                "取消勾选：只切换「控制台」和「多媒体」，保留语音通话（通讯）设备不变。");
            tips.SetToolTip(_installCheck,
                "把本程序复制到 %LOCALAPPDATA%\\AudioSwitch 并让快捷方式指向该副本，\r\n" +
                "这样即使你移动或删除了下载来的程序，快捷方式依然有效。");
            tips.SetToolTip(_hotkeyBox,
                "快捷方式必须放在桌面或开始菜单中，全局快捷键才会生效。");
            tips.SetToolTip(_nameBox,
                "切换设备时会自动填入该设备的名称。\r\n" +
                "手动改成别的名字后，该设备会记住你的名字；\r\n" +
                "清空输入框即可恢复自动填写。");
        }

        private void BuildFooter()
        {
            _testButton = new FlatButton();
            _testButton.Text = "立即测试切换";
            _testButton.Location = new Point(20, 684);
            _testButton.Size = new Size(116, 42);
            _testButton.Click += delegate { TestSwitch(); };
            Controls.Add(_testButton);

            _openFolderButton = new FlatButton();
            _openFolderButton.Text = "打开所在文件夹";
            _openFolderButton.Location = new Point(144, 684);
            _openFolderButton.Size = new Size(156, 42);
            _openFolderButton.Click += delegate { OpenOutputFolder(); };
            Controls.Add(_openFolderButton);

            _createButton = new FlatButton();
            _createButton.Text = "一键生成快捷方式";
            _createButton.Kind = ButtonKind.Primary;
            _createButton.Font = Theme.ButtonBold;
            _createButton.Location = new Point(570, 684);
            _createButton.Size = new Size(190, 42);
            _createButton.Click += delegate { CreateShortcut(); };
            Controls.Add(_createButton);

            _statusLabel = new Label();
            _statusLabel.Location = new Point(20, 734);
            _statusLabel.Size = new Size(740, 20);
            _statusLabel.Font = Theme.Small;
            _statusLabel.ForeColor = Theme.TextSecondary;
            _statusLabel.AutoEllipsis = true;
            Controls.Add(_statusLabel);
        }

        private static Label MakeLabel(string text, int x, int y, int width)
        {
            Label label = new Label();
            label.Text = text;
            label.Location = new Point(x, y);
            label.Size = new Size(width, 20);
            label.Font = Theme.Body;
            label.ForeColor = Theme.TextSecondary;
            return label;
        }

        private static CheckBox MakeCheckBox(string text, int x, int y, int width)
        {
            CheckBox box = new CheckBox();
            box.Text = text;
            box.Location = new Point(x, y);
            box.Size = new Size(width, 24);
            box.Font = Theme.Body;
            box.ForeColor = Theme.TextPrimary;
            box.FlatStyle = FlatStyle.Flat;
            return box;
        }

        // ------------------------------------------------------------------
        // Data
        // ------------------------------------------------------------------

        private void LoadDevices()
        {
            string previousId = "";
            AudioEndpoint selected = _deviceList.SelectedEndpoint;
            if (selected != null) previousId = selected.Id;

            List<AudioEndpoint> devices;
            try
            {
                devices = AudioManager.GetOutputDevices(DeviceStateFlags.ALL);
            }
            catch (Exception ex)
            {
                SetStatus("读取音频设备失败：" + ex.Message, StatusKind.Error);
                return;
            }

            _deviceList.BeginUpdate();
            _deviceList.Items.Clear();
            foreach (AudioEndpoint device in devices) _deviceList.Items.Add(device);
            _deviceList.EndUpdate();

            List<string> defaultIds = new List<string>();
            foreach (ERole role in AudioManager.AllRoles)
            {
                string id = AudioManager.GetDefaultDeviceId(role);
                if (!string.IsNullOrEmpty(id) && !defaultIds.Contains(id)) defaultIds.Add(id);
            }
            _deviceList.SetDefaultIds(defaultIds);

            int index = -1;
            if (previousId.Length > 0)
            {
                for (int i = 0; i < _deviceList.Items.Count; i++)
                {
                    AudioEndpoint device = (AudioEndpoint)_deviceList.Items[i];
                    if (string.Equals(device.Id, previousId, StringComparison.OrdinalIgnoreCase))
                    {
                        index = i;
                        break;
                    }
                }
            }

            if (index < 0)
            {
                // Prefer an active device, and among those the one Windows is using.
                for (int i = 0; i < _deviceList.Items.Count; i++)
                {
                    AudioEndpoint device = (AudioEndpoint)_deviceList.Items[i];
                    if (device.IsActive && defaultIds.Contains(device.Id)) { index = i; break; }
                }
            }
            if (index < 0)
            {
                for (int i = 0; i < _deviceList.Items.Count; i++)
                {
                    if (((AudioEndpoint)_deviceList.Items[i]).IsActive) { index = i; break; }
                }
            }
            if (index < 0 && _deviceList.Items.Count > 0) index = 0;

            if (index >= 0) _deviceList.SelectedIndex = index;
            else SetStatus("未找到任何音频输出设备。", StatusKind.Error);
        }

        private void OnDeviceSelectionChanged()
        {
            AudioEndpoint device = _deviceList.SelectedEndpoint;
            if (device == null) return;

            ApplyShortcutNameFor(device);

            if (!device.IsActive)
            {
                SetStatus("该设备当前" + device.StateText + "，切换可能不会生效。", StatusKind.Warn);
            }
            else
            {
                SetStatus("已选择：" + device.BestName, StatusKind.Info);
            }
        }

        /// <summary>
        /// Fill the shortcut name for a newly selected device: the user's own name
        /// for that device if they typed one, otherwise the device's default name.
        /// </summary>
        private void ApplyShortcutNameFor(AudioEndpoint device)
        {
            if (device == null) return;

            string custom;
            bool hasCustom = _customNames.TryGetValue(device.Id, out custom)
                             && !string.IsNullOrEmpty(custom);

            _autoName = device.BestName;
            _fillingName = true;
            try
            {
                _nameBox.Text = hasCustom ? custom : device.BestName;
            }
            finally
            {
                _fillingName = false;
            }
        }

        /// <summary>
        /// Remember what the user typed for the selected device. Typing the default
        /// name back (or clearing the box) forgets the custom name, so auto-naming
        /// takes over again.
        /// </summary>
        private void OnShortcutNameEdited()
        {
            if (_fillingName) return;

            AudioEndpoint device = _deviceList == null ? null : _deviceList.SelectedEndpoint;
            if (device == null) return;

            string typed = _nameBox.Text.Trim();
            if (typed.Length == 0 || string.Equals(typed, _autoName, StringComparison.Ordinal))
                _customNames.Remove(device.Id);
            else
                _customNames[device.Id] = typed;
        }

        private void UpdateRoleCheckState()
        {
            // When "all roles" is off the shortcut only touches console + multimedia,
            // leaving the communications (VoIP) device alone.
            _allRolesCheck.ForeColor = _allRolesCheck.Checked ? Theme.TextPrimary : Theme.TextSecondary;
        }

        private RoleSelection SelectedRoles
        {
            get
            {
                if (_allRolesCheck.Checked) return RoleSelection.All;
                return RoleSelection.Console | RoleSelection.Multimedia;
            }
        }

        private string SelectedHotkey
        {
            get
            {
                string value = _hotkeyBox.SelectedItem as string;
                if (string.IsNullOrEmpty(value) || value == "无") return "";
                return value;
            }
        }

        // ------------------------------------------------------------------
        // Icon
        // ------------------------------------------------------------------

        private void BrowseForIcon()
        {
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Title = "选择图标文件";
                dialog.Filter =
                    "图标文件 (*.ico)|*.ico|" +
                    "程序或图标库 (*.exe;*.dll)|*.exe;*.dll|" +
                    "所有文件 (*.*)|*.*";
                dialog.CheckFileExists = true;
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    _iconPathBox.Text = dialog.FileName;
                    UpdateIconPreview();
                }
            }
        }

        private void UpdateIconPreview()
        {
            if (_iconPreview.Image != null)
            {
                _iconPreview.Image.Dispose();
                _iconPreview.Image = null;
            }

            string path = _iconPathBox.Text.Trim();
            if (path.Length == 0)
            {
                _iconPreview.Image = IconLoader.LoadApplicationPreview(Theme.S(48));
                _iconHint.ForeColor = Theme.TextMuted;
                _iconHint.Text = "未选择图标，快捷方式将使用本程序自带图标。";
                return;
            }

            string error;
            Bitmap preview = IconLoader.LoadPreview(path, Theme.S(48), out error);
            if (preview == null)
            {
                // A preview failure must never block generating the shortcut: the
                // shell reads the .ico itself and copes with more than we can draw.
                _iconPreview.Image = IconLoader.LoadApplicationPreview(Theme.S(48));
                _iconHint.ForeColor = Theme.Danger;
                _iconHint.Text = error;
                return;
            }

            _iconPreview.Image = preview;
            _iconHint.ForeColor = Theme.Success;
            _iconHint.Text = "已选择：快捷方式将使用该图标。";
        }

        private void BrowseForDirectory()
        {
            using (FolderBrowserDialog dialog = new FolderBrowserDialog())
            {
                dialog.Description = "选择快捷方式的保存位置";
                string current = _directoryBox.Text.Trim();
                if (current.Length > 0 && Directory.Exists(current)) dialog.SelectedPath = current;
                if (dialog.ShowDialog(this) == DialogResult.OK) _directoryBox.Text = dialog.SelectedPath;
            }
        }

        // ------------------------------------------------------------------
        // Actions
        // ------------------------------------------------------------------

        private void TestSwitch()
        {
            AudioEndpoint device = _deviceList.SelectedEndpoint;
            if (device == null)
            {
                SetStatus("请先选择一个音频输出设备。", StatusKind.Error);
                return;
            }

            string error = AudioManager.SetDefaultDevice(device.Id, SelectedRoles);
            if (error != null)
            {
                SetStatus(error, StatusKind.Error);
                return;
            }

            _deviceList.SetDefaultIds(CollectDefaultIds());
            _deviceList.Invalidate();
            SetStatus("已切换默认输出到：" + device.BestName, StatusKind.Success);
        }

        private List<string> CollectDefaultIds()
        {
            List<string> ids = new List<string>();
            foreach (ERole role in AudioManager.AllRoles)
            {
                string id = AudioManager.GetDefaultDeviceId(role);
                if (!string.IsNullOrEmpty(id) && !ids.Contains(id)) ids.Add(id);
            }
            return ids;
        }

        private void CreateShortcut()
        {
            AudioEndpoint device = _deviceList.SelectedEndpoint;
            if (device == null)
            {
                SetStatus("请先选择一个音频输出设备。", StatusKind.Error);
                return;
            }

            string directory = _directoryBox.Text.Trim();
            if (directory.Length == 0)
            {
                SetStatus("请选择快捷方式的保存位置。", StatusKind.Error);
                return;
            }

            try
            {
                if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);
            }
            catch (Exception ex)
            {
                SetStatus("无法创建保存目录：" + ex.Message, StatusKind.Error);
                return;
            }

            string target = ResolveShortcutTarget();
            if (target == null)
            {
                SetStatus("无法准备要启动的程序。", StatusKind.Error);
                return;
            }

            ShortcutOptions options = new ShortcutOptions();
            options.Directory = directory;
            options.Name = _nameBox.Text.Trim();
            options.TargetPath = target;
            options.Arguments = CommandLine.BuildSwitchArguments(device, _notifyCheck.Checked, SelectedRoles);
            options.IconPath = _iconPathBox.Text.Trim();
            options.Hotkey = SelectedHotkey;
            options.Description = "将默认音频输出设备切换为 " + device.BestName;
            options.DeviceId = device.Id;
            options.DeviceName = device.BestName;
            options.Roles = SelectedRoles;
            options.Notify = _notifyCheck.Checked;

            bool accessDenied;
            string error = ShortcutFactory.Create(options, out accessDenied);
            if (error != null)
            {
                SetStatus(error, StatusKind.Error);
                if (accessDenied) OfferElevatedRetry(options);
                return;
            }

            _lastCreatedLink = options.FullPath;
            _lastOpenFolder = directory;
            SetStatus("已生成快捷方式：" + options.FullPath, StatusKind.Success);
        }

        /// <summary>
        /// The destination refused the write. Usually that is security software
        /// (Controlled Folder Access, or an antivirus folder shield) or a folder the
        /// user cannot write to — not something this program needs admin for in
        /// general, so offer a one-off elevated retry rather than demanding it always.
        /// </summary>
        private void OfferElevatedRetry(ShortcutOptions options)
        {
            string message =
                "无法在这个位置创建快捷方式：" + Environment.NewLine +
                options.Directory + Environment.NewLine + Environment.NewLine +
                "系统拒绝了写入。常见原因：" + Environment.NewLine +
                "  · 杀毒软件 / Windows「受控文件夹访问」拦截了对该文件夹的写入" + Environment.NewLine +
                "  · 该目录需要更高权限（例如 Program Files）" + Environment.NewLine + Environment.NewLine +
                "可以改用别的保存位置（如桌面或「文档」），" + Environment.NewLine +
                "也可以以管理员身份重试这一次操作。" + Environment.NewLine + Environment.NewLine +
                "是否以管理员身份重试？";

            DialogResult choice = MessageBox.Show(this, message, "创建快捷方式失败",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
            if (choice != DialogResult.Yes) return;

            string failure = RunElevatedCreate(options);
            if (failure == null && File.Exists(options.FullPath))
            {
                _lastCreatedLink = options.FullPath;
                _lastOpenFolder = options.Directory;
                SetStatus("已生成快捷方式：" + options.FullPath, StatusKind.Success);
                return;
            }
            SetStatus(failure ?? "管理员进程未能生成快捷方式。", StatusKind.Error);
        }

        /// <summary>
        /// Re-run the same creation through an elevated copy of this program, using
        /// the existing --make-shortcut command. Returns null on success, otherwise
        /// a message describing what went wrong.
        /// </summary>
        private string RunElevatedCreate(ShortcutOptions options)
        {
            StringBuilder arguments = new StringBuilder();
            arguments.Append("--make-shortcut");
            arguments.Append(" --device ").Append(CommandLine.Quote(options.DeviceId));
            arguments.Append(" --label ").Append(CommandLine.Quote(options.DeviceName));
            arguments.Append(" --name ").Append(CommandLine.Quote(options.Name));
            arguments.Append(" --dir ").Append(CommandLine.Quote(options.Directory));
            arguments.Append(" --target ").Append(CommandLine.Quote(options.TargetPath));
            arguments.Append(" --roles ").Append(CommandLine.RolesToString(options.Roles));
            if (options.Notify) arguments.Append(" --notify");
            if (!string.IsNullOrEmpty(options.IconPath))
                arguments.Append(" --icon ").Append(CommandLine.Quote(options.IconPath));
            if (!string.IsNullOrEmpty(options.Hotkey))
                arguments.Append(" --hotkey ").Append(CommandLine.Quote(options.Hotkey));

            try
            {
                ProcessStartInfo start = new ProcessStartInfo();
                start.FileName = Application.ExecutablePath;
                start.Arguments = arguments.ToString();
                start.UseShellExecute = true;   // required for the runas verb
                start.Verb = "runas";

                using (Process elevated = Process.Start(start))
                {
                    if (elevated == null) return "无法启动管理员进程。";
                    if (!elevated.WaitForExit(120000))
                        return "管理员进程超时未返回。";
                    if (elevated.ExitCode != 0)
                        return "管理员进程返回错误码 " + elevated.ExitCode + "。";
                }
                return null;
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                // 1223 = ERROR_CANCELLED, i.e. the user dismissed the UAC prompt.
                if (ex.NativeErrorCode == 1223) return "已取消管理员授权。";
                return "无法以管理员身份启动：" + ex.Message;
            }
            catch (Exception ex)
            {
                return "以管理员身份重试失败：" + ex.Message;
            }
        }

        /// <summary>
        /// Decide which copy of this program the shortcut should launch. Copying to
        /// %LOCALAPPDATA% keeps shortcuts working after the download is moved.
        /// </summary>
        private string ResolveShortcutTarget()
        {
            string current = Application.ExecutablePath;
            if (!_installCheck.Checked) return current;

            try
            {
                string installDirectory = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "AudioSwitch");
                string installed = Path.Combine(installDirectory, Path.GetFileName(current));

                if (string.Equals(Path.GetFullPath(current), Path.GetFullPath(installed),
                        StringComparison.OrdinalIgnoreCase))
                {
                    return installed; // already running from the stable location
                }

                Directory.CreateDirectory(installDirectory);
                File.Copy(current, installed, true);
                return installed;
            }
            catch (Exception ex)
            {
                SetStatus("复制程序失败，快捷方式将指向当前路径（" + ex.Message + "）", StatusKind.Warn);
                return current;
            }
        }

        private void OpenOutputFolder()
        {
            string folder = _lastOpenFolder;
            if (string.IsNullOrEmpty(folder)) folder = _directoryBox.Text.Trim();
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
            {
                SetStatus("保存位置尚不存在。", StatusKind.Warn);
                return;
            }

            try
            {
                if (!string.IsNullOrEmpty(_lastCreatedLink) && File.Exists(_lastCreatedLink))
                    Process.Start("explorer.exe", "/select,\"" + _lastCreatedLink + "\"");
                else
                    Process.Start("explorer.exe", "\"" + folder + "\"");
            }
            catch (Exception ex)
            {
                SetStatus("无法打开文件夹：" + ex.Message, StatusKind.Error);
            }
        }

        // ------------------------------------------------------------------
        // Status line
        // ------------------------------------------------------------------

        private enum StatusKind
        {
            Info,
            Success,
            Warn,
            Error
        }

        private void SetStatus(string message, StatusKind kind)
        {
            Color color;
            switch (kind)
            {
                case StatusKind.Success: color = Theme.Success; break;
                case StatusKind.Warn: color = Color.FromArgb(0xB4, 0x53, 0x09); break;
                case StatusKind.Error: color = Theme.Danger; break;
                default: color = Theme.TextSecondary; break;
            }
            _statusLabel.ForeColor = color;
            _statusLabel.Text = message;
        }
    }
}
