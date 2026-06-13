using System;
using System.Collections.Generic;
using System.IO;
using FileOrganizer.Models;

namespace FileOrganizer.Services;

public static class Categorizer
{
    private static readonly Dictionary<string, string> TypeMap = BuildTypeMap();

    public static string ByType(FileInfo file)
    {
        var ext = file.Extension.TrimStart('.').ToLowerInvariant();
        return TypeMap.TryGetValue(ext, out var category) ? category : "Other";
    }

    public static string BySize(FileInfo file)
    {
        var bytes = file.Length;
        const long KB = 1024;
        const long MB = KB * 1024;
        const long GB = MB * 1024;

        return bytes switch
        {
            < 1 * MB => "Tiny (<1MB)",
            < 10 * MB => "Small (1-10MB)",
            < 100 * MB => "Medium (10-100MB)",
            < 1 * GB => "Large (100MB-1GB)",
            _ => "Huge (>=1GB)",
        };
    }

    public static string ByDate(FileInfo file, DateGranularity granularity, DateSource source)
    {
        var when = source == DateSource.Created ? file.CreationTime : file.LastWriteTime;
        return granularity == DateGranularity.YearMonth
            ? when.ToString("yyyy-MM")
            : when.ToString("yyyy");
    }

    private static Dictionary<string, string> BuildTypeMap()
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        void Add(string category, params string[] exts)
        {
            foreach (var e in exts) map[e] = category;
        }

        Add("Images", "jpg", "jpeg", "png", "gif", "bmp", "svg", "webp", "tiff", "tif", "heic", "ico", "raw");
        Add("Documents", "pdf", "doc", "docx", "txt", "md", "rtf", "odt", "xls", "xlsx", "ppt", "pptx", "csv", "tsv", "epub");
        Add("Audio", "mp3", "wav", "flac", "ogg", "m4a", "aac", "wma", "opus");
        Add("Video", "mp4", "avi", "mkv", "mov", "wmv", "flv", "webm", "m4v", "mpg", "mpeg");
        Add("Archives", "zip", "rar", "7z", "tar", "gz", "bz2", "xz", "tgz", "iso");
        Add("Code", "cs", "py", "js", "ts", "tsx", "jsx", "java", "kt", "cpp", "c", "h", "hpp", "go", "rs", "rb", "php", "swift", "dart", "html", "htm", "css", "scss", "less", "json", "xml", "yaml", "yml", "toml", "ini", "sh", "ps1", "bat", "cmd", "sql");
        Add("Executables", "exe", "msi", "dmg", "pkg", "deb", "rpm", "appimage", "app");
        Add("Fonts", "ttf", "otf", "woff", "woff2", "eot");

        return map;
    }
}
