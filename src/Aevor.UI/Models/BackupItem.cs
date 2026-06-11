using System.Windows.Media;

namespace Aevor.UI.Models;

public class BackupItem
{
    public string BackupName      { get; set; } = string.Empty;
    public string ProfileName     { get; set; } = string.Empty;
    public string CreatedDate     { get; set; } = string.Empty;
    public string Size            { get; set; } = string.Empty;
    public long   SizeBytes       { get; set; }
    public string ValidationStatus { get; set; } = "Valid";
    public string Notes           { get; set; } = string.Empty;

    // ── Badges & Brushes ────────────────────────────────────────────────
    public Brush ValidationColor => ValidationStatus switch
    {
        "Invalid" => new SolidColorBrush(Color.FromRgb(239, 68,  68)),  // #EF4444
        "Warning" => new SolidColorBrush(Color.FromRgb(245, 158, 11)),  // #F59E0B
        _         => new SolidColorBrush(Color.FromRgb(16,  185, 129)), // #10B981
    };

    public Brush ValidationBadgeBackground => ValidationStatus switch
    {
        "Invalid" => new SolidColorBrush(Color.FromRgb(254, 226, 226)), // #FEE2E2
        "Warning" => new SolidColorBrush(Color.FromRgb(254, 243, 199)), // #FEF3C7
        _         => new SolidColorBrush(Color.FromRgb(209, 250, 229)), // #D1FAE5
    };

    public Brush ValidationBadgeText => ValidationStatus switch
    {
        "Invalid" => new SolidColorBrush(Color.FromRgb(153, 27,  27)),  // #991B1B
        "Warning" => new SolidColorBrush(Color.FromRgb(146, 64,  14)),  // #92400E
        _         => new SolidColorBrush(Color.FromRgb(6,   95,  70)),  // #065F46
    };
}
