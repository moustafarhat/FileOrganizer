using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using FileOrganizer.Models;

namespace FileOrganizer.Services;

public static class OperationLogStore
{
    private const int MaxKept = 20;

    public static string LogDir { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "FileOrganizer", "history");

    public static string Save(OperationLog log)
    {
        Directory.CreateDirectory(LogDir);
        var name = log.TimestampUtc.ToString("yyyyMMdd_HHmmss_fff") + ".json";
        var path = Path.Combine(LogDir, name);
        File.WriteAllText(path, JsonSerializer.Serialize(log, JsonOpts));
        PruneOldLogs();
        return path;
    }

    public static OperationLog? LoadLatest()
    {
        if (!Directory.Exists(LogDir)) return null;
        var newest = Directory.EnumerateFiles(LogDir, "*.json")
            .Select(f => new FileInfo(f))
            .OrderByDescending(f => f.LastWriteTimeUtc)
            .FirstOrDefault();
        return newest is null ? null : Load(newest.FullName);
    }

    public static OperationLog? Load(string path)
    {
        try
        {
            var text = File.ReadAllText(path);
            return JsonSerializer.Deserialize<OperationLog>(text, JsonOpts);
        }
        catch
        {
            return null;
        }
    }

    public static void DeleteLatest()
    {
        if (!Directory.Exists(LogDir)) return;
        var newest = Directory.EnumerateFiles(LogDir, "*.json")
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault();
        if (newest is not null) File.Delete(newest);
    }

    private static void PruneOldLogs()
    {
        var files = Directory.EnumerateFiles(LogDir, "*.json")
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .ToList();
        foreach (var stale in files.Skip(MaxKept))
        {
            try { File.Delete(stale); } catch { }
        }
    }

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
    };
}
