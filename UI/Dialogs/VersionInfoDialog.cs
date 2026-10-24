using System.Drawing;
using System.Reflection;
using System.Windows.Forms;

namespace SpeedScanManager;

internal class VersionInfoDialog : Form
{
    private readonly ScannerState? _scannerState;
    private readonly Bitmap _githubSource;
    private readonly PictureBox _pbGithub;

    public VersionInfoDialog(ScannerState? scannerState = null)
    {
        _scannerState = scannerState;
        Text = "SpeedScan Manager \u2013 Versionsinformationen";
        Icon = TrayIcons.GetAppIcon();
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = false;
        AutoScaleMode = AutoScaleMode.None;
        ClientSize = new Size(390, 390);
        Font = new Font("Microsoft Sans Serif", 8.25f);

        var font = new Font("Microsoft Sans Serif", 8.25f);

        // === Logo line: logo icon + text wordmark ===
        var logoBmp = AppResources.Logo;
        var textBmp = AppResources.TextLogo;
        int logoH = 36;
        int logoW = (int)(logoBmp.Width * (logoH / (double)logoBmp.Height));
        int textH = 28;
        int textW = (int)(textBmp.Width * (textH / (double)textBmp.Height));

        var pbLogo = new PictureBox
        {
            Image = logoBmp,
            SizeMode = PictureBoxSizeMode.Zoom,
            Location = new Point(24, 12),
            Size = new Size(logoW, logoH)
        };
        var pbText = new PictureBox
        {
            Image = textBmp,
            SizeMode = PictureBoxSizeMode.Zoom,
            Location = new Point(24 + logoW + 6, 16),
            Size = new Size(textW, textH)
        };

        // === Version title line ===
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

        var lblVersionTitle = new Label
        {
            Text = $"SpeedScan Manager Version {displayVersion}",
            Font = new Font("Microsoft Sans Serif", 9f, FontStyle.Bold),
            Location = new Point(24, 58),
            AutoSize = true
        };

        // === Info text block ===
        var lblLicense = new Label
        {
            Text = "SpeedScan Manager\n\n" +
                   "Open-Source TWAIN-Scanning-Software.\n\n" +
                   "Lizenziert unter GNU General Public License v3 (GPL-3.0).\n" +
                   "Dieses Programm ist freie Software: Sie können es weitergeben und/oder " +
                   "modifizieren unter Beachtung der GPL-3.0-Bedingungen.\n" +
                   "Credits müssen in allen Kopien und abgeleiteten Werken erhalten bleiben.\n\n" +
                   "TWAIN-Unterstützung durch NTwain.\n" +
                   "OCR-Engine: Tesseract.NET.\n" +
                   "PDF-Verarbeitung: PdfSharpCore.\n\n" +
                   "Copyright © 2026 VeridonNetzwerk",
            Font = font,
            Location = new Point(20, 90),
            Size = new Size(350, 256),
            Padding = new Padding(8, 6, 8, 6),
            AutoSize = false
        };
        // Former white info box: secondary surface in both themes.
        ThemeManager.SetRole(lblLicense, ThemeRole.Secondary);

        // === Detail button (right of text block) ===
        var btnDetail = new Button
        {
            Text = "Detail...",
            Location = new Point(305, 56),
            Size = new Size(65, 22),
            Font = font,
            FlatStyle = FlatStyle.Standard,
            UseVisualStyleBackColor = true
        };
        btnDetail.Click += (s, e) =>
        {
            using var dlg = new ScannerDriverInfoDialog(_scannerState);
            dlg.ShowDialog(this);
        };

        // === GitHub + Discord link icons (inside white box, below text) ===
        _githubSource = CreateGithubIcon();
        var pbGithub = _pbGithub = new PictureBox
        {
            Image = TintGithubIcon(),
            SizeMode = PictureBoxSizeMode.AutoSize,
            Location = new Point(350 - 8 - 28 - 8 - 28, 256 - 8 - 28),
            Cursor = Cursors.Hand
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
            SizeMode = PictureBoxSizeMode.AutoSize,
            Location = new Point(350 - 8 - 28, 256 - 8 - 28),
            Cursor = Cursors.Hand
        };
        pbDiscord.Click += (s, e) =>
        {
            try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "https://discord.gg/CmCXZhAr59",
                UseShellExecute = true
            }); } catch { }
        };

        // === Footer (OK + Hilfe) ===
        var footerTable = new TableLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 36,
            ColumnCount = 5,
            ColumnStyles =
            {
                new ColumnStyle(SizeType.Percent, 100f),
                new ColumnStyle(SizeType.Absolute, 75f),
                new ColumnStyle(SizeType.Absolute, 12f),
                new ColumnStyle(SizeType.Absolute, 75f),
                new ColumnStyle(SizeType.Percent, 100f)
            },
            RowCount = 1,
            RowStyles = { new RowStyle(SizeType.Percent, 100f) }
        };
        ThemeManager.SetRole(footerTable, ThemeRole.Secondary);

        var btnOk = new Button
        {
            Text = "OK",
            DialogResult = DialogResult.OK,
            Size = new Size(75, 24),
            Font = font,
            FlatStyle = FlatStyle.Standard,
            UseVisualStyleBackColor = true,
            Anchor = AnchorStyles.None
        };

        var btnHelp = new Button
        {
            Text = "Hilfe",
            Size = new Size(75, 24),
            Font = font,
            FlatStyle = FlatStyle.Standard,
            UseVisualStyleBackColor = true,
            Anchor = AnchorStyles.None
        };
        btnHelp.Click += (s, e) =>
        {
            using var help = new HelpForm("version-info");
            help.ShowDialog(this);
        };

        footerTable.Controls.Add(new Panel(), 0, 0);
        footerTable.Controls.Add(btnOk, 1, 0);
        footerTable.Controls.Add(new Panel(), 2, 0);
        footerTable.Controls.Add(btnHelp, 3, 0);
        footerTable.Controls.Add(new Panel(), 4, 0);

        lblLicense.Controls.Add(pbDiscord);
        lblLicense.Controls.Add(pbGithub);

        Controls.Add(footerTable);
        Controls.Add(lblLicense);
        Controls.Add(btnDetail);
        Controls.Add(lblVersionTitle);
        Controls.Add(pbText);
        Controls.Add(pbLogo);

        AcceptButton = btnOk;

        ThemeManager.Apply(this);
        ThemeManager.ThemeChanged += OnThemeChanged;
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
}
