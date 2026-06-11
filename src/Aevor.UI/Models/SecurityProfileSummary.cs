using System.Windows.Media;

namespace Aevor.UI.Models;

public class SecurityProfileSummary
{
    public string ProfileName  { get; set; } = string.Empty;
    public int    RiskScore    { get; set; }
    public string RiskLabel    { get; set; } = "Low";
    public int    FindingCount { get; set; }
    public string LastScanned  { get; set; } = string.Empty;

    // Avatar initial
    public string Initial => string.IsNullOrWhiteSpace(ProfileName) ? "?" : ProfileName[0].ToString().ToUpper();

    // RiskColor derived brush
    public Brush RiskColor => RiskLabel switch
    {
        "Critical" => new SolidColorBrush(Color.FromRgb(220, 38,  38)),  // #DC2626
        "High"     => new SolidColorBrush(Color.FromRgb(239, 68,  68)),  // #EF4444
        "Medium"   => new SolidColorBrush(Color.FromRgb(245, 158, 11)),  // #F59E0B
        _          => new SolidColorBrush(Color.FromRgb(16,  185, 129)), // #10B981 (Low)
    };
}
