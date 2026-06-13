using System.Collections.Generic;

namespace FileOrganizer.Models;

public sealed class DuplicateGroup
{
    public long Size { get; init; }
    public string Hash { get; init; } = string.Empty;
    public List<string> Paths { get; init; } = new();

    public long WastedBytes => Size * (Paths.Count - 1);
}
