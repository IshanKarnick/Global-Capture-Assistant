using System.Runtime.InteropServices;

namespace OneNoteAnalyzeAddIn.AddIn;

[ComVisible(true)]
[Guid("D3D78A72-7DF9-45BE-A7A6-588940F65B0A")]
[ProgId("OneNoteAnalyzeAddIn.Connect")]
[ClassInterface(ClassInterfaceType.None)]
public sealed class OneNoteAnalyzeComAddIn : IDTExtensibility2, IRibbonExtensibility
{
    public void OnConnection(object application, ext_ConnectMode connectMode, object addInInst, ref Array custom)
    {
    }

    public void OnDisconnection(ext_DisconnectMode removeMode, ref Array custom)
    {
    }

    public void OnAddInsUpdate(ref Array custom)
    {
    }

    public void OnStartupComplete(ref Array custom)
    {
    }

    public void OnBeginShutdown(ref Array custom)
    {
    }

    public string GetCustomUI(string ribbonId)
    {
        return string.Empty;
    }
}
