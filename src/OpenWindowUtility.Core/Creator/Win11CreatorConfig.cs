namespace OpenWindowUtility.Core.Creator;

public sealed class Win11CreatorConfig
{
    // Hardware & Requirement Bypasses
    public bool BypassTpm { get; set; } = true;
    public bool BypassSecureBoot { get; set; } = true;
    public bool BypassRam { get; set; } = true;
    public bool BypassCpu { get; set; } = true;
    public bool BypassStorage { get; set; } = true;
    public bool BypassDiskCheck { get; set; } = true;
    public bool DisableBitLockerAutomaticDeviceEncryption { get; set; } = true;

    // Account & OOBE Bypasses
    public bool BypassMicrosoftAccount { get; set; } = true;
    public bool DisableOobeTelemetry { get; set; } = true;
    public bool DisableConsumerExperience { get; set; } = true;
    public bool SkipEula { get; set; } = true;

    // Local Account Details
    public string LocalUsername { get; set; } = "User";
    public string LocalPassword { get; set; } = string.Empty;
    public bool AutoLogon { get; set; } = false;
    public string UserGroup { get; set; } = "Administrators";

    // System Localization & Metadata
    public string? ComputerName { get; set; }
    public string? TimeZone { get; set; }
    public string SystemLanguage { get; set; } = "en-US";

    // Post-Install Actions
    public bool EnableFirstLogonTweaks { get; set; } = false;
    public List<string> CustomFirstLogonCommands { get; set; } = [];
}
