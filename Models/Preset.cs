using System.Collections.Generic;

namespace FileOrganizer.Models;

public sealed class Preset
{
    public string Name { get; set; } = string.Empty;
    public List<string> Folders { get; set; } = new();
    public int Tab { get; set; }
    public bool Recursive { get; set; }

    public OrganizeMode OrganizeMode { get; set; } = OrganizeMode.ByType;
    public DateGranularity DateGranularity { get; set; } = DateGranularity.YearMonth;
    public DateSource DateSource { get; set; } = DateSource.Modified;

    public RenameStyle RenameStyle { get; set; } = RenameStyle.SnakeCase;
    public string CustomPattern { get; set; } = "{name}{ext}";
    public int SequenceStart { get; set; } = 1;
    public int SequencePadding { get; set; } = 3;
}
