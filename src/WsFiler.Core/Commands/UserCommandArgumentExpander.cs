using System.Text;

namespace WsFiler.Core.Commands;

public static class UserCommandArgumentExpander
{
    public static IReadOnlyList<string> Expand(string? template, UserCommandContext context)
    {
        if (string.IsNullOrWhiteSpace(template))
        {
            return [];
        }

        var tokens = Tokenize(template);
        var arguments = new List<string>();
        foreach (var token in tokens)
        {
            if (string.Equals(token, "{markedFileNames}", StringComparison.OrdinalIgnoreCase))
            {
                arguments.AddRange(context.EffectiveItems.Select(item => item.Name));
                continue;
            }

            if (string.Equals(token, "{markedFullPaths}", StringComparison.OrdinalIgnoreCase))
            {
                arguments.AddRange(context.EffectiveItems.Select(item => item.FullPath));
                continue;
            }

            arguments.Add(ExpandScalarMacros(token, context));
        }

        return arguments;
    }

    private static string ExpandScalarMacros(string value, UserCommandContext context)
    {
        return value
            .Replace("{currentDir}", context.CurrentDirectory, StringComparison.OrdinalIgnoreCase)
            .Replace("{currentFileName}", context.CurrentItem?.Name ?? "", StringComparison.OrdinalIgnoreCase)
            .Replace("{currentFullPath}", context.CurrentItem?.FullPath ?? "", StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<string> Tokenize(string template)
    {
        var tokens = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;
        var escapeNext = false;

        foreach (var c in template)
        {
            if (escapeNext)
            {
                current.Append(c);
                escapeNext = false;
                continue;
            }

            if (c == '\\' && inQuotes)
            {
                escapeNext = true;
                continue;
            }

            if (c == '"')
            {
                inQuotes = !inQuotes;
                continue;
            }

            if (char.IsWhiteSpace(c) && !inQuotes)
            {
                AddCurrent();
                continue;
            }

            current.Append(c);
        }

        AddCurrent();
        return tokens;

        void AddCurrent()
        {
            if (current.Length == 0)
            {
                return;
            }

            tokens.Add(current.ToString());
            current.Clear();
        }
    }
}
