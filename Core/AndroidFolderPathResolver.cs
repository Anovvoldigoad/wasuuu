using AndroidUri = Android.Net.Uri;
using Android.Provider;

namespace NSC_ModManager_Android.Core;

/// <summary>
/// Converts Android ExternalStorageProvider tree URI to the direct filesystem
/// path required by the native CPK bridge. Cloud/document-provider URIs are not
/// accepted because they do not expose a stable native filesystem path.
/// </summary>
public static class AndroidFolderPathResolver
{
    public static bool TryResolve(AndroidUri treeUri, out string path, out string error)
    {
        path = string.Empty;
        error = string.Empty;
        try
        {
            if (!string.Equals(treeUri.Authority, "com.android.externalstorage.documents", StringComparison.OrdinalIgnoreCase))
            {
                error = "Choose the game folder from Android Internal storage or SD card, not a cloud/document provider.";
                return false;
            }

            string? documentId = DocumentsContract.GetTreeDocumentId(treeUri);
            if (string.IsNullOrWhiteSpace(documentId))
            {
                error = "Android did not return a filesystem document ID for this folder.";
                return false;
            }

            int colon = documentId.IndexOf(':');
            string volume = colon >= 0 ? documentId[..colon] : documentId;
            string relative = colon >= 0 ? documentId[(colon + 1)..] : string.Empty;

            string primary = Android.OS.Environment.ExternalStorageDirectory?.AbsolutePath ?? "/storage/emulated/0";
            string root = volume.Equals("primary", StringComparison.OrdinalIgnoreCase)
                ? primary
                : volume.Equals("home", StringComparison.OrdinalIgnoreCase)
                    ? Path.Combine(primary, "Documents")
                    : Path.Combine("/storage", volume);

            relative = relative.Replace('/', Path.DirectorySeparatorChar).TrimStart(Path.DirectorySeparatorChar);
            string candidate = string.IsNullOrEmpty(relative) ? root : Path.Combine(root, relative);
            candidate = Path.GetFullPath(candidate).TrimEnd(Path.DirectorySeparatorChar);

            string normalizedRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!candidate.Equals(Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar), StringComparison.Ordinal)
                && !candidate.StartsWith(normalizedRoot, StringComparison.Ordinal))
            {
                error = "Selected folder resolved outside its Android storage volume.";
                return false;
            }

            path = candidate;
            if (!Directory.Exists(candidate))
                error = "Folder path was resolved, but direct access is not available yet. Tap Storage Access, grant All Files Access, then use Save / Check Path.";
            return true;
        }
        catch (Exception ex)
        {
            error = "Cannot resolve selected folder to a direct filesystem path: " + ex.Message;
            return false;
        }
    }
}
