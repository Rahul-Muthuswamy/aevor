using System;
using System.Collections.Generic;
using System.Linq;
using Aevor.Application.Interfaces;
using Aevor.Core.Models;

namespace Aevor.Infrastructure.Services;

public class TemplateBuilder : ITemplateBuilder
{
    public AevorTemplate Build(
        ProfileAnalysisResult analysisResult,
        SecurityScanResult scanResult,
        string templateName,
        string templateDescription,
        string generatorVersion = "1.0.0")
    {
        if (analysisResult == null)
        {
            throw new ArgumentNullException(nameof(analysisResult));
        }

        if (scanResult == null)
        {
            throw new ArgumentNullException(nameof(scanResult));
        }

        // 1. Metadata
        var metadata = new TemplateMetadata(
            Name: templateName,
            Description: templateDescription,
            CreatedTimestamp: DateTime.UtcNow,
            TemplateVersion: TemplateVersion.V1_0,
            SourceBrowser: "Brave",
            SourceBrowserVersion: "1.0.0",
            SourceProfileName: analysisResult.ProfileName,
            GeneratorVersion: generatorVersion
        );

        // 2. Settings (excluding sensitive findings and using values from analysisResult)
        var browserPreferences = new Dictionary<string, object>();
        var settings = new TemplateSettings(
            Theme: analysisResult.Theme,
            SearchEngine: analysisResult.SearchEngine,
            Sidebar: analysisResult.Sidebar,
            VerticalTabs: analysisResult.VerticalTabs,
            BrowserPreferences: browserPreferences
        );

        // 3. Extensions
        var extensions = analysisResult.InstalledExtensions ?? new List<ExtensionInfo>();

        // 4. Assets placeholder
        var assets = new TemplateAssets(
            Wallpaper: null,
            Icon: null,
            FutureAssets: new Dictionary<string, string>()
        );

        // 5. Exclusions and Warnings from scan results
        var excludedArtifacts = new List<ExcludedArtifact>();
        var warnings = new List<TemplateWarning>();

        if (scanResult.Findings != null)
        {
            foreach (var finding in scanResult.Findings)
            {
                excludedArtifacts.Add(new ExcludedArtifact(
                    Name: finding.Name,
                    Path: finding.Path,
                    Reason: finding.Description
                ));

                warnings.Add(new TemplateWarning(
                    Message: $"Sensitive artifact '{finding.Name}' at '{finding.Path}' was automatically excluded.",
                    Code: "SEC_EXCLUSION"
                ));
            }
        }

        // Add warnings from profile analysis
        if (analysisResult.Warnings != null)
        {
            foreach (var warn in analysisResult.Warnings)
            {
                warnings.Add(new TemplateWarning(
                    Message: warn,
                    Code: "ANALYSIS_WARNING"
                ));
            }
        }

        // Add errors from profile analysis
        if (analysisResult.Errors != null)
        {
            foreach (var err in analysisResult.Errors)
            {
                warnings.Add(new TemplateWarning(
                    Message: err,
                    Code: "ANALYSIS_ERROR"
                ));
            }
        }

        // Check overall export safety
        if (!scanResult.ExportSafe)
        {
            warnings.Add(new TemplateWarning(
                Message: "Profile contained sensitive credentials or wallet data. Template was sanitized.",
                Code: "SEC_EXPORT_UNSAFE"
            ));
        }

        return new AevorTemplate(
            Metadata: metadata,
            Settings: settings,
            Extensions: extensions,
            Assets: assets,
            Warnings: warnings,
            ExcludedArtifacts: excludedArtifacts
        );
    }
}
