/*
 * ====================================================================================
 * PROJECT:   Wallpaper Slideshow
 * AUTHOR:    Osman Onur Koç (@osmanonurkoc)
 * WEBSITE:   https://www.osmanonurkoc.com
 * LICENSE:   MIT License
 *
 * DESCRIPTION:
 * A lightweight, native .NET utility to cycle desktop wallpapers automatically.
 * Features System Tray support, Dark/Light theme detection, Context Menu integration,
 * and zero-dependency configuration. Ported from Python to .NET for performance.
 * ====================================================================================
 */

using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using System.Diagnostics;
using System.Text.Json;
using System.Reflection;

namespace WallpaperSlideshow;

/// <summary>
/// Represents the application configuration settings serialized to JSON.
/// </summary>
public class Config
{
    public string WallpaperFolder { get; set; } = @"C:\Windows\Web\Wallpaper";
    public int IntervalMinutes { get; set; } = 15;
    public bool Randomize { get; set; } = true;
    public bool RunAtStartup { get; set; } = false;
    public int LastIndex { get; set; } = 0;
}

/// <summary>
/// Handles system theme detection (Light/Dark mode) and provides the centralized color palette.
/// </summary>
public static class Theme
{
    // Active Colors (Used by Custom Controls)
    public static Color BackColor { get; private set; }
    public static Color SurfaceColor { get; private set; }
    public static Color TextColor { get; private set; }
    public static Color SubTextColor { get; private set; }
    public static Color BorderColor { get; private set; }
    public static Color AccentColor { get; private set; }
    public static Color HoverColor { get; private set; }
    public static int Radius = 8;
    public static bool IsDarkMode { get; private set; }

    /// <summary>
    /// Checks the Windows Registry for the current app theme and updates the color palette.
    /// </summary>
    public static void ApplySystemTheme()
    {
        IsDarkMode = GetSystemTheme() == 0; // 0 = Dark, 1 = Light

        if (IsDarkMode)
        {
            // Dark Mode Palette
            BackColor = Color.FromArgb(32, 32, 32);
            SurfaceColor = Color.FromArgb(45, 45, 48);
            TextColor = Color.FromArgb(243, 243, 243);
            SubTextColor = Color.FromArgb(170, 170, 170);
            BorderColor = Color.FromArgb(60, 60, 60);
            AccentColor = Color.FromArgb(0, 120, 215);
            HoverColor = Color.FromArgb(60, 60, 65);
        }
        else
        {
            // Light Mode Palette
            BackColor = Color.FromArgb(243, 243, 243);
            SurfaceColor = Color.FromArgb(255, 255, 255);
            TextColor = Color.FromArgb(32, 32, 32);
            SubTextColor = Color.FromArgb(100, 100, 100);
            BorderColor = Color.FromArgb(200, 200, 200);
            AccentColor = Color.FromArgb(0, 120, 215);
            HoverColor = Color.FromArgb(230, 230, 230);
        }
    }

    private static int GetSystemTheme()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is int val ? val : 0;
        }
        catch { return 0; } // Default to Dark Mode on error
    }
}

/// <summary>
/// Helper class for GDI+ drawing operations.
/// </summary>
public static class Gfx
{
    public static GraphicsPath GetRoundedPath(Rectangle rect, int radius)
    {
        GraphicsPath path = new();
        int d = radius * 2;
        path.AddArc(rect.X, rect.Y, d, d, 180, 90);
        path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
        path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}

// --- CUSTOM CONTROLS SECTION ---

public class ModernButton : Button
{
    public bool IsPrimary { get; set; }
    private bool _isHovered;

    public ModernButton()
    {
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        Cursor = Cursors.Hand;
        Size = new Size(100, 35);
        Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
        SetStyle(ControlStyles.Selectable | ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint, true);
    }

    protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); _isHovered = true; Invalidate(); }
    protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); _isHovered = false; Invalidate(); }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using (var b = new SolidBrush(Theme.BackColor)) e.Graphics.FillRectangle(b, ClientRectangle);

        var rect = new Rectangle(0, 0, Width - 1, Height - 1);
        var path = Gfx.GetRoundedPath(rect, Theme.Radius);

        Color bg = IsPrimary
        ? (_isHovered ? ControlPaint.Light(Theme.AccentColor) : Theme.AccentColor)
        : (_isHovered ? Theme.HoverColor : Theme.SurfaceColor);

        Color txtColor = IsPrimary ? Color.White : Theme.TextColor;

        using (var b = new SolidBrush(bg)) e.Graphics.FillPath(b, path);

        if (!IsPrimary)
        {
            using (var p = new Pen(Theme.BorderColor)) e.Graphics.DrawPath(p, path);
        }

        TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle, txtColor, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
    }
}

public class ModernTextBox : Panel
{
    public TextBox Inner;
    public ModernTextBox()
    {
        Padding = new Padding(10, 8, 10, 8);
        BackColor = Theme.SurfaceColor;
        Size = new Size(200, 36);
        Inner = new TextBox
        {
            BorderStyle = BorderStyle.None,
            BackColor = Theme.SurfaceColor,
            ForeColor = Theme.TextColor,
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 10)
        };
        Controls.Add(Inner);
    }

    public void UpdateTheme()
    {
        BackColor = Theme.SurfaceColor;
        Inner.BackColor = Theme.SurfaceColor;
        Inner.ForeColor = Theme.TextColor;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using (var b = new SolidBrush(Theme.BackColor)) e.Graphics.FillRectangle(b, ClientRectangle);
        var rect = new Rectangle(0, 0, Width - 1, Height - 1);
        using (var b = new SolidBrush(Theme.SurfaceColor)) e.Graphics.FillPath(b, Gfx.GetRoundedPath(rect, Theme.Radius));
        using (var p = new Pen(Theme.BorderColor)) e.Graphics.DrawPath(p, Gfx.GetRoundedPath(rect, Theme.Radius));
    }
}

public class ModernCheckBox : CheckBox
{
    public ModernCheckBox()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        Cursor = Cursors.Hand;
        Font = new Font("Segoe UI", 9.5f);
        ForeColor = Theme.TextColor;
        AutoSize = false;
        Size = new Size(300, 26);
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using (var b = new SolidBrush(Theme.BackColor)) e.Graphics.FillRectangle(b, ClientRectangle);

        var boxRect = new Rectangle(0, 5, 16, 16);
        var path = Gfx.GetRoundedPath(boxRect, 4);

        if (Checked)
        {
            using (var b = new SolidBrush(Theme.AccentColor)) e.Graphics.FillPath(b, path);
            using (var p = new Pen(Color.White, 2))
            {
                e.Graphics.DrawLine(p, 3, 12, 6, 15);
                e.Graphics.DrawLine(p, 6, 15, 13, 8);
            }
        }
        else
        {
            using (var b = new SolidBrush(Theme.SurfaceColor)) e.Graphics.FillPath(b, path);
            using (var p = new Pen(Theme.BorderColor)) e.Graphics.DrawPath(p, path);
        }

        var textRect = new Rectangle(24, 0, Width - 25, Height);
        TextRenderer.DrawText(e.Graphics, Text, Font, textRect, Theme.TextColor, TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPrefix);
    }
}

// --- MAIN APPLICATION FORM ---

public class MainForm : Form
{
    // Custom Message for IPC (Inter-Process Communication)
    public const int WM_USER = 0x0400;
    public const int WM_NEXT_WALLPAPER = WM_USER + 1;

    private NotifyIcon? _trayIcon;
    private System.Windows.Forms.Timer? _slideTimer;
    private Config _config = new();
    private string _configPath = "";
    private string _appDataFolder = "";

    private ModernTextBox? _txtPath;
    private ModernTextBox? _txtInterval;
    private ModernCheckBox? _chkRandom;
    private ModernCheckBox? _chkStartup;
    private PictureBox? _previewBox;
    private Panel? _headerPanel;
    private ModernButton? _btnContext;

    private Image? _appImage;
    private bool _forceHidden = false;

    public MainForm(bool startHidden)
    {
        _forceHidden = startHidden; // Determine startup visibility state

        // 1. Initialize Theme Engine
        Theme.ApplySystemTheme();

        InitPaths();
        LoadConfig();
        LoadResources();

        // 2. Form Properties
        Text = "Wallpaper Slideshow";
        Size = new Size(420, 680);
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;

        // 3. Apply Theme & UI
        ApplyFormTheme();

        // Native Dark Mode Title Bar Support (Win10/11)
        Application.SetHighDpiMode(HighDpiMode.SystemAware);
        UpdateDwmTitleBar();

        InitializeUI();
        InitializeTray();
        UpdateContextMenuButtonState();

        // 4. Timer Logic
        _slideTimer = new System.Windows.Forms.Timer();
        UpdateTimer();
        _slideTimer.Tick += OnSlideTimerTick;

        if (Directory.Exists(_config.WallpaperFolder))
        {
            _slideTimer.Start();
            LoadPreview();
        }

        // 5. System Events for Theme Change
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
    }

    private void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category == UserPreferenceCategory.General)
        {
            Theme.ApplySystemTheme();
            // Invoke on UI Thread
            if (InvokeRequired) Invoke(new Action(() => { ApplyFormTheme(); UpdateDwmTitleBar(); }));
            else { ApplyFormTheme(); UpdateDwmTitleBar(); }
        }
    }

    private void ApplyFormTheme()
    {
        BackColor = Theme.BackColor;
        ForeColor = Theme.TextColor;

        _headerPanel?.Invalidate();
        _previewBox?.Invalidate();

        foreach (Control c in Controls)
        {
            if (c is ModernTextBox mtb) mtb.UpdateTheme();
            else if (c is Label lbl) lbl.ForeColor = Theme.SubTextColor;
            else c.Invalidate();
        }
    }

    private void UpdateDwmTitleBar()
    {
        int useImmersiveDarkMode = Theme.IsDarkMode ? 1 : 0;
        DwmSetWindowAttribute(Handle, 20, ref useImmersiveDarkMode, sizeof(int));
    }

    private void LoadResources()
    {
        try
        {
            // Try loading embedded icon resource
            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = assembly.GetManifestResourceNames().FirstOrDefault(n => n.EndsWith("icon.png"));
            if (resourceName != null)
            {
                using var stream = assembly.GetManifestResourceStream(resourceName);
                if (stream != null) _appImage = Image.FromStream(stream);
            }
        }
        catch { }

        try
        {
            if (File.Exists("icon.ico")) Icon = new Icon("icon.ico");
            else Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        }
        catch { }
    }

    private void OnSlideTimerTick(object? sender, EventArgs e)
    {
        ChangeWallpaper();
    }

    private void InitPaths()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        _appDataFolder = Path.Combine(localAppData, "WallpaperSlideshow");
        _configPath = Path.Combine(_appDataFolder, "config.json");
        if (!Directory.Exists(_appDataFolder)) Directory.CreateDirectory(_appDataFolder);
    }

    [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    // Handle Inter-Process Communication (IPC) messages
    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_NEXT_WALLPAPER) ChangeWallpaper();
        base.WndProc(ref m);
    }

    // --- VISIBILITY LOGIC ---
    protected override void SetVisibleCore(bool value)
    {
        if (_forceHidden && value)
        {
            value = false;
            if (!IsHandleCreated) CreateHandle();
        }
        base.SetVisibleCore(value);
    }

    private void ShowApp()
    {
        _forceHidden = false; // Release the lock
        Show();
        WindowState = FormWindowState.Normal;
        ShowInTaskbar = true;
        Opacity = 1;
        Activate();
        BringToFront();
    }

    private void OpenUrl(string url)
    {
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); } catch { }
    }

    private void InitializeUI()
    {
        _headerPanel = new Panel { Dock = DockStyle.Top, Height = 100, Cursor = Cursors.Hand };
        _headerPanel.Paint += HeaderPanel_Paint;
        _headerPanel.Click += HeaderPanel_Click;
        Controls.Add(_headerPanel);

        int x = 25, y = 110, width = 355;

        _previewBox = new PictureBox
        {
            Location = new Point(x, y),
            Size = new Size(width, 200),
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = Theme.SurfaceColor
        };
        _previewBox.Paint += PreviewBox_Paint;
        Controls.Add(_previewBox);

        y += 220;
        CreateLabel("Wallpaper Folder", x, y);

        _txtPath = new ModernTextBox { Location = new Point(x, y + 22), Size = new Size(width - 50, 36) };
        _txtPath.Inner.Text = _config.WallpaperFolder;
        _txtPath.Inner.ReadOnly = true;
        Controls.Add(_txtPath);

        var btnBrowse = new ModernButton { Text = "...", Location = new Point(x + width - 45, y + 22), Size = new Size(45, 36) };
        btnBrowse.Click += BrowseFolder;
        Controls.Add(btnBrowse);

        y += 75;
        CreateLabel("Interval (Min)", x, y);
        CreateLabel("Order", x + 180, y);

        _txtInterval = new ModernTextBox { Location = new Point(x, y + 22), Size = new Size(160, 36) };
        _txtInterval.Inner.Text = _config.IntervalMinutes.ToString();
        _txtInterval.Inner.TextAlign = HorizontalAlignment.Center;
        _txtInterval.Inner.KeyPress += Interval_KeyPress;
        Controls.Add(_txtInterval);

        _chkRandom = new ModernCheckBox { Text = "Randomize", Location = new Point(x + 185, y + 30), Checked = _config.Randomize };
        Controls.Add(_chkRandom);

        y += 75;
        _chkStartup = new ModernCheckBox { Text = "Run at Windows Startup", Location = new Point(x, y), Checked = _config.RunAtStartup };
        Controls.Add(_chkStartup);

        y += 40;
        var btnNext = new ModernButton { Text = "Next Wall", Location = new Point(x, y), Size = new Size(170, 40) };
        btnNext.Click += BtnNext_Click;
        Controls.Add(btnNext);

        _btnContext = new ModernButton { Text = "Ctx Menu", Location = new Point(x + 185, y), Size = new Size(170, 40) };
        _btnContext.Click += ToggleContextMenu;
        Controls.Add(_btnContext);

        y += 55;
        var btnSave = new ModernButton { Text = "Save & Minimize", Location = new Point(x, y), Size = new Size(width, 45), IsPrimary = true };
        btnSave.Click += SaveAndHide;
        Controls.Add(btnSave);
    }

    private void HeaderPanel_Click(object? sender, EventArgs e) => OpenUrl("https://www.osmanonurkoc.com");

        private void PreviewBox_Paint(object? sender, PaintEventArgs e)
        {
            using var p = new Pen(Theme.BorderColor, 2);
            if (_previewBox != null)
                e.Graphics.DrawPath(p, Gfx.GetRoundedPath(_previewBox.ClientRectangle, Theme.Radius));
        }

        private void Interval_KeyPress(object? sender, KeyPressEventArgs e)
        {
            if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar)) e.Handled = true;
        }

        private void BtnNext_Click(object? sender, EventArgs e) => ChangeWallpaper();

        private void ToggleContextMenu(object? sender, EventArgs e)
        {
            try
            {
                string keyPath = @"Software\Classes\DesktopBackground\Shell\WallpaperSlideshowNext";
                using var key = Registry.CurrentUser.OpenSubKey(keyPath, true);

                if (key == null)
                {
                    using var newKey = Registry.CurrentUser.CreateSubKey(keyPath);
                    newKey.SetValue("", "Next Wallpaper");
                    newKey.SetValue("Icon", Application.ExecutablePath);
                    using var cmdKey = newKey.CreateSubKey("command");
                    cmdKey.SetValue("", $"\"{Application.ExecutablePath}\" --next");
                    MessageBox.Show("Added to Context Menu!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    Registry.CurrentUser.DeleteSubKeyTree(keyPath);
                    MessageBox.Show("Removed from Context Menu!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                UpdateContextMenuButtonState();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void UpdateContextMenuButtonState()
        {
            if (_btnContext == null) return;
            try
            {
                string keyPath = @"Software\Classes\DesktopBackground\Shell\WallpaperSlideshowNext";
                using var key = Registry.CurrentUser.OpenSubKey(keyPath, false);
                if (key != null)
                {
                    _btnContext.Text = "Remove from Ctx";
                    _btnContext.IsPrimary = false;
                }
                else
                {
                    _btnContext.Text = "Add to Ctx";
                    _btnContext.IsPrimary = true;
                }
                _btnContext.Invalidate();
            }
            catch { }
        }

        private void SaveAndHide(object? sender, EventArgs e)
        {
            SaveConfig();
            Hide();
            _trayIcon?.ShowBalloonTip(2000, "Wallpaper Slideshow", "Running in background.", ToolTipIcon.Info);
        }

        private void HeaderPanel_Paint(object? sender, PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.HighQuality;
            e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            e.Graphics.Clear(Theme.BackColor);

            int iconSize = 64;
            int spacing = 15;
            string title = "Wallpaper Slideshow";
            string subtitle = "@osmanonurkoc";
            var titleFont = new Font("Segoe UI", 14, FontStyle.Bold);
            var subFont = new Font("Segoe UI", 9, FontStyle.Regular);

            float totalTextWidth = Math.Max(e.Graphics.MeasureString(title, titleFont).Width, e.Graphics.MeasureString(subtitle, subFont).Width);
            float totalGroupWidth = iconSize + spacing + totalTextWidth;
            float startX = (_headerPanel!.Width - totalGroupWidth) / 2;
            float startY = (_headerPanel.Height - iconSize) / 2;

            if (_appImage != null)
            {
                e.Graphics.DrawImage(_appImage, new Rectangle((int)startX, (int)startY, iconSize, iconSize));
            }

            float textX = startX + iconSize + spacing;
            float textY = startY + (iconSize / 2) - 20;

            using (var b = new SolidBrush(Theme.TextColor)) e.Graphics.DrawString(title, titleFont, b, textX, textY);
            using (var b = new SolidBrush(Theme.SubTextColor)) e.Graphics.DrawString(subtitle, subFont, b, textX + 2, textY + 26);
        }

        private void CreateLabel(string text, int x, int y)
        {
            Controls.Add(new Label { Text = text, Location = new Point(x, y), AutoSize = true, ForeColor = Theme.SubTextColor, Font = new Font("Segoe UI", 9) });
        }

        private void BrowseFolder(object? sender, EventArgs e)
        {
            using var fbd = new FolderBrowserDialog();
            if (fbd.ShowDialog() == DialogResult.OK && _txtPath != null)
                _txtPath.Inner.Text = fbd.SelectedPath;
        }

        private void ChangeWallpaper()
        {
            if (!Directory.Exists(_config.WallpaperFolder)) return;
            try
            {
                var ext = new List<string> { ".jpg", ".jpeg", ".png", ".bmp" };
                var files = Directory.GetFiles(_config.WallpaperFolder, "*.*", SearchOption.AllDirectories)
                .Where(f => ext.Contains(Path.GetExtension(f).ToLower()))
                .ToList();

                if (files.Count == 0) return;

                int index;
                if (_config.Randomize)
                    index = new Random().Next(files.Count);
                else
                    index = (_config.LastIndex + 1) % files.Count;

                _config.LastIndex = index;

                SetWallpaper(files[index]);
                LoadPreview();
                SaveConfig();
                GC.Collect();
            }
            catch { }
        }

        // Windows API to set wallpaper
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        static extern int SystemParametersInfo(int uAction, int uParam, string lpvParam, int fuWinIni);
        private void SetWallpaper(string path)
        {
            // 0x0014 = SPI_SETDESKWALLPAPER
            // 0x01 | 0x02 = SPIF_UPDATEINIFILE | SPIF_SENDCHANGE
            SystemParametersInfo(0x0014, 0, path, 0x01 | 0x02);
        }

        private void LoadPreview()
        {
            try
            {
                if (Directory.Exists(_config.WallpaperFolder) && _previewBox != null)
                {
                    var ext = new List<string> { ".jpg", ".jpeg", ".png", ".bmp" };
                    var files = Directory.GetFiles(_config.WallpaperFolder, "*.*", SearchOption.AllDirectories)
                    .Where(f => ext.Contains(Path.GetExtension(f).ToLower()))
                    .ToList();

                    if (files.Count > 0 && _config.LastIndex < files.Count && _config.LastIndex >= 0)
                    {
                        using var temp = Image.FromFile(files[_config.LastIndex]);
                        _previewBox.Image = new Bitmap(temp);
                    }
                }
            }
            catch { }
        }

        private void SaveConfig()
        {
            if (_txtPath != null) _config.WallpaperFolder = _txtPath.Inner.Text;
            if (_txtInterval != null && int.TryParse(_txtInterval.Inner.Text, out int interval))
                _config.IntervalMinutes = Math.Max(1, interval);
            if (_chkRandom != null) _config.Randomize = _chkRandom.Checked;
            if (_chkStartup != null) _config.RunAtStartup = _chkStartup.Checked;

            try
            {
                string json = JsonSerializer.Serialize(_config, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_configPath, json);
                SetStartup(_config.RunAtStartup);
                UpdateTimer();
            }
            catch { }
        }

        private void InitializeTray()
        {
            var menu = new ContextMenuStrip();
            menu.Items.Add("Settings", null, (s, e) => ShowApp());
            menu.Items.Add("Next Wallpaper", null, (s, e) => ChangeWallpaper());
            menu.Items.Add("-");
            menu.Items.Add("Exit", null, (s, e) => {
                if (_trayIcon != null) _trayIcon.Visible = false;
                SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
                Application.Exit();
            });

            _trayIcon = new NotifyIcon { Text = "Wallpaper Slideshow", Icon = Icon, Visible = true, ContextMenuStrip = menu };
            _trayIcon.DoubleClick += (s, e) => ShowApp();
        }

        private void SetStartup(bool enable)
        {
            try
            {
                using var rk = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);
                if (enable) rk?.SetValue("WallpaperSlideshow", $"\"{Application.ExecutablePath}\" --startup");
                else rk?.DeleteValue("WallpaperSlideshow", false);
            }
            catch { }
        }

        private void UpdateTimer()
        {
            if (_slideTimer != null)
                _slideTimer.Interval = _config.IntervalMinutes * 60 * 1000;
        }

        private void LoadConfig()
        {
            if (File.Exists(_configPath))
            {
                try
                {
                    string content = File.ReadAllText(_configPath);
                    var loaded = JsonSerializer.Deserialize<Config>(content);
                    if (loaded != null)
                    {
                        _config = loaded;
                        _config.WallpaperFolder = _config.WallpaperFolder.Replace("\\\\", "\\");
                    }
                }
                catch { }
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; Hide(); }
            base.OnFormClosing(e);
        }
}

static class Program
{
    public static bool StartHidden = false;
    private static Mutex? _mutex = null;

    [DllImport("user32.dll", SetLastError = true)]
    static extern IntPtr FindWindow(string? lpClassName, string lpWindowName);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    static extern IntPtr SendMessage(IntPtr hWnd, UInt32 Msg, IntPtr wParam, IntPtr lParam);

    [STAThread]
    static void Main(string[] args)
    {
        AppDomain.CurrentDomain.UnhandledException += (s, e) => {
            try { File.WriteAllText(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "wallpaper_crash.txt"), e.ExceptionObject.ToString()); } catch { }
        };

        bool nextOnly = false;
        foreach (var arg in args)
        {
            if (arg == "--startup") StartHidden = true;
            if (arg == "--next") nextOnly = true;
        }

        // Global Mutex to prevent multiple instances
        _mutex = new Mutex(true, "Global\\OSMANONURKOC_WALLPAPER_IPC", out bool createdNew);

        if (!createdNew)
        {
            // If another instance exists and "--next" was passed, send a signal to change wallpaper
            if (nextOnly)
            {
                IntPtr hWnd = FindWindow(null, "Wallpaper Slideshow");
                if (hWnd != IntPtr.Zero)
                {
                    SendMessage(hWnd, MainForm.WM_NEXT_WALLPAPER, IntPtr.Zero, IntPtr.Zero);
                }
            }
            return; // Exit this instance
        }

        // If explicitly triggered via context menu but no instance was running,
        // we start the app hidden to handle the request silently.
        if (nextOnly) StartHidden = true;

        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm(StartHidden));
    }
}
