using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using System.IO;
using System.Text.Json;
using System.Net.Http;
using System.Management;
using System.Net.NetworkInformation;
using System.Threading;
using System.Collections.Generic;
using System.Linq;
using LibreHardwareMonitor.Hardware;

namespace TaskNetByAli
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (s, e) => MessageBox.Show(e.Exception.ToString(), "Thread Crash");
            AppDomain.CurrentDomain.UnhandledException += (s, e) => MessageBox.Show(e.ExceptionObject.ToString(), "App Crash");
            try { SetProcessDpiAwarenessContext(new IntPtr(-4)); } catch { }
            try {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new MainForm());
            } catch (Exception ex) { MessageBox.Show(ex.ToString(), "Main Run Crash"); }
        }
        [DllImport("user32.dll")] static extern bool SetProcessDpiAwarenessContext(IntPtr value);
    }

    // ══════════════════════════════════════════
    //  APP SETTINGS
    // ══════════════════════════════════════════
    public class AppSettings
    {
        public int    X                { get; set; } = -1;
        public int    Y                { get; set; } = -1;
        public int    FontSize         { get; set; } = 8;
        public string FontFamily       { get; set; } = "Poppins";
        public string AccentColor      { get; set; } = "#60cdff";
        public string StatLabelColor   { get; set; } = "#8892a4";
        public bool   ShowCpu          { get; set; } = true;
        public bool   ShowRam          { get; set; } = true;
        public bool   ShowGpu          { get; set; } = true;
        public bool   ShowGpuTemp      { get; set; } = true;
        public bool   ShowUpload       { get; set; } = true;
        public bool   ShowDownload     { get; set; } = true;
        public bool   ShowOverlay      { get; set; } = true;
        public bool   LockOverlay      { get; set; } = false;
        public bool   StartWithWindows { get; set; } = false;
        public bool   HideOnFullscreen { get; set; } = true;
        public bool   DarkMode         { get; set; } = true;
        public string NetworkAdapter   { get; set; } = "All Networks";

        // Overlay appearance
        public bool   OverlayBgEnabled { get; set; } = true;
        public int    OverlayBgOpacity { get; set; } = 70;        // 0–100 %
        public string OverlayBgColor   { get; set; } = "#0A0A0E";
        public int    OverlayScale     { get; set; } = 100;       // 70–140 %
        public bool   AutoThemeFollow  { get; set; } = false;

        public static bool IsSystemLightMode()
        {
            try {
                using var k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                if (k != null) {
                    var v = k.GetValue("AppsUseLightTheme");
                    if (v == null) v = k.GetValue("SystemUsesLightTheme");
                    if (v != null) return (int)v != 0;
                }
            } catch { }
            return false;
        }
        public bool ShouldDrawBg() => AutoThemeFollow ? IsSystemLightMode() : OverlayBgEnabled;

        static string ConfigPath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "TaskNetByAli", "config.json");

        public static AppSettings Load()
        {
            try {
                string p = ConfigPath;
                Directory.CreateDirectory(Path.GetDirectoryName(p)!);
                if (File.Exists(p))
                    return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(p)) ?? new();
            } catch { }
            return new();
        }

        public void Save()
        {
            try {
                string p = ConfigPath;
                Directory.CreateDirectory(Path.GetDirectoryName(p)!);
                File.WriteAllText(p, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
            } catch { }
        }

        public void SetStartup(bool on)
        {
            try {
                using var k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);
                if (on) k?.SetValue("TaskNetByAli", Application.ExecutablePath);
                else    k?.DeleteValue("TaskNetByAli", false);
            } catch { }
        }

        public Color AccentCol()    { try { return ColorTranslator.FromHtml(AccentColor);    } catch { return Color.FromArgb(96, 205, 255);  } }
        public Color StatLblCol()   { try { return ColorTranslator.FromHtml(StatLabelColor); } catch { return Color.FromArgb(136, 146, 164); } }
        public Color OverlayBgCol() { try { return ColorTranslator.FromHtml(OverlayBgColor); } catch { return Color.FromArgb(10, 10, 14);    } }
    }

    // ══════════════════════════════════════════
    //  WIN11 TOGGLE SWITCH
    // ══════════════════════════════════════════
    public class ToggleSwitch : Control
    {
        private bool _on;
        private float _pos;
        private readonly System.Windows.Forms.Timer _anim;
        private bool _hover;

        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public bool Checked
        {
            get => _on;
            set { _on = value; _pos = value ? 1f : 0f; Invalidate(); }
        }
        public event EventHandler? CheckedChanged;

        public ToggleSwitch()
        {
            Size = new Size(50, 28); Cursor = Cursors.Hand;
            SetStyle(ControlStyles.SupportsTransparentBackColor |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.AllPaintingInWmPaint  |
                     ControlStyles.UserPaint, true);
            _anim = new System.Windows.Forms.Timer { Interval = 16 };
            _anim.Tick += (s, e) => {
                float t = _on ? 1f : 0f;
                _pos += (t - _pos) * 0.22f;
                if (Math.Abs(_pos - t) < 0.01f) { _pos = t; _anim.Stop(); }
                Invalidate();
            };
            BackColor = Color.Transparent;
        }
        protected override void OnMouseEnter(EventArgs e) { _hover = true;  Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnClick(EventArgs e) { _on = !_on; _anim.Start(); CheckedChanged?.Invoke(this, EventArgs.Empty); base.OnClick(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
            if (BackColor.A == 255) g.Clear(BackColor);
            
            var tr = new Rectangle(2, 4, Width - 4, Height - 8);
            int rad = tr.Height / 2;
            using var path = RRect(tr, rad);
            Color onBg = _hover ? Color.FromArgb(235, 70, 70) : Color.FromArgb(215, 50, 50);
            
            // Try to find the nearest non-transparent parent backcolor to determine Theme (light/dark)
            Color pbg = Color.FromArgb(42, 42, 48); // default dark
            var p = Parent;
            while(p != null) { if (p.BackColor.A == 255) { pbg = p.BackColor; break; } p = p.Parent; }

            bool lit = (pbg.R + pbg.G + pbg.B) > 380;
            Color offTrk = lit ? Color.FromArgb(180, 180, 192) : Color.FromArgb(70, 70, 86);
            
            using var bgBr = new SolidBrush(Lerp(offTrk, onBg, _pos));
            g.FillPath(bgBr, path);
            int outA = (int)((1f - _pos) * 210);
            if (outA > 0) { using var op = new Pen(Color.FromArgb(outA, lit ? Color.FromArgb(145,145,158) : Color.FromArgb(88,88,102)), 1.5f); g.DrawPath(op, path); }
            float thOff = tr.Height - 10f, thOn = tr.Height - 6f, th = thOff + _pos * (thOn - thOff);
            Color thC = Lerp(lit ? Color.FromArgb(95,95,108) : Color.FromArgb(185,185,200), Color.White, _pos);
            float tx = (tr.X + 4) + _pos * (tr.Right - th - 4 - (tr.X + 4)), ty = tr.Y + (tr.Height - th) / 2f;
            using var tb = new SolidBrush(thC); g.FillEllipse(tb, tx, ty, th, th);
        }
        static Color Lerp(Color a, Color b, float t) =>
            Color.FromArgb(
                (int)(a.R + t * (b.R - a.R)),
                (int)(a.G + t * (b.G - a.G)),
                (int)(a.B + t * (b.B - a.B)));
        static GraphicsPath RRect(Rectangle rc, int r)
        {
            var p = new GraphicsPath(); int d = r * 2;
            p.AddArc(rc.X, rc.Y, d, d, 180, 90); p.AddArc(rc.Right-d, rc.Y, d, d, 270, 90);
            p.AddArc(rc.Right-d, rc.Bottom-d, d, d, 0, 90); p.AddArc(rc.X, rc.Bottom-d, d, d, 90, 90);
            p.CloseFigure(); return p;
        }
    }

    // ══════════════════════════════════════════
    //  FLAT SLIDER  (custom Win11-style)
    // ══════════════════════════════════════════
    sealed class FlatSlider : Control
    {
        private bool _drag;
        private int _min = 0, _max = 100, _val = 50;

        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public int Minimum { get => _min; set { _min = value; _val = Math.Clamp(_val,_min,_max); Invalidate(); } }
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public int Maximum { get => _max; set { _max = value; _val = Math.Clamp(_val,_min,_max); Invalidate(); } }
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public int Value
        {
            get => _val;
            set { int v = Math.Clamp(value,_min,_max); if (v==_val) return; _val=v; Invalidate(); ValueChanged?.Invoke(this, EventArgs.Empty); }
        }
        public event EventHandler? ValueChanged;
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public Color TrackBg   { get; set; } = Color.FromArgb(68, 68, 84);
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public Color TrackFill { get; set; } = Color.FromArgb(215, 50, 50);

        public FlatSlider()
        {
            Size = new Size(200, 28); Cursor = Cursors.Hand;
            SetStyle(ControlStyles.SupportsTransparentBackColor | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
            BackColor = Color.Transparent;
        }
        protected override void OnMouseDown(MouseEventArgs e) { _drag=true; Hit(e.X); base.OnMouseDown(e); }
        protected override void OnMouseMove(MouseEventArgs e) { if(_drag) Hit(e.X); base.OnMouseMove(e); }
        protected override void OnMouseUp(MouseEventArgs e)   { _drag=false; base.OnMouseUp(e); }
        void Hit(int x) { float p = Math.Clamp((float)x/Width,0f,1f); Value = _min+(int)(p*(_max-_min)); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
            if (BackColor.A == 255) g.Clear(BackColor);
            int cy = Height/2, th = 4;
            float pct = _max>_min ? (float)(_val-_min)/(_max-_min) : 0f;
            float fw = Width * pct;
            // Track background
            using (var p = new GraphicsPath()) { AddRR(p, new RectangleF(0, cy-th/2f, Width, th), 2); using var b=new SolidBrush(TrackBg); g.FillPath(b,p); }
            // Track fill
            if (fw > 1) { using var p=new GraphicsPath(); AddRR(p, new RectangleF(0, cy-th/2f, fw, th), 2); using var b=new SolidBrush(TrackFill); g.FillPath(b,p); }
            // Thumb
            float tx = Math.Clamp(fw, 9, Width-9);
            using var tb = new SolidBrush(Color.White); g.FillEllipse(tb, tx-9, cy-9, 18, 18);
            using var tp = new Pen(Color.FromArgb(40, 0,0,0), 1f); g.DrawEllipse(tp, tx-8, cy-8, 16, 16);
        }
        static void AddRR(GraphicsPath p, RectangleF rc, float r)
        {
            float d = r*2;
            p.AddArc(rc.X, rc.Y, d, d, 180, 90); p.AddArc(rc.Right-d, rc.Y, d, d, 270, 90);
            p.AddArc(rc.Right-d, rc.Bottom-d, d, d, 0, 90); p.AddArc(rc.X, rc.Bottom-d, d, d, 90, 90);
            p.CloseFigure();
        }
    }

    // ══════════════════════════════════════════
    //  WIN UI CARD
    // ══════════════════════════════════════════
    sealed class WinUICard : Panel
    {
        private bool _hover;
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public Color CardNorm  { get; set; } = Color.FromArgb(42,42,48);
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public Color CardHover { get; set; } = Color.FromArgb(50,50,58);
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public Color CardBord  { get; set; } = Color.FromArgb(62,62,72);
        public WinUICard()
        {
            SetStyle(ControlStyles.SupportsTransparentBackColor |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.AllPaintingInWmPaint  |
                     ControlStyles.UserPaint, true);
            BackColor = Color.Transparent;
        }
        protected override void OnMouseEnter(EventArgs e) { _hover=true;  Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover=false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
            using var path = RR(new Rectangle(0,0,Width-1,Height-1), 8);
            using var fill = new SolidBrush(_hover ? CardHover : CardNorm); g.FillPath(fill,path);
            using var pen  = new Pen(CardBord, 1f); g.DrawPath(pen,path);
        }
        static GraphicsPath RR(Rectangle rc, int r)
        {
            var p = new GraphicsPath(); int d=r*2;
            p.AddArc(rc.X,rc.Y,d,d,180,90); p.AddArc(rc.Right-d,rc.Y,d,d,270,90);
            p.AddArc(rc.Right-d,rc.Bottom-d,d,d,0,90); p.AddArc(rc.X,rc.Bottom-d,d,d,90,90);
            p.CloseFigure(); return p;
        }
    }

    // ══════════════════════════════════════════
    //  SIDEBAR NAV BUTTON
    // ══════════════════════════════════════════
    sealed class NavButton : Control
    {
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public bool Selected { get; set; }
        private bool _hover;
        private readonly string _icon, _label;
        static readonly Color AccBar = Color.FromArgb(215, 50, 50);
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public Color ThemeFg     { get; set; } = Color.White;
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public Color ThemeSideBg { get; set; } = Color.FromArgb(28,28,34);

        public NavButton(string icon, string label)
        {
            _icon=icon; _label=label; Height=40; Cursor=Cursors.Hand;
            SetStyle(ControlStyles.SupportsTransparentBackColor |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.AllPaintingInWmPaint  |
                     ControlStyles.UserPaint, true);
            BackColor = Color.Transparent;
        }
        protected override void OnMouseEnter(EventArgs e) { _hover=true;  Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover=false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g=e.Graphics; g.SmoothingMode=SmoothingMode.AntiAlias;
            if (BackColor.A == 255) g.Clear(BackColor);
            
            Color bg=ThemeSideBg;
            bool lit=(bg.R+bg.G+bg.B)>380;
            Color sel=lit?Color.FromArgb(200,200,208):Color.FromArgb(46,46,54);
            Color hov=lit?Color.FromArgb(214,214,222):Color.FromArgb(36,36,44);
            if (Selected||_hover) { using var path=RRect(new Rectangle(8,2,Width-16,Height-4),6); using var f=new SolidBrush(Selected?sel:hov); g.FillPath(f,path); }
            if (Selected) { using var ab=new SolidBrush(AccBar); using var ap=RRect(new Rectangle(10,(Height-18)/2,3,18),2); g.FillPath(ab,ap); }
            using var fi=new Font("Segoe MDL2 Assets",13f); using var ft=new Font("Segoe UI",9.5f);
            int alpha=Selected?255:(_hover?215:155);
            using var br=new SolidBrush(Color.FromArgb(alpha,ThemeFg));
            g.DrawString(_icon,fi,br,22,(Height-16)/2f); g.DrawString(_label,ft,br,46,(Height-14)/2f);
        }
        static GraphicsPath RRect(Rectangle rc, int r)
        {
            var p=new GraphicsPath(); int d=r*2;
            p.AddArc(rc.X,rc.Y,d,d,180,90); p.AddArc(rc.Right-d,rc.Y,d,d,270,90);
            p.AddArc(rc.Right-d,rc.Bottom-d,d,d,0,90); p.AddArc(rc.X,rc.Bottom-d,d,d,90,90);
            p.CloseFigure(); return p;
        }
    }

    // ══════════════════════════════════════════
    //  PILL BUTTON
    // ══════════════════════════════════════════
    sealed class PillButton : Control
    {
        private bool _hover, _press;
        private readonly Color _bg, _fg;
        private readonly string _text;
        public PillButton(string text, Color bg, Color fg) { _text=text;_bg=bg;_fg=fg; Height=36; Cursor=Cursors.Hand; SetStyle(ControlStyles.SupportsTransparentBackColor|ControlStyles.OptimizedDoubleBuffer|ControlStyles.AllPaintingInWmPaint|ControlStyles.UserPaint,true); BackColor=Color.Transparent; }
        protected override void OnMouseEnter(EventArgs e) { _hover=true;  Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover=false; _press=false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { _press=true;  Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp  (MouseEventArgs e) { _press=false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g=e.Graphics; g.SmoothingMode=SmoothingMode.AntiAlias;
            if (BackColor.A == 255) g.Clear(BackColor);
            int off=_press?1:0;
            using var path=Pill(new Rectangle(off,off,Width-1,Height-1));
            float bright=_press?0.84f:(_hover?1.13f:1f);
            using var fill=new SolidBrush(Tint(_bg,bright)); g.FillPath(fill,path);
            using var pen =new Pen(Color.FromArgb(48,255,255,255),1f); g.DrawPath(pen,path);
            using var f  =new Font("Segoe UI",9f,FontStyle.Bold);
            using var tb =new SolidBrush(_fg);
            var sz=g.MeasureString(_text,f);
            g.DrawString(_text,f,tb,(Width-sz.Width)/2f,(Height-sz.Height)/2f);
        }
        static GraphicsPath Pill(Rectangle rc) { var p=new GraphicsPath(); int r=rc.Height/2,d=r*2; p.AddArc(rc.X,rc.Y,d,d,180,90); p.AddArc(rc.Right-d,rc.Y,d,d,270,90); p.AddArc(rc.Right-d,rc.Bottom-d,d,d,0,90); p.AddArc(rc.X,rc.Bottom-d,d,d,90,90); p.CloseFigure(); return p; }
        static Color Tint(Color c,float f)=>Color.FromArgb(c.A,Math.Min(255,(int)(c.R*f)),Math.Min(255,(int)(c.G*f)),Math.Min(255,(int)(c.B*f)));
    }

    // ══════════════════════════════════════════
    //  SETTINGS WINDOW
    // ══════════════════════════════════════════
    public class SettingsWindow : Form
    {
        public AppSettings Result { get; private set; }
        private AppSettings _cfg;
        private string _accentHex, _labelHex, _ovBgHex;

        // ── Palettes ──────────────────────────
        static readonly Color D_BG   = Color.FromArgb(20,20,25);
        static readonly Color D_SIDE = Color.FromArgb(26,26,32);
        static readonly Color D_CONT = Color.FromArgb(30,30,37);
        static readonly Color D_CARD = Color.FromArgb(38,38,47);
        static readonly Color D_DIV  = Color.FromArgb(56,56,68);
        static readonly Color D_FG   = Color.FromArgb(236,236,244);
        static readonly Color D_SUB  = Color.FromArgb(145,145,163);
        static readonly Color L_BG   = Color.FromArgb(240,240,245);
        static readonly Color L_SIDE = Color.FromArgb(230,230,237);
        static readonly Color L_CONT = Color.FromArgb(246,246,252);
        static readonly Color L_CARD = Color.FromArgb(255,255,255);
        static readonly Color L_DIV  = Color.FromArgb(205,205,216);
        static readonly Color L_FG   = Color.FromArgb(14,14,20);
        static readonly Color L_SUB  = Color.FromArgb(88,88,108);
        static readonly Color W_ACC  = Color.FromArgb(215,48,48);
        static readonly Color W_GRN  = Color.FromArgb(68,196,68);
        static readonly Color W_RED  = Color.FromArgb(255,88,88);

        private Color W_BG,W_SIDE,W_CONT,W_CARD,W_DIV,W_FG,W_SUB;
        void LoadTheme() { if(_cfg.DarkMode){W_BG=D_BG;W_SIDE=D_SIDE;W_CONT=D_CONT;W_CARD=D_CARD;W_DIV=D_DIV;W_FG=D_FG;W_SUB=D_SUB;}else{W_BG=L_BG;W_SIDE=L_SIDE;W_CONT=L_CONT;W_CARD=L_CARD;W_DIV=L_DIV;W_FG=L_FG;W_SUB=L_SUB;} }

        // Font helper
        Font SF(float sz, FontStyle st=FontStyle.Regular) { try{return new Font(_cfg.FontFamily,sz,st);}catch{return new Font("Segoe UI",sz,st);} }

        // ── All settings controls ──────────────
        private ToggleSwitch _tgOverlay=null!, _tgHideFs=null!, _tgLock=null!, _tgStartup=null!, _tgDark=null!;
        private ToggleSwitch _tgCpu=null!, _tgRam=null!, _tgGpu=null!, _tgGpuTemp=null!, _tgUp=null!, _tgDn=null!;
        private ToggleSwitch _tgOvBg=null!, _tgAutoTheme=null!;
        private ComboBox     _cbAdapter=null!, _cbFont=null!;
        private FlatSlider   _sliderOpacity=null!, _sliderScale=null!;
        private Label        _lblOpacity=null!, _lblScale=null!;
        private Panel        _ovBgPreview=null!;

        // Nav
        private Panel _pgGeneral=null!, _pgMonitors=null!, _pgHardware=null!, _pgAppearance=null!, _pgAbout=null!;
        private NavButton[] _navBtns = null!;
        private Panel _contentArea = null!;
        private Action _onChanged;

        public SettingsWindow(AppSettings cfg, Action onChanged)
        {
            _cfg=cfg; Result=cfg;
            _accentHex=cfg.AccentColor; _labelHex=cfg.StatLabelColor; _ovBgHex=cfg.OverlayBgColor;
            _onChanged=onChanged; LoadTheme(); Build();
        }

        // ══════════════════════════════════════════
        //  BUILD SHELL
        // ══════════════════════════════════════════
        void Build()
        {
            Text="TaskNet — Settings"; Width=820; Height=640;
            MinimumSize=MaximumSize=new Size(820,640);
            FormBorderStyle=FormBorderStyle.FixedSingle; MaximizeBox=false;
            StartPosition=FormStartPosition.CenterScreen;
            BackColor=W_BG; ForeColor=W_FG; Font=SF(9.5f);
            TrySetIcon(); ApplyDwm(_cfg.DarkMode);

            // ── Sidebar ──────────────────────────
            var sidebar = new Panel { Width=200, Dock=DockStyle.Left, BackColor=W_SIDE };
            sidebar.Paint += (s,e) => { using var p=new Pen(W_DIV,1f); e.Graphics.DrawLine(p,199,0,199,sidebar.Height); };
            sidebar.Controls.Add(new Label { Text="TaskNet", Font=SF(15f,FontStyle.Bold), ForeColor=W_FG, BackColor=Color.Transparent, Location=new Point(16,24), AutoSize=true });

            var navDefs = new (string ico, string lbl, int pg)[]
            { ("\uE713","General",0), ("\uE7F4","Monitors",1), ("\uE950","Hardware",2), ("\uE771","Appearance",3), ("\uE946","About",4) };
            _navBtns = new NavButton[navDefs.Length];
            int ny=88;
            for (int i=0; i<navDefs.Length; i++)
            {
                var nb = new NavButton(navDefs[i].ico, navDefs[i].lbl) { Location=new Point(0,ny), Width=200, Selected=i==0, ThemeFg=W_FG, ThemeSideBg=W_SIDE };
                int pg=navDefs[i].pg; nb.Click+=(s,e)=>ShowPage(pg);
                _navBtns[i]=nb; sidebar.Controls.Add(nb); ny+=44;
            }
            var sup=new Label { Text="♥  Support", Font=SF(8.5f,FontStyle.Bold), ForeColor=W_ACC, BackColor=Color.Transparent, Cursor=Cursors.Hand, Location=new Point(16,568), AutoSize=true };
            sup.Click+=(s,e)=>OpenUrl("https://github.com/imali019/TaskNetByAli");
            sidebar.Controls.Add(sup); Controls.Add(sidebar);

            // ── Content area ─────────────────────
            _contentArea = new Panel { Location=new Point(200,0), Width=604, Height=541, BackColor=W_CONT };
            Controls.Add(_contentArea);

            // ── Bottom action bar ─────────────────
            // Add bot panel *after* sidebar but anchored/positioned inside the remaining area
            var bot = new Panel { Dock=DockStyle.Bottom, Height=60, BackColor=W_BG };
            bot.Paint += (s,e) => {
                using var pen=new Pen(W_DIV,1f); e.Graphics.DrawLine(pen,0,0,bot.Width,0);
            };
            
            // Close Settings button — bottom-left of the content portion
            var btnClose = new PillButton("✕  Close Settings", W_ACC, Color.White) { Size=new Size(162,36), Location=new Point(16, 12) };
            btnClose.Click += (s,e) => Close();
            bot.Controls.Add(btnClose);
            Controls.Add(bot);

            // ── Pages ────────────────────────────
            _pgGeneral    = BuildGeneralPage();
            _pgMonitors   = BuildMonitorsPage();
            _pgHardware   = BuildHardwarePage();
            _pgAppearance = BuildAppearancePage();
            _pgAbout      = BuildAboutPage();
            foreach (var pg in new[]{_pgGeneral,_pgMonitors,_pgHardware,_pgAppearance,_pgAbout})
            { pg.Visible=false; _contentArea.Controls.Add(pg); }
            _pgGeneral.Visible=true;
        }

        void ShowPage(int idx)
        {
            var pages=new[]{_pgGeneral,_pgMonitors,_pgHardware,_pgAppearance,_pgAbout};
            for(int i=0;i<pages.Length;i++) pages[i].Visible=(i==idx);
            for(int i=0;i<_navBtns.Length;i++){_navBtns[i].Selected=(i==idx);_navBtns[i].Invalidate();}
        }

        // ══════════════════════════════════════════
        //  PAGE: GENERAL
        // ══════════════════════════════════════════
        Panel BuildGeneralPage()
        {
            var pg=MakePage("General","Overlay behavior and startup options");
            var card=MakeCard(pg,80,_contentArea.Width-32); int cy=0;
            _tgOverlay = CardRow(card,"Show Hardware Overlay",   null,                                          _cfg.ShowOverlay,      ref cy);
            _tgHideFs  = CardRow(card,"Hide on Fullscreen",      "Hides overlay when any app goes fullscreen",  _cfg.HideOnFullscreen, ref cy);
            _tgLock    = CardRow(card,"Lock Overlay Position",   "Prevent dragging the overlay widget",         _cfg.LockOverlay,      ref cy);
            _tgStartup = CardRow(card,"Launch on Windows Startup",null,                                         _cfg.StartWithWindows, ref cy);
            _tgDark    = CardRow(card,"Dark Mode",               "Switch between dark and light interface",     _cfg.DarkMode,         ref cy);
            _tgDark.CheckedChanged += (s,e) => {
                _cfg.DarkMode=_tgDark.Checked; _cfg.Save();
                var pos=Location; Close(); var nw=new SettingsWindow(_cfg,_onChanged); nw.Location=pos; nw.Show();
            };
            card.Height=cy+8; return pg;
        }

        // ══════════════════════════════════════════
        //  PAGE: MONITORS  (all stats + CPU temp)
        // ══════════════════════════════════════════
        Panel BuildMonitorsPage()
        {
            var pg=MakePage("Monitors","Choose which stats appear on the overlay");
            var card=MakeCard(pg,80,_contentArea.Width-32); int cy=0;
            // System
            card.Controls.Add(new Label{Text="System",Font=SF(8.5f,FontStyle.Bold),ForeColor=W_ACC,BackColor=Color.Transparent,Location=new Point(16,12),AutoSize=true});
            cy=36;
            _tgCpu     = CardRow(card,"CPU Usage",       null,                                          _cfg.ShowCpu,     ref cy);
            _tgRam     = CardRow(card,"RAM Usage",       null,                                          _cfg.ShowRam,     ref cy);
            _tgGpu     = CardRow(card,"GPU Usage",       null,                                          _cfg.ShowGpu,     ref cy);
            _tgGpuTemp = CardRow(card,"GPU Temperature", "Reads temp in °C via local drivers — shows Unsupported if unavailable", _cfg.ShowGpuTemp, ref cy);
            // Separator
            card.Controls.Add(new Panel{Location=new Point(12,cy+4),Size=new Size(card.Width-24,1),BackColor=W_DIV}); cy+=14;
            // Network
            card.Controls.Add(new Label{Text="Network",Font=SF(8.5f,FontStyle.Bold),ForeColor=W_ACC,BackColor=Color.Transparent,Location=new Point(16,cy+4),AutoSize=true}); cy+=26;
            _tgUp = CardRow(card,"Upload Speed",   null, _cfg.ShowUpload,   ref cy);
            _tgDn = CardRow(card,"Download Speed", null, _cfg.ShowDownload, ref cy);
            card.Height=cy+8; return pg;
        }

        // ══════════════════════════════════════════
        //  PAGE: HARDWARE
        // ══════════════════════════════════════════
        Panel BuildHardwarePage()
        {
            var pg=MakePage("Hardware","Hardware selection context");
            var card=MakeCard(pg,80,_contentArea.Width-32);
            card.Controls.Add(new Label{Text="Network Adapter",Font=SF(10f),ForeColor=W_FG,BackColor=Color.Transparent,Location=new Point(16,16),AutoSize=true});
            card.Controls.Add(new Label{Text="Select which network interface to measure",Font=SF(8.5f),ForeColor=W_SUB,BackColor=Color.Transparent,Location=new Point(16,38),AutoSize=true});
            var adapters=new[]{"All Networks","Only Ethernet","Only WiFi"};
            _cbAdapter=MakeCombo(card.Width-224,22,adapters,_cfg.NetworkAdapter);
            _cbAdapter.SelectedIndexChanged+=TriggerChange;
            card.Controls.Add(_cbAdapter);
            card.Height=76; return pg;
        }

        // ══════════════════════════════════════════
        //  PAGE: APPEARANCE (renamed from Theme)
        // ══════════════════════════════════════════
        Panel BuildAppearancePage()
        {
            var pg=MakePage("Appearance","Colors, fonts, and overlay look");
            int y=80;

            // ── Stats color ──
            { var card=MakeCard(pg,y,_contentArea.Width-32);
              card.Controls.Add(new Label{Text="Stats Color",Font=SF(10f,FontStyle.Bold),ForeColor=W_FG,BackColor=Color.Transparent,Location=new Point(16,14),AutoSize=true});
              card.Controls.Add(new Label{Text="Applied to stat values — CPU%, MB/s, etc.",Font=SF(8.5f),ForeColor=W_SUB,BackColor=Color.Transparent,Location=new Point(16,36),AutoSize=true});
              int cx=16;
              foreach(var h in new[]{"#60cdff","#f56565","#48bb78","#ecc94b","#ed64a6","#9f7aea","#4fd1c5","#ffffff"})
              { var sw=MakeSwatch(h,"ACC"); sw.Location=new Point(cx,62); card.Controls.Add(sw); cx+=48; }
              card.Controls.Add(MakePickerBtn("ACC",cx,62)); card.Height=112; y+=124; }

            // ── Label color ──
            { var card=MakeCard(pg,y,_contentArea.Width-32);
              card.Controls.Add(new Label{Text="Label Color",Font=SF(10f,FontStyle.Bold),ForeColor=W_FG,BackColor=Color.Transparent,Location=new Point(16,14),AutoSize=true});
              card.Controls.Add(new Label{Text="Color of UP / DN / CPU / RAM / GPU / TEMP labels",Font=SF(8.5f),ForeColor=W_SUB,BackColor=Color.Transparent,Location=new Point(16,36),AutoSize=true});
              int cx=16;
              foreach(var h in new[]{"#8892a4","#ffffff","#f56565","#48bb78","#ecc94b","#ed64a6","#9f7aea","#4fd1c5"})
              { var sw=MakeSwatch(h,"LBL"); sw.Location=new Point(cx,62); card.Controls.Add(sw); cx+=48; }
              card.Controls.Add(MakePickerBtn("LBL",cx,62)); card.Height=112; y+=124; }

            // ── Overlay Font ──
            { var card=MakeCard(pg,y,_contentArea.Width-32);
              card.Controls.Add(new Label{Text="Overlay Font",Font=SF(10f,FontStyle.Bold),ForeColor=W_FG,BackColor=Color.Transparent,Location=new Point(16,16),AutoSize=true});
              card.Controls.Add(new Label{Text="Font used in the floating overlay widget",Font=SF(8.5f),ForeColor=W_SUB,BackColor=Color.Transparent,Location=new Point(16,38),AutoSize=true});
              var fonts=new[]{"Poppins","Segoe UI","Inter","Roboto","Consolas","Cascadia Code","Arial","Calibri","Verdana","Tahoma"};
              _cbFont=MakeCombo(card.Width-224,22,fonts,_cfg.FontFamily);
              _cbFont.SelectedIndexChanged+=TriggerChange; card.Controls.Add(_cbFont); card.Height=76; y+=88; }

            // ── Overlay Background ──
            { var card=MakeCard(pg,y,_contentArea.Width-32); int cy=0;
              card.Controls.Add(new Label{Text="Overlay Background",Font=SF(10f,FontStyle.Bold),ForeColor=W_FG,BackColor=Color.Transparent,Location=new Point(16,12),AutoSize=true}); cy=36;
              // Enable toggle
              _tgAutoTheme = CardRow(card,"Follow Windows Theme","Auto-enable background in Light mode",_cfg.AutoThemeFollow,ref cy);
              _tgOvBg = CardRow(card,"Enable Background","Show a semi-transparent backdrop behind overlay text",_cfg.OverlayBgEnabled,ref cy);
              // Color row
              if(cy>0) card.Controls.Add(new Panel{Location=new Point(12,cy),Size=new Size(card.Width-24,1),BackColor=W_DIV}); cy+=1;
              card.Controls.Add(new Label{Text="Background Color",Font=SF(10f),ForeColor=W_FG,BackColor=Color.Transparent,Location=new Point(16,cy+16),AutoSize=true});
              _ovBgPreview = new Panel { Size=new Size(26,26), BackColor=_cfg.OverlayBgCol(), Cursor=Cursors.Hand, Location=new Point(card.Width-78,cy+12) };
              _ovBgPreview.Paint += (s,e2) => {
                  var g2=e2.Graphics; g2.SmoothingMode=SmoothingMode.AntiAlias;
                  using var br=new SolidBrush(_ovBgPreview.BackColor); g2.FillEllipse(br,1,1,23,23);
                  using var pen=new Pen(W_DIV,1.5f); g2.DrawEllipse(pen,1,1,23,23);
              };
              _ovBgPreview.Click += (s,e2) => {
                  using var cd=new ColorDialog{FullOpen=true,Color=_ovBgPreview.BackColor};
                  if(cd.ShowDialog()==DialogResult.OK) { _ovBgHex=$"#{cd.Color.R:X2}{cd.Color.G:X2}{cd.Color.B:X2}"; _ovBgPreview.BackColor=cd.Color; _ovBgPreview.Invalidate(); TriggerChange(); }
              };
              var pickBtn=MakePickerBtn("OVB",card.Width-44,cy+10); card.Controls.AddRange(new Control[]{_ovBgPreview,pickBtn}); cy+=52;
              // Opacity slider
              if(cy>0) card.Controls.Add(new Panel{Location=new Point(12,cy),Size=new Size(card.Width-24,1),BackColor=W_DIV}); cy+=1;
              card.Controls.Add(new Label{Text="Background Opacity",Font=SF(10f),ForeColor=W_FG,BackColor=Color.Transparent,Location=new Point(16,cy+14),AutoSize=true});
              _lblOpacity=new Label{Text=$"{_cfg.OverlayBgOpacity}%",Font=SF(9f,FontStyle.Bold),ForeColor=W_ACC,BackColor=Color.Transparent,Location=new Point(card.Width-68,cy+14),AutoSize=true};
              _sliderOpacity=new FlatSlider{Minimum=5,Maximum=100,Value=_cfg.OverlayBgOpacity,Location=new Point(16,cy+40),Width=card.Width-48,BackColor=Color.Transparent,TrackBg=W_DIV,TrackFill=W_ACC};
              _sliderOpacity.ValueChanged+=(s,e2)=>{_lblOpacity.Text=$"{_sliderOpacity.Value}%";TriggerChange();};
              card.Controls.AddRange(new Control[]{_lblOpacity,_sliderOpacity}); cy+=76;
              card.Height=cy+8; y+=card.Height+12; }

            // ── Overlay Scale ──
            { var card=MakeCard(pg,y,_contentArea.Width-32); int cy=0;
              card.Controls.Add(new Label{Text="Overlay Size",Font=SF(10f,FontStyle.Bold),ForeColor=W_FG,BackColor=Color.Transparent,Location=new Point(16,12),AutoSize=true}); cy=36;
              if(cy>0) card.Controls.Add(new Panel{Location=new Point(12,cy),Size=new Size(card.Width-24,1),BackColor=W_DIV}); cy+=1;
              card.Controls.Add(new Label{Text="Scale",Font=SF(10f),ForeColor=W_FG,BackColor=Color.Transparent,Location=new Point(16,cy+14),AutoSize=true});
              card.Controls.Add(new Label{Text="Resize the overlay (70% – 140%)",Font=SF(8.5f),ForeColor=W_SUB,BackColor=Color.Transparent,Location=new Point(16,cy+34),AutoSize=true});
              _lblScale=new Label{Text=$"{_cfg.OverlayScale}%",Font=SF(9f,FontStyle.Bold),ForeColor=W_ACC,BackColor=Color.Transparent,Location=new Point(card.Width-68,cy+14),AutoSize=true};
              _sliderScale=new FlatSlider{Minimum=70,Maximum=140,Value=_cfg.OverlayScale,Location=new Point(16,cy+58),Width=card.Width-48,BackColor=Color.Transparent,TrackBg=W_DIV,TrackFill=W_ACC};
              _sliderScale.ValueChanged+=(s,e2)=>{_lblScale.Text=$"{_sliderScale.Value}%";TriggerChange();};
              card.Controls.AddRange(new Control[]{_lblScale,_sliderScale}); cy+=94;
              card.Height=cy+8; }
            return pg;
        }

        // ══════════════════════════════════════════
        //  PAGE: ABOUT
        // ══════════════════════════════════════════
        Panel BuildAboutPage()
        {
            var pg=MakePage("About","");
            int mid=_contentArea.Width/2;
            pg.Controls.Add(new Label{Text="TaskNet",Font=SF(26f,FontStyle.Bold),ForeColor=W_FG,BackColor=Color.Transparent,TextAlign=ContentAlignment.MiddleCenter,Location=new Point(0,70),Width=_contentArea.Width,Height=48});

            // Version badge
            int vw=130;
            var ver=new Panel{BackColor=Color.Transparent,Width=vw,Height=28,Location=new Point(mid-(vw/2),126)};
            ver.Paint+=(s,e)=>{ var g=e.Graphics; g.SmoothingMode=SmoothingMode.AntiAlias;
                using var path=RR(new Rectangle(0,0,vw-1,27),14); using var b=new SolidBrush(W_CARD); g.FillPath(b,path);
                using var p=new Pen(W_DIV,1f); g.DrawPath(p,path);
                using var fIco=new Font("Segoe MDL2 Assets", 9.5f); using var f=SF(8.5f,FontStyle.Bold); using var br=new SolidBrush(W_SUB); using var brAcc=new SolidBrush(W_ACC);
                g.DrawString("Version 4.5.0",f,br,16,8); g.DrawString("\uE950",fIco,brAcc,98,9);
            };
            pg.Controls.Add(ver);
            pg.Controls.Add(new Label{Text="Lightweight System Stats Overlay for Windows 11",Font=SF(9.5f),ForeColor=W_SUB,BackColor=Color.Transparent,TextAlign=ContentAlignment.MiddleCenter,Location=new Point(0,164),Width=_contentArea.Width,Height=22});

            // Buttons
            var btnBg=_cfg.DarkMode?Color.FromArgb(46,46,56):Color.FromArgb(206,206,218);
            var bGh=new PillButton("  View on GitHub",  btnBg,W_FG){Size=new Size(168,38),Location=new Point(mid-178,198)};
            var bUp=new PillButton("  Check for Updates",btnBg,W_FG){Size=new Size(168,38),Location=new Point(mid+10,198)};
            var bFeat=new PillButton("  TaskNet Features",btnBg,W_FG){Size=new Size(168,38),Location=new Point(mid-84,248)};
            bGh.Click+=(s,e)=>OpenUrl("https://github.com/imali019/TaskNetByAli");
            var lSt=new Label{Text="",Location=new Point(mid-200,298),Size=new Size(400,22),TextAlign=ContentAlignment.MiddleCenter,ForeColor=W_SUB,Font=SF(8.5f),BackColor=Color.Transparent};
            var pb=new ProgressBar{Location=new Point(mid-200,324),Size=new Size(400,4),Visible=false,Style=ProgressBarStyle.Marquee,MarqueeAnimationSpeed=30};
            bUp.Click+=async(s,e)=>await DoUpdate(bUp,lSt,pb);
            bFeat.Click+=(s,e)=>ShowFeaturesPopup();

            // ── Instagram 2026 icon ──────────────
            var igIcon=new Panel{Size=new Size(56,56),Location=new Point(mid-28,350),BackColor=Color.Transparent,Cursor=Cursors.Hand};
            igIcon.Paint+=(s,e)=>{
                var g=e.Graphics; g.SmoothingMode=SmoothingMode.AntiAlias;
                // Instagram gradient (purple → red → gold)
                var rc=new Rectangle(0,0,55,55);
                using var path=RR(rc,14);
                using var lgb=new LinearGradientBrush(rc,Color.Black,Color.Black,45f);
                var blend=new ColorBlend(3);
                blend.Colors=new[]{Color.FromArgb(128,78,228),Color.FromArgb(225,48,72),Color.FromArgb(252,186,30)};
                blend.Positions=new[]{0f,0.5f,1f};
                lgb.InterpolationColors=blend;
                g.FillPath(lgb,path);
                // Camera body (rounded rect)
                using var wp=new Pen(Color.White,2f);
                g.DrawRoundedRectangle(wp,12,13,30,28,7);
                // Lens circle
                g.DrawEllipse(wp,18,18,18,18);
                // Flash dot
                using var wb=new SolidBrush(Color.White); g.FillEllipse(wb,36,12,6,6);
            };
            igIcon.Click+=(s,e)=>OpenUrl("https://instagram.com/022.a2");

            var igHandle=new Label{Text="022.a2",Font=SF(9.5f,FontStyle.Bold),ForeColor=W_FG,BackColor=Color.Transparent,TextAlign=ContentAlignment.MiddleCenter,Location=new Point(mid-50,412),Size=new Size(100,20),Cursor=Cursors.Hand};
            igHandle.Click+=(s,e)=>OpenUrl("https://instagram.com/022.a2");
            pg.Controls.Add(igHandle);
            pg.Controls.Add(new Label{Text="Instagram",Font=SF(8f),ForeColor=W_SUB,BackColor=Color.Transparent,TextAlign=ContentAlignment.MiddleCenter,Location=new Point(mid-50,432),Size=new Size(100,18)});

            // Footer
            pg.Controls.Add(new Label{Text="TaskNet Powered By Ali",Font=SF(8.5f,FontStyle.Bold),ForeColor=W_SUB,BackColor=Color.Transparent,TextAlign=ContentAlignment.MiddleCenter,Location=new Point(0,468),Width=_contentArea.Width,Height=20});

            pg.Controls.AddRange(new Control[]{bGh,bUp,bFeat,lSt,pb,igIcon});
            return pg;
        }

        void ShowFeaturesPopup()
        {
            var frm = new Form {
                Text = "TaskNet Features",
                Size = new Size(400, 360),
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false, MinimizeBox = false,
                BackColor = W_CONT,
                ShowIcon = false
            };
            var lbl = new Label {
                Text = "• Real-time Upload & Download speed monitoring\n\n" +
                       "• CPU, RAM, GPU usage tracking\n\n" +
                       "• GPU Temperature monitoring\n\n" +
                       "• Clean overlay on taskbar\n\n" +
                       "• Customizable appearance (colors, fonts, size, opacity)\n\n" +
                       "• Auto theme switching (based on Windows theme)\n\n" +
                       "• Lightweight performance (very low resource usage)",
                Font = SF(9.5f),
                ForeColor = W_FG,
                Dock = DockStyle.Fill,
                Padding = new Padding(20)
            };
            frm.Controls.Add(lbl);
            frm.ShowDialog(this);
        }

        // ══════════════════════════════════════════
        //  HELPERS
        // ══════════════════════════════════════════
        static GraphicsPath RR(Rectangle rc, int r)
        {
            var p=new GraphicsPath(); int d=r*2;
            p.AddArc(rc.X,rc.Y,d,d,180,90); p.AddArc(rc.Right-d,rc.Y,d,d,270,90);
            p.AddArc(rc.Right-d,rc.Bottom-d,d,d,0,90); p.AddArc(rc.X,rc.Bottom-d,d,d,90,90);
            p.CloseFigure(); return p;
        }

        Panel MakePage(string title, string sub)
        {
            var pg=new Panel{Dock=DockStyle.Fill,BackColor=W_CONT,AutoScroll=true};
            var lbl=new Label{Text=title,Font=SF(20f,FontStyle.Bold),ForeColor=W_FG,BackColor=Color.Transparent,Location=new Point(16,24),AutoSize=true};
            pg.Controls.Add(lbl);
            if(!string.IsNullOrEmpty(sub)){int tw=TextRenderer.MeasureText(title,lbl.Font).Width;pg.Controls.Add(new Label{Text=sub,Font=SF(9.5f),ForeColor=W_SUB,BackColor=Color.Transparent,Location=new Point(16+tw+10,39),AutoSize=true});}
            return pg;
        }

        WinUICard MakeCard(Panel parent, int y, int w)
        {
            bool lit=!_cfg.DarkMode;
            var c=new WinUICard{Location=new Point(16,y),Width=w-16,CardNorm=W_CARD,CardHover=lit?Color.FromArgb(244,244,252):Color.FromArgb(44,44,54),CardBord=W_DIV};
            parent.Controls.Add(c); return c;
        }

        ToggleSwitch CardRow(WinUICard card, string label, string? sub, bool val, ref int cy)
        {
            if(cy>0) card.Controls.Add(new Panel{Location=new Point(12,cy),Size=new Size(card.Width-24,1),BackColor=W_DIV});
            int rH=sub!=null?64:52; int pad=cy>0?1:0;
            card.Controls.Add(new Label{Text=label,Font=SF(10f),ForeColor=W_FG,BackColor=Color.Transparent,Location=new Point(16,cy+pad+(sub!=null?10:(rH-20)/2)),AutoSize=true});
            if(sub!=null) card.Controls.Add(new Label{Text=sub,Font=SF(8.5f),ForeColor=W_SUB,BackColor=Color.Transparent,Location=new Point(16,cy+pad+30),AutoSize=true});
            var tg=new ToggleSwitch{Checked=val,BackColor=Color.Transparent,Location=new Point(card.Width-68,cy+pad+(rH-28)/2)};
            tg.CheckedChanged+=TriggerChange; card.Controls.Add(tg); cy+=rH+pad; return tg;
        }

        ComboBox MakeCombo(int x, int y, string[] items, string sel)
        {
            Color bg=_cfg.DarkMode?Color.FromArgb(48,48,60):Color.FromArgb(240,240,250);
            var cb=new ComboBox{Location=new Point(x,y),Size=new Size(204,32),DropDownStyle=ComboBoxStyle.DropDownList,DrawMode=DrawMode.OwnerDrawFixed,ItemHeight=28,BackColor=bg,ForeColor=W_FG,FlatStyle=FlatStyle.Flat,Font=SF(9.5f)};
            cb.DrawItem+=(s,e)=>{
                if(e.Index<0) return; bool isSel=(e.State&DrawItemState.Selected)!=0;
                Color itemBg=isSel?W_ACC:bg; e.Graphics.FillRectangle(new SolidBrush(itemBg),e.Bounds);
                using var f=SF(9.5f); e.Graphics.DrawString(cb.Items[e.Index]?.ToString()??"",f,new SolidBrush(isSel?Color.White:W_FG),e.Bounds.X+10,e.Bounds.Y+(e.Bounds.Height-16)/2f);
            };
            cb.Items.AddRange(items); int idx=Array.IndexOf(items,sel); cb.SelectedIndex=idx>=0?idx:0; return cb;
        }

        Panel MakePickerBtn(string tag, int x, int y)
        {
            var btn=new Panel{Size=new Size(36,36),BackColor=Color.Transparent,Cursor=Cursors.Hand,Location=new Point(x,y)};
            btn.Paint+=(s,e)=>{
                var g=e.Graphics; g.SmoothingMode=SmoothingMode.AntiAlias; // no solid clear
                var rc=new RectangleF(3,3,30,30);
                using var lgb=new LinearGradientBrush(new Rectangle(3,3,30,31),Color.FromArgb(255,72,72),Color.FromArgb(72,72,255),45f);
                g.FillEllipse(lgb,rc); using var wp=new Pen(Color.White,1.2f); g.DrawEllipse(wp,4,4,27,27);
                using var wf=new Font("Segoe UI",12f,FontStyle.Bold); using var wb=new SolidBrush(Color.White);
                var sz=g.MeasureString("+",wf); g.DrawString("+",wf,wb,(36-sz.Width)/2f-0.5f,(36-sz.Height)/2f-1f);
            };
            btn.Click+=(s,e)=>{
                using var cd=new ColorDialog{FullOpen=true};
                try{ cd.Color=tag=="ACC"?ColorTranslator.FromHtml(_accentHex):tag=="LBL"?ColorTranslator.FromHtml(_labelHex):ColorTranslator.FromHtml(_ovBgHex); }catch{}
                if(cd.ShowDialog()==DialogResult.OK){
                    string hex=$"#{cd.Color.R:X2}{cd.Color.G:X2}{cd.Color.B:X2}";
                    if(tag=="ACC") _accentHex=hex; else if(tag=="LBL") _labelHex=hex; else { _ovBgHex=hex; if(_ovBgPreview!=null){_ovBgPreview.BackColor=cd.Color;_ovBgPreview.Invalidate();} }
                    TriggerChange();
                }
            };
            return btn;
        }

        Panel MakeSwatch(string hex, string tag)
        {
            Color c; try{c=ColorTranslator.FromHtml(hex);}catch{c=Color.Gray;}
            bool IsSelected()=>tag=="ACC"?_accentHex.Equals(hex,StringComparison.OrdinalIgnoreCase):_labelHex.Equals(hex,StringComparison.OrdinalIgnoreCase);
            var dot=new Panel{Size=new Size(40,40),BackColor=Color.Transparent,Cursor=Cursors.Hand,Tag=tag};
            dot.Paint+=(s,e)=>{
                var g=e.Graphics; g.SmoothingMode=SmoothingMode.AntiAlias;
                bool sel=IsSelected();
                if(sel){using var rp=new Pen(W_FG,2f);g.DrawEllipse(rp,2,2,35,35);}
                using var br=new SolidBrush(c); g.FillEllipse(br,sel?5:8,sel?5:8,sel?30:24,sel?30:24);
            };
            dot.Click+=(s,e)=>{
                if(tag=="ACC")_accentHex=hex;else _labelHex=hex;
                if(dot.Parent!=null) foreach(Control ctrl in dot.Parent.Controls) if(ctrl is Panel dp&&tag.Equals(dp.Tag?.ToString())) dp.Invalidate();
                TriggerChange();
            };
            return dot;
        }

        // ── TRIGGER CHANGE: syncs ALL settings ─────
        void TriggerChange(object? s=null, EventArgs? e=null)
        {
            if(_tgOverlay==null) return;
            _cfg.ShowOverlay      = _tgOverlay.Checked;
            _cfg.HideOnFullscreen = _tgHideFs.Checked;
            _cfg.LockOverlay      = _tgLock.Checked;
            _cfg.StartWithWindows = _tgStartup.Checked;
            if(_tgDark    !=null) _cfg.DarkMode      = _tgDark.Checked;
            _cfg.ShowCpu      = _tgCpu.Checked;
            _cfg.ShowRam      = _tgRam.Checked;
            _cfg.ShowGpu      = _tgGpu.Checked;
            if(_tgGpuTemp !=null) _cfg.ShowGpuTemp   = _tgGpuTemp.Checked;
            _cfg.ShowUpload   = _tgUp.Checked;
            _cfg.ShowDownload = _tgDn.Checked;
            _cfg.NetworkAdapter = _cbAdapter?.Text ?? _cfg.NetworkAdapter;

            _cfg.AccentColor    = _accentHex;
            _cfg.StatLabelColor = _labelHex;
            if(_cbFont?.SelectedItem!=null) _cfg.FontFamily=_cbFont.SelectedItem.ToString()!;
            if(_tgAutoTheme!=null) _cfg.AutoThemeFollow = _tgAutoTheme.Checked;
            if(_tgOvBg   !=null) _cfg.OverlayBgEnabled = _tgOvBg.Checked;
            if(_sliderOpacity!=null) _cfg.OverlayBgOpacity = _sliderOpacity.Value;
            if(_sliderScale  !=null) _cfg.OverlayScale     = _sliderScale.Value;
            _cfg.OverlayBgColor = _ovBgHex;
            Result=_cfg; _onChanged?.Invoke();
        }

        async System.Threading.Tasks.Task DoUpdate(Control btn, Label lbl, ProgressBar pb)
        {
            const string VER="4.5.0";
            btn.Enabled=false; pb.Style=ProgressBarStyle.Marquee; pb.Visible=true;
            lbl.ForeColor=W_SUB; lbl.Text="Connecting...";
            try{
                using var http=new HttpClient(); http.Timeout=TimeSpan.FromSeconds(10); http.DefaultRequestHeaders.Add("User-Agent","TaskNetByAli");
                var json=await http.GetStringAsync("https://api.github.com/repos/imali019/TaskNetByAli/releases/latest");
                var doc=JsonDocument.Parse(json); string latest=doc.RootElement.GetProperty("tag_name").GetString()?.TrimStart('v')??VER;
                string url=""; foreach(var a in doc.RootElement.GetProperty("assets").EnumerateArray()){string n=a.GetProperty("name").GetString()??""; if(n.EndsWith(".exe",StringComparison.OrdinalIgnoreCase)){url=a.GetProperty("browser_download_url").GetString()??"";break;}}
                if(latest==VER){pb.Visible=false;lbl.ForeColor=W_GRN;lbl.Text="✓  Already up to date!";btn.Enabled=true;return;}
                if(string.IsNullOrEmpty(url)){pb.Visible=false;lbl.ForeColor=Color.FromArgb(252,175,60);lbl.Text="!  No installer in release.";btn.Enabled=true;return;}
                if(MessageBox.Show($"v{latest} is available! Install now?","Update",MessageBoxButtons.YesNo,MessageBoxIcon.Information)!=DialogResult.Yes){pb.Visible=false;btn.Enabled=true;return;}
                pb.Style=ProgressBarStyle.Continuous; pb.Minimum=0; pb.Maximum=100; pb.Value=0; lbl.Text="Downloading...";
                string tmp=Path.Combine(Path.GetTempPath(),"TaskNet_Setup.exe");
                using var resp=await http.GetAsync(url,HttpCompletionOption.ResponseHeadersRead); resp.EnsureSuccessStatusCode();
                long? total=resp.Content.Headers.ContentLength;
                using var st=await resp.Content.ReadAsStreamAsync(); using var fs=new FileStream(tmp,FileMode.Create);
                byte[] buf=new byte[65536]; long dl=0; int rd;
                while((rd=await st.ReadAsync(buf))>0){await fs.WriteAsync(buf.AsMemory(0,rd));dl+=rd;if(total.HasValue){pb.Value=(int)(dl*100/total.Value);lbl.Text=$"Downloading... {pb.Value}%";}}
                fs.Close(); lbl.ForeColor=W_GRN; lbl.Text="✓  Launching installer...";
                await System.Threading.Tasks.Task.Delay(400);
                Process.Start(new ProcessStartInfo{FileName=tmp,UseShellExecute=true}); Application.Exit();
            }catch(HttpRequestException){pb.Visible=false;lbl.ForeColor=W_RED;lbl.Text="✕  No internet.";btn.Enabled=true;}
             catch(Exception ex){pb.Visible=false;lbl.ForeColor=W_RED;lbl.Text=$"✕  {ex.Message[..Math.Min(52,ex.Message.Length)]}";btn.Enabled=true;}
        }

        static void OpenUrl(string url){try{Process.Start(new ProcessStartInfo(url){UseShellExecute=true});}catch{}}
        void TrySetIcon(){try{string p=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"tasknet.ico");if(File.Exists(p))Icon=new Icon(p);}catch{}}
        [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd,int attr,ref int val,int size);
        void ApplyDwm(bool dark){try{int v=dark?1:0;DwmSetWindowAttribute(Handle,20,ref v,sizeof(int));int r=2;DwmSetWindowAttribute(Handle,33,ref r,sizeof(int));}catch{}}
    }

    // ══════════════════════════════════════════
    //  GRAPHICS EXTENSION
    // ══════════════════════════════════════════
    static class GfxExt
    {
        public static void DrawRoundedRectangle(this Graphics g, Pen pen, float x, float y, float w, float h, float r)
        {
            float d=r*2; using var path=new GraphicsPath();
            path.AddArc(x,y,d,d,180,90); path.AddArc(x+w-d,y,d,d,270,90);
            path.AddArc(x+w-d,y+h-d,d,d,0,90); path.AddArc(x,y+h-d,d,d,90,90);
            path.CloseFigure(); g.DrawPath(pen,path);
        }
    }

    // ══════════════════════════════════════════
    //  MAIN OVERLAY FORM
    // ══════════════════════════════════════════
    public class MainForm : Form
    {
        [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr h, IntPtr hA, int x, int y, int cx, int cy, uint f);
        [DllImport("shell32.dll")] static extern uint SHAppBarMessage(uint msg, ref APPBARDATA d);
        [DllImport("kernel32.dll")] static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX m);
        [DllImport("user32.dll")] static extern bool GetCursorPos(out POINT pt);
        [DllImport("user32.dll")] static extern short GetAsyncKeyState(int vKey);
        [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECT r);
        [DllImport("user32.dll",CharSet=CharSet.Auto)] static extern int GetClassName(IntPtr hWnd, System.Text.StringBuilder lpClassName, int nMaxCount);
        [DllImport("gdi32.dll")] static extern IntPtr CreateRoundRectRgn(int x1,int y1,int x2,int y2,int cx,int cy);
        [DllImport("user32.dll")] static extern int SetWindowRgn(IntPtr hWnd, IntPtr hRgn, bool bRedraw);
        [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr hObject);

        static readonly IntPtr HWND_TOPMOST=new(-1);
        const uint SWP_NOMOVE=2,SWP_NOSIZE=1,SWP_NOACTIVATE=16,ABM_NEW=0,ABM_REMOVE=1,WM_USER=0x0400,APPBAR_MSG=WM_USER+100;

        [StructLayout(LayoutKind.Sequential)] struct APPBARDATA{public uint cbSize,uCallbackMessage,uEdge;public IntPtr hWnd;public RECT rc;public int lParam;}
        [StructLayout(LayoutKind.Sequential)] struct RECT{public int L,T,R,B;}
        [StructLayout(LayoutKind.Sequential)] struct MEMORYSTATUSEX{public uint dwLength,dwMemoryLoad;public ulong a,b,c,d,e,f,g;}
        [StructLayout(LayoutKind.Sequential)] struct POINT{public int X,Y;}

        AppSettings _cfg;
        System.Threading.Timer? _statTimer,_topTimer;
        System.Windows.Forms.Timer? _dragTimer;
        bool _appBarRegistered,_gpuStop,_dragging;
        NotifyIcon? _tray;
        PerformanceCounter? _cpu;
        Computer? _lhm;

        // Overlay labels
        Label? _lUp,_lUpV, _lDn,_lDnV, _lCpu,_lCpuV, _lRam,_lRamV, _lGpu,_lGpuV, _lGpuT,_lGpuTV;
        TableLayoutPanel? _layout;

        float _lastGpu=-1, _lastGpuTemp=-1;
        readonly Queue<float> _gpuTempHistory = new(5);
        float _lastStableGpuTemp = -1f;
        int _lastCpu=-1,_lastRam=-1,_lastGpuPct=-1,_lastGpuTempI=-999;
        string _lastUp="",_lastDn="";
        Point _dragFormOrigin,_dragMouseOrigin;

        // Unique near-invisible color used as TransparencyKey
        static readonly Color TRANS_KEY = Color.FromArgb(1, 0, 1);

        public MainForm()
        {
            _cfg=AppSettings.Load(); InitCpu(); BuildOverlay(); InitTray(); StartTimers();
            Load+=(s,e)=>{ RegisterAppBar(); PositionOverlay(); if(_cfg.X==-1) OpenSettings(); };
        }

        void InitCpu()
        {
            try{_cpu=new PerformanceCounter("Processor Information","% Processor Utility","_Total",true);_cpu.NextValue();}
            catch{try{_cpu=new PerformanceCounter("Processor","% Processor Time","_Total",true);_cpu.NextValue();}catch{}}
        }

        protected override CreateParams CreateParams
        {
            get { var cp=base.CreateParams; cp.ExStyle|=0x00080000; cp.ExStyle|=0x08000000; return cp; }
        }

        // ── Overlay background painted in OnPaint ──
        protected override void OnPaint(PaintEventArgs e)
        {
            if (!_cfg.ShowOverlay || !_cfg.ShouldDrawBg() || Controls.Count==0) return;
            var g=e.Graphics; g.SmoothingMode=SmoothingMode.AntiAlias;
            using var path=RRect(new Rectangle(0,0,Width-1,Height-1),6);
            using var fill=new SolidBrush(_cfg.OverlayBgCol());
            g.FillPath(fill,path);
        }

        void BuildOverlay()
        {
            SetStyle(ControlStyles.SupportsTransparentBackColor|ControlStyles.OptimizedDoubleBuffer|ControlStyles.AllPaintingInWmPaint|ControlStyles.UserPaint,true);
            UpdateStyles(); FormBorderStyle=FormBorderStyle.None; TopMost=true; ShowInTaskbar=false;
            StartPosition=FormStartPosition.Manual;
            BackColor=TRANS_KEY; TransparencyKey=TRANS_KEY;
            AutoSize=true; AutoSizeMode=AutoSizeMode.GrowAndShrink;
            try{string p=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"tasknet.ico");if(File.Exists(p))Icon=new Icon(p);}catch{}
            BuildControls();
        }

        void ApplyOverlayOpacity()
        {
            // Opacity dimming applied cleanly to overlay
            Opacity = _cfg.ShouldDrawBg() ? Math.Clamp(_cfg.OverlayBgOpacity / 100.0, 0.08, 1.0) : 1.0;
        }

        void ApplyWindowRgn()
        {
            if (Width <= 0 || Height <= 0) return;
            try {
                var rgn = CreateRoundRectRgn(0, 0, Width+1, Height+1, 6, 6);
                SetWindowRgn(Handle, rgn, true);
                DeleteObject(rgn);
            } catch { }
        }

        void BuildControls()
        {
            Controls.Clear();
            _lUp=null; _lUpV=null; _lDn=null; _lDnV=null; _lCpu=null; _lCpuV=null; _lRam=null; _lRamV=null; _lGpu=null; _lGpuV=null; _lGpuT=null; _lGpuTV=null;
            _layout=null;
            
            if (!_cfg.ShowOverlay) { ApplyOverlayOpacity(); return; }

            float sf = Math.Clamp(_cfg.OverlayScale / 100f, 0.7f, 1.4f);
            int fs   = Math.Max(6, (int)(_cfg.FontSize * sf));

            Font fb, fl;
            try   { fb=new Font(_cfg.FontFamily,fs,FontStyle.Bold); fl=new Font(_cfg.FontFamily,fs,FontStyle.Bold); }
            catch { fb=new Font("Segoe UI",fs,FontStyle.Bold);      fl=new Font("Segoe UI",fs,FontStyle.Bold); }

            Padding = new Padding((int)(8 * sf)); // Form padding dynamically hugs background tightly

            // Create fully responsive TableLayoutPanel
            _layout = new TableLayoutPanel {
                BackColor = Color.Transparent, // Ensures it doesn't draw blocks
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Margin = new Padding(0),
                Padding = new Padding(0),
                RowCount = 2,
                ColumnCount = 0
            };
            
            _layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            _layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            // Set up Column 1 (Network)
            bool hasNet = _cfg.ShowUpload || _cfg.ShowDownload;
            if (hasNet) {
                _layout.ColumnCount += 2;
                _layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
                _layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
                if (_cfg.ShowUpload)   { _lUp=_L("UP:",_cfg.StatLblCol(),fl); _lUpV=_L("-",_cfg.AccentCol(),fb); _layout.Controls.Add(_lUp, 0, 0); _layout.Controls.Add(_lUpV, 1, 0); }
                if (_cfg.ShowDownload) { _lDn=_L("DN:",_cfg.StatLblCol(),fl); _lDnV=_L("-",_cfg.AccentCol(),fb); _layout.Controls.Add(_lDn, 0, 1); _layout.Controls.Add(_lDnV, 1, 1); }
            }

            // Set up Column 2 (System)
            bool hasSys = _cfg.ShowCpu || _cfg.ShowRam;
            if (hasSys) {
                int colOffset = _layout.ColumnCount;
                _layout.ColumnCount += 2;
                // Add padding before this column for spacing
                _layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
                _layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
                if (_cfg.ShowCpu) { _lCpu=_L("CPU:",_cfg.StatLblCol(),fl, true); _lCpuV=_L("-",_cfg.AccentCol(),fb); _layout.Controls.Add(_lCpu, colOffset, 0); _layout.Controls.Add(_lCpuV, colOffset+1, 0); }
                if (_cfg.ShowRam) { _lRam=_L("RAM:",_cfg.StatLblCol(),fl, true); _lRamV=_L("-",_cfg.AccentCol(),fb); _layout.Controls.Add(_lRam, colOffset, 1); _layout.Controls.Add(_lRamV, colOffset+1, 1); }
            }

            // Set up Column 3 (Hardware)
            bool hasGpu = _cfg.ShowGpu || _cfg.ShowGpuTemp;
            if (hasGpu) {
                int colOffset = _layout.ColumnCount;
                _layout.ColumnCount += 2;
                _layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
                _layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
                if (_cfg.ShowGpu)     { _lGpu=_L("GPU:",_cfg.StatLblCol(),fl, true);   _lGpuV=_L("-",_cfg.AccentCol(),fb);  _layout.Controls.Add(_lGpu, colOffset, 0); _layout.Controls.Add(_lGpuV, colOffset+1, 0); }
                if (_cfg.ShowGpuTemp) { _lGpuT=_L(_cfg.ShowGpu?"TEMP:":"GPU TEMP:",_cfg.StatLblCol(),fl, true); _lGpuTV=_L("-",_cfg.AccentCol(),fb); _layout.Controls.Add(_lGpuT, colOffset, 1); _layout.Controls.Add(_lGpuTV, colOffset+1, 1); }
            }

            Controls.Add(_layout);

            ApplyOverlayOpacity();

            ApplyOverlayOpacity();
            if (IsHandleCreated) ApplyWindowRgn();
            Invalidate();
        }

        Label _L(string text, Color color, Font font, bool isNewColumn = false) {
            return new Label {
                Text = text,
                ForeColor = color,
                Font = font,
                BackColor = Color.Transparent, // No black boxes, ever.
                AutoSize = true,
                Margin = new Padding(isNewColumn ? 12 : 2, 2, 4, 2),
                TextAlign = ContentAlignment.MiddleLeft
            };
        }

        void InitTray()
        {
            var menu=new ContextMenuStrip();
            menu.Items.Add(">  Show / Hide Overlay",null,(s,e)=>{_cfg.ShowOverlay=!_cfg.ShowOverlay;if(_cfg.ShowOverlay){BuildControls();PositionOverlay();Show();ForceTop();}else Hide();_cfg.Save();});
            menu.Items.Add("*  Open Settings",null,(s,e)=>OpenSettings());
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("X  Exit",null,(s,e)=>ExitApp());
            _tray=new NotifyIcon{Text="TaskNet v4.5.0",ContextMenuStrip=menu,Visible=true};
            try{string p=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"tasknet.ico");_tray.Icon=File.Exists(p)?new Icon(p):SystemIcons.Application;}catch{_tray.Icon=SystemIcons.Application;}
            _tray.DoubleClick+=(s,e)=>OpenSettings();
        }

        void StartTimers()
        {
            _statTimer=new System.Threading.Timer(OnStatTick,null,1500,1500);
            _topTimer =new System.Threading.Timer(OnTopTick, null,1000,1000);
            _dragTimer=new System.Windows.Forms.Timer{Interval=80};
            _dragTimer.Tick+=OnDragTick; _dragTimer.Start();

            try {
                _lhm = new Computer { IsGpuEnabled = true };
                _lhm.Open();
            } catch { }

            // GPU thread
            if (_cfg.ShowGpu) {
                new Thread(()=>{
                    while(!_gpuStop){
                        _lastGpu = ReadGpuUsage();
                        Thread.Sleep(1500);
                    }
                }){IsBackground=true,Priority=ThreadPriority.Lowest}.Start();
            }

            // GPU Temperature thread
            if (_cfg.ShowGpuTemp) {
                new Thread(()=>{
                    while(!_gpuStop){
                        _lastGpuTemp = ReadGpuTemp();
                        Thread.Sleep(5000);
                    }
                }){IsBackground=true,Priority=ThreadPriority.Lowest}.Start();
            }

            Load += (s,e) => ApplyWindowRgn();
        }

        bool IsDedicatedGpu(IHardware hw) {
            if (hw.HardwareType == HardwareType.GpuIntel) return false;
            string n = hw.Name.ToLower();
            bool isIntegrated = n.Contains("radeon graphics") || n.Contains("amd radeon(tm) graphics") || n.Contains("vega");
            return !isIntegrated;
        }

        float ReadGpuUsage()
        {
            if (_lhm == null) return -1f;
            try {
                float max = -1;
                foreach (var hw in _lhm.Hardware) {
                    if (hw.HardwareType != HardwareType.GpuNvidia &&
                        hw.HardwareType != HardwareType.GpuAmd   &&
                        hw.HardwareType != HardwareType.GpuIntel) continue;
                    if (!IsDedicatedGpu(hw)) continue;
                    hw.Update();
                    foreach (var s in hw.Sensors) {
                        if (s.SensorType == SensorType.Load && s.Value.HasValue && s.Value.Value > max)
                            max = s.Value.Value;
                    }
                }
                if (max >= 0f) return max <= 3f ? 0f : max;
            } catch { }
            return -1f;
        }

        float ReadGpuTemp()
        {
            // ── 1. Sensor selection ────────────────────────────────────────
            float raw = -1f;
            if (_lhm != null) {
                try {
                    var preferred = new List<float>();
                    var fallback  = new List<float>();
                    string[] excluded  = { "hotspot", "memory", "vrm", "package" };
                    string[] goodNames = { "gpu core", "gpu" };

                    foreach (var hw in _lhm.Hardware) {
                        if (hw.HardwareType != HardwareType.GpuNvidia &&
                            hw.HardwareType != HardwareType.GpuAmd   &&
                            hw.HardwareType != HardwareType.GpuIntel) continue;
                        if (!IsDedicatedGpu(hw)) continue;
                        hw.Update();
                        foreach (var s in hw.Sensors) {
                            if (s.SensorType != SensorType.Temperature) continue;
                            float val = s.Value ?? -1f;
                            if (val < 30f || val > 120f) continue;           // range gate
                            string sn = s.Name.Trim().ToLower();
                            if (excluded.Any(e => sn.Contains(e))) continue; // exclude noisy sensors
                            if (goodNames.Any(g => sn == g))
                                preferred.Add(val);
                            else
                                fallback.Add(val);
                        }
                    }
                    raw = preferred.Count > 0 ? preferred.Min()
                        : fallback.Count  > 0 ? fallback.Min()
                        : -1f;
                } catch { }
            }

            // ── 2. Stability layer ─────────────────────────────────────────
            if (raw < 0f) return -1f;

            if (_gpuTempHistory.Count < 2) {
                // Cold-start: accept unconditionally, show raw directly
                _gpuTempHistory.Enqueue(raw);
                _lastStableGpuTemp = raw;
                return raw;
            }

            if (Math.Abs(raw - _lastStableGpuTemp) <= 15f) {
                // Normal: accept, smooth
                if (_gpuTempHistory.Count == 5) _gpuTempHistory.Dequeue();
                _gpuTempHistory.Enqueue(raw);
                _lastStableGpuTemp = raw;
                return _gpuTempHistory.Average();
            }

            // Spike (> 15°C jump): keep last displayed value
            return _lastGpuTemp >= 0f ? _lastGpuTemp : raw;
        }

        PerformanceCounter? _netSend,_netRecv; string _netAdp=""; NetworkInterface[]? _cachedNics;

        void InitNetCounters(string adp)
        {
            try{
                _netSend?.Dispose();_netRecv?.Dispose();_netSend=null;_netRecv=null;
                var cat=new PerformanceCounterCategory("Network Interface"); string[] instances=cat.GetInstanceNames();
                var nics=NetworkInterface.GetAllNetworkInterfaces(); var matched=new List<string>();
                foreach(var inst in instances){ foreach(var ni in nics){ if(ni.OperationalStatus!=OperationalStatus.Up||ni.NetworkInterfaceType==NetworkInterfaceType.Loopback) continue;
                    string desc=ni.Description.Replace('(','[').Replace(')',']');
                    if(!inst.Equals(desc,StringComparison.OrdinalIgnoreCase)&&!inst.Contains(ni.Name,StringComparison.OrdinalIgnoreCase)&&!ni.Description.Contains(inst,StringComparison.OrdinalIgnoreCase)&&!inst.Contains(ni.Description.Split(' ')[0],StringComparison.OrdinalIgnoreCase)) continue;
                    if(adp=="Only Ethernet"&&ni.NetworkInterfaceType!=NetworkInterfaceType.Ethernet) continue;
                    if(adp=="Only WiFi"&&ni.NetworkInterfaceType!=NetworkInterfaceType.Wireless80211) continue;
                    matched.Add(inst);break;}}
                if(matched.Count==0&&adp=="All Networks"){ foreach(var inst in instances){if(inst.ToLower().Contains("loopback")||inst.ToLower().Contains("local")||inst.ToLower().Contains("isatap"))continue;matched.Add(inst);}}
                if(matched.Count>0){ _netSend=new PerformanceCounter("Network Interface","Bytes Sent/sec",matched[0],true); _netRecv=new PerformanceCounter("Network Interface","Bytes Received/sec",matched[0],true); _netSend.NextValue();_netRecv.NextValue();}
                _netAdp=adp;
            }catch{_netSend=null;_netRecv=null;}
        }

        (double,double) GetNetDelta()
        {
            try{ string adp=_cfg.NetworkAdapter; if(_netSend==null||_netRecv==null||adp!=_netAdp) InitNetCounters(adp);
                 if(_netSend==null||_netRecv==null) return(0,0);
                 double up=_netSend.NextValue(),dn=_netRecv.NextValue(); _cachedNics=new NetworkInterface[1];
                 return(up>0?up:0,dn>0?dn:0);
            }catch{_netSend=null;_netRecv=null;return(0,0);}
        }

        void OnStatTick(object? _)
        {
            try{
                float cpu=_cpu!=null?Math.Clamp(_cpu.NextValue(),0,100):0;
                var mem=new MEMORYSTATUSEX{dwLength=(uint)Marshal.SizeOf<MEMORYSTATUSEX>()};
                GlobalMemoryStatusEx(ref mem);
                var(up,dn)=GetNetDelta();
                int cpuI=(int)cpu, ramI=(int)mem.dwMemoryLoad, gpuI=(int)_lastGpu, tempI=(int)_lastGpuTemp;
                string upS=Fmt(up),dnS=Fmt(dn);
                bool changed=cpuI!=_lastCpu||ramI!=_lastRam||gpuI!=_lastGpuPct||upS!=_lastUp||dnS!=_lastDn||tempI!=_lastGpuTempI;
                if(!changed) return;
                _lastCpu=cpuI;_lastRam=ramI;_lastGpuPct=gpuI;_lastGpuTempI=tempI;_lastUp=upS;_lastDn=dnS;
                BeginInvoke(()=>{
                    void Set(Label? l,string v){if(l!=null&&l.Text!=v)l.Text=v;}
                    if(_cfg.ShowCpu)      Set(_lCpuV, $"{cpuI}%");
                    if(_cfg.ShowRam)      Set(_lRamV, $"{ramI}%");
                    if(_cfg.ShowGpu)      Set(_lGpuV, gpuI>=0?$"{gpuI}%":"Not Available");
                    if(_cfg.ShowGpuTemp)  Set(_lGpuTV, _lastGpuTemp>=0?$"{_lastGpuTemp:0}°C":"Not Available");
                    bool noNet=_netSend==null;
                    if(_cfg.ShowUpload)   Set(_lUpV,  noNet?"-":upS);
                    if(_cfg.ShowDownload) Set(_lDnV,  noNet?"-":dnS);
                    if(_tray!=null) _tray.Text=$"TaskNet v4.5.0 | CPU:{cpuI}% RAM:{ramI}%";
                });
            }catch{}
        }

        static string Fmt(double b){if(b<1000)return $"{b:0}B/s";if(b<1000000)return $"{b/1000:0.0}KB/s";return $"{b/1000000:0.00}MB/s";}

        void OnDragTick(object? s, EventArgs e)
        {
            if(_cfg.LockOverlay) return;
            bool down=(GetAsyncKeyState(0x01)&0x8000)!=0; GetCursorPos(out POINT cur); var curPt=new Point(cur.X,cur.Y);
            if(down&&!_dragging&&new Rectangle(Location,Size).Contains(curPt)){_dragging=true;_dragFormOrigin=Location;_dragMouseOrigin=curPt;}
            if(_dragging&&down) Location=new Point(_dragFormOrigin.X+curPt.X-_dragMouseOrigin.X,_dragFormOrigin.Y+curPt.Y-_dragMouseOrigin.Y);
            if(!down&&_dragging){_dragging=false;_cfg.X=Location.X;_cfg.Y=Location.Y;_cfg.Save();}
        }

        void RegisterAppBar()
        {
            try{var abd=new APPBARDATA{cbSize=(uint)Marshal.SizeOf<APPBARDATA>(),hWnd=Handle,uCallbackMessage=APPBAR_MSG};SHAppBarMessage(ABM_NEW,ref abd);_appBarRegistered=true;}catch{}
        }

        void PositionOverlay()
        {
            try{
                if(!_cfg.ShowOverlay){if(Visible)Hide();return;}
                if(_cfg.X==-1){var sc=Screen.PrimaryScreen!.WorkingArea;Location=new Point(sc.Right-Width-10,sc.Bottom-Height-2);_cfg.X=Location.X;_cfg.Y=Location.Y;}
                else Location=new Point(_cfg.X,_cfg.Y);
                if(!Visible)Show(); ForceTop();
            }catch{}
        }

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            if(m.Msg==APPBAR_MSG&&(int)m.WParam==3){if((int)m.LParam==1){if(Visible)Hide();}else{if(_cfg.ShowOverlay&&!Visible){Show();ForceTop();}}}
        }

        protected override void OnResize(EventArgs e) { base.OnResize(e); if(IsHandleCreated) ApplyWindowRgn(); }

        bool IsFullscreenActive()
        {
            if(!_cfg.HideOnFullscreen) return false;
            try{
                var hwnd=GetForegroundWindow(); if(hwnd==IntPtr.Zero||hwnd==Handle) return false;
                var sb=new System.Text.StringBuilder(256); GetClassName(hwnd,sb,256); string cls=sb.ToString();
                if(cls is "Progman" or "WorkerW" or "Shell_TrayWnd" or "Shell_SecondaryTrayWnd") return false;
                GetWindowRect(hwnd,out RECT r); var screen=Screen.FromHandle(hwnd).Bounds;
                return r.L<=screen.Left&&r.T<=screen.Top&&r.R>=screen.Right&&r.B>=screen.Bottom;
            }catch{return false;}
        }

        private bool _lastThemeLight;
        void OnTopTick(object? _)
        {
            try{
                if(!IsHandleCreated||IsDisposed) return;
                Invoke(new Action(()=>{
                    if(!_cfg.ShowOverlay) { if(Visible)Hide(); return; }
                    if(IsFullscreenActive()) { if(Visible)Hide(); return; }
                    
                    if(_cfg.AutoThemeFollow) {
                        bool curLgt = AppSettings.IsSystemLightMode();
                        if(curLgt != _lastThemeLight) {
                            _lastThemeLight = curLgt;
                            ApplyOverlayOpacity();
                            Invalidate();
                        }
                    }
                    if(!Visible) Show(); 
                    ForceTop();
                }));
            }catch{}
        }

        void ForceTop(){try{SetWindowPos(Handle,HWND_TOPMOST,0,0,0,0,SWP_NOMOVE|SWP_NOSIZE|SWP_NOACTIVATE);}catch{}}

        void OpenSettings()
        {
            using var dlg=new SettingsWindow(_cfg,()=>{
                _cfg.Save(); _cfg.SetStartup(_cfg.StartWithWindows); _netAdp="";
                BuildControls(); PositionOverlay(); if(!_cfg.ShowOverlay)Hide();
            });
            dlg.ShowDialog();
        }

        void ExitApp()
        {
            _gpuStop=true;
            if(_appBarRegistered){try{var abd=new APPBARDATA{cbSize=(uint)Marshal.SizeOf<APPBARDATA>(),hWnd=Handle};SHAppBarMessage(ABM_REMOVE,ref abd);}catch{}}
            _dragTimer?.Stop();_dragTimer?.Dispose(); _statTimer?.Dispose();_topTimer?.Dispose();_cpu?.Dispose();
            _netSend?.Dispose();_netRecv?.Dispose();
            _lhm?.Close();
            if(_tray!=null){_tray.Visible=false;_tray.Dispose();} Application.Exit();
        }

        protected override void OnFormClosing(FormClosingEventArgs e){ExitApp();base.OnFormClosing(e);}

        static GraphicsPath RRect(Rectangle rc, int r)
        {
            var p=new GraphicsPath(); int d=r*2;
            p.AddArc(rc.X,rc.Y,d,d,180,90); p.AddArc(rc.Right-d,rc.Y,d,d,270,90);
            p.AddArc(rc.Right-d,rc.Bottom-d,d,d,0,90); p.AddArc(rc.X,rc.Bottom-d,d,d,90,90);
            p.CloseFigure(); return p;
        }
    }
}
