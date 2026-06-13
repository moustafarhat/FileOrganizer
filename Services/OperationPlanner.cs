using System.Collections.Generic;
using System.IO;
using System.Linq;
using FileOrganizer.Models;

namespace FileOrganizer.Services;

public sealed class OrganizeOptions
{
    public OrganizeMode Mode { get; set; } = OrganizeMode.ByType;
    public DateGranularity DateGranularity { get; set; } = DateGranularity.YearMonth;
    public DateSource DateSource { get; set; } = DateSource.Modified;
    public bool Recursive { get; set; }
}

public static class OperationPlanner
{
    public static List<PlannedOperation> PlanOrganize(string rootFolder, OrganizeOptions options)
    {
        var ops = new List<PlannedOperation>();
        if (string.IsNullOrEmpty(rootFolder) || !Directory.Exists(rootFolder)) return ops;

        var searchOption = options.Recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        var files = Directory.EnumerateFiles(rootFolder, "*", searchOption);

        foreach (var path in files)
        {
            var info = new FileInfo(path);
            var bucket = options.Mode switch
            {
                OrganizeMode.ByType => Categorizer.ByType(info),
                OrganizeMode.BySize => Categorizer.BySize(info),
                OrganizeMode.ByDate => Categorizer.ByDate(info, options.DateGranularity, options.DateSource),
                _ => "Other",
            };

            var targetDir = Path.Combine(rootFolder, bucket);
            var targetPath = Path.Combine(targetDir, info.Name);

            if (string.Equals(Path.GetDirectoryName(path), targetDir, System.StringComparison.OrdinalIgnoreCase))
                continue;

            ops.Add(new PlannedOperation
            {
                Kind = OperationKind.Move,
                SourcePath = path,
                TargetPath = targetPath,
                TargetRelative = Path.Combine(bucket, info.Name),
            });
        }

        return ops;
    }

    public static PlannedOperation? PlanOneOrganize(string filePath, string rootFolder, OrganizeOptions options)
    {
        if (!File.Exists(filePath)) return null;

        var info = new FileInfo(filePath);
        var bucket = options.Mode switch
        {
            OrganizeMode.ByType => Categorizer.ByType(info),
            OrganizeMode.BySize => Categorizer.BySize(info),
            OrganizeMode.ByDate => Categorizer.ByDate(info, options.DateGranularity, options.DateSource),
            _ => "Other",
        };

        var targetDir = Path.Combine(rootFolder, bucket);
        if (string.Equals(Path.GetDirectoryName(filePath), targetDir, System.StringComparison.OrdinalIgnoreCase))
            return null;

        return new PlannedOperation
        {
            Kind = OperationKind.Move,
            SourcePath = filePath,
            TargetPath = Path.Combine(targetDir, info.Name),
            TargetRelative = Path.Combine(bucket, info.Name),
        };
    }

    public static List<PlannedOperation> PlanRename(string rootFolder, RenameOptions options, bool recursive)
    {
        var ops = new List<PlannedOperation>();
        if (string.IsNullOrEmpty(rootFolder) || !Directory.Exists(rootFolder)) return ops;

        var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        var files = Directory.EnumerateFiles(rootFolder, "*", searchOption).ToList();

        for (var i = 0; i < files.Count; i++)
        {
            var info = new FileInfo(files[i]);
            var newName = Renamer.Apply(info, options, i);
            if (string.Equals(newName, info.Name, System.StringComparison.Ordinal)) continue;

            var targetPath = Path.Combine(info.DirectoryName ?? rootFolder, newName);
            ops.Add(new PlannedOperation
            {
                Kind = OperationKind.Rename,
                SourcePath = info.FullName,
                TargetPath = targetPath,
                TargetRelative = newName,
            });
        }

        return ops;
    }
}
