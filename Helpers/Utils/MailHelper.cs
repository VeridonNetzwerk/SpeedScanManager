using System.Runtime.InteropServices;

namespace SpeedScanManager;

/// <summary>
/// Opens a new message in the default mail client (Simple MAPI) with subject, optional
/// recipient and the scanned files attached. Nothing is sent automatically — the user
/// sends the message from the mail client.
///
/// Uses the Unicode entry point MAPISendMailW with manually marshalled structures
/// (MapiMessageW, MapiRecipDescW, MapiFileDescW). No separate MAPILogon is needed:
/// MAPI_LOGON_UI lets the client log on itself. The call blocks until the compose window
/// is closed by some clients (e.g. Outlook), so callers should run it on its own STA
/// thread (see <see cref="SendMailAsync"/>).
/// </summary>
internal static class MailHelper
{
    [DllImport("mapi32.dll", EntryPoint = "MAPISendMailW", ExactSpelling = true)]
    private static extern uint MAPISendMailW(IntPtr lhSession, IntPtr ulUIParam, IntPtr lpMessage, uint flFlags, uint ulReserved);

    private const uint MAPI_LOGON_UI = 0x00000001;
    private const uint MAPI_DIALOG = 0x00000008;
    private const uint MAPI_TO = 1;

    private const uint SUCCESS_SUCCESS = 0;
    private const uint MAPI_USER_ABORT = 1;

    // Layouts follow mapi.h (MapiMessageW etc.). Strings are LPWSTR (IntPtr here, allocated
    // with StringToHGlobalUni) so the structures are blittable and can be copied with
    // Marshal.StructureToPtr into unmanaged arrays.
    [StructLayout(LayoutKind.Sequential)]
    internal struct MapiMessageW
    {
        public uint ulReserved;
        public IntPtr lpszSubject;
        public IntPtr lpszNoteText;
        public IntPtr lpszMessageType;
        public IntPtr lpszDateReceived;
        public IntPtr lpszConversationID;
        public uint flFlags;
        public IntPtr lpOriginator;
        public uint nRecipCount;
        public IntPtr lpRecips;
        public uint nFileCount;
        public IntPtr lpFiles;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct MapiRecipDescW
    {
        public uint ulReserved;
        public uint ulRecipClass;
        public IntPtr lpszName;
        public IntPtr lpszAddress;
        public uint ulEIDSize;
        public IntPtr lpEntryID;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct MapiFileDescW
    {
        public uint ulReserved;
        public uint flFlags;
        public uint nPosition;
        public IntPtr lpszPathName;
        public IntPtr lpszFileName;
        public IntPtr lpFileType;
    }

    /// <summary>Result of a mail attempt.</summary>
    public sealed record MailResult(bool Success, bool UserAborted, string? ErrorMessage);

    /// <summary>
    /// Runs <see cref="OpenMailWithAttachments"/> on a dedicated STA thread so a blocking
    /// MAPI client doesn't freeze the tray (and its TWAIN message loop).
    /// </summary>
    public static Task<MailResult> SendMailAsync(IntPtr ownerHandle, string subject, string recipient, IReadOnlyList<string> filePaths)
    {
        var tcs = new TaskCompletionSource<MailResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try { tcs.SetResult(OpenMailWithAttachments(ownerHandle, subject, recipient, filePaths)); }
            catch (Exception ex) { tcs.SetResult(new MailResult(false, false, ex.Message)); }
        })
        {
            IsBackground = true,
            Name = "MAPI"
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return tcs.Task;
    }

    /// <summary>
    /// Opens the default mail client with the given subject, optional recipient and attachments.
    /// Returns a result instead of throwing; there is no silent fallback without attachment.
    /// </summary>
    public static MailResult OpenMailWithAttachments(IntPtr ownerHandle, string subject, string recipient, IReadOnlyList<string> filePaths)
    {
        if (filePaths.Count == 0)
            return new MailResult(false, false, "Es gibt keine Datei, die angehängt werden kann.");

        var strings = new List<IntPtr>();
        IntPtr filesPtr = IntPtr.Zero, recipPtr = IntPtr.Zero, msgPtr = IntPtr.Zero;

        IntPtr Str(string? s)
        {
            if (s == null) return IntPtr.Zero;
            var p = Marshal.StringToHGlobalUni(s);
            strings.Add(p);
            return p;
        }

        try
        {
            int fileSize = Marshal.SizeOf<MapiFileDescW>();
            filesPtr = Marshal.AllocHGlobal(fileSize * filePaths.Count);
            for (int i = 0; i < filePaths.Count; i++)
            {
                var desc = new MapiFileDescW
                {
                    ulReserved = 0,
                    flFlags = 0,
                    nPosition = uint.MaxValue, // (ULONG)-1: client decides where the attachment goes
                    lpszPathName = Str(filePaths[i]),
                    lpszFileName = Str(Path.GetFileName(filePaths[i])),
                    lpFileType = IntPtr.Zero
                };
                Marshal.StructureToPtr(desc, filesPtr + i * fileSize, false);
            }

            uint recipCount = 0;
            if (!string.IsNullOrWhiteSpace(recipient))
            {
                var addr = recipient.Trim();
                var recip = new MapiRecipDescW
                {
                    ulRecipClass = MAPI_TO,
                    lpszName = Str(addr),
                    lpszAddress = Str(addr.StartsWith("SMTP:", StringComparison.OrdinalIgnoreCase) ? addr : "SMTP:" + addr)
                };
                recipPtr = Marshal.AllocHGlobal(Marshal.SizeOf<MapiRecipDescW>());
                Marshal.StructureToPtr(recip, recipPtr, false);
                recipCount = 1;
            }

            var msg = new MapiMessageW
            {
                lpszSubject = Str(subject ?? ""),
                lpszNoteText = Str(""),
                nRecipCount = recipCount,
                lpRecips = recipPtr,
                nFileCount = (uint)filePaths.Count,
                lpFiles = filesPtr
            };
            msgPtr = Marshal.AllocHGlobal(Marshal.SizeOf<MapiMessageW>());
            Marshal.StructureToPtr(msg, msgPtr, false);

            uint rc = MAPISendMailW(IntPtr.Zero, ownerHandle, msgPtr, MAPI_DIALOG | MAPI_LOGON_UI, 0);
            DiagLog.Write($"MailHelper: MAPISendMailW rc={rc}");

            return rc switch
            {
                SUCCESS_SUCCESS => new MailResult(true, false, null),
                MAPI_USER_ABORT => new MailResult(false, true, null),
                _ => new MailResult(false, false, DescribeError(rc))
            };
        }
        catch (DllNotFoundException)
        {
            return new MailResult(false, false, "Es ist kein MAPI-fähiges E-Mail-Programm installiert (mapi32.dll fehlt).");
        }
        catch (EntryPointNotFoundException)
        {
            return new MailResult(false, false, "Das Standard-E-Mail-Programm unterstützt kein Simple MAPI.");
        }
        finally
        {
            if (msgPtr != IntPtr.Zero) Marshal.FreeHGlobal(msgPtr);
            if (recipPtr != IntPtr.Zero) Marshal.FreeHGlobal(recipPtr);
            if (filesPtr != IntPtr.Zero) Marshal.FreeHGlobal(filesPtr);
            foreach (var p in strings) Marshal.FreeHGlobal(p);
        }
    }

    private static string DescribeError(uint rc) => rc switch
    {
        2 => "Das E-Mail-Programm meldet einen allgemeinen Fehler (MAPI_E_FAILURE). Ist ein Standard-E-Mail-Programm eingerichtet?",
        3 => "Die Anmeldung am E-Mail-Programm ist fehlgeschlagen.",
        5 => "Nicht genügend Arbeitsspeicher für das E-Mail-Programm.",
        11 => "Eine Anlage wurde nicht gefunden.",
        12 => "Eine Anlage konnte nicht geöffnet werden.",
        9 => "Zu viele Anlagen.",
        14 => "Der Empfänger ist unbekannt.",
        21 => "Der Empfänger ist nicht eindeutig.",
        25 => "Die E-Mail-Adresse des Empfängers ist ungültig.",
        26 => "Das E-Mail-Programm unterstützt diese Funktion nicht.",
        _ => $"Das E-Mail-Programm konnte nicht geöffnet werden (MAPI-Fehler {rc})."
    };
}
