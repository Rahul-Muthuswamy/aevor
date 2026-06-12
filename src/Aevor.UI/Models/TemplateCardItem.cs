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

    /// <summary>Short label displayed on the badge — e.g. "Work", "Security".</summary>
    public string TagLabel { get; set; } = string.Empty;

    // ── Tag badge colors ────────────────────────────────────────────────
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

    public Brush TagBackgroundBrush { get; set; } = CreateFrozenBrush(237, 233, 254); // #EDE9FE default (purple tint)
    public Brush TagTextBrush { get; set; } = CreateFrozenBrush(109, 40, 217);  // #6D28D9 default

    // ── Avatar initial ──────────────────────────────────────────────────
    public string Initial =>
        string.IsNullOrWhiteSpace(TemplateName) ? "?" : TemplateName[0].ToString().ToUpper();

    /// <summary>Reference to the raw AevorTemplate for backend service calls.</summary>
    public AevorTemplate? SourceTemplate { get; set; }

    /// <summary>Full path to the .json file on disk.</summary>
    public string? FilePath { get; set; }

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
            TagBackgroundBrush = CreateFrozenBrush(bgHex),
            TagTextBrush       = CreateFrozenBrush(fgHex),
        };
    }
}

