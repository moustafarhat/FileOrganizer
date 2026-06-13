using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using FileOrganizer.Models;

namespace FileOrganizer.ViewModels;

public partial class DuplicateFileViewModel : ObservableObject
{
    [ObservableProperty]
    private bool isSelectedForRemoval;

    public string Path { get; init; } = string.Empty;
    public string FileName => System.IO.Path.GetFileName(Path);
}

public sealed class DuplicateGroupViewModel
{
    public long Size { get; init; }
    public string Hash { get; init; } = string.Empty;
    public ObservableCollection<DuplicateFileViewModel> Files { get; } = new();

    public string Summary
    {
        get
        {
            var saved = Size * (Files.Count - 1);
            return $"{Files.Count} copies • {FormatBytes(Size)} each • {FormatBytes(saved)} recoverable • {Hash[..12]}…";
        }
    }

    public static DuplicateGroupViewModel From(DuplicateGroup group)
    {
        var vm = new DuplicateGroupViewModel { Size = group.Size, Hash = group.Hash };
        for (var i = 0; i < group.Paths.Count; i++)
        {
            vm.Files.Add(new DuplicateFileViewModel
            {
                Path = group.Paths[i],
                IsSelectedForRemoval = i != 0,
            });
        }
        return vm;
    }

    public IEnumerable<string> SelectedPaths() => Files.Where(f => f.IsSelectedForRemoval).Select(f => f.Path);

    private static string FormatBytes(long bytes)
    {
        string[] units = { "B", "KB", "MB", "GB", "TB" };
        double v = bytes;
        var u = 0;
        while (v >= 1024 && u < units.Length - 1) { v /= 1024; u++; }
        return $"{v:0.##} {units[u]}";
    }
}
