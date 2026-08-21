using System.Xml.Linq;
using OpenWindowUtility.Core.Creator;
using OpenWindowUtility.Core.Operations;

namespace OpenWindowUtility.Core.Tests;

public sealed class Win11CreatorTests
{
    [Fact]
    public void Generate_DefaultConfig_ProducesValidUnattendXml()
    {
        var config = new Win11CreatorConfig();
        var xml = UnattendedXmlGenerator.Generate(config);

        Assert.False(string.IsNullOrWhiteSpace(xml));

        var doc = XDocument.Parse(xml);
        Assert.NotNull(doc.Root);
        Assert.Equal("unattend", doc.Root.Name.LocalName);

        // Check windowsPE pass
        var winPePass = doc.Root.Elements().FirstOrDefault(e => e.Attribute("pass")?.Value == "windowsPE");
        Assert.NotNull(winPePass);
        var winPeContent = winPePass.ToString();
        Assert.Contains("BypassTPMCheck", winPeContent);
        Assert.Contains("BypassSecureBootCheck", winPeContent);
        Assert.Contains("BypassRAMCheck", winPeContent);
        Assert.Contains("BypassCPUCheck", winPeContent);
        Assert.Contains("BypassStorageCheck", winPeContent);
        Assert.Contains("PreventDeviceEncryption", winPeContent);

        // Check specialize pass
        var specializePass = doc.Root.Elements().FirstOrDefault(e => e.Attribute("pass")?.Value == "specialize");
        Assert.NotNull(specializePass);
        var specializeContent = specializePass.ToString();
        Assert.Contains("BypassNRO", specializeContent);
        Assert.Contains("Open Window Utility", specializeContent);

        // Check oobeSystem pass
        var oobePass = doc.Root.Elements().FirstOrDefault(e => e.Attribute("pass")?.Value == "oobeSystem");
        Assert.NotNull(oobePass);
        var oobeContent = oobePass.ToString();
        Assert.Contains("<DisplayName>User</DisplayName>", oobeContent);
        Assert.Contains("<Group>Administrators</Group>", oobeContent);
        Assert.Contains("<HideOnlineAccountScreens>true</HideOnlineAccountScreens>", oobeContent);
        Assert.Contains("<ProtectYourPC>3</ProtectYourPC>", oobeContent);
    }

    [Fact]
    public void Generate_CustomUserAndAutoLogon_ReflectedInXml()
    {
        var config = new Win11CreatorConfig
        {
            LocalUsername = "HaydernDev",
            LocalPassword = "Password@123",
            AutoLogon = true,
            ComputerName = "MY-CUSTOM-PC",
            TimeZone = "SE Asia Standard Time",
            EnableFirstLogonTweaks = true,
            CustomFirstLogonCommands = ["echo SetupFinished"]
        };

        var xml = UnattendedXmlGenerator.Generate(config);
        var doc = XDocument.Parse(xml);
        var xmlString = doc.ToString();

        Assert.Contains("<DisplayName>HaydernDev</DisplayName>", xmlString);
        Assert.Contains("<Value>Password@123</Value>", xmlString);
        Assert.Contains("<AutoLogon", xmlString);
        Assert.Contains("<ComputerName>MY-CUSTOM-PC</ComputerName>", xmlString);
        Assert.Contains("<TimeZone>SE Asia Standard Time</TimeZone>", xmlString);
        Assert.Contains("<FirstLogonCommands>", xmlString);
        Assert.Contains("echo SetupFinished", xmlString);
    }

    [Fact]
    public void Generate_DisabledBypasses_OmitsCommands()
    {
        var config = new Win11CreatorConfig
        {
            BypassTpm = false,
            BypassSecureBoot = false,
            BypassRam = false,
            BypassCpu = false,
            BypassStorage = false,
            BypassDiskCheck = false,
            DisableBitLockerAutomaticDeviceEncryption = false,
            BypassMicrosoftAccount = false
        };

        var xml = UnattendedXmlGenerator.Generate(config);
        var doc = XDocument.Parse(xml);
        var xmlString = doc.ToString();

        Assert.DoesNotContain("BypassTPMCheck", xmlString);
        Assert.DoesNotContain("BypassSecureBootCheck", xmlString);
        Assert.DoesNotContain("BypassRAMCheck", xmlString);
        Assert.DoesNotContain("BypassCPUCheck", xmlString);
        Assert.DoesNotContain("BypassStorageCheck", xmlString);
        Assert.DoesNotContain("BypassNRO", xmlString);
    }

    [Fact]
    public async Task IsoEngine_ExportUnattendedXmlAsync_WritesValidFile()
    {
        var engine = new IsoEngine();
        var tempFile = Path.Combine(Path.GetTempPath(), $"test_autounattend_{Guid.NewGuid():N}.xml");

        try
        {
            var config = new Win11CreatorConfig { LocalUsername = "TestUser" };
            await engine.ExportUnattendedXmlAsync(tempFile, config);

            Assert.True(File.Exists(tempFile));
            var content = await File.ReadAllTextAsync(tempFile);
            var doc = XDocument.Parse(content);
            Assert.NotNull(doc.Root);
            Assert.Contains("TestUser", content);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public void ProcessAllowlist_Allows_Oscdimg()
    {
        Assert.True(ProcessAllowlist.IsAllowed("oscdimg.exe", false));
        Assert.True(ProcessAllowlist.IsAllowed("oscdimg", false));
    }
}
