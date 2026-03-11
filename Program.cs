using System;
using System.Drawing;
using System.Windows.Forms;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Timers;
using Microsoft.Win32;
using System.IO;
using System.Text.Json;
using System.Net.Http;
using System.Threading.Tasks;

namespace TaskNetByAli
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }

    public class AppSettings
    {
        public int X { get; set; } = -1;
        public int Y { get; set; } = -1;
        public string BgColor { get; set; } = "#111111";
        public string LabelColor { get; set; } = "#aaaaaa";
        public string ValueColor { get; set; } = "#ffffff";
        public int FontSize { get; set; } = 9;
        public double Opacity { get; set; } = 0.92;
        public bool ShowCpu { get; set; } = true;
        public bool ShowRam { get; set; } = true;
        public bool ShowGpu { get; set; } = false;
        public bool ShowNetwork { get; set; } = true;
        public bool StartWithWindows { get; set; } = false;
        public int UpdateInterval { get; set; } = 2000;

        private static string ConfigPath =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                         "TaskNetByAli", "config.json");

        public static AppSettings Load()
        {
            try
            {
                var dir = Path.GetDirectoryName(ConfigPath);
                if (dir != null) Directory.CreateDirectory(dir);
                if (File.Exists(ConfigPath))
                {
                    var json = File.ReadAllText(ConfigPath);
                    return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
                }
            }
            catch { }
            return new AppSettings();
        }

        public void Save()
        {
            try
            {
                var dir = Path.GetDirectoryName(ConfigPath);
                if (dir != null) Directory.CreateDirectory(dir);
                File.WriteAllText(ConfigPath, JsonSerializer.Serialize(this,
                    new JsonSerializerOptions { WriteIndented = true }));
            }
            catch { }
        }

        public void SetStartup(bool enable)
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Run", true);
                if (enable)
                    key?.SetValue("TaskNetByAli", Application.ExecutablePath);
                else
                    key?.DeleteValue("TaskNetByAli", false);
            }
            catch { }
        }

        public Color GetBgColor()    => ColorTranslator.FromHtml(BgColor);
        public Color GetLabelColor() => ColorTranslator.FromHtml(LabelColor);
        public Color GetValueColor() => ColorTranslator.FromHtml(ValueColor);
    }

    public class MainForm : Form
    {
        [DllImport("user32.dll")]
        static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
            int X, int Y, int cx, int cy, uint uFlags);
        static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        const uint SWP_NOMOVE = 0x0002, SWP_NOSIZE = 0x0001, SWP_NOACTIVATE = 0x0010;

        [DllImport("kernel32.dll")]
        static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

        [StructLayout(LayoutKind.Sequential)]
        public struct MEMORYSTATUSEX
        {
            public uint dwLength;
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;
        }

        private AppSettings _settings;
        private System.Timers.Timer? _timer;
        private System.Timers.Timer? _topTimer;
        private PerformanceCounter? _cpuCounter;

        private long _prevBytesSent = 0;
        private long _prevBytesRecv = 0;
        private DateTime _prevNetTime = DateTime.Now;

        private Label? _lblUp, _lblUpVal;
        private Label? _lblDn, _lblDnVal;
        private Label? _lblCpu, _lblCpuVal;
        private Label? _lblRam, _lblRamVal;
        private Label? _lblGpu, _lblGpuVal;

        private Point _dragStart;
        private bool _dragging = false;

        private NotifyIcon? _trayIcon;
        private ContextMenuStrip? _trayMenu;

        private const string APP_VERSION = "2.0.0";
        private const string GITHUB_OWNER = "imali019";
        private const string GITHUB_REPO  = "TaskNetByAli";

        public MainForm()
        {
            _settings = AppSettings.Load();
            InitializeCounters();
            InitializeOverlay();
            InitializeTray();
            InitializeTimer();
        }

        private void InitializeCounters()
        {
            try
            {
                _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                _cpuCounter.NextValue();
                System.Threading.Thread.Sleep(1000);
                _cpuCounter.NextValue();
            }
            catch { }
        }

        private void InitializeOverlay()
        {
            FormBorderStyle = FormBorderStyle.None;
            TopMost = true;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            Opacity = _settings.Opacity;
            BackColor = _settings.GetBgColor();
            Padding = new Padding(8, 5, 8, 5);

            try
            {
                string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tasknet.ico");
                if (File.Exists(iconPath)) Icon = new Icon(iconPath);
            }
            catch { }

            BuildLabels();
            PositionWindow();
            BindEvents();

            if (_settings.X == -1)
                Load += (s, e) => ShowSettings();

            Load += async (s, e) =>
            {
                await Task.Delay(5000);
                await CheckForUpdates(silent: true);
            };
        }

        private void BuildLabels()
        {
            Controls.Clear();
            var bg   = _settings.GetBgColor();
            var fg   = _settings.GetLabelColor();
            var vc   = _settings.GetValueColor();
            var fs   = _settings.FontSize;
            var fLbl = new Font("Consolas", fs);
            var fVal = new Font("Consolas", fs, FontStyle.Bold);

            int y1 = 2;
            int y2 = y1 + fs + 8;
            int x  = 0;

            _lblUp     = MakeLabel("UP:",  fg, fLbl, x,    y1); x += 28;
            _lblUpVal  = MakeLabel("─",    vc, fVal, x,    y1); x += 78;
            _lblCpu    = MakeLabel("CPU:", fg, fLbl, x,    y1); x += 36;
            _lblCpuVal = MakeLabel("─",    vc, fVal, x,    y1); x += 46;
            _lblGpu    = MakeLabel("GPU:", fg, fLbl, x,    y1); x += 36;
            _lblGpuVal = MakeLabel("─",    vc, fVal, x,    y1);

            x = 0;
            _lblDn     = MakeLabel("DN:",  fg, fLbl, x,    y2); x += 28;
            _lblDnVal  = MakeLabel("─",    vc, fVal, x,    y2); x += 78;
            _lblRam    = MakeLabel("RAM:", fg, fLbl, x,    y2); x += 36;
            _lblRamVal = MakeLabel("─",    vc, fVal, x,    y2);

            UpdateVisibility();
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
        }

        private Label MakeLabel(string text, Color color, Font font, int x, int y)
        {
            var lbl = new Label
            {
                Text      = text,
                ForeColor = color,
                BackColor = _settings.GetBgColor(),
                Font      = font,
                AutoSize  = true,
                Location  = new Point(x, y),
            };
            Controls.Add(lbl);
            BindDragToControl(lbl);
            return lbl;
        }

        private void UpdateVisibility()
        {
            if (_lblUp    != null) _lblUp.Visible     = _settings.ShowNetwork;
            if (_lblUpVal != null) _lblUpVal.Visible  = _settings.ShowNetwork;
            if (_lblDn    != null) _lblDn.Visible     = _settings.ShowNetwork;
            if (_lblDnVal != null) _lblDnVal.Visible  = _settings.ShowNetwork;
            if (_lblCpu   != null) _lblCpu.Visible    = _settings.ShowCpu;
            if (_lblCpuVal!= null) _lblCpuVal.Visible = _settings.ShowCpu;
            if (_lblRam   != null) _lblRam.Visible    = _settings.ShowRam;
            if (_lblRamVal!= null) _lblRamVal.Visible = _settings.ShowRam;
            if (_lblGpu   != null) _lblGpu.Visible    = _settings.ShowGpu;
            if (_lblGpuVal!= null) _lblGpuVal.Visible = _settings.ShowGpu;
        }

        private void PositionWindow()
        {
            if (_settings.X == -1)
            {
                var screen = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1920, 1080);
                Location = new Point(screen.Right - 280, screen.Bottom - 45);
            }
            else
            {
                Location = new Point(_settings.X, _settings.Y);
            }
        }

        private void BindEvents()
        {
            BindDragToControl(this);
            MouseClick += OnMouseClick;
        }

        private void BindDragToControl(Control c)
        {
            c.MouseDown += (s, e) => {
                if (e.Button == MouseButtons.Left)
                { _dragging = true; _dragStart = e.Location; }
            };
            c.MouseMove += (s, e) => {
                if (_dragging)
                    Location = new Point(
                        Location.X + e.X - _dragStart.X,
                        Location.Y + e.Y - _dragStart.Y);
            };
            c.MouseUp += (s, e) => {
                if (e.Button == MouseButtons.Left)
                {
                    _dragging = false;
                    _settings.X = Location.X;
                    _settings.Y = Location.Y;
                    _settings.Save();
                }
                if (e.Button == MouseButtons.Right)
                    ShowContextMenu(PointToScreen(e.Location));
            };
        }

        private void OnMouseClick(object? sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
                ShowContextMenu(PointToScreen(e.Location));
        }

        private void ShowContextMenu(Point pos)
        {
            var menu = new ContextMenuStrip();
            menu.BackColor = Color.FromArgb(22, 27, 34);
            menu.ForeColor = Color.FromArgb(240, 246, 252);
            menu.Font = new Font("Segoe UI", 9);

            var title = new ToolStripMenuItem("TaskNet By Ali") { Enabled = false };
            title.Font = new Font("Segoe UI", 9, FontStyle.Bold);
            menu.Items.Add(title);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Settings",       null, (s, e) => ShowSettings());
            menu.Items.Add("Check Updates",  null, async (s, e) => await CheckForUpdates(silent: false));
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Exit",           null, (s, e) => ExitApp());
            menu.Show(pos);
        }

        private void InitializeTimer()
        {
            _timer = new System.Timers.Timer(_settings.UpdateInterval);
            _timer.Elapsed += OnTimerElapsed;
            _timer.AutoReset = true;
            _timer.Start();

            _topTimer = new System.Timers.Timer(500);
            _topTimer.Elapsed += (s, e) => ForceTopMost();
            _topTimer.AutoReset = true;
            _topTimer.Start();
        }

        private void ForceTopMost()
        {
            try
            {
                Invoke(new Action(() =>
                {
                    SetWindowPos(Handle, HWND_TOPMOST, 0, 0, 0, 0,
                                 SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
                }));
            }
            catch { }
        }

        private void OnTimerElapsed(object? sender, ElapsedEventArgs e)
        {
            try
            {
                float cpu = 0;
                if (_cpuCounter != null)
                {
                    cpu = _cpuCounter.NextValue();
                    if (cpu > 100) cpu = 100;
                    if (cpu < 0)   cpu = 0;
                }

                var memStatus = new MEMORYSTATUSEX();
                memStatus.dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
                GlobalMemoryStatusEx(ref memStatus);
                float ram = memStatus.dwMemoryLoad;

                var netStats = GetNetworkSpeed();
                float gpu = GetGpuUsage();

                Invoke(new Action(() => UpdateDisplay(cpu, ram, netStats.Item1, netStats.Item2, gpu)));
            }
            catch { }
        }

        private Tuple<double, double> GetNetworkSpeed()
        {
            try
            {
                long sent = 0, recv = 0;
                foreach (var ni in System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus == System.Net.NetworkInformation.OperationalStatus.Up)
                    {
                        var stats = ni.GetIPv4Statistics();
                        sent += stats.BytesSent;
                        recv += stats.BytesReceived;
                    }
                }
                var now = DateTime.Now;
                double elapsed = (now - _prevNetTime).TotalSeconds;
                double up = elapsed > 0 ? (sent - _prevBytesSent) / elapsed : 0;
                double dn = elapsed > 0 ? (recv - _prevBytesRecv) / elapsed : 0;
                _prevBytesSent = sent;
                _prevBytesRecv = recv;
                _prevNetTime   = now;
                return Tuple.Create(Math.Max(0, up), Math.Max(0, dn));
            }
            catch { return Tuple.Create(0.0, 0.0); }
        }

        private float GetGpuUsage()
        {
            try
            {
                var searcher = new System.Management.ManagementObjectSearcher(
                    "SELECT * FROM Win32_PerfFormattedData_GPUPerformanceCounters_GPUEngine");
                float maxUsage = 0;
                foreach (System.Management.ManagementObject obj in searcher.Get())
                {
                    var util = obj["UtilizationPercentage"];
                    if (util != null)
                    {
                        float val = Convert.ToSingle(util);
                        if (val > maxUsage) maxUsage = val;
                    }
                }
                return maxUsage;
            }
            catch { return -1; }
        }

        private void UpdateDisplay(float cpu, float ram, double up, double dn, float gpu)
        {
            if (_settings.ShowCpu && _lblCpuVal != null)
                _lblCpuVal.Text = string.Format("{0:0}%", cpu);
            if (_settings.ShowRam && _lblRamVal != null)
                _lblRamVal.Text = string.Format("{0:0}%", ram);
            if (_settings.ShowNetwork)
            {
                if (_lblUpVal != null) _lblUpVal.Text = FormatSpeed(up);
                if (_lblDnVal != null) _lblDnVal.Text = FormatSpeed(dn);
            }
            if (_settings.ShowGpu && _lblGpuVal != null)
                _lblGpuVal.Text = gpu >= 0 ? string.Format("{0:0}%", gpu) : "N/A";

            if (_trayIcon != null)
                _trayIcon.Text = string.Format("TaskNet By Ali  CPU:{0:0}% RAM:{1:0}%", cpu, ram);
        }

        private string FormatSpeed(double bytes)
        {
            if (bytes < 1024)    return string.Format("{0:0} B/s",   bytes);
            if (bytes < 1048576) return string.Format("{0:0.0} KB/s", bytes / 1024);
            return string.Format("{0:0.0} MB/s", bytes / 1048576);
        }

        private void InitializeTray()
        {
            _trayMenu = new ContextMenuStrip();
            _trayMenu.Items.Add("Show Overlay",  null, (s, e) => { Show(); TopMost = true; });
            _trayMenu.Items.Add("Settings",      null, (s, e) => ShowSettings());
            _trayMenu.Items.Add("Check Updates", null, async (s, e) => await CheckForUpdates(silent: false));
            _trayMenu.Items.Add(new ToolStripSeparator());
            _trayMenu.Items.Add("Exit",          null, (s, e) => ExitApp());

            _trayIcon = new NotifyIcon
            {
                Text             = "TaskNet By Ali",
                ContextMenuStrip = _trayMenu,
                Visible          = true,
            };

            try
            {
                string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tasknet.ico");
                if (File.Exists(iconPath)) _trayIcon.Icon = new Icon(iconPath);
                else _trayIcon.Icon = SystemIcons.Application;
            }
            catch { _trayIcon.Icon = SystemIcons.Application; }

            _trayIcon.DoubleClick += (s, e) => { Show(); TopMost = true; };
        }

        private void ShowSettings()
        {
            using var dlg = new SettingsForm(_settings);
            if (dlg.ShowDialog() == DialogResult.OK)
            {
                _settings = dlg.UpdatedSettings;
                _settings.Save();
                _settings.SetStartup(_settings.StartWithWindows);
                Opacity   = _settings.Opacity;
                BackColor = _settings.GetBgColor();
                if (_timer != null) _timer.Interval = _settings.UpdateInterval;
                BuildLabels();
            }
        }

        private async Task CheckForUpdates(bool silent)
        {
            try
            {
                using var client = new HttpClient();
                client.DefaultRequestHeaders.Add("User-Agent", "TaskNetByAli");
                string url = string.Format("https://api.github.com/repos/{0}/{1}/releases/latest",
                                           GITHUB_OWNER, GITHUB_REPO);
                var response = await client.GetStringAsync(url);
                var doc = JsonDocument.Parse(response);
                string latestTag = doc.RootElement.GetProperty("tag_name").GetString() ?? "v2.0.0";
                string latestVersion = latestTag.TrimStart('v');

                string downloadUrl = string.Format("https://github.com/{0}/{1}/releases/latest",
                                                   GITHUB_OWNER, GITHUB_REPO);
                try
                {
                    var assets = doc.RootElement.GetProperty("assets");
                    if (assets.GetArrayLength() > 0)
                        downloadUrl = assets[0].GetProperty("browser_download_url").GetString() ?? downloadUrl;
                }
                catch { }

                string finalUrl = downloadUrl;
                if (latestVersion != APP_VERSION)
                {
                    Invoke(new Action(() =>
                    {
                        string msg = string.Format(
                            "New version v{0} is available!\n\nCurrent: v{1}\nLatest:  v{0}\n\nDownload now?",
                            latestVersion, APP_VERSION);
                        var result = MessageBox.Show(msg, "Update Available",
                            MessageBoxButtons.YesNo, MessageBoxIcon.Information);
                        if (result == DialogResult.Yes)
                            Process.Start(new ProcessStartInfo { FileName = finalUrl, UseShellExecute = true });
                    }));
                }
                else if (!silent)
                {
                    Invoke(new Action(() =>
                        MessageBox.Show("You are running the latest version!",
                            "TaskNet By Ali", MessageBoxButtons.OK, MessageBoxIcon.Information)));
                }
            }
            catch
            {
                if (!silent)
                    Invoke(new Action(() =>
                        MessageBox.Show("Could not check for updates.",
                            "TaskNet By Ali", MessageBoxButtons.OK, MessageBoxIcon.Warning)));
            }
        }

        private void ExitApp()
        {
            _timer?.Stop();
            _topTimer?.Stop();
            if (_trayIcon != null) _trayIcon.Visible = false;
            Application.Exit();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            ExitApp();
            base.OnFormClosing(e);
        }
    }

    public class SettingsForm : Form
    {
        public AppSettings UpdatedSettings { get; private set; }
        private AppSettings _settings;

        private CheckBox? _chkCpu, _chkRam, _chkGpu, _chkNet, _chkStartup;
        private NumericUpDown? _nudFont;
        private TrackBar? _tbOpacity;
        private Label? _lblOpacityVal;
        private Button? _btnBg, _btnLabel, _btnValue;

        public SettingsForm(AppSettings settings)
        {
            _settings = AppSettings.Load();
            UpdatedSettings = _settings;
            InitializeUI();
        }

        private void InitializeUI()
        {
            Text            = "TaskNet By Ali - Settings";
            Size            = new Size(420, 540);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox     = false;
            MinimizeBox     = false;
            StartPosition   = FormStartPosition.CenterScreen;
            BackColor       = Color.FromArgb(13, 17, 23);
            ForeColor       = Color.FromArgb(201, 209, 217);
            Font            = new Font("Segoe UI", 9);

            try
            {
                string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tasknet.ico");
                if (File.Exists(iconPath)) Icon = new Icon(iconPath);
            }
            catch { }

            var header = new Panel { Dock = DockStyle.Top, Height = 60, BackColor = Color.FromArgb(22, 27, 34) };
            var lblTitle = new Label
            {
                Text = "TaskNet By Ali", Font = new Font("Segoe UI", 13, FontStyle.Bold),
                ForeColor = Color.FromArgb(79, 195, 247), Location = new Point(18, 10), AutoSize = true
            };
            var lblSub = new Label
            {
                Text = "System Stats Overlay - Settings", Font = new Font("Segoe UI", 8),
                ForeColor = Color.FromArgb(139, 148, 158), Location = new Point(18, 36), AutoSize = true
            };
            header.Controls.AddRange(new Control[] { lblTitle, lblSub });
            Controls.Add(header);

            int y = 75;

            AddSection("SHOW METRICS", ref y);
            _chkCpu     = AddCheck("Show CPU",             _settings.ShowCpu,     ref y);
            _chkRam     = AddCheck("Show RAM",             _settings.ShowRam,     ref y);
            _chkGpu     = AddCheck("Show GPU",             _settings.ShowGpu,     ref y);
            _chkNet     = AddCheck("Show Network UP / DN", _settings.ShowNetwork, ref y);

            AddSection("APPEARANCE", ref y);

            AddRowLabel("Font Size", y);
            _nudFont = new NumericUpDown
            {
                Minimum = 7, Maximum = 16, Value = _settings.FontSize,
                Location = new Point(200, y), Size = new Size(60, 24),
                BackColor = Color.FromArgb(33, 38, 45), ForeColor = Color.White
            };
            Controls.Add(_nudFont); y += 30;

            AddRowLabel("Opacity", y);
            _tbOpacity = new TrackBar
            {
                Minimum = 20, Maximum = 100, Value = (int)(_settings.Opacity * 100),
                Location = new Point(200, y - 5), Size = new Size(150, 30),
                TickFrequency = 10, BackColor = Color.FromArgb(13, 17, 23)
            };
            _lblOpacityVal = new Label
            {
                Text = string.Format("{0}%", (int)(_settings.Opacity * 100)),
                Location = new Point(355, y), AutoSize = true,
                ForeColor = Color.FromArgb(201, 209, 217)
            };
            var lbl = _lblOpacityVal;
            var tb  = _tbOpacity;
            tb.ValueChanged += (s, e) => lbl.Text = string.Format("{0}%", tb.Value);
            Controls.Add(_tbOpacity);
            Controls.Add(_lblOpacityVal);
            y += 35;

            AddRowLabel("Background Color", y);
            _btnBg = AddColorButton(_settings.BgColor, new Point(200, y)); y += 32;

            AddRowLabel("Label Color", y);
            _btnLabel = AddColorButton(_settings.LabelColor, new Point(200, y)); y += 32;

            AddRowLabel("Value Color", y);
            _btnValue = AddColorButton(_settings.ValueColor, new Point(200, y)); y += 40;

            AddSection("BEHAVIOR", ref y);
            _chkStartup = AddCheck("Start with Windows", _settings.StartWithWindows, ref y);

            var btnSave = new Button
            {
                Text = "Save & Apply", Location = new Point(220, 470),
                Size = new Size(120, 32), BackColor = Color.FromArgb(35, 134, 54),
                ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand
            };
            btnSave.FlatAppearance.BorderSize = 0;
            btnSave.Click += BtnSave_Click;

            var btnCancel = new Button
            {
                Text = "Cancel", Location = new Point(100, 470),
                Size = new Size(100, 32), BackColor = Color.FromArgb(33, 38, 45),
                ForeColor = Color.FromArgb(139, 148, 158), FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand
            };
            btnCancel.FlatAppearance.BorderSize = 0;
            btnCancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };

            Controls.AddRange(new Control[] { btnSave, btnCancel });
        }

        private void AddSection(string title, ref int y)
        {
            Controls.Add(new Label
            {
                Text = title, Font = new Font("Segoe UI", 8, FontStyle.Bold),
                ForeColor = Color.FromArgb(139, 148, 158),
                Location = new Point(18, y), AutoSize = true
            });
            Controls.Add(new Panel
            {
                Location = new Point(18, y + 18), Size = new Size(370, 1),
                BackColor = Color.FromArgb(33, 38, 45)
            });
            y += 28;
        }

        private CheckBox AddCheck(string text, bool value, ref int y)
        {
            var chk = new CheckBox
            {
                Text = text, Checked = value,
                Location = new Point(30, y), AutoSize = true,
                ForeColor = Color.FromArgb(201, 209, 217),
                BackColor = Color.FromArgb(13, 17, 23)
            };
            Controls.Add(chk);
            y += 26;
            return chk;
        }

        private void AddRowLabel(string text, int y)
        {
            Controls.Add(new Label
            {
                Text = text, Location = new Point(30, y), AutoSize = true,
                ForeColor = Color.FromArgb(201, 209, 217)
            });
        }

        private Button AddColorButton(string hexColor, Point location)
        {
            var btn = new Button
            {
                BackColor = ColorTranslator.FromHtml(hexColor),
                Location  = location, Size = new Size(80, 24),
                FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand,
                Tag       = hexColor
            };
            btn.FlatAppearance.BorderColor = Color.FromArgb(48, 54, 61);
            btn.Click += (s, e) =>
            {
                using var dlg = new ColorDialog { Color = btn.BackColor };
                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    btn.BackColor = dlg.Color;
                    btn.Tag = ColorTranslator.ToHtml(dlg.Color);
                }
            };
            Controls.Add(btn);
            return btn;
        }

        private void BtnSave_Click(object? sender, EventArgs e)
        {
            if (_chkCpu != null)     _settings.ShowCpu          = _chkCpu.Checked;
            if (_chkRam != null)     _settings.ShowRam          = _chkRam.Checked;
            if (_chkGpu != null)     _settings.ShowGpu          = _chkGpu.Checked;
            if (_chkNet != null)     _settings.ShowNetwork      = _chkNet.Checked;
            if (_nudFont != null)    _settings.FontSize         = (int)_nudFont.Value;
            if (_tbOpacity != null)  _settings.Opacity          = _tbOpacity.Value / 100.0;
            if (_btnBg != null)      _settings.BgColor          = (string)(_btnBg.Tag ?? "#111111");
            if (_btnLabel != null)   _settings.LabelColor       = (string)(_btnLabel.Tag ?? "#aaaaaa");
            if (_btnValue != null)   _settings.ValueColor       = (string)(_btnValue.Tag ?? "#ffffff");
            if (_chkStartup != null) _settings.StartWithWindows = _chkStartup.Checked;

            UpdatedSettings = _settings;
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
