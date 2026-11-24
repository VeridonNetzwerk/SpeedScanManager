using System.ComponentModel;
using System.Diagnostics;
using System.Windows.Forms;

namespace SpeedScanManager;

/// <summary>
/// Update notice like in the Modrinth App: checked in the background (shortly after start, then
/// every 6 hours), a green "Update" pill in the settings window and an entry at the top of the
/// tray menu; a notification only if the update was ignored for 24 hours (once per version).
/// Installing downloads the setup, starts it in update mode and exits the app.
/// </summary>
internal partial class TrayApplicationContext
{
    private static readonly TimeSpan UpdateFirstCheck = TimeSpan.FromSeconds(45);
    private static readonly TimeSpan UpdateInterval = TimeSpan.FromHours(6);
    private static readonly TimeSpan UpdateReminderAfter = TimeSpan.FromHours(24);

    private System.Windows.Forms.Timer? _updateTimer;
    private UpdateInfo? _update;
    private bool _updateChecking;
    private ToolStripMenuItem? _miUpdate;
    private ToolStripMenuItem? _miCheckUpdates;
    private bool _balloonOpensUpdate;

    private void InitUpdates()
    {
        _miUpdate = new ToolStripMenuItem(L.T("Update installieren…")) { Visible = false };
        ThemeManager.SetMenuItemGlyph(_miUpdate, Glyph.Download);
        ThemeManager.SetMenuItemRole(_miUpdate, ThemeRole.Success);
        _miUpdate.Click += (_, _) => ShowUpdateDialog();
        int scanIndex = _contextMenu.Items.IndexOf(_miScan);
        _contextMenu.Items.Insert(scanIndex, _miUpdate);

        _miCheckUpdates = new ToolStripMenuItem(L.T("Nach Updates suchen"));
        ThemeManager.SetMenuItemGlyph(_miCheckUpdates, Glyph.Download);
        _miCheckUpdates.Click += async (_, _) => await CheckForUpdateAsync(manual: true);
        int versionIndex = _miHelp.DropDownItems.Count - 1; // above "Versionsinformationen"
        _miHelp.DropDownItems.Insert(versionIndex, _miCheckUpdates);
        ThemeManager.Apply(_contextMenu);

        _notifyIcon.BalloonTipClicked += (_, _) =>
        {
            if (_balloonOpensUpdate && _update != null) ShowUpdateDialog();
        };
        _notifyIcon.BalloonTipClosed += (_, _) => _balloonOpensUpdate = false;

        _updateTimer = new System.Windows.Forms.Timer { Interval = (int)UpdateFirstCheck.TotalMilliseconds };
        _updateTimer.Tick += async (_, _) =>
        {
            _updateTimer!.Interval = (int)UpdateInterval.TotalMilliseconds;
            if (_appSettings.CheckForUpdates)
                await CheckForUpdateAsync(manual: false);
            else
                RemindUpdateIfDue();
        };
        _updateTimer.Start();
    }

    private void DisposeUpdates()
    {
        _updateTimer?.Stop();
        _updateTimer?.Dispose();
        _updateTimer = null;
    }

    /// <summary>"Nach Updates suchen" switched in the settings window.</summary>
    private void OnUpdateSettingApplied()
    {
        if (!_appSettings.CheckForUpdates)
        {
            if (_update != null) SetAvailableUpdate(null);
        }
        else if (_update == null)
        {
            _ = CheckForUpdateAsync(manual: false);
        }
    }

    private async Task CheckForUpdateAsync(bool manual)
    {
        if (_updateChecking || _disposed) return;
        _updateChecking = true;
        try
        {
            var info = await UpdateService.CheckAsync();
            if (_disposed) return;
            LogDiag(info == null ? "Update check: up to date" : $"Update check: {UpdateService.Format(info.Version)} available");
            SetAvailableUpdate(info);
            if (manual)
            {
                if (info != null)
                    ShowUpdateDialog();
                else
                    TrayMessageBox.Info(DialogOwner, L.F("SpeedScan Manager ist aktuell (Version {0}).", UpdateService.CurrentVersionText));
            }
            else
            {
                RemindUpdateIfDue();
            }
        }
        catch (Exception ex)
        {
            LogDiag($"Update check failed: {ex.Message}");
            if (manual && !_disposed)
                TrayMessageBox.Warning(DialogOwner, L.T("Die Suche nach Updates ist fehlgeschlagen. Bitte Internetverbindung prüfen."));
        }
        finally
        {
            _updateChecking = false;
        }
    }

    private void SetAvailableUpdate(UpdateInfo? info)
    {
        _update = info;
        if (info != null)
        {
            var text = UpdateService.Format(info.Version);
            if (_appSettings.UpdateSeenVersion != text)
            {
                _appSettings.UpdateSeenVersion = text;
                _appSettings.UpdateSeenUtc = DateTime.UtcNow;
                _appSettings.Save();
            }
            if (_miUpdate != null)
            {
                _miUpdate.Text = L.F("Update auf {0} installieren…", text);
                _miUpdate.Visible = true;
            }
        }
        else if (_miUpdate != null)
        {
            _miUpdate.Visible = false;
        }
        UpdateMainFormUpdatePill();
    }

    /// <summary>Notification once per version, when the update was ignored for 24 hours.</summary>
    private void RemindUpdateIfDue()
    {
        if (_update == null || _busy) return;
        var text = UpdateService.Format(_update.Version);
        if (_appSettings.UpdateRemindedVersion == text) return;
        if (_appSettings.UpdateSeenUtc is not { } seen || DateTime.UtcNow - seen < UpdateReminderAfter) return;
        _appSettings.UpdateRemindedVersion = text;
        _appSettings.Save();
        _balloonOpensUpdate = true;
        ShowBalloon(L.T("Update verfügbar"), L.F("SpeedScan Manager {0} ist da. Zum Installieren hier klicken.", text), ToolTipIcon.Info);
    }

    /// <summary>The green pill in the settings window header.</summary>
    private void UpdateMainFormUpdatePill()
    {
        if (_mainForm == null || _mainForm.IsDisposed) return;
        if (_update == null)
            _mainForm.SetHeaderAction(null, Glyph.None, "", null);
        else
            _mainForm.SetHeaderAction($"Update {UpdateService.Format(_update.Version)}", Glyph.Download,
                L.T("Eine neue Version von SpeedScan Manager ist verfügbar"), ShowUpdateDialog);
    }

    private string? UpdateBlockedReason() =>
        _busy ? L.T("Während eines Scans kann nicht aktualisiert werden. Danach geht es weiter.") : null;

    private void ShowUpdateDialog()
    {
        if (_update == null) return;
        var dlg = new UpdateDialog(_update, UpdateBlockedReason);
        dlg.SetupReady += path => StartSetup(dlg, path);
        OwnedWindows.Show(dlg, DialogOwner, key: "update");
    }

    /// <summary>Busy state changed: the update window enables/disables "Jetzt installieren".</summary>
    private void RefreshUpdateDialog()
    {
        if (OwnedWindows.Find("update") is UpdateDialog dlg)
            dlg.UpdateBlocked();
        if (!_busy) RemindUpdateIfDue();
    }

    private void StartSetup(UpdateDialog dlg, string setupPath)
    {
        if (_busy)
        {
            dlg.SetupNotStarted(UpdateBlockedReason()!);
            return;
        }
        // Unsaved settings: ask first (Speichern / Verwerfen / Abbrechen = no update now)
        if (_mainForm is { IsDisposed: false, Visible: true })
        {
            _mainForm.Close();
            if (_mainForm is { IsDisposed: false, Visible: true })
            {
                dlg.SetupNotStarted(L.T("Update abgebrochen: das Einstellungsfenster ist noch offen."));
                return;
            }
        }
        try
        {
            // The setup asks for administrator rights (UAC); it closes this app itself and starts
            // it again when it's done.
            Process.Start(new ProcessStartInfo(setupPath, "--update") { UseShellExecute = true });
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            dlg.SetupNotStarted(L.T("Das Update braucht Administratorrechte. Bitte erneut versuchen und mit „Ja“ bestätigen."));
            return;
        }
        catch (Exception ex)
        {
            LogDiag($"Starting the setup failed: {ex.Message}");
            dlg.SetupNotStarted(L.T("Das Setup konnte nicht gestartet werden."));
            return;
        }
        LogDiag($"Update: setup started ({setupPath}), exiting");
        try { _mainForm?.CloseDiscarding(); } catch { }
        DoExit();
    }
}
