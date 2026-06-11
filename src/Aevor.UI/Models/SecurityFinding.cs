using System.Windows.Media;

namespace Aevor.UI.Models;

public class SecurityFinding
{
    public string FindingTitle    { get; set; } = string.Empty;
    public string FindingDetail   { get; set; } = string.Empty;
    public string AffectedProfile { get; set; } = string.Empty;
    public string Severity        { get; set; } = "Info";

    // ── Badges & Brushes ────────────────────────────────────────────────
    public Brush SeverityColor => Severity switch
    {
        "Critical" => new SolidColorBrush(Color.FromRgb(220, 38,  38)),  // #DC2626
        "Warning"  => new SolidColorBrush(Color.FromRgb(245, 158, 11)),  // #F59E0B
        "Excluded" => new SolidColorBrush(Color.FromRgb(16,  185, 129)), // #10B981
        _          => new SolidColorBrush(Color.FromRgb(59,  130, 246)), // #3B82F6 (Info)
    };

    public Brush SeverityBadgeBackground => Severity switch
    {
        "Critical" => new SolidColorBrush(Color.FromRgb(254, 226, 226)), // #FEE2E2
        "Warning"  => new SolidColorBrush(Color.FromRgb(254, 243, 199)), // #FEF3C7
        "Excluded" => new SolidColorBrush(Color.FromRgb(209, 250, 229)), // #D1FAE5
        _          => new SolidColorBrush(Color.FromRgb(219, 234, 254)), // #DBEAFE (Info)
    };

    public Brush SeverityBadgeText => Severity switch
    {
        "Critical" => new SolidColorBrush(Color.FromRgb(153, 27,  27)),  // #991B1B
        "Warning"  => new SolidColorBrush(Color.FromRgb(146, 64,  14)),  // #92400E
        "Excluded" => new SolidColorBrush(Color.FromRgb(6,   95,  70)),  // #065F46
        _          => new SolidColorBrush(Color.FromRgb(30,  58,  138)), // #1E3A8A (Info)
    };
}
