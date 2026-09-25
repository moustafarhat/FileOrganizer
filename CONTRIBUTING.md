# Contributing to File Organizer

Thanks for your interest! Bug reports, ideas and pull requests are all welcome.

## Reporting bugs and ideas

Use the [issue forms](https://github.com/moustafarhat/FileOrganizer/issues/new/choose). For bugs, include your OS, the app version and the steps to reproduce. Security problems go through [private reporting](SECURITY.md), not public issues.

## Getting set up

You need the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).

```sh
git clone https://github.com/moustafarhat/FileOrganizer.git
cd FileOrganizer
dotnet run                                   # start the app
dotnet run --project FileOrganizer.SmokeTest # run the smoke tests
```

## Where things live

- `Services/`: all file logic (planning, moving, undo, duplicates, presets). Pure C#, no UI.
- `ViewModels/` and `Views/`: the Avalonia UI.
- `FileOrganizer.SmokeTest/`: headless checks that link the `Services/` and `Models/` sources directly.

If you change anything in `Services/`, add a `Check(...)` to the smoke test that covers it. This app moves people's files, so every behavior change needs a test, and every operation must stay undoable.

## Pull requests

1. Fork, then create a branch from `main`.
2. Keep each PR focused on one change.
3. Make sure `dotnet build FileOrganizer.csproj` and the smoke tests pass. CI runs both on Windows and Linux.
4. Update the README if the change is visible to users.

Maintainers handle releases (see *Releases* in the README), so you don't need to change the version number.

## Good first contributions

Look for issues labeled [`good first issue`](https://github.com/moustafarhat/FileOrganizer/labels/good%20first%20issue), or pick something from the [Roadmap](README.md#roadmap). Translations and Linux/macOS file-manager integration are especially welcome.

By contributing, you agree that your contributions are licensed under the [MIT License](LICENSE) and that you'll follow the [Code of Conduct](CODE_OF_CONDUCT.md).
