using System;
using System.Windows.Media;
using Aevor.UI.ViewModels;

namespace Aevor.UI.Models;

public class BackupItem : BaseViewModel
{
    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
    public Guid   BackupId        { get; set; }
    public string BackupName      { get; set; } = string.Empty;
    public string ProfileName     { get; set; } = string.Empty;
    public string CreatedDate     { get; set; } = string.Empty;
    public string Size            { get; set; } = string.Empty;
    public long   SizeBytes       { get; set; }
    public string ValidationStatus { get; set; } = "Valid";
    public string Notes           { get; set; } = string.Empty;

    // ── Badges & Brushes ────────────────────────────────────────────────
    public Brush ValidationColor => ValidationStatus switch
    {
        "Invalid" => new SolidColorBrush(Color.FromRgb(239, 68,  68)),  // #EF4444
        "Warning" => new SolidColorBrush(Color.FromRgb(245, 158, 11)),  // #F59E0B
        "Unknown" => new SolidColorBrush(Color.FromRgb(156, 163, 175)), // #9CA3AF
        _         => new SolidColorBrush(Color.FromRgb(16,  185, 129)), // #10B981
    };

    public Brush ValidationBadgeBackground => ValidationStatus switch
    {
        "Invalid" => new SolidColorBrush(Color.FromRgb(254, 226, 226)), // #FEE2E2
        "Warning" => new SolidColorBrush(Color.FromRgb(254, 243, 199)), // #FEF3C7
        "Unknown" => new SolidColorBrush(Color.FromRgb(243, 244, 246)), // #F3F4F6
        _         => new SolidColorBrush(Color.FromRgb(209, 250, 229)), // #D1FAE5
    };

    public Brush ValidationBadgeText => ValidationStatus switch
    {
        "Invalid" => new SolidColorBrush(Color.FromRgb(153, 27,  27)),  // #991B1B
        "Warning" => new SolidColorBrush(Color.FromRgb(146, 64,  14)),  // #92400E
        "Unknown" => new SolidColorBrush(Color.FromRgb(55,  65,  81)),  // #374151
        _         => new SolidColorBrush(Color.FromRgb(6,   95,  70)),  // #065F46
    };
}
