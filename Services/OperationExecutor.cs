using System.Collections.Generic;
using System.IO;
using System.Linq;
using FileOrganizer.Models;

namespace FileOrganizer.Services;

public static class OperationExecutor
{
    public static OperationLog Execute(IEnumerable<PlannedOperation> operations, string description, IEnumerable<string> rootFolders)
    {
        var log = new OperationLog
        {
            Description = description,
            RootFolders = rootFolders.Distinct(System.StringComparer.OrdinalIgnoreCase).ToList(),
        };

        foreach (var op in operations)
        {
            if (op.Status != OperationStatus.Pending) continue;

            try
            {
                var targetDir = Path.GetDirectoryName(op.TargetPath);
                if (!string.IsNullOrEmpty(targetDir)) Directory.CreateDirectory(targetDir);

                var finalTarget = ResolveCollision(op.SourcePath, op.TargetPath);
                File.Move(op.SourcePath, finalTarget);
                op.Status = OperationStatus.Applied;

                log.Entries.Add(new OperationLogEntry
                {
                    Kind = op.Kind,
                    SourcePath = op.SourcePath,
                    AppliedTarget = finalTarget,
                });
            }
            catch (System.Exception ex)
            {
                op.Status = OperationStatus.Failed;
                op.Error = ex.Message;
            }
        }

        return log;
    }

    public static (int Reversed, int Failed) Undo(OperationLog log)
    {
        var reversed = 0;
        var failed = 0;

        for (var i = log.Entries.Count - 1; i >= 0; i--)
        {
            var entry = log.Entries[i];
            try
            {
                if (!File.Exists(entry.AppliedTarget)) { failed++; continue; }

                var sourceDir = Path.GetDirectoryName(entry.SourcePath);
                if (!string.IsNullOrEmpty(sourceDir)) Directory.CreateDirectory(sourceDir);

                var restoreTo = ResolveCollision(entry.AppliedTarget, entry.SourcePath);
                File.Move(entry.AppliedTarget, restoreTo);
                reversed++;
            }
            catch
            {
                failed++;
            }
        }

        foreach (var root in log.RootFolders) DeleteEmptyChildren(root);
        return (reversed, failed);
    }

    private static void DeleteEmptyChildren(string root)
    {
        if (!Directory.Exists(root)) return;
        foreach (var dir in Directory.EnumerateDirectories(root, "*", SearchOption.AllDirectories).OrderByDescending(d => d.Length))
        {
            try
            {
                if (!Directory.EnumerateFileSystemEntries(dir).Any()) Directory.Delete(dir);
            }
            catch { }
        }
    }

    private static string ResolveCollision(string sourcePath, string targetPath)
    {
        if (!File.Exists(targetPath)) return targetPath;

        if (string.Equals(Path.GetFullPath(sourcePath), Path.GetFullPath(targetPath), System.StringComparison.OrdinalIgnoreCase))
            return targetPath;

        var dir = Path.GetDirectoryName(targetPath) ?? string.Empty;
        var nameNoExt = Path.GetFileNameWithoutExtension(targetPath);
        var ext = Path.GetExtension(targetPath);

        for (var i = 1; i < 10_000; i++)
        {
            var candidate = Path.Combine(dir, $"{nameNoExt} ({i}){ext}");
            if (!File.Exists(candidate)) return candidate;
        }
        return targetPath;
    }
}
