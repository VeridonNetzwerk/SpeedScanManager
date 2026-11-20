using System.Drawing;
using System.Reflection;
using System.Windows.Forms;

namespace SpeedScanManager;

/// <summary>
/// Version information (tray menu "Versionsinformationen"): logo, version, license and credits,
/// GitHub/Discord links and "Detail..." (scanner and driver information). Modern layout: cards
/// in the body, help in the header, OK in the footer.
/// </summary>
internal class VersionInfoDialog : ModernWindow
{
    private readonly ScannerState? _scannerState;
    private readonly Bitmap _githubSource;
    private readonly PictureBox _pbGithub;

    public VersionInfoDialog(ScannerState? scannerState = null)
    {
        _scannerState = scannerState;
        SuspendLayout();
        Text = "SpeedScan Manager – Versionsinformationen";
        Title = "Versionsinformationen";
        Subtitle = "SpeedScan Manager";
        HeaderButtons = ModernHeaderButtons.Help | ModernHeaderButtons.Close;
        HelpTopic = "version-info";
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = false;
        AutoSizeToContent = true;
        MinimumContentWidth = 460;

        // === Version ===
        var asm = Assembly.GetExecutingAssembly();
        var exePath = Environment.ProcessPath ?? AppContext.BaseDirectory;
        var version = !string.IsNullOrEmpty(exePath)
            ? (System.Diagnostics.FileVersionInfo.GetVersionInfo(exePath).FileVersion ?? asm.GetName().Version?.ToString() ?? "0.1.0.0")
            : (asm.GetName().Version?.ToString() ?? "0.1.0.0");

        // Trim trailing .0 segments for display (0.1.0.0 -> 0.1.0), but keep at least 3 segments
        var displayVersion = version;
        var parts = displayVersion.Split('.');
        while (parts.Length > 3 && parts[^1] == "0")
        {
            displayVersion = string.Join('.', parts[..^1]);
            parts = displayVersion.Split('.');
        }

        // === Logo line: logo icon + text wordmark, version below the wordmark ===
        var logoBmp = AppResources.Logo;
        var textBmp = AppResources.TextLogo;
        int logoH = 52;
        int logoW = (int)(logoBmp.Width * (logoH / (double)logoBmp.Height));
        int textH = 26;
        int textW = (int)(textBmp.Width * (textH / (double)textBmp.Height));

        var hero = new Panel { Height = 84, Margin = Padding.Empty };
        var pbLogo = new PictureBox
        {
            Image = logoBmp,
            SizeMode = PictureBoxSizeMode.Zoom,
            Location = new Point(0, 16),
            Size = new Size(logoW, logoH)
        };
        var pbText = new PictureBox
        {
            Image = textBmp,
            SizeMode = PictureBoxSizeMode.Zoom,
            Location = new Point(logoW + 14, 16),
            Size = new Size(textW, textH)
        };
        var lblVersionTitle = new Label
        {
            Text = $"SpeedScan Manager Version {displayVersion}",
            Font = ModernFonts.RowTitle,
            Location = new Point(logoW + 14, 46),
            AutoSize = true,
            UseMnemonic = false
        };
        ThemeManager.SetRole(lblVersionTitle, ThemeRole.Muted);
        hero.Controls.AddRange(new Control[] { pbLogo, pbText, lblVersionTitle });

        var cardHero = new SettingsCard { ShowDividers = false };
        cardHero.Add(hero);

        // === License and credits ===
        var license = new WrapText(
            "Open-Source TWAIN-Scanning-Software.\n\n" +
            "Lizenziert unter GNU General Public License v3 (GPL-3.0).\n" +
            "Dieses Programm ist freie Software: Sie können es weitergeben und/oder " +
            "modifizieren unter Beachtung der GPL-3.0-Bedingungen.\n" +
            "Credits müssen in allen Kopien und abgeleiteten Werken erhalten bleiben.\n\n" +
            "TWAIN-Unterstützung durch NTwain.\n" +
            "OCR-Engine: Tesseract.NET.\n" +
            "PDF-Verarbeitung: PdfSharpCore.\n\n" +
            "Copyright © 2026 VeridonNetzwerk");

        // === GitHub + Discord link icons ===
        _githubSource = CreateGithubIcon();
        var pbGithub = _pbGithub = new PictureBox
        {
            Image = TintGithubIcon(),
            SizeMode = PictureBoxSizeMode.Zoom,
            Size = new Size(28, 28),
            Location = new Point(0, 0),
            Cursor = Cursors.Hand,
            AccessibleName = "GitHub"
        };
        pbGithub.Click += (s, e) =>
        {
            try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "https://github.com/VeridonNetzwerk/SpeedScanManager",
                UseShellExecute = true
            }); } catch { }
        };

        var pbDiscord = new PictureBox
        {
            Image = CreateDiscordIcon(),
            SizeMode = PictureBoxSizeMode.Zoom,
            Size = new Size(28, 28),
            Location = new Point(40, 0),
            Cursor = Cursors.Hand,
            AccessibleName = "Discord"
        };
        pbDiscord.Click += (s, e) =>
        {
            try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "https://discord.gg/CmCXZhAr59",
                UseShellExecute = true
            }); } catch { }
        };

        var links = new Panel { Size = new Size(68, 28), Margin = Padding.Empty };
        links.Controls.Add(pbGithub);
        links.Controls.Add(pbDiscord);

        var cardLicense = new SettingsCard("Lizenz und Credits");
        cardLicense.Add(license);
        cardLicense.AddRow("Projekt und Community", "Quellcode auf GitHub, Austausch auf Discord.", links, 68);

        // === Detail button: scanner and driver information ===
        var btnDetail = new Button
        {
            Text = "Detail...",
            Size = new Size(120, 32)
        };
        ThemeManager.SetGlyph(btnDetail, Glyph.Scanner);
        btnDetail.Click += (s, e) =>
        {
            using var dlg = new ScannerDriverInfoDialog(_scannerState);
            dlg.ShowDialog(this);
        };
        var cardDetail = new SettingsCard();
        cardDetail.AddRow("Scanner- und Treiberinformationen", "Scanner, Treiber und Dateiversionen.", btnDetail);

        var stack = new ModernStack { Dock = DockStyle.Top, Padding = new Padding(20, 18, 20, 20) };
        stack.Controls.Add(cardHero);
        stack.Controls.Add(cardLicense);
        stack.Controls.Add(cardDetail);
        Body.Controls.Add(stack);

        // === Footer (OK; help is the header's "?" button) ===
        var btnOk = AddFooterButton("OK", ThemeRole.Primary, Glyph.Check, DialogResult.OK);
        AcceptButton = btnOk;

        ThemeManager.Apply(this);
        ThemeManager.ThemeChanged += OnThemeChanged;
        ResumeLayout(true);
    }

    /// <summary>The GitHub mark is near-black: recolor it to the theme text color (visible in dark mode).</summary>
    private Bitmap TintGithubIcon()
    {
        var color = ThemeManager.Current.Text;
        var bmp = new Bitmap(_githubSource.Width, _githubSource.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        for (int y = 0; y < bmp.Height; y++)
            for (int x = 0; x < bmp.Width; x++)
                bmp.SetPixel(x, y, Color.FromArgb(_githubSource.GetPixel(x, y).A, color));
        return bmp;
    }

    private void OnThemeChanged(object? sender, EventArgs e)
    {
        var old = _pbGithub.Image;
        _pbGithub.Image = TintGithubIcon();
        old?.Dispose();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            ThemeManager.ThemeChanged -= OnThemeChanged;
            _pbGithub.Image?.Dispose();
            _githubSource.Dispose();
        }
        base.Dispose(disposing);
    }

    private static Bitmap CreateGithubIcon()
    {
        return LoadSvgResource("github_logo.svg", 28, 28);
    }

    private static Bitmap CreateDiscordIcon()
    {
        return LoadSvgResource("discord_logo.svg", 28, 28);
    }

    private static Bitmap LoadSvgResource(string resourceName, int width, int height)
    {
        try
        {
            var asm = Assembly.GetExecutingAssembly();
            using var stream = asm.GetManifestResourceStream(resourceName);
            if (stream != null)
            {
                var doc = Svg.SvgDocument.Open<Svg.SvgDocument>(stream);
                doc.Width = width;
                doc.Height = height;
                return doc.Draw(width, height);
            }
        }
        catch { }
        return new Bitmap(width, height);
    }

    /// <summary>Word-wrapped paragraph text whose height follows the available width.</summary>
    private sealed class WrapText : Control, IHeightForWidth, IThemeAware
    {
        private const TextFormatFlags Flags = TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding;

        public WrapText(string text)
        {
            Text = text;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                     | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            SetStyle(ControlStyles.Selectable, false);
            AccessibleRole = AccessibleRole.StaticText;
        }

        public void ApplyTheme(ThemePalette palette) => Invalidate();

        private int Pad => ModernLayout.Scale(this, 12);

        public int GetHeightForWidth(int width)
            => TextRenderer.MeasureText(Text, ModernFonts.RowTitle, new Size(Math.Max(1, width), 0), Flags).Height + 2 * Pad;

        public override Size GetPreferredSize(Size proposedSize)
        {
            int w = proposedSize.Width > 0 && proposedSize.Width < int.MaxValue / 2 ? proposedSize.Width : Width;
            return new Size(w, GetHeightForWidth(w));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var p = ThemeManager.Current;
            e.Graphics.Clear(ModernLayout.ParentBack(this));
            TextRenderer.DrawText(e.Graphics, Text, ModernFonts.RowTitle,
                new Rectangle(0, Pad, Width, Height - 2 * Pad), p.Text, Flags);
        }
    }
}
