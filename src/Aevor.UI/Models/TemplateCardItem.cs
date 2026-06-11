using System.Windows.Media;

namespace Aevor.UI.Models;

public class TemplateCardItem
{
    public string TemplateName    { get; set; } = string.Empty;
    public string Browser         { get; set; } = "Brave Browser";
    public string Version         { get; set; } = "1.0";
    public string CreatedDate     { get; set; } = string.Empty;
    public int    ExtensionCount  { get; set; }
    public string Description     { get; set; } = string.Empty;

    /// <summary>Short label displayed on the badge — e.g. "Work", "Security".</summary>
    public string TagLabel { get; set; } = string.Empty;

    // ── Tag badge colors ────────────────────────────────────────────────
    public Brush TagBackgroundBrush { get; set; } =
        new SolidColorBrush(Color.FromRgb(237, 233, 254)); // #EDE9FE default (purple tint)

    public Brush TagTextBrush { get; set; } =
        new SolidColorBrush(Color.FromRgb(109, 40, 217));  // #6D28D9 default

    // ── Avatar initial ──────────────────────────────────────────────────
    public string Initial =>
        string.IsNullOrWhiteSpace(TemplateName) ? "?" : TemplateName[0].ToString().ToUpper();

    // ── Factory helpers for consistent tag styling ──────────────────────
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
            TagBackgroundBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(bgHex)),
            TagTextBrush       = new SolidColorBrush((Color)ColorConverter.ConvertFromString(fgHex)),
        };
    }
}
