using System.Runtime.InteropServices;

namespace SpeedScanManager;

/// <summary>
/// COM interop for IConnectionPointContainer.
/// </summary>
[ComImport]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
[Guid("B196B284-BAB4-101A-B69C-00AA00341D07")]
internal interface IConnectionPointContainer
{
    void EnumConnectionPoints(out IntPtr ppEnum);
    void FindConnectionPoint(ref Guid riid, out IConnectionPoint? ppCP);
}

/// <summary>
/// COM interop for IConnectionPoint.
/// </summary>
[ComImport]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
[Guid("B196B286-BAB4-101A-B69C-00AA00341D07")]
internal interface IConnectionPoint
{
    void GetConnectionInterface(out Guid pIID);
    void GetConnectionPointContainer(out IConnectionPointContainer ppCPC);
    void Advise(object pUnkSink, out int pdwCookie);
    void Unadvise(int dwCookie);
    void EnumConnections(out IntPtr ppEnum);
}

/// <summary>
/// Minimal IDispatch interface for COM event sinks.
/// </summary>
[ComImport]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
[Guid("00020400-0000-0000-C000-000000000046")]
internal interface IDispatch
{
    [PreserveSig] int GetTypeInfoCount(out int pctinfo);
    [PreserveSig] int GetTypeInfo(int iTInfo, int lcid, out IntPtr ppTInfo);
    [PreserveSig] int GetIDsOfNames(ref Guid riid, string[] rgszNames, int cNames, int lcid, int[] rgDispId);
    [PreserveSig] int Invoke(int dispIdMember, ref Guid riid, int lcid, short wFlags,
        ref System.Runtime.InteropServices.ComTypes.DISPPARAMS pDispParams,
        out object? pVarResult, out IntPtr pExcepInfo, out int puArgErr);
}

/// <summary>
/// IWiaDevMgr2 COM interface (WIA 2.0 Device Manager).
/// GUID: {79C07CF1-CBDD-41EE-8EC3-F00080CADA7A}
/// Vtable layout from wia_lh.idl.
/// </summary>
[ComImport]
[Guid("79C07CF1-CBDD-41EE-8EC3-F00080CADA7A")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IWiaDevMgr2
{
    // slot 3: EnumDeviceInfo
    [PreserveSig] int EnumDeviceInfo(int lFlags, out IntPtr ppIEnum);
    // slot 4: CreateDevice
    [PreserveSig] int CreateDevice(int lFlags, string bstrDeviceID, out IntPtr ppWiaItem2Root);
    // slot 5: SelectDeviceDlg
    [PreserveSig] int SelectDeviceDlg(IntPtr hwndParent, int lDeviceType, int lFlags, ref string pbstrDeviceID, out IntPtr ppItemRoot);
    // slot 6: SelectDeviceDlgID
    [PreserveSig] int SelectDeviceDlgID(IntPtr hwndParent, int lDeviceType, int lFlags, out string pbstrDeviceID);
    // slot 7: RegisterEventCallbackInterface
    [PreserveSig] int RegisterEventCallbackInterface(int lFlags, string bstrDeviceID, ref Guid pEventGUID, IWiaEventCallback pIWiaEventCallback, out IntPtr pEventObject);
    // slot 8: RegisterEventCallbackProgram
    [PreserveSig] int RegisterEventCallbackProgram(int lFlags, string bstrDeviceID, ref Guid pEventGUID, string bstrFullAppName, string bstrCommandLineArg, string bstrName, string bstrDescription, string bstrIcon);
    // slot 9: RegisterEventCallbackCLSID
    [PreserveSig] int RegisterEventCallbackCLSID(int lFlags, string bstrDeviceID, ref Guid pEventGUID, ref Guid pClsID, string bstrName, string bstrDescription, string bstrIcon);
    // slot 10: GetImage
    [PreserveSig] int GetImage(int lFlags, string bstrDeviceID, IntPtr hwndParent, string bstrDirName, string bstrCaption, int lFilterType, out string pbstrFilename, out int plNumFiles, out IntPtr ppbstrFiles);
}

/// <summary>
/// IWiaEventCallback COM interface.
/// GUID: {AE6287B0-0084-11D2-973B-00A0C9068F2E}
/// </summary>
[ComImport]
[Guid("AE6287B0-0084-11D2-973B-00A0C9068F2E")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IWiaEventCallback
{
    [PreserveSig]
    int ImageEventCallback(
        string bstrEventGuid,
        string bstrDeviceID,
        string bstrDeviceDescription,
        int dwDeviceType,
        string bstrFullItemName,
        out int plRetVal);
}

internal static class NativeMethods
{
    [DllImport("ole32.dll")]
    internal static extern int CoCreateInstance(
        ref Guid clsid, IntPtr pUnkOuter, int clsContext,
        ref Guid iid, out IntPtr ppv);
}

/// <summary>
/// Helper class for subscribing to WIA COM events.
/// </summary>
internal static class ComEventHelper
{
    // WIA 2.0 DeviceManager CLSID (32-bit: {B6C292BC-7C88-41EE-8B54-8EC92617E599})
    private static readonly Guid CLSID_WiaDevMgr2 = new("B6C292BC-7C88-41EE-8B54-8EC92617E599");

    /// <summary>
    /// Creates a WIA DeviceManager via CoCreateInstance with the IWiaDevMgr2 interface,
    /// bypassing the automation wrapper that doesn't support vtable interfaces.
    /// </summary>
    public static IWiaDevMgr2? CreateWiaDevMgr2()
    {
        var iid = typeof(IWiaDevMgr2).GUID;
        var clsid = CLSID_WiaDevMgr2;
        int hr = NativeMethods.CoCreateInstance(
            ref clsid, IntPtr.Zero, 23, // CLSCTX_ALL = INPROC_SERVER|INPROC_HANDLER|LOCAL_SERVER
            ref iid, out var pDevMgr);
        if (hr < 0 || pDevMgr == IntPtr.Zero) return null;
        return (IWiaDevMgr2)Marshal.GetObjectForIUnknown(pDevMgr);
    }
}

/// <summary>
/// Implementation of IWiaEventCallback that forwards events.
/// </summary>
[ComVisible(true)]
[ClassInterface(ClassInterfaceType.None)]
internal sealed class WiaEventCallbackImpl : IWiaEventCallback
{
    public event Action<string, string>? OnImageEvent;

    public int ImageEventCallback(
        string bstrEventGuid,
        string bstrDeviceID,
        string bstrDeviceDescription,
        int dwDeviceType,
        string bstrFullItemName,
        out int plRetVal)
    {
        plRetVal = 0;
        OnImageEvent?.Invoke(bstrEventGuid, bstrDeviceID);
        return 0;
    }
}
