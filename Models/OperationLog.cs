using System;
using System.Collections.Generic;

namespace FileOrganizer.Models;

public sealed class OperationLogEntry
{
    public OperationKind Kind { get; init; }
    public string SourcePath { get; init; } = string.Empty;
    public string AppliedTarget { get; init; } = string.Empty;
}

public sealed class OperationLog
{
    public DateTime TimestampUtc { get; init; } = DateTime.UtcNow;
    public string Description { get; init; } = string.Empty;
    public List<string> RootFolders { get; init; } = new();
    public List<OperationLogEntry> Entries { get; init; } = new();
}
