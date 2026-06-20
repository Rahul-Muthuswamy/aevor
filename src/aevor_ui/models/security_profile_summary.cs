using System.Windows.Media;

namespace Aevor.UI.Models;

public class SecurityProfileSummary
{
    public string ProfileName  { get; set; } = string.Empty;
    public int    RiskScore    { get; set; }
    public string RiskLabel    { get; set; } = "Low";
    public int    FindingCount { get; set; }
    public string LastScanned  { get; set; } = string.Empty;

    public string Initial => string.IsNullOrWhiteSpace(ProfileName) ? "?" : ProfileName[0].ToString().ToUpper();

    public Brush RiskColor => RiskLabel switch
    {
        "Critical" => new SolidColorBrush(Color.FromRgb(220, 38,  38)),
        "High"     => new SolidColorBrush(Color.FromRgb(239, 68,  68)),
        "Medium"   => new SolidColorBrush(Color.FromRgb(245, 158, 11)),
        _          => new SolidColorBrush(Color.FromRgb(16,  185, 129)),
    };
}
