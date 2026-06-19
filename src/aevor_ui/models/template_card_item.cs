using System.Windows.Media;
using Aevor.Core.Models;

namespace Aevor.UI.Models;

public class TemplateCardItem
{
    public string TemplateName    { get; set; } = string.Empty;
    public string Browser         { get; set; } = "Brave Browser";
    public string Version         { get; set; } = "1.0";
    public string CreatedDate     { get; set; } = string.Empty;
    public int    ExtensionCount  { get; set; }
    public string Description     { get; set; } = string.Empty;

    public string TagLabel { get; set; } = string.Empty;

    private static Brush CreateFrozenBrush(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }

    private static Brush CreateFrozenBrush(string hex)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        brush.Freeze();
        return brush;
    }

    public Brush TagBackgroundBrush { get; set; } = CreateFrozenBrush(237, 233, 254);
    public Brush TagTextBrush { get; set; } = CreateFrozenBrush(109, 40, 217);

    public string Initial =>
        string.IsNullOrWhiteSpace(TemplateName) ? "?" : TemplateName[0].ToString().ToUpper();

    public AevorTemplate? SourceTemplate { get; set; }

    public string? FilePath { get; set; }

    public static TemplateCardItem Create(
        string name, string browser, string version,
        string created, int extensions, string description,
        string tag, string bgHex, string fgHex)
    {
        return new TemplateCardItem
        {
            TemplateName       = name,
            Browser            = browser,
            Version            = version,
            CreatedDate        = created,
            ExtensionCount     = extensions,
            Description        = description,
            TagLabel           = tag,
            TagBackgroundBrush = CreateFrozenBrush(bgHex),
            TagTextBrush       = CreateFrozenBrush(fgHex),
        };
    }
}
