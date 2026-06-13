using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using FileOrganizer.Models;

namespace FileOrganizer.Services;

public static class DuplicateFinder
{
    public static List<DuplicateGroup> Find(string root, bool recursive, Action<int, int>? progress = null)
        => Find(new[] { root }, recursive, progress);

    public static List<DuplicateGroup> Find(IEnumerable<string> roots, bool recursive, Action<int, int>? progress = null)
    {
        var results = new List<DuplicateGroup>();
        var option = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;

        var allFiles = roots
            .Where(r => !string.IsNullOrEmpty(r) && Directory.Exists(r))
            .SelectMany(r =>
            {
                try { return Directory.EnumerateFiles(r, "*", option); }
                catch { return Enumerable.Empty<string>(); }
            })
            .Select(p =>
            {
                try { return new FileInfo(p); }
                catch { return null; }
            })
            .Where(f => f is { Length: > 0 })
            .Cast<FileInfo>()
            .GroupBy(f => f.FullName, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList();

        var bySize = allFiles
            .GroupBy(f => f.Length)
            .Where(g => g.Count() > 1)
            .ToList();

        var total = bySize.Sum(g => g.Count());
        var done = 0;

        foreach (var sizeGroup in bySize)
        {
            var byHash = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            foreach (var file in sizeGroup)
            {
                string hash;
                try { hash = HashFile(file.FullName); }
                catch { done++; progress?.Invoke(done, total); continue; }

                if (!byHash.TryGetValue(hash, out var list))
                {
                    list = new List<string>();
                    byHash[hash] = list;
                }
                list.Add(file.FullName);
                done++;
                progress?.Invoke(done, total);
            }

            foreach (var (hash, paths) in byHash)
            {
                if (paths.Count > 1)
                {
                    results.Add(new DuplicateGroup
                    {
                        Size = sizeGroup.Key,
                        Hash = hash,
                        Paths = paths.OrderBy(p => p.Length).ThenBy(p => p, StringComparer.Ordinal).ToList(),
                    });
                }
            }
        }

        return results.OrderByDescending(g => g.WastedBytes).ToList();
    }

    private static string HashFile(string path)
    {
        using var sha = SHA256.Create();
        using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        var bytes = sha.ComputeHash(stream);
        return Convert.ToHexString(bytes);
    }
}
