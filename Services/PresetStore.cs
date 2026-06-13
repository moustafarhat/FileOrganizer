using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using FileOrganizer.Models;

namespace FileOrganizer.Services;

public static class PresetStore
{
    public static string PresetDir { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "FileOrganizer", "presets");

    public static List<Preset> LoadAll()
    {
        if (!Directory.Exists(PresetDir)) return new();
        var presets = new List<Preset>();
        foreach (var file in Directory.EnumerateFiles(PresetDir, "*.json"))
        {
            try
            {
                var preset = JsonSerializer.Deserialize<Preset>(File.ReadAllText(file), JsonOpts);
                if (preset is not null) presets.Add(preset);
            }
            catch { }
        }
        return presets.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public static void Save(Preset preset)
    {
        Directory.CreateDirectory(PresetDir);
        var path = Path.Combine(PresetDir, Sanitize(preset.Name) + ".json");
        File.WriteAllText(path, JsonSerializer.Serialize(preset, JsonOpts));
    }

    public static void Delete(string name)
    {
        var path = Path.Combine(PresetDir, Sanitize(name) + ".json");
        if (File.Exists(path)) File.Delete(path);
    }

    private static string Sanitize(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return string.Concat(name.Select(c => invalid.Contains(c) ? '_' : c)).Trim();
    }

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
    };
}
