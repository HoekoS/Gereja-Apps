// src/ChurchProjection.Api/Media/MediaPaths.cs
namespace ChurchProjection.Api.Media;

public static class MediaPaths
{
    /// <summary>
    /// Resolves a stored filename inside the media root, or null if the result
    /// escapes it. Containment is checked on the fully resolved path rather than
    /// by looking for "..", because there are more ways out of a directory than
    /// two dots — a symlink and an absolute path are two of them.
    /// </summary>
    public static string? Resolve(string mediaRoot, string filename)
    {
        var root = Path.GetFullPath(mediaRoot);
        var full = Path.GetFullPath(Path.Combine(root, filename));

        var rooted = root.EndsWith(Path.DirectorySeparatorChar) ? root : root + Path.DirectorySeparatorChar;

        return full.StartsWith(rooted, StringComparison.OrdinalIgnoreCase) ? full : null;
    }

    /// <summary>Strips every directory component from an uploaded name.</summary>
    public static string Sanitise(string filename)
    {
        var bare = Path.GetFileName(filename.Replace('\\', '/'));

        return string.Join('_', bare.Split(Path.GetInvalidFileNameChars()));
    }
}
