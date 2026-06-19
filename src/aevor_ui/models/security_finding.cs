using System.Windows.Media;

namespace Aevor.UI.Models;

public class SecurityFinding
{
    public string FindingTitle    { get; set; } = string.Empty;
    public string FindingDetail   { get; set; } = string.Empty;
    public string AffectedProfile { get; set; } = string.Empty;
    public string Severity        { get; set; } = "Info";

    public Brush SeverityColor => Severity switch
    {
        "Critical" => new SolidColorBrush(Color.FromRgb(220, 38,  38)),
        "Warning"  => new SolidColorBrush(Color.FromRgb(245, 158, 11)),
        "Excluded" => new SolidColorBrush(Color.FromRgb(16,  185, 129)),
        _          => new SolidColorBrush(Color.FromRgb(30,  64,  175)),
    };

    public Brush SeverityBadgeBackground => Severity switch
    {
        "Critical" => new SolidColorBrush(Color.FromRgb(254, 226, 226)),
        "Warning"  => new SolidColorBrush(Color.FromRgb(254, 243, 199)),
        "Excluded" => new SolidColorBrush(Color.FromRgb(209, 250, 229)),
        _          => new SolidColorBrush(Color.FromRgb(239, 246, 255)),
    };

    public Brush SeverityBadgeText => Severity switch
    {
        "Critical" => new SolidColorBrush(Color.FromRgb(153, 27,  27)),
        "Warning"  => new SolidColorBrush(Color.FromRgb(146, 64,  14)),
        "Excluded" => new SolidColorBrush(Color.FromRgb(6,   95,  70)),
        _          => new SolidColorBrush(Color.FromRgb(30,  64,  175)),
    };
}
