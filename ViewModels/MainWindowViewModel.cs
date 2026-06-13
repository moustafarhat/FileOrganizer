using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FileOrganizer.Models;
using FileOrganizer.Services;

namespace FileOrganizer.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly List<FolderWatcher> _watchers = new();

    [ObservableProperty]
    private string? selectedFolder;

    [ObservableProperty]
    private int selectedTabIndex;

    [ObservableProperty]
    private OrganizeMode organizeMode = OrganizeMode.ByType;

    [ObservableProperty]
    private DateGranularity dateGranularity = DateGranularity.YearMonth;

    [ObservableProperty]
    private DateSource dateSource = DateSource.Modified;

    [ObservableProperty]
    private bool recursive;

    [ObservableProperty]
    private RenameStyle renameStyle = RenameStyle.SnakeCase;

    [ObservableProperty]
    private string customPattern = "{date:yyyy-MM-dd}_{snake}{ext}";

    [ObservableProperty]
    private int sequenceStart = 1;

    [ObservableProperty]
    private int sequencePadding = 3;

    [ObservableProperty]
    private string statusText = "Add a folder and click Preview.";

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private bool canUndo;

    [ObservableProperty]
    private string undoLabel = "Undo last batch";

    [ObservableProperty]
    private bool isWatching;

    [ObservableProperty]
    private string newPresetName = string.Empty;

    [ObservableProperty]
    private Preset? selectedPreset;

    public ObservableCollection<string> Folders { get; } = new();
    public ObservableCollection<PlannedOperation> Operations { get; } = new();
    public ObservableCollection<DuplicateGroupViewModel> Duplicates { get; } = new();
    public ObservableCollection<Preset> Presets { get; } = new();

    public bool HasFolders => Folders.Count > 0;
    public bool HasNoFolders => Folders.Count == 0;

    public OrganizeMode[] OrganizeModes { get; } = (OrganizeMode[])Enum.GetValues(typeof(OrganizeMode));
    public DateGranularity[] DateGranularities { get; } = (DateGranularity[])Enum.GetValues(typeof(DateGranularity));
    public DateSource[] DateSources { get; } = (DateSource[])Enum.GetValues(typeof(DateSource));
    public RenameStyle[] RenameStyles { get; } = (RenameStyle[])Enum.GetValues(typeof(RenameStyle));

    public Func<Task<IReadOnlyList<string>>>? FolderPicker { get; set; }
    public Func<string, string, Task<bool>>? ConfirmDialog { get; set; }
    public Func<Task>? ShellIntegrationAction { get; set; }

    public MainWindowViewModel()
    {
        Folders.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(HasFolders));
            OnPropertyChanged(nameof(HasNoFolders));
            if (IsWatching) StopWatching();
        };
        RefreshUndoState();
        ReloadPresets();
    }

    public void AddFolder(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path)) return;
        var full = Path.GetFullPath(path);
        if (!Folders.Any(f => string.Equals(Path.GetFullPath(f), full, StringComparison.OrdinalIgnoreCase)))
            Folders.Add(full);
    }

    partial void OnSelectedPresetChanged(Preset? value)
    {
        if (value is null) return;
        Folders.Clear();
        foreach (var f in value.Folders) AddFolder(f);
        SelectedTabIndex = value.Tab;
        Recursive = value.Recursive;
        OrganizeMode = value.OrganizeMode;
        DateGranularity = value.DateGranularity;
        DateSource = value.DateSource;
        RenameStyle = value.RenameStyle;
        CustomPattern = value.CustomPattern;
        SequenceStart = value.SequenceStart;
        SequencePadding = value.SequencePadding;
        StatusText = $"Loaded preset '{value.Name}'.";
    }

    [RelayCommand]
    private async Task AddFoldersAsync()
    {
        if (FolderPicker is null) return;
        var picked = await FolderPicker();
        foreach (var p in picked) AddFolder(p);
    }

    [RelayCommand]
    private void RemoveFolder()
    {
        if (SelectedFolder is null) return;
        Folders.Remove(SelectedFolder);
    }

    [RelayCommand]
    private void ClearFolders() => Folders.Clear();

    [RelayCommand]
    private void Preview()
    {
        Operations.Clear();
        if (Folders.Count == 0)
        {
            StatusText = "Add at least one folder.";
            return;
        }

        try
        {
            IsBusy = true;
            var allOps = new List<PlannedOperation>();
            foreach (var folder in Folders)
            {
                if (!Directory.Exists(folder)) continue;
                var ops = SelectedTabIndex == 0 ? BuildOrganizePlan(folder) : BuildRenamePlan(folder);
                allOps.AddRange(ops);
            }

            foreach (var op in allOps) Operations.Add(op);
            StatusText = allOps.Count == 0
                ? "Nothing to do — no files match this plan."
                : $"{allOps.Count} operation(s) planned across {Folders.Count} folder(s).";
        }
        catch (Exception ex)
        {
            StatusText = $"Preview failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ApplyAsync()
    {
        if (Operations.Count == 0) return;

        try
        {
            IsBusy = true;
            StatusText = "Applying...";
            var description = SelectedTabIndex == 0 ? $"Organize {OrganizeMode}" : $"Rename {RenameStyle}";
            var roots = Folders.ToList();
            var log = await Task.Run(() => OperationExecutor.Execute(Operations, description, roots));

            var applied = Operations.Count(o => o.Status == OperationStatus.Applied);
            var failed = Operations.Count(o => o.Status == OperationStatus.Failed);

            if (log.Entries.Count > 0)
            {
                OperationLogStore.Save(log);
                RefreshUndoState();
            }

            StatusText = failed == 0
                ? $"Applied {applied} operation(s)."
                : $"Applied {applied}, {failed} failed. See list for details.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task UndoLastAsync()
    {
        var log = OperationLogStore.LoadLatest();
        if (log is null)
        {
            StatusText = "Nothing to undo.";
            return;
        }

        try
        {
            IsBusy = true;
            StatusText = $"Undoing {log.Entries.Count} operation(s)...";
            var (reversed, failed) = await Task.Run(() => OperationExecutor.Undo(log));
            OperationLogStore.DeleteLatest();
            RefreshUndoState();
            Operations.Clear();
            StatusText = failed == 0
                ? $"Undid {reversed} operation(s)."
                : $"Undid {reversed}, {failed} could not be reversed.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void Clear()
    {
        Operations.Clear();
        Duplicates.Clear();
        StatusText = "Cleared.";
    }

    [RelayCommand]
    private async Task ScanDuplicatesAsync()
    {
        Duplicates.Clear();
        if (Folders.Count == 0)
        {
            StatusText = "Add at least one folder.";
            return;
        }

        try
        {
            IsBusy = true;
            StatusText = "Scanning for duplicates...";
            var roots = Folders.ToList();
            var groups = await Task.Run(() => DuplicateFinder.Find(roots, Recursive,
                (done, total) => Dispatcher.UIThread.Post(() => StatusText = $"Hashing... {done}/{total}")));

            foreach (var g in groups) Duplicates.Add(DuplicateGroupViewModel.From(g));

            var totalWasted = groups.Sum(g => g.WastedBytes);
            StatusText = groups.Count == 0
                ? "No duplicates found."
                : $"Found {groups.Count} duplicate group(s) — {FormatBytes(totalWasted)} recoverable.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task RemoveDuplicatesAsync()
    {
        var selected = Duplicates.SelectMany(g => g.SelectedPaths()).ToList();
        if (selected.Count == 0)
        {
            StatusText = "No duplicates selected.";
            return;
        }

        if (ConfirmDialog is not null)
        {
            var ok = await ConfirmDialog(
                "Move duplicates to quarantine?",
                $"{selected.Count} file(s) will be moved to a '_Duplicates_<timestamp>' folder inside their containing root.\n\nThey can be restored via Undo last batch.");
            if (!ok) return;
        }

        try
        {
            IsBusy = true;
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var roots = Folders.ToList();

            var ops = selected.Select(p =>
            {
                var root = GetContainingRoot(p, roots) ?? Path.GetDirectoryName(p) ?? string.Empty;
                var quarantine = Path.Combine(root, $"_Duplicates_{timestamp}");
                return new PlannedOperation
                {
                    Kind = OperationKind.Move,
                    SourcePath = p,
                    TargetPath = Path.Combine(quarantine, Path.GetFileName(p)),
                    TargetRelative = Path.Combine($"_Duplicates_{timestamp}", Path.GetFileName(p)),
                };
            }).ToList();

            var log = await Task.Run(() => OperationExecutor.Execute(ops, "Quarantine duplicates", roots));
            if (log.Entries.Count > 0)
            {
                OperationLogStore.Save(log);
                RefreshUndoState();
            }

            var moved = ops.Count(o => o.Status == OperationStatus.Applied);
            StatusText = $"Moved {moved} duplicate(s) to quarantine. Undo to restore.";
            Duplicates.Clear();
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void SavePreset()
    {
        if (string.IsNullOrWhiteSpace(NewPresetName))
        {
            StatusText = "Enter a preset name first.";
            return;
        }

        var preset = new Preset
        {
            Name = NewPresetName.Trim(),
            Folders = Folders.ToList(),
            Tab = SelectedTabIndex,
            Recursive = Recursive,
            OrganizeMode = OrganizeMode,
            DateGranularity = DateGranularity,
            DateSource = DateSource,
            RenameStyle = RenameStyle,
            CustomPattern = CustomPattern,
            SequenceStart = SequenceStart,
            SequencePadding = SequencePadding,
        };
        PresetStore.Save(preset);
        ReloadPresets();
        SelectedPreset = Presets.FirstOrDefault(p => p.Name == preset.Name);
        NewPresetName = string.Empty;
        StatusText = $"Saved preset '{preset.Name}'.";
    }

    [RelayCommand]
    private void DeletePreset()
    {
        if (SelectedPreset is null) return;
        var name = SelectedPreset.Name;
        PresetStore.Delete(name);
        ReloadPresets();
        StatusText = $"Deleted preset '{name}'.";
    }

    [RelayCommand]
    private void ToggleWatch()
    {
        if (IsWatching) StopWatching();
        else StartWatching();
    }

    [RelayCommand]
    private async Task ShellIntegrationAsync()
    {
        if (ShellIntegrationAction is not null) await ShellIntegrationAction();
    }

    private void StartWatching()
    {
        if (Folders.Count == 0)
        {
            StatusText = "Add at least one folder before watching.";
            return;
        }
        if (SelectedTabIndex != 0)
        {
            StatusText = "Watch only auto-organizes (use the Organize tab).";
            return;
        }

        foreach (var folder in Folders.ToList())
        {
            if (!Directory.Exists(folder)) continue;
            var root = folder;
            var watcher = new FolderWatcher(
                onFileReady: path => Dispatcher.UIThread.Post(() => HandleWatchedFile(path, root)),
                onError: msg => Dispatcher.UIThread.Post(() => StatusText = msg));
            try
            {
                watcher.Start(root, Recursive);
                _watchers.Add(watcher);
            }
            catch (Exception ex)
            {
                StatusText = $"Could not start watching {root}: {ex.Message}";
                watcher.Dispose();
            }
        }

        if (_watchers.Count > 0)
        {
            IsWatching = true;
            StatusText = $"Watching {_watchers.Count} folder(s) — new files will be auto-organized.";
        }
    }

    private void StopWatching()
    {
        foreach (var w in _watchers) w.Dispose();
        _watchers.Clear();
        IsWatching = false;
        StatusText = "Stopped watching.";
    }

    private void HandleWatchedFile(string path, string root)
    {
        var options = new OrganizeOptions
        {
            Mode = OrganizeMode,
            DateGranularity = DateGranularity,
            DateSource = DateSource,
            Recursive = Recursive,
        };
        var op = OperationPlanner.PlanOneOrganize(path, root, options);
        if (op is null) return;

        var log = OperationExecutor.Execute(new[] { op }, $"Auto-organize {Path.GetFileName(path)}", new[] { root });
        if (log.Entries.Count > 0)
        {
            OperationLogStore.Save(log);
            RefreshUndoState();
            StatusText = $"Auto-organized: {Path.GetFileName(path)} → {op.TargetRelative}";
        }
    }

    private static string? GetContainingRoot(string filePath, IEnumerable<string> roots)
    {
        var full = Path.GetFullPath(filePath);
        return roots
            .Select(Path.GetFullPath)
            .Where(r => full.StartsWith(r + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                     || full.StartsWith(r + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(r => r.Length)
            .FirstOrDefault();
    }

    private List<PlannedOperation> BuildOrganizePlan(string folder) =>
        OperationPlanner.PlanOrganize(folder, new OrganizeOptions
        {
            Mode = OrganizeMode,
            DateGranularity = DateGranularity,
            DateSource = DateSource,
            Recursive = Recursive,
        });

    private List<PlannedOperation> BuildRenamePlan(string folder) =>
        OperationPlanner.PlanRename(folder, new RenameOptions
        {
            Style = RenameStyle,
            CustomPattern = CustomPattern,
            SequenceStart = SequenceStart,
            SequencePadding = SequencePadding,
        }, Recursive);

    private void RefreshUndoState()
    {
        var log = OperationLogStore.LoadLatest();
        CanUndo = log is { Entries.Count: > 0 };
        UndoLabel = log is null
            ? "Undo last batch"
            : $"Undo last batch ({log.Entries.Count} op(s), {log.Description})";
    }

    private void ReloadPresets()
    {
        Presets.Clear();
        foreach (var p in PresetStore.LoadAll()) Presets.Add(p);
    }

    private static string FormatBytes(long bytes)
    {
        string[] units = { "B", "KB", "MB", "GB", "TB" };
        double v = bytes;
        var u = 0;
        while (v >= 1024 && u < units.Length - 1) { v /= 1024; u++; }
        return $"{v:0.##} {units[u]}";
    }
}
