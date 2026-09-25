# Security Policy

## Supported versions

Only the [latest release](https://github.com/moustafarhat/FileOrganizer/releases/latest) receives fixes.

## Reporting a vulnerability

Please **don't** open a public issue. Report it privately through [GitHub security advisories](https://github.com/moustafarhat/FileOrganizer/security/advisories/new).

Include what you found, how to reproduce it, and the impact (e.g. files moved outside the selected folder, data loss, code execution). You'll get a reply within a few days, and credit in the release notes if you'd like.

## Scope notes

File Organizer runs fully offline and never uploads anything. The areas most worth scrutiny are path handling (moves or renames escaping the selected folders), the undo journal, and the Windows shell-integration registry entries (HKCU only).
