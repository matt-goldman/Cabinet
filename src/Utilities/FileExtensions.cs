namespace Cabinet.Utilities;

/// <summary>
/// Provides extension methods for file path operations.
/// </summary>
public static class FileExtensions
{
    /// <summary>
    /// Ensures that the directory for the specified file path exists.
    /// Creates the directory if it does not already exist.
    /// </summary>
    /// <param name="path">The file path for which to ensure directory existence</param>
    /// <returns>The original file path</returns>
    public static string EnsureDirectory(this string path)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
        return path;
    }
}
