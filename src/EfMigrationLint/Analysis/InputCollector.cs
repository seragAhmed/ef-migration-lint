namespace EfMigrationLint.Analysis;

/// <summary>A C# file to analyze.</summary>
/// <param name="FullPath">Absolute path on disk.</param>
/// <param name="DisplayPath">Path shown in output: relative to the working directory with forward slashes when possible.</param>
public sealed record InputFile(string FullPath, string DisplayPath);

/// <summary>The expanded command-line paths.</summary>
/// <param name="Files">Files to analyze.</param>
/// <param name="Skipped">Display paths of generated designer and model snapshot files that were skipped.</param>
/// <param name="Errors">Inputs that could not be used. Any error makes the scan incomplete.</param>
public sealed record CollectedInputs(IReadOnlyList<InputFile> Files, IReadOnlyList<string> Skipped, IReadOnlyList<string> Errors);

/// <summary>Turns file and directory arguments into the list of migration source files to analyze.</summary>
public static class InputCollector
{
    private static readonly HashSet<string> ExcludedDirectoryNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "bin", "obj", ".git", "node_modules",
    };

    private static readonly StringComparer PathComparer =
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    /// <summary>
    /// True for files EF Core generates next to migrations that never contain <c>Up()</c> operations:
    /// <c>*.Designer.cs</c> and <c>*ModelSnapshot.cs</c>.
    /// </summary>
    public static bool IsGeneratedFile(string path)
    {
        var fileName = Path.GetFileName(path);
        return fileName.EndsWith(".Designer.cs", StringComparison.OrdinalIgnoreCase)
            || fileName.EndsWith("ModelSnapshot.cs", StringComparison.OrdinalIgnoreCase);
    }

    public static CollectedInputs Collect(IEnumerable<string> paths, string workingDirectory)
    {
        var files = new List<InputFile>();
        var skipped = new List<string>();
        var errors = new List<string>();
        var seen = new HashSet<string>(PathComparer);

        void Add(string fullPath)
        {
            if (!seen.Add(fullPath))
            {
                return;
            }

            var display = ToDisplayPath(fullPath, workingDirectory);
            if (IsGeneratedFile(fullPath))
            {
                skipped.Add(display);
            }
            else
            {
                files.Add(new InputFile(fullPath, display));
            }
        }

        foreach (var path in paths)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                errors.Add("Empty path argument.");
                continue;
            }

            var fullPath = Path.GetFullPath(path, workingDirectory);

            if (File.Exists(fullPath))
            {
                if (!fullPath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                {
                    errors.Add($"Not a C# source file: {path}");
                    continue;
                }

                Add(fullPath);
            }
            else if (Directory.Exists(fullPath))
            {
                try
                {
                    foreach (var file in EnumerateSourceFiles(fullPath))
                    {
                        Add(file);
                    }
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    errors.Add($"Could not read directory {path}: {ex.Message}");
                }
            }
            else
            {
                errors.Add($"Path not found: {path}");
            }
        }

        return new CollectedInputs(files, skipped, errors);
    }

    public static string ToDisplayPath(string fullPath, string workingDirectory)
    {
        var relative = Path.GetRelativePath(workingDirectory, fullPath);
        if (Path.IsPathRooted(relative) || relative == ".." || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            return fullPath;
        }

        return relative.Replace('\\', '/');
    }

    private static IEnumerable<string> EnumerateSourceFiles(string root)
    {
        var pending = new Stack<string>();
        pending.Push(root);
        var results = new List<string>();

        while (pending.Count > 0)
        {
            var directory = pending.Pop();

            results.AddRange(Directory.EnumerateFiles(directory, "*.cs", SearchOption.TopDirectoryOnly));

            foreach (var child in Directory.EnumerateDirectories(directory))
            {
                if (!ExcludedDirectoryNames.Contains(Path.GetFileName(child)))
                {
                    pending.Push(child);
                }
            }
        }

        // Stable output order across platforms.
        results.Sort(StringComparer.Ordinal);
        return results;
    }
}
