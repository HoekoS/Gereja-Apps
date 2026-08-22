namespace ChurchProjection.Infrastructure.Storage;

/// <summary>
/// The one place a media row's filename is turned into a file on disk. It lives
/// here rather than in the API because the repository has to answer the same
/// question — a row is available if, and only if, this resolves and the file
/// exists — and two implementations of that rule is what let a restored backup
/// disagree with itself.
/// </summary>
public static class MediaPaths
{
    /// <summary>
    /// The content types the app projects, keyed by extension. Uploads are
    /// refused unless the extension is on this list and responses are served as
    /// what the list says, never as what the client called it: a browser told
    /// text/html by an upload runs that HTML on the API's own origin, which is
    /// the origin that holds the pair cookie.
    /// </summary>
    private static readonly Dictionary<string, string> ContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".png"] = "image/png",
        [".gif"] = "image/gif",
        [".webp"] = "image/webp",
        [".bmp"] = "image/bmp",
        [".mp4"] = "video/mp4",
        [".m4v"] = "video/mp4",
        [".webm"] = "video/webm",
        [".mov"] = "video/quicktime",
        [".mp3"] = "audio/mpeg",
        [".m4a"] = "audio/mp4",
        [".ogg"] = "audio/ogg",
        [".wav"] = "audio/wav",
    };

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

    /// <summary>Whether the file behind a stored filename is on disk right now.</summary>
    public static bool Exists(string mediaRoot, string filename) =>
        Resolve(mediaRoot, filename) is { } path && File.Exists(path);

    /// <summary>Strips every directory component from an uploaded name.</summary>
    public static string Sanitise(string filename)
    {
        var bare = Path.GetFileName(filename.Replace('\\', '/'));

        return string.Join('_', bare.Split(Path.GetInvalidFileNameChars()));
    }

    /// <summary>
    /// The content type for a stored filename, or null when the extension is not
    /// something this app projects.
    /// </summary>
    public static string? ContentTypeFor(string filename) =>
        ContentTypes.GetValueOrDefault(Path.GetExtension(filename));
}
