using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Hi3Helper.Plugin.Core;
using Hi3Helper.Plugin.Core.Management;

namespace Hi3Helper.Plugin.Wuwa.CN;

public partial class Exports : SharedStaticV1Ext<Exports>
{
    static Exports()
    {
        Load<WuwaPlugin>(!RuntimeFeature.IsDynamicCodeCompiled ? new GameVersion(0, 5, 2, 0) : default);
        // Loads the IPlugin instance as WuwaPlugin.
    }

    [UnmanagedCallersOnly(EntryPoint = "TryGetApiExport", CallConvs = [typeof(CallConvCdecl)])]
    public static unsafe int TryGetApiExport(char* exportName, void** delegateP)
    {
        return TryGetApiExportPointer(exportName, delegateP);
    }
}