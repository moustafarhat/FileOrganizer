namespace FileOrganizer.Models;

public enum OrganizeMode
{
    ByType,
    BySize,
    ByDate,
}

public enum DateGranularity
{
    Year,
    YearMonth,
}

public enum DateSource
{
    Modified,
    Created,
}
