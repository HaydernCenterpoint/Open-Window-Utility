using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenWindowUtility.Core.Operations;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(RegistrySetOperation), "registrySet")]
[JsonDerivedType(typeof(RegistryDeleteOperation), "registryDelete")]
[JsonDerivedType(typeof(ServiceStartupOperation), "serviceStartup")]
[JsonDerivedType(typeof(ScheduledTaskOperation), "scheduledTask")]
[JsonDerivedType(typeof(AppxRemoveOperation), "appxRemove")]
[JsonDerivedType(typeof(ProcessOperation), "process")]
[JsonDerivedType(typeof(DismFeatureOperation), "dismFeature")]
[JsonDerivedType(typeof(PowerPlanOperation), "powerPlan")]
[JsonDerivedType(typeof(CreateRestorePointOperation), "createRestorePoint")]
public abstract class Operation;

public sealed class RegistrySetOperation : Operation
{
    public required string Hive { get; init; }
    public required string Path { get; init; }
    public required string Name { get; init; }
    public required string ValueKind { get; init; }
    public required JsonElement Value { get; init; }
}

public sealed class RegistryDeleteOperation : Operation
{
    public required string Hive { get; init; }
    public required string Path { get; init; }
    public required string Name { get; init; }
}

public sealed class ServiceStartupOperation : Operation
{
    public required string ServiceName { get; init; }
    public required string StartupType { get; init; }
    public bool Stop { get; init; }
    public bool Start { get; init; }
}

public sealed class ScheduledTaskOperation : Operation
{
    public required string TaskPath { get; init; }
    public required string State { get; init; }
}

public sealed class AppxRemoveOperation : Operation
{
    public required string PackageName { get; init; }
}

public sealed class ProcessOperation : Operation
{
    public required string FileName { get; init; }
    public string Arguments { get; init; } = "";
    public bool WaitForExit { get; init; } = true;
    public bool UseShellExecute { get; init; }
}

public sealed class DismFeatureOperation : Operation
{
    public required string FeatureName { get; init; }
    public bool Enable { get; init; } = true;
}

public sealed class PowerPlanOperation : Operation
{
    public required string SchemeGuid { get; init; }
}

public sealed class CreateRestorePointOperation : Operation
{
    public string Description { get; init; } = "Open Window Utility";
}
