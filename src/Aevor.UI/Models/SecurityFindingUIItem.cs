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

    // ── Computed ────────────────────────────────────────────────────────
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
        SecuritySeverity.Critical => new SolidColorBrush(Color.FromRgb(220, 38,  38)),  // Dark Red: #DC2626
        SecuritySeverity.High     => new SolidColorBrush(Color.FromRgb(239, 68,  68)),  // Red: #EF4444
        SecuritySeverity.Medium   => new SolidColorBrush(Color.FromRgb(245, 158, 11)),  // Amber: #F59E0B
        SecuritySeverity.Low      => new SolidColorBrush(Color.FromRgb(59,  130, 246)), // Blue: #3B82F6
        _                         => new SolidColorBrush(Color.FromRgb(107, 114, 128)) // Muted: #6B7280
    };

    public Brush SeverityBadgeBackground => Severity switch
    {
        SecuritySeverity.Critical => new SolidColorBrush(Color.FromRgb(254, 226, 226)), // light red: #FEE2E2
        SecuritySeverity.High     => new SolidColorBrush(Color.FromRgb(254, 226, 226)), // light red: #FEE2E2
        SecuritySeverity.Medium   => new SolidColorBrush(Color.FromRgb(254, 243, 199)), // light amber: #FEF3C7
        SecuritySeverity.Low      => new SolidColorBrush(Color.FromRgb(219, 234, 254)), // light blue: #DBEAFE
        _                         => new SolidColorBrush(Color.FromRgb(243, 244, 246)) // light gray: #F3F4F6
    };

    public static SecurityFindingUIItem FromFinding(SecurityFinding finding)
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
