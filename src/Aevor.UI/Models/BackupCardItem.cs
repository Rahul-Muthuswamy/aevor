using System;
using System.Windows.Media;
using Aevor.Core.Models;

namespace Aevor.UI.Models;

public class BackupCardItem
{
    public Guid           BackupId         { get; set; }
    public string         ProfileName      { get; set; } = string.Empty;
    public string         ProfilePath      { get; set; } = string.Empty;
    public DateTime       CreatedTimestamp { get; set; }
    public long           BackupSize       { get; set; }
    public string         BackupVersion    { get; set; } = "1.0";
    public BackupStatus   Status           { get; set; }
    public bool           IsSelected       { get; set; }

    // ── Computed ────────────────────────────────────────────────────────
    public string FormattedDate => CreatedTimestamp.ToLocalTime().ToString("MMM d, yyyy h:mm tt");

    public string FormattedSize
    {
        get
        {
            if (BackupSize >= 1024 * 1024)
                return $"{BackupSize / (1024.0 * 1024.0):F1} MB";
            if (BackupSize >= 1024)
                return $"{BackupSize / 1024.0:F1} KB";
            return $"{BackupSize} B";
        }
    }

    public string StatusLabel => Status switch
    {
        BackupStatus.InProgress => "In Progress",
        BackupStatus.Completed  => "Completed",
        BackupStatus.Failed     => "Failed",
        BackupStatus.Corrupted  => "Corrupted",
        _                       => Status.ToString()
    };

    public Brush StatusColor => Status switch
    {
        BackupStatus.InProgress => new SolidColorBrush(Color.FromRgb(59,  130, 246)), // Blue: #3B82F6
        BackupStatus.Completed  => new SolidColorBrush(Color.FromRgb(16,  185, 129)), // Green: #10B981
        BackupStatus.Failed     => new SolidColorBrush(Color.FromRgb(239, 68,  68)),  // Red: #EF4444
        BackupStatus.Corrupted  => new SolidColorBrush(Color.FromRgb(245, 158, 11)),  // Orange: #F59E0B
        _                       => new SolidColorBrush(Color.FromRgb(107, 114, 128)) // Gray: #6B7280
    };

    public Brush StatusBackground => Status switch
    {
        BackupStatus.InProgress => new SolidColorBrush(Color.FromRgb(219, 234, 254)), // #DBEAFE
        BackupStatus.Completed  => new SolidColorBrush(Color.FromRgb(209, 250, 229)), // #D1FAE5
        BackupStatus.Failed     => new SolidColorBrush(Color.FromRgb(254, 226, 226)), // #FEE2E2
        BackupStatus.Corrupted  => new SolidColorBrush(Color.FromRgb(254, 243, 199)), // #FEF3C7
        _                       => new SolidColorBrush(Color.FromRgb(243, 244, 246)) // #F3F4F6
    };

    public string Initial => string.IsNullOrWhiteSpace(ProfileName) ? "?" : ProfileName[0].ToString().ToUpper();

    public static BackupCardItem FromMetadata(BackupMetadata metadata)
    {
        return new BackupCardItem
        {
            BackupId         = metadata.BackupId,
            ProfileName      = metadata.ProfileName,
            ProfilePath      = metadata.ProfilePath,
            CreatedTimestamp = metadata.CreatedTimestamp,
            BackupSize       = metadata.BackupSize,
            BackupVersion    = metadata.BackupVersion,
            Status           = metadata.Status
        };
    }
}
