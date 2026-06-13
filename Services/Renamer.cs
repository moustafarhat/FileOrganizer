using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using FileOrganizer.Models;

namespace FileOrganizer.Services;

public static class Renamer
{
    public static string Apply(FileInfo file, RenameOptions options, int sequenceIndex)
    {
        var nameNoExt = Path.GetFileNameWithoutExtension(file.Name);
        var ext = file.Extension;

        return options.Style switch
        {
            RenameStyle.SnakeCase => ToSnake(nameNoExt) + ext.ToLowerInvariant(),
            RenameStyle.KebabCase => ToKebab(nameNoExt) + ext.ToLowerInvariant(),
            RenameStyle.PascalCase => ToPascal(nameNoExt) + ext,
            RenameStyle.CamelCase => ToCamel(nameNoExt) + ext,
            RenameStyle.LowerCase => nameNoExt.ToLowerInvariant() + ext.ToLowerInvariant(),
            RenameStyle.UpperCase => nameNoExt.ToUpperInvariant() + ext,
            RenameStyle.DatePrefix => $"{file.LastWriteTime:yyyy-MM-dd}_{nameNoExt}{ext}",
            RenameStyle.SequentialPrefix => $"{(options.SequenceStart + sequenceIndex).ToString().PadLeft(options.SequencePadding, '0')}_{nameNoExt}{ext}",
            RenameStyle.Custom => ApplyCustom(file, options, sequenceIndex),
            _ => file.Name,
        };
    }

    private static string ApplyCustom(FileInfo file, RenameOptions options, int sequenceIndex)
    {
        var nameNoExt = Path.GetFileNameWithoutExtension(file.Name);
        var ext = file.Extension;

        return Regex.Replace(options.CustomPattern, @"\{([^}]+)\}", match =>
        {
            var token = match.Groups[1].Value;
            return ResolveToken(token, file, nameNoExt, ext, options, sequenceIndex);
        });
    }

    private static string ResolveToken(string token, FileInfo file, string nameNoExt, string ext, RenameOptions options, int sequenceIndex)
    {
        if (token.StartsWith("date:", StringComparison.OrdinalIgnoreCase))
        {
            var fmt = token.Substring("date:".Length);
            return file.LastWriteTime.ToString(fmt, CultureInfo.InvariantCulture);
        }
        if (token.StartsWith("created:", StringComparison.OrdinalIgnoreCase))
        {
            var fmt = token.Substring("created:".Length);
            return file.CreationTime.ToString(fmt, CultureInfo.InvariantCulture);
        }
        if (token.StartsWith("seq", StringComparison.OrdinalIgnoreCase))
        {
            var pad = options.SequencePadding;
            var colonIdx = token.IndexOf(':');
            if (colonIdx >= 0 && int.TryParse(token.Substring(colonIdx + 1), out var p)) pad = p;
            return (options.SequenceStart + sequenceIndex).ToString().PadLeft(pad, '0');
        }

        return token.ToLowerInvariant() switch
        {
            "name" => nameNoExt,
            "ext" => ext,
            "upper" => nameNoExt.ToUpperInvariant(),
            "lower" => nameNoExt.ToLowerInvariant(),
            "snake" => ToSnake(nameNoExt),
            "kebab" => ToKebab(nameNoExt),
            "pascal" => ToPascal(nameNoExt),
            "camel" => ToCamel(nameNoExt),
            "size" => file.Length.ToString(),
            _ => "{" + token + "}",
        };
    }

    private static string ToSnake(string s) => Slug(s, '_').ToLowerInvariant();
    private static string ToKebab(string s) => Slug(s, '-').ToLowerInvariant();

    private static string Slug(string s, char separator)
    {
        var sb = new StringBuilder();
        var prevSep = true;
        foreach (var ch in s)
        {
            if (char.IsLetterOrDigit(ch))
            {
                sb.Append(ch);
                prevSep = false;
            }
            else if (!prevSep)
            {
                sb.Append(separator);
                prevSep = true;
            }
        }
        return sb.ToString().Trim(separator);
    }

    private static string ToPascal(string s)
    {
        var parts = SplitWords(s);
        return string.Concat(parts.Select(Capitalize));
    }

    private static string ToCamel(string s)
    {
        var parts = SplitWords(s).ToList();
        if (parts.Count == 0) return s;
        return parts[0].ToLowerInvariant() + string.Concat(parts.Skip(1).Select(Capitalize));
    }

    private static System.Collections.Generic.IEnumerable<string> SplitWords(string s)
    {
        if (string.IsNullOrEmpty(s)) yield break;
        var sb = new StringBuilder();
        for (var i = 0; i < s.Length; i++)
        {
            var ch = s[i];
            if (!char.IsLetterOrDigit(ch))
            {
                if (sb.Length > 0) { yield return sb.ToString(); sb.Clear(); }
                continue;
            }
            if (i > 0 && char.IsUpper(ch) && (char.IsLower(s[i - 1]) || (i + 1 < s.Length && char.IsLower(s[i + 1]))))
            {
                if (sb.Length > 0) { yield return sb.ToString(); sb.Clear(); }
            }
            sb.Append(ch);
        }
        if (sb.Length > 0) yield return sb.ToString();
    }

    private static string Capitalize(string s) =>
        string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s.Substring(1).ToLowerInvariant();
}
