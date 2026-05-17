using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

// QuickSheet Environment Variable Inspector extension
// Protocol: JSON-lines stdin/stdout
// Usage: env: VAR_NAME | env: list | env: filter:PATTERN | env: PATH

namespace QuickSheetEnvCk;

class Program
{
    // Patterns that suggest a value should be masked
    static readonly string[] SensitivePatterns =
    [
        "key", "secret", "token", "password", "passwd", "pwd",
        "auth", "credential", "api", "access", "private", "cert",
        "oauth", "bearer", "signature", "hash", "salt"
    ];

    static void Main()
    {
        // Protocol: extension emits register on startup. Host listens; it does NOT
        // send init. Prefix has no trailing colon — host adds it.
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            type = "register",
            prefix = "env",
            width = 1,
            height = 20
        }));
        Console.Out.Flush();

        string? line;
        while ((line = Console.ReadLine()) != null)
        {
            try
            {
                var doc = JsonDocument.Parse(line);
                var root = doc.RootElement;
                var msgType = root.GetProperty("type").GetString();

                if (msgType != "activate") continue;

                var id = root.TryGetProperty("id", out var idProp) ? idProp.GetString() : "0";
                var cells = BuildCells(root);
                Console.WriteLine(JsonSerializer.Serialize(new
                {
                    type = "write",
                    id,
                    cells
                }));
                Console.Out.Flush();
            }
            catch { /* ignore malformed messages */ }
        }
    }

    static List<object> BuildCells(JsonElement root)
    {
        // Get the params/cells value
        string query = "";
        if (root.TryGetProperty("params", out var pEl) && pEl.ValueKind == JsonValueKind.Array)
        {
            var parts = new List<string>();
            foreach (var el in pEl.EnumerateArray())
                parts.Add(el.GetString() ?? "");
            query = string.Join(" ", parts).Trim();
        }
        else if (root.TryGetProperty("cells", out var cEl) && cEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var el in cEl.EnumerateArray())
            {
                if (el.ValueKind == JsonValueKind.Array)
                {
                    foreach (var inner in el.EnumerateArray())
                        query += (inner.GetString() ?? "") + " ";
                }
                else
                {
                    query += (el.GetString() ?? "") + " ";
                }
            }
            query = query.Trim();
        }
        // Strip leading "env:" prefix if echoed back
        if (query.StartsWith("env:", StringComparison.OrdinalIgnoreCase))
            query = query[4..].Trim();

        var results = new List<(string name, string value, bool masked)>();

        if (string.IsNullOrWhiteSpace(query) || query.Equals("list", StringComparison.OrdinalIgnoreCase))
        {
            // Show all vars sorted, masked if sensitive
            results = GetAllVars().Take(20).ToList();
        }
        else if (query.StartsWith("filter:", StringComparison.OrdinalIgnoreCase))
        {
            var pattern = query[7..].Trim();
            results = GetAllVars()
                .Where(v => v.name.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                .Take(20)
                .ToList();
        }
        else if (query.Equals("path", StringComparison.OrdinalIgnoreCase) ||
                 query.Equals("PATH", StringComparison.Ordinal))
        {
            // Special: split PATH into individual entries
            var pathVal = Environment.GetEnvironmentVariable("PATH") ?? "";
            var sep = OperatingSystem.IsWindows() ? ';' : ':';
            var entries = pathVal.Split(sep, StringSplitOptions.RemoveEmptyEntries);
            var cells = new List<object>();
            cells.Add(MakeCell(0, 0, $"PATH ({entries.Length} entries)"));
            for (int i = 0; i < Math.Min(entries.Length, 19); i++)
                cells.Add(MakeCell(i + 1, 0, entries[i]));
            return cells;
        }
        else
        {
            // Single variable lookup
            var val = Environment.GetEnvironmentVariable(query);
            if (val == null)
            {
                // Case-insensitive search
                var match = Environment.GetEnvironmentVariables()
                    .Cast<System.Collections.DictionaryEntry>()
                    .FirstOrDefault(e => string.Equals(e.Key?.ToString(), query, StringComparison.OrdinalIgnoreCase));
                val = match.Value?.ToString();
                if (match.Key != null)
                    query = match.Key.ToString()!;
            }
            if (val == null)
            {
                return [MakeCell(0, 0, $"⚠️  {query}: not set")];
            }
            bool isSensitive = IsSensitive(query);
            if (isSensitive)
            {
                return [MakeCell(0, 0, $"{query} = {Mask(val)}")];
            }
            // Multi-line values or long values split across rows
            var lines = val.Split('\n');
            if (lines.Length > 1)
            {
                var cells = new List<object>();
                cells.Add(MakeCell(0, 0, $"{query}:"));
                for (int i = 0; i < Math.Min(lines.Length, 19); i++)
                    cells.Add(MakeCell(i + 1, 0, lines[i].TrimEnd('\r')));
                return cells;
            }
            return [MakeCell(0, 0, $"{query} = {val}")];
        }

        // Convert results to cells
        {
            var cells = new List<object>();
            for (int i = 0; i < results.Count; i++)
            {
                var (name, value, masked) = results[i];
                var display = masked ? $"{name} = {Mask(value)}" : $"{name} = {Truncate(value, 40)}";
                cells.Add(MakeCell(i, 0, display));
            }
            if (cells.Count == 0)
                cells.Add(MakeCell(0, 0, "⚠️  No matching variables"));
            return cells;
        }
    }

    static List<(string name, string value, bool masked)> GetAllVars()
    {
        return Environment.GetEnvironmentVariables()
            .Cast<System.Collections.DictionaryEntry>()
            .Select(e => (
                name: e.Key?.ToString() ?? "",
                value: e.Value?.ToString() ?? "",
                masked: IsSensitive(e.Key?.ToString() ?? "")
            ))
            .OrderBy(v => v.name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    static bool IsSensitive(string name) =>
        SensitivePatterns.Any(p => name.Contains(p, StringComparison.OrdinalIgnoreCase));

    static string Mask(string value)
    {
        if (value.Length <= 4) return "****";
        return value[..4] + new string('*', Math.Min(value.Length - 4, 8));
    }

    static string Truncate(string value, int max) =>
        value.Length > max ? value[..max] + "…" : value;

    static object MakeCell(int row, int col, string value) =>
        new { r = row, c = col, v = value };
}
