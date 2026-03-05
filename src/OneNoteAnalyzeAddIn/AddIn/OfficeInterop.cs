using System.Runtime.InteropServices;

namespace OneNoteAnalyzeAddIn.AddIn;

[ComImport]
[Guid("000C0396-0000-0000-C000-000000000046")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IRibbonExtensibility
{
    [return: MarshalAs(UnmanagedType.BStr)]
    string GetCustomUI([MarshalAs(UnmanagedType.BStr)] string ribbonId);
}

[ComImport]
[Guid("000C0395-0000-0000-C000-000000000046")]
[InterfaceType(ComInterfaceType.InterfaceIsIDispatch)]
public interface IRibbonControl
{
    [DispId(1)]
    string Id { [return: MarshalAs(UnmanagedType.BStr)] get; }
}

[ComImport]
[Guid("B65AD801-ABAF-11D0-BB8B-00A0C90F2744")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IDTExtensibility2
{
    void OnConnection([MarshalAs(UnmanagedType.IDispatch)] object application, ext_ConnectMode connectMode, [MarshalAs(UnmanagedType.IDispatch)] object addInInst, ref Array custom);

    void OnDisconnection(ext_DisconnectMode removeMode, ref Array custom);

    void OnAddInsUpdate(ref Array custom);

    void OnStartupComplete(ref Array custom);

    void OnBeginShutdown(ref Array custom);
}

public enum ext_ConnectMode
{
    ext_cm_AfterStartup = 0,
    ext_cm_Startup = 1,
    ext_cm_External = 2,
    ext_cm_CommandLine = 3,
    ext_cm_Solution = 4
}

public enum ext_DisconnectMode
{
    ext_dm_HostShutdown = 0,
    ext_dm_UserClosed = 1,
    ext_dm_UISetupComplete = 2
}
