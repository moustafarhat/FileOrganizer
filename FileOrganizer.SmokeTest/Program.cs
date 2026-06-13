using System;
using System.IO;
using System.Linq;
using FileOrganizer.Models;
using FileOrganizer.Services;

var workspace = Path.Combine(Path.GetTempPath(), "fo_smoke_" + Guid.NewGuid().ToString("N").Substring(0, 8));
var rootA = Path.Combine(workspace, "FolderA");
var rootB = Path.Combine(workspace, "FolderB");
Directory.CreateDirectory(rootA);
Directory.CreateDirectory(rootB);
Console.WriteLine($"Workspace: {workspace}");

int passed = 0, failed = 0;
void Check(string label, bool ok, string? detail = null)
{
    if (ok) { passed++; Console.WriteLine($"  PASS  {label}"); }
    else { failed++; Console.WriteLine($"  FAIL  {label}  {detail}"); }
}

// --- Seed both folders with files ---
var aFiles = new (string name, int sizeBytes)[]
{
    ("a_photo.JPG", 500_000),
    ("a_song.mp3", 5_000_000),
    ("a_notes.txt", 1_000),
};
var bFiles = new (string name, int sizeBytes)[]
{
    ("b_doc.pdf", 200_000),
    ("b_movie.mp4", 100_000_000),
    ("b_script.py", 3_000),
};
foreach (var (name, size) in aFiles)
{
    var path = Path.Combine(rootA, name);
    using var fs = File.Create(path);
    fs.SetLength(size);
    File.SetLastWriteTime(path, new DateTime(2026, 5, 1));
}
foreach (var (name, size) in bFiles)
{
    var path = Path.Combine(rootB, name);
    using var fs = File.Create(path);
    fs.SetLength(size);
    File.SetLastWriteTime(path, new DateTime(2026, 5, 1));
}

Console.WriteLine("\n== Multi-folder organize by type ==");
var planA = OperationPlanner.PlanOrganize(rootA, new OrganizeOptions { Mode = OrganizeMode.ByType });
var planB = OperationPlanner.PlanOrganize(rootB, new OrganizeOptions { Mode = OrganizeMode.ByType });
var combined = planA.Concat(planB).ToList();
Check("6 ops planned across 2 folders", combined.Count == 6);
Check("FolderA has Images bucket", combined.Any(o => o.SourcePath.StartsWith(rootA) && o.TargetRelative.StartsWith("Images")));
Check("FolderB has Code bucket", combined.Any(o => o.SourcePath.StartsWith(rootB) && o.TargetRelative.StartsWith("Code")));

var multiLog = OperationExecutor.Execute(combined, "Multi-folder organize", new[] { rootA, rootB });
Check("multi-folder log records 6 entries", multiLog.Entries.Count == 6);
Check("multi-folder log records 2 roots", multiLog.RootFolders.Count == 2);
Check("a_photo.JPG in FolderA/Images", File.Exists(Path.Combine(rootA, "Images", "a_photo.JPG")));
Check("b_script.py in FolderB/Code", File.Exists(Path.Combine(rootB, "Code", "b_script.py")));

Console.WriteLine("\n== Undo multi-folder ==");
var (rev, undoFail) = OperationExecutor.Undo(multiLog);
Check("6 reversed", rev == 6 && undoFail == 0);
Check("a_photo.JPG back at FolderA root", File.Exists(Path.Combine(rootA, "a_photo.JPG")));
Check("b_script.py back at FolderB root", File.Exists(Path.Combine(rootB, "b_script.py")));
Check("FolderA/Images cleaned", !Directory.Exists(Path.Combine(rootA, "Images")));
Check("FolderB/Code cleaned", !Directory.Exists(Path.Combine(rootB, "Code")));

Console.WriteLine("\n== Cross-folder duplicate finder ==");
var payload = new byte[4096];
new Random(99).NextBytes(payload);
File.WriteAllBytes(Path.Combine(rootA, "shared.bin"), payload);
File.WriteAllBytes(Path.Combine(rootB, "shared.bin"), payload);
File.WriteAllBytes(Path.Combine(rootA, "unique_a.bin"), new byte[4096]);

var dupGroups = DuplicateFinder.Find(new[] { rootA, rootB }, recursive: true);
var cross = dupGroups.FirstOrDefault(g => g.Paths.Count == 2 && g.Paths.Any(p => p.StartsWith(rootA)) && g.Paths.Any(p => p.StartsWith(rootB)));
Check("cross-folder duplicate detected", cross is not null);
Check("cross-folder group has wasted bytes = 4096", cross?.WastedBytes == 4096);

Console.WriteLine("\n== Multi-folder quarantine + undo ==");
var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
var quarantineOps = (cross?.Paths ?? new()).Skip(1).Select(p =>
{
    var root = p.StartsWith(rootA) ? rootA : rootB;
    var quarantine = Path.Combine(root, $"_Duplicates_{timestamp}");
    return new PlannedOperation
    {
        Kind = OperationKind.Move,
        SourcePath = p,
        TargetPath = Path.Combine(quarantine, Path.GetFileName(p)),
        TargetRelative = Path.Combine($"_Duplicates_{timestamp}", Path.GetFileName(p)),
    };
}).ToList();
var qLog = OperationExecutor.Execute(quarantineOps, "Cross-folder quarantine", new[] { rootA, rootB });
Check("1 cross-folder duplicate quarantined", qLog.Entries.Count == 1);

var (qRev, qFail) = OperationExecutor.Undo(qLog);
Check("quarantine undo reverses", qRev == 1 && qFail == 0);

Console.WriteLine("\n== Preset with multi-folder ==");
var preset = new Preset
{
    Name = "downloads-plus-screenshots",
    Folders = new() { rootA, rootB },
    Tab = 0,
    Recursive = true,
    OrganizeMode = OrganizeMode.ByType,
};
var json = System.Text.Json.JsonSerializer.Serialize(preset);
var rehydrated = System.Text.Json.JsonSerializer.Deserialize<Preset>(json);
Check("preset round-trips with 2 folders", rehydrated?.Folders.Count == 2);
Check("preset preserves first folder", rehydrated?.Folders[0] == rootA);

Console.WriteLine("\n== Single-file plan (watch path) for each root ==");
var bPhoto = Path.Combine(rootB, "b_doc.pdf");
var oneOp = OperationPlanner.PlanOneOrganize(bPhoto, rootB, new OrganizeOptions { Mode = OrganizeMode.ByType });
Check("single-file plan respects per-folder root",
    oneOp is not null && oneOp.TargetPath.StartsWith(Path.Combine(rootB, "Documents")));

Console.WriteLine("\n== Shell integration support flag ==");
Check("IsSupported true on Windows", ShellIntegrationService.IsSupported == OperatingSystem.IsWindows());
if (OperatingSystem.IsWindows())
{
    var before = ShellIntegrationService.IsRegistered();
    try
    {
        ShellIntegrationService.Register();
        Check("after Register, IsRegistered=true", ShellIntegrationService.IsRegistered());
        ShellIntegrationService.Unregister();
        Check("after Unregister, IsRegistered=false", !ShellIntegrationService.IsRegistered());
    }
    finally
    {
        if (before) { try { ShellIntegrationService.Register(); } catch { } }
        else { try { ShellIntegrationService.Unregister(); } catch { } }
    }
}
else
{
    Check("IsRegistered=false on non-Windows", !ShellIntegrationService.IsRegistered());
}

Console.WriteLine($"\nResults: {passed} passed, {failed} failed");
try { Directory.Delete(workspace, recursive: true); } catch { }
Environment.Exit(failed == 0 ? 0 : 1);
