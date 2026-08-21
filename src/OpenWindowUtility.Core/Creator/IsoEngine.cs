using System.Diagnostics;
using System.Text;
using OpenWindowUtility.Core.Jobs;
using OpenWindowUtility.Core.Operations;

namespace OpenWindowUtility.Core.Creator;

public sealed class IsoEngine
{
    private readonly IProcessRunner _processRunner;

    public IsoEngine(IProcessRunner? processRunner = null)
    {
        _processRunner = processRunner ?? new ProcessRunner();
    }

    public async Task ExportUnattendedXmlAsync(
        string targetFilePath,
        Win11CreatorConfig config,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetFilePath);
        ArgumentNullException.ThrowIfNull(config);

        var directory = Path.GetDirectoryName(targetFilePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var xml = UnattendedXmlGenerator.Generate(config);
        await File.WriteAllTextAsync(targetFilePath, xml, Encoding.UTF8, cancellationToken).ConfigureAwait(false);
    }

    public async Task BuildCustomIsoAsync(
        string sourceIsoPath,
        string outputIsoPath,
        Win11CreatorConfig config,
        IJobContext log,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceIsoPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputIsoPath);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(log);

        if (!File.Exists(sourceIsoPath))
        {
            throw new FileNotFoundException($"Source ISO file not found: {sourceIsoPath}", sourceIsoPath);
        }

        var outDir = Path.GetDirectoryName(outputIsoPath);
        if (!string.IsNullOrWhiteSpace(outDir))
        {
            Directory.CreateDirectory(outDir);
        }

        var stagingDir = Path.Combine(Path.GetTempPath(), $"owu_iso_build_{Guid.NewGuid():N}");
        log.Info($"Preparing staging directory: {stagingDir}");
        Directory.CreateDirectory(stagingDir);

        try
        {
            log.Info($"Mounting source ISO: {sourceIsoPath}");
            var mountedDrive = await MountIsoAndGetDriveLetterAsync(sourceIsoPath, cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(mountedDrive))
            {
                throw new InvalidOperationException("Failed to mount source ISO image.");
            }

            try
            {
                log.Info($"Copying ISO files from {mountedDrive} to {stagingDir}...");
                await CopyDirectoryAsync(mountedDrive, stagingDir, log, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                log.Info($"Dismounting source ISO from {mountedDrive}...");
                await DismountIsoAsync(sourceIsoPath, cancellationToken).ConfigureAwait(false);
            }

            var autounattendPath = Path.Combine(stagingDir, "autounattend.xml");
            log.Info("Injecting customized autounattend.xml into root...");
            await ExportUnattendedXmlAsync(autounattendPath, config, cancellationToken).ConfigureAwait(false);

            log.Info("Building bootable Windows 11 ISO...");
            await PackageIsoAsync(stagingDir, outputIsoPath, log, cancellationToken).ConfigureAwait(false);

            log.Info($"Custom ISO successfully created at: {outputIsoPath}");
        }
        finally
        {
            try
            {
                if (Directory.Exists(stagingDir))
                {
                    log.Info("Cleaning up temporary build files...");
                    Directory.Delete(stagingDir, recursive: true);
                }
            }
            catch (Exception ex)
            {
                log.Warn($"Could not fully remove staging directory: {ex.Message}");
            }
        }
    }

    private async Task<string?> MountIsoAndGetDriveLetterAsync(string isoPath, CancellationToken ct)
    {
        var escapedIso = EscapePowerShellString(isoPath);
        var script = $"$m = Mount-DiskImage -ImagePath '{escapedIso}' -PassThru; ($m | Get-Volume).DriveLetter";
        var result = await _processRunner.RunAsync(
            "powershell.exe",
            $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command \"{script}\"",
            ct).ConfigureAwait(false);

        if (result.ExitCode != 0)
        {
            return null;
        }

        var letter = result.StandardOutput.Trim().Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
        if (string.IsNullOrWhiteSpace(letter))
        {
            return null;
        }

        return letter.Length == 1 ? $"{letter}:\\" : $"{letter.TrimEnd('\\')}\\";
    }

    private async Task DismountIsoAsync(string isoPath, CancellationToken ct)
    {
        var escapedIso = EscapePowerShellString(isoPath);
        var script = $"Dismount-DiskImage -ImagePath '{escapedIso}'";
        await _processRunner.RunAsync(
            "powershell.exe",
            $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command \"{script}\"",
            ct).ConfigureAwait(false);
    }

    private static async Task CopyDirectoryAsync(string sourceDir, string targetDir, IJobContext log, CancellationToken ct)
    {
        var allDirs = Directory.GetDirectories(sourceDir, "*", SearchOption.AllDirectories);
        foreach (var dir in allDirs)
        {
            ct.ThrowIfCancellationRequested();
            var relative = Path.GetRelativePath(sourceDir, dir);
            Directory.CreateDirectory(Path.Combine(targetDir, relative));
        }

        var allFiles = Directory.GetFiles(sourceDir, "*.*", SearchOption.AllDirectories);
        var total = allFiles.Length;
        var current = 0;

        foreach (var file in allFiles)
        {
            ct.ThrowIfCancellationRequested();
            var relative = Path.GetRelativePath(sourceDir, file);
            var dest = Path.Combine(targetDir, relative);
            File.Copy(file, dest, overwrite: true);
            current++;

            if (current % 100 == 0 || current == total)
            {
                log.Progress?.Report((double)current / total * 0.7);
            }
        }
    }

    private async Task PackageIsoAsync(string stagingDir, string outputIsoPath, IJobContext log, CancellationToken ct)
    {
        var oscdimg = FindOscdimgPath();
        if (!string.IsNullOrWhiteSpace(oscdimg) && File.Exists(oscdimg))
        {
            log.Info($"Using oscdimg tool at: {oscdimg}");
            var bootData = $"2#p0,e,b\"{Path.Combine(stagingDir, "boot", "etfsboot.com")}\"#pEF,e,b\"{Path.Combine(stagingDir, "efi", "microsoft", "boot", "efisys.bin")}\"";
            var args = $"-m -o -u2 -udfver102 -bootdata:{bootData} \"{stagingDir}\" \"{outputIsoPath}\"";

            var result = await _processRunner.RunAsync(oscdimg, args, ct).ConfigureAwait(false);
            if (result.ExitCode != 0)
            {
                throw new InvalidOperationException($"oscdimg failed (exit code {result.ExitCode}): {result.StandardError}");
            }
        }
        else
        {
            log.Warn("oscdimg.exe was not detected on system. Using PowerShell CDImage/DISM fallback wrapper...");
            var escapedStaging = EscapePowerShellString(stagingDir);
            var escapedOut = EscapePowerShellString(outputIsoPath);
            
            // Fallback script creating standard ISO
            var script = $@"
$ErrorActionPreference = 'Stop'
$etfs = Join-Path '{escapedStaging}' 'boot\etfsboot.com'
$efisys = Join-Path '{escapedStaging}' 'efi\microsoft\boot\efisys.bin'
if (Get-Command oscdimg -ErrorAction SilentlyContinue) {{
    & oscdimg -m -o -u2 -udfver102 -bootdata:2#p0,e,b""$etfs""#pEF,e,b""$efisys"" '{escapedStaging}' '{escapedOut}'
}} else {{
    Write-Warning 'oscdimg not in PATH; please install Windows ADK for native bootable ISO creation or use generated autounattend.xml with Rufus/Ventoy.'
    throw 'oscdimg.exe not found. Please export autounattend.xml directly or install Windows ADK Deployment Tools.'
}}
";
            var result = await _processRunner.RunAsync(
                "powershell.exe",
                $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command \"{script}\"",
                ct).ConfigureAwait(false);

            if (result.ExitCode != 0)
            {
                throw new InvalidOperationException($"ISO build failed: {result.StandardError}");
            }
        }

        log.Progress?.Report(1.0);
    }

    private static string? FindOscdimgPath()
    {
        var candidates = new[]
        {
            Path.Combine(AppPaths.Root, "tools", "oscdimg.exe"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tools", "oscdimg.exe"),
            @"C:\Program Files (x86)\Windows Kits\10\Assessment and Deployment Kit\Deployment Tools\amd64\Oscdimg\oscdimg.exe",
            @"C:\Program Files (x86)\Windows Kits\10\Assessment and Deployment Kit\Deployment Tools\x86\Oscdimg\oscdimg.exe",
            @"C:\Program Files\Windows Kits\10\Assessment and Deployment Kit\Deployment Tools\amd64\Oscdimg\oscdimg.exe"
        };

        return candidates.FirstOrDefault(File.Exists);
    }

    private static string EscapePowerShellString(string input) =>
        input.Replace("'", "''");
}
