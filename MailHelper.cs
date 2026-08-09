using System.Diagnostics;
using System.Runtime.InteropServices;

namespace SpeedScanManager;

/// <summary>
/// Opens the default system mail client (MAPI) with pre-filled subject and attachment.
/// No SMTP sending – the user sends the email manually from their mail client.
/// </summary>
internal static class MailHelper
{
    [DllImport("mapi32.dll", CharSet = CharSet.Auto)]
    private static extern int MAPISendMail(IntPtr lhSession, IntPtr ulUIParam, MapiMessage lpMessage, uint flFlags, uint ulReserved);

    [DllImport("mapi32.dll", CharSet = CharSet.Auto)]
    private static extern int MAPILogon(IntPtr ulUIParam, string? lpszProfileName, string? lpszPassword,
        uint flFlags, uint ulReserved, ref IntPtr lplhSession);

    [DllImport("mapi32.dll", CharSet = CharSet.Auto)]
    private static extern int MAPILogoff(IntPtr lhSession, IntPtr ulUIParam, uint flFlags, uint ulReserved);

    private const uint MAPI_LOGON_UI = 0x00000001;
    private const uint MAPI_DIALOG = 0x00000008;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MapiMessage
    {
        public uint ulReserved;
        public string? lpszSubject;
        public string? lpszNoteText;
        public string? lpszMessageType;
        public string? lpszDateReceived;
        public string? lpszConversationID;
        public uint flFlags;
        public IntPtr lpOriginator;
        public uint nRecipCount;
        public IntPtr lpRecips;
        public uint nFileCount;
        public IntPtr lpFiles;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MapiFileDesc
    {
        public uint ulReserved;
        public uint flFlags;
        public uint nPosition;
        public string? lpszPathName;
        public string? lpszFileName;
        public IntPtr lpFileType;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MapiRecipDesc
    {
        public uint ulReserved;
        public uint ulRecipClass;
        public string? lpszName;
        public string? lpszAddress;
        public uint ulEIDSize;
        public IntPtr lpEntryID;
    }

    private const uint MAPI_TO = 1;

    /// <summary>
    /// Opens the default mail client with the given subject, optional recipient, and file attachment(s).
    /// </summary>
    public static bool OpenMailWithAttachment(string subject, string recipient, List<string> filePaths)
    {
        if (filePaths.Count == 0)
            return false;

        try
        {
            var session = IntPtr.Zero;
            int logonResult = MAPILogon(IntPtr.Zero, null, null, MAPI_LOGON_UI, 0, ref session);

            // Build file descriptors
            var fileDescs = new MapiFileDesc[filePaths.Count];
            var fileHandles = GCHandle.Alloc(fileDescs, GCHandleType.Pinned);
            try
            {
                for (int i = 0; i < filePaths.Count; i++)
                {
                    fileDescs[i] = new MapiFileDesc
                    {
                        ulReserved = 0,
                        flFlags = 0,
                        nPosition = (uint)(i + 1),
                        lpszPathName = filePaths[i],
                        lpszFileName = Path.GetFileName(filePaths[i]),
                        lpFileType = IntPtr.Zero
                    };
                }

                // Build recipient if provided
                MapiRecipDesc[]? recipDescs = null;
                GCHandle recipHandle = default;
                IntPtr recipPtr = IntPtr.Zero;

                if (!string.IsNullOrWhiteSpace(recipient))
                {
                    recipDescs = new MapiRecipDesc[1];
                    recipDescs[0] = new MapiRecipDesc
                    {
                        ulReserved = 0,
                        ulRecipClass = MAPI_TO,
                        lpszName = recipient,
                        lpszAddress = $"SMTP:{recipient}",
                        ulEIDSize = 0,
                        lpEntryID = IntPtr.Zero
                    };
                    recipHandle = GCHandle.Alloc(recipDescs, GCHandleType.Pinned);
                    recipPtr = recipHandle.AddrOfPinnedObject();
                }

                try
                {
                    var message = new MapiMessage
                    {
                        ulReserved = 0,
                        lpszSubject = subject,
                        lpszNoteText = "",
                        lpszMessageType = null,
                        lpszDateReceived = null,
                        lpszConversationID = null,
                        flFlags = 0,
                        lpOriginator = IntPtr.Zero,
                        nRecipCount = recipDescs != null ? 1u : 0u,
                        lpRecips = recipPtr,
                        nFileCount = (uint)filePaths.Count,
                        lpFiles = fileHandles.AddrOfPinnedObject()
                    };

                    int result = MAPISendMail(session, IntPtr.Zero, message, MAPI_DIALOG, 0);

                    if (logonResult == 0 && session != IntPtr.Zero)
                    {
                        MAPILogoff(session, IntPtr.Zero, 0, 0);
                    }

                    return result == 0;
                }
                finally
                {
                    if (recipDescs != null)
                        recipHandle.Free();
                }
            }
            finally
            {
                fileHandles.Free();
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"MAPI failed: {ex.Message}");

            // Fallback: use mailto: protocol (no attachment support, but at least opens mail client)
            try
            {
                var mailto = $"mailto:{recipient}?subject={Uri.EscapeDataString(subject)}";
                Process.Start(new ProcessStartInfo(mailto) { UseShellExecute = true });
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
