using System.Text;
using System.Xml.Linq;

namespace OpenWindowUtility.Core.Creator;

public static class UnattendedXmlGenerator
{
    private static readonly XNamespace UnattendNs = "urn:schemas-microsoft-com:unattend";
    private static readonly XNamespace WcmNs = "http://schemas.microsoft.com/WMIConfig/2002/State";
    private static readonly XNamespace XsiNs = "http://www.w3.org/2001/XMLSchema-instance";

    public static string Generate(Win11CreatorConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        var doc = new XDocument(
            new XDeclaration("1.0", "utf-8", null),
            new XElement(UnattendNs + "unattend",
                new XAttribute(XNamespace.Xmlns + "wcm", WcmNs.NamespaceName),
                new XAttribute(XNamespace.Xmlns + "xsi", XsiNs.NamespaceName),
                CreateWindowsPePass(config),
                CreateSpecializePass(config),
                CreateOobeSystemPass(config)
            )
        );

        var sb = new StringBuilder();
        using (var writer = new Utf8StringWriter(sb))
        {
            doc.Save(writer, SaveOptions.None);
        }

        return sb.ToString();
    }

    private static XElement CreateWindowsPePass(Win11CreatorConfig config)
    {
        var runSync = new XElement(UnattendNs + "RunSynchronous");
        var order = 1;

        if (config.BypassTpm)
        {
            runSync.Add(CreateSyncCommand(order++, "reg add HKLM\\SYSTEM\\Setup\\LabConfig /v BypassTPMCheck /t REG_DWORD /d 1 /f"));
        }

        if (config.BypassSecureBoot)
        {
            runSync.Add(CreateSyncCommand(order++, "reg add HKLM\\SYSTEM\\Setup\\LabConfig /v BypassSecureBootCheck /t REG_DWORD /d 1 /f"));
        }

        if (config.BypassRam)
        {
            runSync.Add(CreateSyncCommand(order++, "reg add HKLM\\SYSTEM\\Setup\\LabConfig /v BypassRAMCheck /t REG_DWORD /d 1 /f"));
        }

        if (config.BypassCpu)
        {
            runSync.Add(CreateSyncCommand(order++, "reg add HKLM\\SYSTEM\\Setup\\LabConfig /v BypassCPUCheck /t REG_DWORD /d 1 /f"));
        }

        if (config.BypassStorage)
        {
            runSync.Add(CreateSyncCommand(order++, "reg add HKLM\\SYSTEM\\Setup\\LabConfig /v BypassStorageCheck /t REG_DWORD /d 1 /f"));
        }

        if (config.BypassDiskCheck)
        {
            runSync.Add(CreateSyncCommand(order++, "reg add HKLM\\SYSTEM\\Setup\\LabConfig /v BypassDiskCheck /t REG_DWORD /d 1 /f"));
        }

        if (config.DisableBitLockerAutomaticDeviceEncryption)
        {
            runSync.Add(CreateSyncCommand(order++, "reg add HKLM\\SYSTEM\\CurrentControlSet\\Control\\BitLocker /v PreventDeviceEncryption /t REG_DWORD /d 1 /f"));
        }

        var setupComponent = new XElement(UnattendNs + "component",
            new XAttribute("name", "Microsoft-Windows-Setup"),
            new XAttribute("processorArchitecture", "amd64"),
            new XAttribute("publicKeyToken", "31bf3856ad364e35"),
            new XAttribute("language", "neutral"),
            new XAttribute("versionScope", "nonSxS")
        );

        if (runSync.HasElements)
        {
            setupComponent.Add(runSync);
        }

        if (config.SkipEula)
        {
            setupComponent.Add(new XElement(UnattendNs + "UserData",
                new XElement(UnattendNs + "AcceptEula", "true")
            ));
        }

        return new XElement(UnattendNs + "settings",
            new XAttribute("pass", "windowsPE"),
            setupComponent
        );
    }

    private static XElement CreateSpecializePass(Win11CreatorConfig config)
    {
        var shellComponent = new XElement(UnattendNs + "component",
            new XAttribute("name", "Microsoft-Windows-Shell-Setup"),
            new XAttribute("processorArchitecture", "amd64"),
            new XAttribute("publicKeyToken", "31bf3856ad364e35"),
            new XAttribute("language", "neutral"),
            new XAttribute("versionScope", "nonSxS")
        );

        if (!string.IsNullOrWhiteSpace(config.ComputerName))
        {
            shellComponent.Add(new XElement(UnattendNs + "ComputerName", config.ComputerName.Trim()));
        }

        if (!string.IsNullOrWhiteSpace(config.TimeZone))
        {
            shellComponent.Add(new XElement(UnattendNs + "TimeZone", config.TimeZone.Trim()));
        }

        shellComponent.Add(new XElement(UnattendNs + "OEMInformation",
            new XElement(UnattendNs + "Manufacturer", "Open Window Utility"),
            new XElement(UnattendNs + "SupportURL", "https://github.com/HaydernCenterpoint/Open-Window-Utility")
        ));

        var deployRunSync = new XElement(UnattendNs + "RunSynchronous");
        var deployOrder = 1;

        if (config.BypassMicrosoftAccount)
        {
            deployRunSync.Add(CreateSyncCommand(deployOrder++, "reg add HKLM\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\OOBE /v BypassNRO /t REG_DWORD /d 1 /f"));
        }

        if (config.DisableConsumerExperience)
        {
            deployRunSync.Add(CreateSyncCommand(deployOrder++, "reg add HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows\\CloudContent /v DisableWindowsConsumerFeatures /t REG_DWORD /d 1 /f"));
        }

        if (config.DisableOobeTelemetry)
        {
            deployRunSync.Add(CreateSyncCommand(deployOrder++, "reg add HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows\\DataCollection /v AllowTelemetry /t REG_DWORD /d 0 /f"));
        }

        var deployComponent = new XElement(UnattendNs + "component",
            new XAttribute("name", "Microsoft-Windows-Deployment"),
            new XAttribute("processorArchitecture", "amd64"),
            new XAttribute("publicKeyToken", "31bf3856ad364e35"),
            new XAttribute("language", "neutral"),
            new XAttribute("versionScope", "nonSxS"),
            deployRunSync
        );

        var specializePass = new XElement(UnattendNs + "settings",
            new XAttribute("pass", "specialize"),
            shellComponent
        );

        if (deployRunSync.HasElements)
        {
            specializePass.Add(deployComponent);
        }

        return specializePass;
    }

    private static XElement CreateOobeSystemPass(Win11CreatorConfig config)
    {
        var shellComponent = new XElement(UnattendNs + "component",
            new XAttribute("name", "Microsoft-Windows-Shell-Setup"),
            new XAttribute("processorArchitecture", "amd64"),
            new XAttribute("publicKeyToken", "31bf3856ad364e35"),
            new XAttribute("language", "neutral"),
            new XAttribute("versionScope", "nonSxS")
        );

        var oobe = new XElement(UnattendNs + "OOBE",
            new XElement(UnattendNs + "HideEULAPage", config.SkipEula ? "true" : "false"),
            new XElement(UnattendNs + "HideLocalAccountScreen", "true"),
            new XElement(UnattendNs + "HideOEMRegistrationScreens", "true"),
            new XElement(UnattendNs + "HideOnlineAccountScreens", config.BypassMicrosoftAccount ? "true" : "false"),
            new XElement(UnattendNs + "HideWirelessSetupInOOBE", "false"),
            new XElement(UnattendNs + "ProtectYourPC", config.DisableOobeTelemetry ? "3" : "1"),
            new XElement(UnattendNs + "UnattendEnableRetailDemo", "false")
        );
        shellComponent.Add(oobe);

        var username = string.IsNullOrWhiteSpace(config.LocalUsername) ? "User" : config.LocalUsername.Trim();
        var password = config.LocalPassword ?? string.Empty;
        var group = string.IsNullOrWhiteSpace(config.UserGroup) ? "Administrators" : config.UserGroup.Trim();

        var localAccount = new XElement(UnattendNs + "LocalAccount",
            new XAttribute(WcmNs + "action", "add"),
            new XElement(UnattendNs + "Description", "Local Administrator Account"),
            new XElement(UnattendNs + "DisplayName", username),
            new XElement(UnattendNs + "Group", group),
            new XElement(UnattendNs + "Name", username),
            new XElement(UnattendNs + "Password",
                new XElement(UnattendNs + "Value", password),
                new XElement(UnattendNs + "PlainText", "true")
            )
        );

        shellComponent.Add(new XElement(UnattendNs + "UserAccounts",
            new XElement(UnattendNs + "LocalAccounts", localAccount)
        ));

        if (config.AutoLogon)
        {
            shellComponent.Add(new XElement(UnattendNs + "AutoLogon",
                new XElement(UnattendNs + "Password",
                    new XElement(UnattendNs + "Value", password),
                    new XElement(UnattendNs + "PlainText", "true")
                ),
                new XElement(UnattendNs + "Enabled", "true"),
                new XElement(UnattendNs + "LogonCount", "1"),
                new XElement(UnattendNs + "Username", username)
            ));
        }

        var firstLogonCommands = new List<string>();
        if (config.EnableFirstLogonTweaks)
        {
            firstLogonCommands.Add("powershell.exe -NoProfile -Command \"Set-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced' -Name 'HideFileExt' -Value 0\"");
            firstLogonCommands.Add("powershell.exe -NoProfile -Command \"Set-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced' -Name 'Hidden' -Value 1\"");
        }

        if (config.CustomFirstLogonCommands.Count > 0)
        {
            firstLogonCommands.AddRange(config.CustomFirstLogonCommands);
        }

        if (firstLogonCommands.Count > 0)
        {
            var syncCommands = new XElement(UnattendNs + "FirstLogonCommands");
            var cmdOrder = 1;
            foreach (var cmd in firstLogonCommands)
            {
                if (!string.IsNullOrWhiteSpace(cmd))
                {
                    syncCommands.Add(new XElement(UnattendNs + "SynchronousCommand",
                        new XAttribute(WcmNs + "action", "add"),
                        new XElement(UnattendNs + "Order", cmdOrder++),
                        new XElement(UnattendNs + "CommandLine", cmd.Trim()),
                        new XElement(UnattendNs + "Description", $"Post-install setup command #{cmdOrder - 1}")
                    ));
                }
            }
            shellComponent.Add(syncCommands);
        }

        return new XElement(UnattendNs + "settings",
            new XAttribute("pass", "oobeSystem"),
            shellComponent
        );
    }

    private static XElement CreateSyncCommand(int order, string commandLine)
    {
        return new XElement(UnattendNs + "RunSynchronousCommand",
            new XAttribute(WcmNs + "action", "add"),
            new XElement(UnattendNs + "Order", order),
            new XElement(UnattendNs + "Path", commandLine)
        );
    }

    private sealed class Utf8StringWriter : StringWriter
    {
        public Utf8StringWriter(StringBuilder sb) : base(sb) { }
        public override Encoding Encoding => Encoding.UTF8;
    }
}
