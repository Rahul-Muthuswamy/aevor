using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using Aevor.Core.Models;

namespace Aevor.UI.Models;

public class ProfileCardItem : INotifyPropertyChanged
{
    private string _profileName = string.Empty;
    public string ProfileName
    {
        get => _profileName;
        set { _profileName = value; OnPropertyChanged(); OnPropertyChanged(nameof(Initial)); }
    }

    public string Browser { get; set; } = "Brave Browser";

    private int _riskScore;
    public int RiskScore
    {
        get => _riskScore;
        set { _riskScore = value; OnPropertyChanged(); }
    }

    private string _riskLabel = "Low";
    public string RiskLabel
    {
        get => _riskLabel;
        set
        {
            _riskLabel = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(RiskColor));
            OnPropertyChanged(nameof(RiskBadgeBackgroundBrush));
            OnPropertyChanged(nameof(RiskBadgeTextBrush));
        }
    }

    private int _extensionCount;
    public int ExtensionCount
    {
        get => _extensionCount;
        set { _extensionCount = value; OnPropertyChanged(); }
    }

    public string LastUsed { get; set; } = string.Empty;
    public bool   IsSelected { get; set; }

    /// <summary>
    /// Reference to the raw BraveProfile for backend service calls.
    /// </summary>
    public BraveProfile? SourceProfile { get; set; }

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

    // ── INotifyPropertyChanged ─────────────────────────────────────────
    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

