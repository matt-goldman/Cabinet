namespace Plugin.Maui.OfflineData.Utilities;

public static class FileExtensions
{
    public static string EnsureDirectory(this string path)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
        return path;
    }
}
