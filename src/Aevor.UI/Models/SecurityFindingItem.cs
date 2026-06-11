using System.Windows.Media;

namespace Aevor.UI.Models;

public class SecurityFindingItem
{
    public string Title    { get; set; } = string.Empty;
    public string Detail   { get; set; } = string.Empty;

    /// <summary>"info" | "warning" | "excluded"</summary>
    public string Severity { get; set; } = "info";

    public string SeverityIcon => Severity switch
    {
        "warning"  => "⚠",
        "excluded" => "○",
        _          => "ℹ",
    };

    public string SeverityLabel => Severity switch
    {
        "warning"  => "Warning",
        "excluded" => "Excluded",
        _          => "Info",
    };

    public Brush SeverityColor => Severity switch
    {
        "warning"  => new SolidColorBrush(Color.FromRgb(245, 158,  11)),  // #F59E0B
        "excluded" => new SolidColorBrush(Color.FromRgb(107, 114, 128)),  // #6B7280
        _          => new SolidColorBrush(Color.FromRgb(99,  102, 241)),  // #6366F1 indigo
    };

    public Brush SeverityBadgeBackground => Severity switch
    {
        "warning"  => new SolidColorBrush(Color.FromRgb(254, 243, 199)),  // #FEF3C7
        "excluded" => new SolidColorBrush(Color.FromRgb(243, 244, 246)),  // #F3F4F6
        _          => new SolidColorBrush(Color.FromRgb(238, 242, 255)),  // indigo-50
    };
}
