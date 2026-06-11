using System.Windows.Media;

namespace Aevor.UI.Models;

public class ProfileCardItem
{
    public string ProfileName { get; set; } = string.Empty;
    public string Browser     { get; set; } = "Brave Browser";
    public int    RiskScore   { get; set; }       // 0–100
    public string RiskLabel   { get; set; } = "Low";
    public int    ExtensionCount { get; set; }
    public string LastUsed    { get; set; } = string.Empty;
    public bool   IsSelected  { get; set; }

    // ── Computed ────────────────────────────────────────────────────────
    public Brush RiskColor => RiskLabel switch
    {
        "High"   => new SolidColorBrush(Color.FromRgb(239, 68,  68)),   // #EF4444
        "Medium" => new SolidColorBrush(Color.FromRgb(245, 158, 11)),   // #F59E0B
        _        => new SolidColorBrush(Color.FromRgb(16,  185, 129)),  // #10B981
    };

    public Brush RiskBadgeBackgroundBrush => RiskLabel switch
    {
        "High"   => new SolidColorBrush(Color.FromRgb(254, 226, 226)),  // #FEE2E2
        "Medium" => new SolidColorBrush(Color.FromRgb(254, 243, 199)),  // #FEF3C7
        _        => new SolidColorBrush(Color.FromRgb(209, 250, 229)),  // #D1FAE5
    };

    public Brush RiskBadgeTextBrush => RiskLabel switch
    {
        "High"   => new SolidColorBrush(Color.FromRgb(153, 27,  27)),   // #991B1B
        "Medium" => new SolidColorBrush(Color.FromRgb(146, 64,  14)),   // #92400E
        _        => new SolidColorBrush(Color.FromRgb(6,   95,  70)),   // #065F46
    };

    // Avatar: first letter of the profile name
    public string Initial => string.IsNullOrWhiteSpace(ProfileName) ? "?" : ProfileName[0].ToString().ToUpper();
}
