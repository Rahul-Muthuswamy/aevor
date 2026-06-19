namespace Aevor.Core.Models;

public record CloneRequest(
    string SourceProfileFolderName,
    string DestinationProfileName,
    string? DestinationProfileFolderName = null,
    bool CopyExtensions = true,
    bool CopyBookmarks = true,
    bool CopySettings = true,
    bool CopyThemes = true,
    bool CopySearchEngines = true,
    bool CreateBackup = true,
    bool IncludeExtensions = true,
    bool IncludeBookmarks = true,
    bool IncludeSettings = true,
    bool IncludeThemes = true,
    bool IncludeSearchEngines = true,
    bool BlockActiveCookies = true,
    bool ExcludeHistory = true,
    bool ExcludePasswords = true
);
