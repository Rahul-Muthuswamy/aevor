using System.Collections.Generic;
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

    public List<string> Extensions { get; set; } = new();
    public List<SecurityFindingItem> SecurityFindings { get; set; } = new();

    public BraveProfile? SourceProfile { get; set; }

    public Brush RiskColor => RiskLabel switch
    {
        "High"   => new SolidColorBrush(Color.FromRgb(239, 68,  68)),
        "Medium" => new SolidColorBrush(Color.FromRgb(245, 158, 11)),
        _        => new SolidColorBrush(Color.FromRgb(16,  185, 129)),
    };

    public Brush RiskBadgeBackgroundBrush => RiskLabel switch
    {
        "High"   => new SolidColorBrush(Color.FromRgb(254, 226, 226)),
        "Medium" => new SolidColorBrush(Color.FromRgb(254, 243, 199)),
        _        => new SolidColorBrush(Color.FromRgb(209, 250, 229)),
    };

    public Brush RiskBadgeTextBrush => RiskLabel switch
    {
        "High"   => new SolidColorBrush(Color.FromRgb(153, 27,  27)),
        "Medium" => new SolidColorBrush(Color.FromRgb(146, 64,  14)),
        _        => new SolidColorBrush(Color.FromRgb(6,   95,  70)),
    };

    public string Initial => string.IsNullOrWhiteSpace(ProfileName) ? "?" : ProfileName[0].ToString().ToUpper();

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
