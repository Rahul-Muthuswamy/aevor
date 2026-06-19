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

    public Brush ValidationColor => ValidationStatus switch
    {
        "Invalid" => new SolidColorBrush(Color.FromRgb(239, 68,  68)),
        "Warning" => new SolidColorBrush(Color.FromRgb(245, 158, 11)),
        "Unknown" => new SolidColorBrush(Color.FromRgb(156, 163, 175)),
        _         => new SolidColorBrush(Color.FromRgb(16,  185, 129)),
    };

    public Brush ValidationBadgeBackground => ValidationStatus switch
    {
        "Invalid" => new SolidColorBrush(Color.FromRgb(254, 226, 226)),
        "Warning" => new SolidColorBrush(Color.FromRgb(254, 243, 199)),
        "Unknown" => new SolidColorBrush(Color.FromRgb(243, 244, 246)),
        _         => new SolidColorBrush(Color.FromRgb(209, 250, 229)),
    };

    public Brush ValidationBadgeText => ValidationStatus switch
    {
        "Invalid" => new SolidColorBrush(Color.FromRgb(153, 27,  27)),
        "Warning" => new SolidColorBrush(Color.FromRgb(146, 64,  14)),
        "Unknown" => new SolidColorBrush(Color.FromRgb(55,  65,  81)),
        _         => new SolidColorBrush(Color.FromRgb(6,   95,  70)),
    };
}
