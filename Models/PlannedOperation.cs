namespace FileOrganizer.Models;

public enum OperationKind
{
    Move,
    Rename,
}

public enum OperationStatus
{
    Pending,
    Applied,
    Skipped,
    Failed,
}

public sealed class PlannedOperation
{
    public required OperationKind Kind { get; init; }
    public required string SourcePath { get; init; }
    public required string TargetPath { get; init; }
    public OperationStatus Status { get; set; } = OperationStatus.Pending;
    public string? Error { get; set; }

    public string SourceName => System.IO.Path.GetFileName(SourcePath);
    public string TargetRelative { get; init; } = string.Empty;
}
