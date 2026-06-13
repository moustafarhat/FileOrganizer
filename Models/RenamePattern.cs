namespace FileOrganizer.Models;

public enum RenameStyle
{
    SnakeCase,
    KebabCase,
    PascalCase,
    CamelCase,
    LowerCase,
    UpperCase,
    DatePrefix,
    SequentialPrefix,
    Custom,
}

public sealed class RenameOptions
{
    public RenameStyle Style { get; set; } = RenameStyle.SnakeCase;
    public string CustomPattern { get; set; } = "{name}{ext}";
    public bool IncludeFolders { get; set; }
    public int SequenceStart { get; set; } = 1;
    public int SequencePadding { get; set; } = 3;
}
