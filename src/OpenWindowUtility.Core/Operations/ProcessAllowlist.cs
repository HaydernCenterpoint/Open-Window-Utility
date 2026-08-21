namespace OpenWindowUtility.Core.Operations;

public static class ProcessAllowlist
{
    private static readonly HashSet<string> Executables = new(StringComparer.OrdinalIgnoreCase)
    {
        "powercfg.exe", "powercfg",
        "bcdedit.exe", "bcdedit",
        "cleanmgr.exe", "cleanmgr",
        "netsh.exe", "netsh",
        "sfc.exe", "sfc",
        "dism.exe", "dism",
        "rstrui.exe", "rstrui",
        "control.exe", "control",
        "mmc.exe", "mmc",
        "winget.exe", "winget",
        "choco.exe", "choco",
        "powershell.exe",
        "schtasks.exe", "schtasks",
        "manage-bde.exe", "manage-bde",
        "ipconfig.exe", "ipconfig",
        "wsreset.exe", "wsreset",
        "w32tm.exe", "w32tm",
        "gpupdate.exe", "gpupdate",
        "oscdimg.exe", "oscdimg"
    };

    private static readonly HashSet<string> ShellDocuments = new(StringComparer.OrdinalIgnoreCase)
    {
        "sysdm.cpl",
        "ncpa.cpl",
        "appwiz.cpl",
        "powercfg.cpl",
        "timedate.cpl",
        "mmsys.cpl",
        "intl.cpl",
        "main.cpl",
        "firewall.cpl",
        "wscui.cpl",
        "desk.cpl",
        "compmgmt.msc",
        "desk.cpl",
        "inetcpl.cpl",
        "lusrmgr.msc",
        "services.msc",
        "taskschd.msc",
        "devmgmt.msc",
        "diskmgmt.msc",
        "eventvwr.msc",
        "gpedit.msc",
        "secpol.msc",
        "certmgr.msc"
    };

    public static bool IsAllowed(string fileName, bool useShellExecute)
    {
        var name = Path.GetFileName(fileName);
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        if (Executables.Contains(name))
        {
            return true;
        }

        return useShellExecute && ShellDocuments.Contains(name);
    }

    public static bool IsSafePackageId(string packageId)
    {
        if (string.IsNullOrWhiteSpace(packageId) || packageId.Length > 128)
        {
            return false;
        }

        foreach (var ch in packageId)
        {
            if (char.IsLetterOrDigit(ch) || ch is '.' or '_' or '-' or '+')
            {
                continue;
            }

            return false;
        }

        return true;
    }

    public static bool IsSafeAppxName(string packageName)
    {
        if (string.IsNullOrWhiteSpace(packageName) || packageName.Length > 128)
        {
            return false;
        }

        foreach (var ch in packageName)
        {
            var ok = char.IsLetterOrDigit(ch) || ch is '.' or '_' or '-';
            if (!ok)
            {
                return false;
            }
        }

        return true;
    }
}
