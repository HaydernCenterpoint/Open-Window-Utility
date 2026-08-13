using System.Management;

namespace OpenWindowUtility.Core.Safety;

public sealed class RestorePointService
{
    public bool TryCreate(string description, out string message)
    {
        try
        {
            using var systemRestore = new ManagementClass(@"\\.\root\default", "SystemRestore", new ObjectGetOptions());
            var parameters = systemRestore.GetMethodParameters("CreateRestorePoint");
            parameters["Description"] = description;
            parameters["RestorePointType"] = 12; // MODIFY_SETTINGS
            parameters["EventType"] = 100; // BEGIN_SYSTEM_CHANGE
            var result = systemRestore.InvokeMethod("CreateRestorePoint", parameters, null);
            var code = result is null ? -1 : Convert.ToInt32(result["ReturnValue"]);
            if (code == 0)
            {
                message = $"Created restore point: {description}";
                return true;
            }

            message = $"System Restore returned code {code}. Enable System Restore for the system drive and retry.";
            return false;
        }
        catch (Exception ex)
        {
            message = $"Could not create a restore point: {ex.Message}";
            return false;
        }
    }
}
