using System;
using System.Windows.Media;
using Aevor.Core.Models;

namespace Aevor.UI.Models;

public class SecurityFindingUIItem
{
    public string           Name        { get; set; } = string.Empty;
    public string           Category    { get; set; } = string.Empty;
    public SecuritySeverity Severity    { get; set; }
    public string           Description { get; set; } = string.Empty;
    public string           Path        { get; set; } = string.Empty;

    public string SeverityLabel => Severity.ToString();

    public string SeverityIcon => Severity switch
    {
        SecuritySeverity.Critical => "☠",
        SecuritySeverity.High     => "⚠",
        SecuritySeverity.Medium   => "⚠",
        _                         => "ℹ"
    };

    public Brush SeverityColor => Severity switch
    {
        SecuritySeverity.Critical => new SolidColorBrush(Color.FromRgb(220, 38,  38)),
        SecuritySeverity.High     => new SolidColorBrush(Color.FromRgb(239, 68,  68)),
        SecuritySeverity.Medium   => new SolidColorBrush(Color.FromRgb(245, 158, 11)),
        SecuritySeverity.Low      => new SolidColorBrush(Color.FromRgb(59,  130, 246)),
        _                         => new SolidColorBrush(Color.FromRgb(107, 114, 128))
    };

    public Brush SeverityBadgeBackground => Severity switch
    {
        SecuritySeverity.Critical => new SolidColorBrush(Color.FromRgb(254, 226, 226)),
        SecuritySeverity.High     => new SolidColorBrush(Color.FromRgb(254, 226, 226)),
        SecuritySeverity.Medium   => new SolidColorBrush(Color.FromRgb(254, 243, 199)),
        SecuritySeverity.Low      => new SolidColorBrush(Color.FromRgb(219, 234, 254)),
        _                         => new SolidColorBrush(Color.FromRgb(243, 244, 246))
    };

    public static SecurityFindingUIItem FromFinding(Aevor.Core.Models.SecurityFinding finding)
    {
        return new SecurityFindingUIItem
        {
            Name        = finding.Name,
            Category    = finding.Category,
            Severity    = finding.Severity,
            Description = finding.Description,
            Path        = finding.Path
        };
    }
}
