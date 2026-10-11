// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace MxcMafAgent;

/// <summary>
/// Lightweight, dependency-free .env configuration loader.
/// </summary>
public static class EnvLoader
{
    public static void Load(string? customPath = null)
    {
        var candidatePaths = new List<string>();

        if (!string.IsNullOrEmpty(customPath))
        {
            candidatePaths.Add(customPath);
        }

        // Local directory candidates
        candidatePaths.Add(Path.Combine(AppContext.BaseDirectory, ".env"));
        candidatePaths.Add(Path.Combine(Directory.GetCurrentDirectory(), ".env"));

        // Fallback candidate from C:\Repo\samples\jev\.env
        candidatePaths.Add(@"C:\Repo\samples\jev\.env");

        string? resolvedPath = candidatePaths.FirstOrDefault(File.Exists);
        if (resolvedPath is null)
        {
            return;
        }

        foreach (var rawLine in File.ReadAllLines(resolvedPath))
        {
            var line = rawLine.Trim();
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#'))
            {
                continue;
            }

            var parts = line.Split('=', 2);
            if (parts.Length == 2)
            {
                var key = parts[0].Trim();
                var value = parts[1].Trim().Trim('"', '\'');

                if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(key)))
                {
                    Environment.SetEnvironmentVariable(key, value);
                }
            }
        }
    }
}
