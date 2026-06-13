using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace FileOrganizer.Services;

public sealed class FolderWatcher : IDisposable
{
    private FileSystemWatcher? _watcher;
    private readonly ConcurrentQueue<string> _pending = new();
    private CancellationTokenSource? _cts;
    private Task? _worker;
    private readonly Action<string> _onFileReady;
    private readonly Action<string> _onError;

    public FolderWatcher(Action<string> onFileReady, Action<string> onError)
    {
        _onFileReady = onFileReady;
        _onError = onError;
    }

    public bool IsWatching => _watcher is { EnableRaisingEvents: true };

    public void Start(string folder, bool recursive)
    {
        Stop();
        if (!Directory.Exists(folder)) throw new DirectoryNotFoundException(folder);

        _cts = new CancellationTokenSource();
        _watcher = new FileSystemWatcher(folder)
        {
            IncludeSubdirectories = recursive,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite,
        };
        _watcher.Created += OnFsEvent;
        _watcher.Renamed += OnFsEvent;
        _watcher.Error += (_, e) => _onError($"Watcher error: {e.GetException().Message}");
        _watcher.EnableRaisingEvents = true;

        _worker = Task.Run(() => ProcessQueue(_cts.Token));
    }

    public void Stop()
    {
        if (_watcher is not null)
        {
            _watcher.EnableRaisingEvents = false;
            _watcher.Dispose();
            _watcher = null;
        }
        _cts?.Cancel();
        try { _worker?.Wait(500); } catch { }
        _cts?.Dispose();
        _cts = null;
        _worker = null;
        while (_pending.TryDequeue(out _)) { }
    }

    public void Dispose() => Stop();

    private void OnFsEvent(object sender, FileSystemEventArgs e)
    {
        if (File.Exists(e.FullPath)) _pending.Enqueue(e.FullPath);
    }

    private async Task ProcessQueue(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            if (_pending.TryDequeue(out var path))
            {
                if (await WaitForFileReady(path, token))
                {
                    try { _onFileReady(path); }
                    catch (Exception ex) { _onError($"Auto-organize failed for {Path.GetFileName(path)}: {ex.Message}"); }
                }
            }
            else
            {
                try { await Task.Delay(200, token); } catch (TaskCanceledException) { return; }
            }
        }
    }

    private static async Task<bool> WaitForFileReady(string path, CancellationToken token)
    {
        for (var i = 0; i < 30; i++)
        {
            if (token.IsCancellationRequested) return false;
            if (!File.Exists(path)) return false;
            try
            {
                using var fs = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                return true;
            }
            catch (IOException)
            {
                try { await Task.Delay(200, token); } catch (TaskCanceledException) { return false; }
            }
        }
        return false;
    }
}
