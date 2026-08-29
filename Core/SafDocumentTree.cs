using Android.Content;
using Android.Database;
using Android.Provider;
using AndroidUri = Android.Net.Uri;
using System.Security.Cryptography;

namespace NSC_ModManager_Android.Core;

/// <summary>
/// Small Storage Access Framework document-tree adapter. It intentionally uses
/// only platform DocumentsContract/ContentResolver APIs so the Android port
/// does not need AndroidX DocumentFile. Paths passed to this class are always
/// relative to the user-selected game folder tree.
/// </summary>
public sealed class SafDocumentTree
{
    const string DirectoryMime = "vnd.android.document/directory";
    static readonly string[] Projection =
    {
        DocumentsContract.Document.ColumnDocumentId,
        DocumentsContract.Document.ColumnDisplayName,
        DocumentsContract.Document.ColumnMimeType
    };

    readonly ContentResolver _resolver;
    readonly AndroidUri _treeUri;
    readonly string _rootDocumentId;
    readonly Dictionary<string, SafEntry> _directoryCache = new(StringComparer.OrdinalIgnoreCase);

    public SafDocumentTree(ContentResolver resolver, AndroidUri treeUri)
    {
        _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
        _treeUri = treeUri ?? throw new ArgumentNullException(nameof(treeUri));
        _rootDocumentId = DocumentsContract.GetTreeDocumentId(treeUri)
                          ?? throw new InvalidDataException("Android document provider returned no tree document ID.");

        AndroidUri rootUri = DocumentsContract.BuildDocumentUriUsingTree(_treeUri, _rootDocumentId)
                             ?? throw new InvalidDataException("Cannot build SAF root document URI.");
        SafEntry root = QuerySingle(rootUri) ?? new SafEntry(_rootDocumentId, "", DirectoryMime, rootUri);
        _directoryCache[string.Empty] = root;
    }

    public AndroidUri TreeUri => _treeUri;

    public bool Exists(string relativePath) => TryResolve(relativePath, out _);

    public bool IsDirectory(string relativePath)
        => TryResolve(relativePath, out SafEntry? entry) && entry.IsDirectory;

    public IReadOnlyList<string> ListChildNames(string relativeDirectory = "")
        => ListChildren(relativeDirectory).Select(x => x.Name).ToArray();

    public void CopyToLocal(string relativePath, string localPath)
    {
        SafEntry entry = ResolveFile(relativePath);
        string? dir = Path.GetDirectoryName(localPath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        using Stream src = _resolver.OpenInputStream(entry.Uri)
                           ?? throw new IOException("Document provider refused read access: " + relativePath);
        using Stream dst = File.Create(localPath);
        src.CopyTo(dst);
    }

    public void CopyFromLocal(string localPath, string relativePath)
    {
        if (!File.Exists(localPath)) throw new FileNotFoundException("Local staging file is missing.", localPath);
        string mime = GuessMimeType(relativePath);
        using Stream src = File.OpenRead(localPath);
        using Stream dst = OpenForOverwrite(relativePath, mime);
        src.CopyTo(dst);
        dst.Flush();
    }

    public void CopyRemote(string sourceRelativePath, string destinationRelativePath)
    {
        SafEntry source = ResolveFile(sourceRelativePath);
        string mime = source.MimeType.Length == 0 ? GuessMimeType(destinationRelativePath) : source.MimeType;
        using Stream src = _resolver.OpenInputStream(source.Uri)
                           ?? throw new IOException("Document provider refused read access: " + sourceRelativePath);
        using Stream dst = OpenForOverwrite(destinationRelativePath, mime);
        src.CopyTo(dst);
        dst.Flush();
    }

    public string ReadText(string relativePath)
    {
        SafEntry entry = ResolveFile(relativePath);
        using Stream stream = _resolver.OpenInputStream(entry.Uri)
                              ?? throw new IOException("Document provider refused read access: " + relativePath);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    public void WriteText(string relativePath, string text)
    {
        using Stream stream = OpenForOverwrite(relativePath, "text/plain");
        using var writer = new StreamWriter(stream);
        writer.Write(text);
        writer.Flush();
    }

    public string Sha256(string relativePath)
    {
        SafEntry entry = ResolveFile(relativePath);
        using Stream stream = _resolver.OpenInputStream(entry.Uri)
                              ?? throw new IOException("Document provider refused read access: " + relativePath);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    public bool DeleteFile(string relativePath)
    {
        if (!TryResolve(relativePath, out SafEntry? entry)) return false;
        if (entry.IsDirectory) throw new IOException("Refusing to delete a directory through DeleteFile: " + relativePath);
        bool deleted = DocumentsContract.DeleteDocument(_resolver, entry.Uri);
        if (!deleted) throw new IOException("Document provider refused delete: " + relativePath);
        return true;
    }

    public bool RemoveDirectoryIfEmpty(string relativePath)
    {
        if (!TryResolve(relativePath, out SafEntry? entry) || !entry.IsDirectory) return false;
        if (ListChildren(relativePath).Count != 0) return false;
        bool deleted = DocumentsContract.DeleteDocument(_resolver, entry.Uri);
        if (!deleted) return false;
        InvalidateDirectoryCache(relativePath);
        return true;
    }

    public IReadOnlyList<string> FindFilesWithSuffix(string relativeRoot, string suffix)
    {
        var result = new List<string>();
        if (!TryResolve(relativeRoot, out SafEntry? root) || !root.IsDirectory) return result;
        EnumerateRecursive(NormalizeRelative(relativeRoot), root, suffix, result);
        return result;
    }

    public (bool Ok, string Message) ValidateGameFolder()
    {
        try
        {
            IReadOnlyList<SafEntry> children = ListChildren("");
            var names = new HashSet<string>(children.Select(x => x.Name), StringComparer.OrdinalIgnoreCase);
            bool marker = names.Contains("NSUNSC.exe") || names.Contains("NSUNS4.exe") || names.Contains("data_win32") || names.Contains("data");
            if (!marker)
                return (true, "SAF folder is readable, but no known Storm marker was found.");
            return (true, "SAF game directory is readable through Android DocumentsProvider.");
        }
        catch (Exception ex)
        {
            return (false, "Cannot access selected SAF game folder: " + ex.Message);
        }
    }

    public string RootDisplayName()
    {
        try
        {
            AndroidUri uri = DocumentsContract.BuildDocumentUriUsingTree(_treeUri, _rootDocumentId)!;
            SafEntry? root = QuerySingle(uri);
            return string.IsNullOrWhiteSpace(root?.Name) ? _rootDocumentId : root.Name;
        }
        catch { return _rootDocumentId; }
    }

    SafEntry ResolveFile(string relativePath)
    {
        if (!TryResolve(relativePath, out SafEntry? entry) || entry.IsDirectory)
            throw new FileNotFoundException("SAF file does not exist: " + relativePath);
        return entry;
    }

    SafEntry EnsureFile(string relativePath, string mimeType)
    {
        string normalized = NormalizeRelative(relativePath);
        string name = Path.GetFileName(normalized.Replace('/', Path.DirectorySeparatorChar));
        string parentRel = NormalizeRelative(Path.GetDirectoryName(normalized.Replace('/', Path.DirectorySeparatorChar))?.Replace(Path.DirectorySeparatorChar, '/') ?? "");
        if (name.Length == 0) throw new InvalidDataException("Invalid SAF file path: " + relativePath);

        SafEntry parent = EnsureDirectory(parentRel);
        SafEntry? existing = FindChild(parent, name);
        if (existing is not null)
        {
            if (existing.IsDirectory) throw new IOException("A directory already exists where a file is required: " + relativePath);
            return existing;
        }

        AndroidUri? created = DocumentsContract.CreateDocument(_resolver, parent.Uri, mimeType, name);
        if (created is null) throw new IOException("Document provider refused to create file: " + relativePath);
        return QuerySingle(created) ?? new SafEntry(DocIdFromUri(created), name, mimeType, created);
    }

    SafEntry EnsureDirectory(string relativePath)
    {
        string normalized = NormalizeRelative(relativePath);
        if (_directoryCache.TryGetValue(normalized, out SafEntry? cached)) return cached;
        if (normalized.Length == 0) return _directoryCache[string.Empty];

        string[] parts = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
        string currentRel = string.Empty;
        SafEntry current = _directoryCache[string.Empty];
        foreach (string part in parts)
        {
            currentRel = currentRel.Length == 0 ? part : currentRel + "/" + part;
            if (_directoryCache.TryGetValue(currentRel, out SafEntry? foundCached))
            {
                current = foundCached;
                continue;
            }

            SafEntry? child = FindChild(current, part);
            if (child is null)
            {
                AndroidUri? created = DocumentsContract.CreateDocument(_resolver, current.Uri, DirectoryMime, part);
                if (created is null) throw new IOException("Document provider refused to create directory: " + currentRel);
                child = QuerySingle(created) ?? new SafEntry(DocIdFromUri(created), part, DirectoryMime, created);
            }
            if (!child.IsDirectory) throw new IOException("A file blocks required directory path: " + currentRel);
            _directoryCache[currentRel] = child;
            current = child;
        }
        return current;
    }

    bool TryResolve(string relativePath, out SafEntry? entry)
    {
        string normalized = NormalizeRelative(relativePath);
        if (normalized.Length == 0)
        {
            entry = _directoryCache[string.Empty];
            return true;
        }

        string[] parts = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
        SafEntry current = _directoryCache[string.Empty];
        string currentRel = string.Empty;
        for (int i = 0; i < parts.Length; i++)
        {
            string nextRel = currentRel.Length == 0 ? parts[i] : currentRel + "/" + parts[i];
            if (i < parts.Length - 1 && _directoryCache.TryGetValue(nextRel, out SafEntry? cached))
            {
                current = cached;
                currentRel = nextRel;
                continue;
            }

            SafEntry? child = FindChild(current, parts[i]);
            if (child is null)
            {
                entry = null;
                return false;
            }
            if (i < parts.Length - 1)
            {
                if (!child.IsDirectory)
                {
                    entry = null;
                    return false;
                }
                _directoryCache[nextRel] = child;
            }
            current = child;
            currentRel = nextRel;
        }
        entry = current;
        return true;
    }

    IReadOnlyList<SafEntry> ListChildren(string relativeDirectory)
    {
        string normalized = NormalizeRelative(relativeDirectory);
        SafEntry directory = normalized.Length == 0 ? _directoryCache[string.Empty] : EnsureResolvedDirectory(normalized);
        return QueryChildren(directory);
    }

    SafEntry EnsureResolvedDirectory(string normalized)
    {
        if (_directoryCache.TryGetValue(normalized, out SafEntry? cached)) return cached;
        if (!TryResolve(normalized, out SafEntry? entry) || !entry.IsDirectory)
            throw new DirectoryNotFoundException("SAF directory does not exist: " + normalized);
        _directoryCache[normalized] = entry;
        return entry;
    }

    SafEntry? FindChild(SafEntry parent, string name)
        => QueryChildren(parent).FirstOrDefault(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

    IReadOnlyList<SafEntry> QueryChildren(SafEntry parent)
    {
        AndroidUri childrenUri = DocumentsContract.BuildChildDocumentsUriUsingTree(_treeUri, parent.DocumentId)
                                 ?? throw new IOException("Cannot build SAF child-documents URI.");
        var result = new List<SafEntry>();
        using ICursor? cursor = _resolver.Query(childrenUri, Projection, null, null, null);
        if (cursor is null) throw new IOException("Document provider returned no cursor while listing " + parent.Name);
        int idCol = cursor.GetColumnIndex(DocumentsContract.Document.ColumnDocumentId);
        int nameCol = cursor.GetColumnIndex(DocumentsContract.Document.ColumnDisplayName);
        int mimeCol = cursor.GetColumnIndex(DocumentsContract.Document.ColumnMimeType);
        while (cursor.MoveToNext())
        {
            string id = idCol >= 0 ? (cursor.GetString(idCol) ?? "") : "";
            string name = nameCol >= 0 ? (cursor.GetString(nameCol) ?? "") : "";
            string mime = mimeCol >= 0 ? (cursor.GetString(mimeCol) ?? "") : "";
            if (id.Length == 0 || name.Length == 0) continue;
            AndroidUri uri = DocumentsContract.BuildDocumentUriUsingTree(_treeUri, id)
                             ?? throw new IOException("Cannot build SAF document URI for " + name);
            result.Add(new SafEntry(id, name, mime, uri));
        }
        return result;
    }

    SafEntry? QuerySingle(AndroidUri uri)
    {
        using ICursor? cursor = _resolver.Query(uri, Projection, null, null, null);
        if (cursor is null || !cursor.MoveToFirst()) return null;
        int idCol = cursor.GetColumnIndex(DocumentsContract.Document.ColumnDocumentId);
        int nameCol = cursor.GetColumnIndex(DocumentsContract.Document.ColumnDisplayName);
        int mimeCol = cursor.GetColumnIndex(DocumentsContract.Document.ColumnMimeType);
        string id = idCol >= 0 ? (cursor.GetString(idCol) ?? "") : DocIdFromUri(uri);
        string name = nameCol >= 0 ? (cursor.GetString(nameCol) ?? "") : "";
        string mime = mimeCol >= 0 ? (cursor.GetString(mimeCol) ?? "") : "";
        return new SafEntry(id, name, mime, uri);
    }

    void EnumerateRecursive(string currentRel, SafEntry current, string suffix, List<string> output)
    {
        foreach (SafEntry child in QueryChildren(current))
        {
            string rel = currentRel.Length == 0 ? child.Name : currentRel + "/" + child.Name;
            if (child.IsDirectory)
                EnumerateRecursive(rel, child, suffix, output);
            else if (child.Name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                output.Add(rel);
        }
    }

    Stream OpenForOverwrite(string relativePath, string mimeType)
    {
        SafEntry target = EnsureFile(relativePath, mimeType);
        try
        {
            Stream? stream = _resolver.OpenOutputStream(target.Uri, "rwt");
            if (stream is not null) return stream;
        }
        catch { }

        try
        {
            Stream? stream = _resolver.OpenOutputStream(target.Uri, "wt");
            if (stream is not null) return stream;
        }
        catch { }

        // A plain "w" is provider-defined and is not guaranteed to truncate.
        // Recreate the document before falling back so shorter binary outputs
        // cannot leave stale trailing bytes behind.
        try { DeleteFile(relativePath); }
        catch { throw new IOException("Document provider cannot safely replace file: " + relativePath); }
        target = EnsureFile(relativePath, mimeType);
        Stream? fallback = _resolver.OpenOutputStream(target.Uri, "w");
        return fallback ?? throw new IOException("Document provider refused write access: " + relativePath);
    }

    void InvalidateDirectoryCache(string relativePath)
    {
        string normalized = NormalizeRelative(relativePath);
        foreach (string key in _directoryCache.Keys.Where(x => x.Equals(normalized, StringComparison.OrdinalIgnoreCase) || x.StartsWith(normalized + "/", StringComparison.OrdinalIgnoreCase)).ToArray())
            _directoryCache.Remove(key);
    }

    static string NormalizeRelative(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return string.Empty;
        string normalized = path.Replace('\\', '/').Trim('/');
        if (normalized.Equals("..", StringComparison.Ordinal) || normalized.StartsWith("../", StringComparison.Ordinal) || normalized.Contains("/../", StringComparison.Ordinal))
            throw new InvalidDataException("SAF path escaped the selected game folder: " + path);
        return normalized;
    }

    static string DocIdFromUri(AndroidUri uri)
    {
        try { return DocumentsContract.GetDocumentId(uri) ?? uri.LastPathSegment ?? ""; }
        catch { return uri.LastPathSegment ?? ""; }
    }

    static string GuessMimeType(string path)
    {
        string ext = Path.GetExtension(path).ToLowerInvariant();
        return ext switch
        {
            ".txt" or ".ini" or ".log" => "text/plain",
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".zip" => "application/zip",
            _ => "application/octet-stream"
        };
    }

    sealed record SafEntry(string DocumentId, string Name, string MimeType, AndroidUri Uri)
    {
        public bool IsDirectory => MimeType.Equals(DirectoryMime, StringComparison.Ordinal);
    }
}
