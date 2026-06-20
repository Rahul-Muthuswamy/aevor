using System;
using System.Collections.Generic;
using Aevor.Application.Interfaces;
using Aevor.Core.Models;

namespace Aevor.Infrastructure.Services;

public class TemplateValidator : ITemplateValidator
{
    public TemplateValidationResult Validate(AevorTemplate? template)
    {
        var errors = new List<TemplateError>();
        var warnings = new List<TemplateWarning>();

        if (template == null)
        {
            errors.Add(new TemplateError("Template is null.", "ERR_NULL_TEMPLATE"));
            return new TemplateValidationResult(false, errors, warnings);
        }

        if (template.Metadata == null)
        {
            errors.Add(new TemplateError("Template metadata is missing.", "ERR_MISSING_METADATA"));
        }
        else
        {
            if (string.IsNullOrWhiteSpace(template.Metadata.Name))
            {
                errors.Add(new TemplateError("Template name is missing or empty.", "ERR_INVALID_NAME"));
            }

            if (template.Metadata.TemplateVersion == null)
            {
                errors.Add(new TemplateError("Template version is missing.", "ERR_MISSING_VERSION"));
            }
            else
            {
                var versionStr = template.Metadata.TemplateVersion.ToString();
                if (versionStr != "1.0")
                {
                    errors.Add(new TemplateError($"Unsupported template version: '{versionStr}'. Supported version is '1.0'.", "ERR_UNSUPPORTED_VERSION"));
                }
            }

            if (string.IsNullOrWhiteSpace(template.Metadata.SourceBrowser))
            {
                errors.Add(new TemplateError("Source browser is missing or empty.", "ERR_INVALID_BROWSER"));
            }

            if (string.IsNullOrWhiteSpace(template.Metadata.SourceProfileName))
            {
                errors.Add(new TemplateError("Source profile name is missing or empty.", "ERR_INVALID_PROFILE"));
            }

            if (string.IsNullOrWhiteSpace(template.Metadata.GeneratorVersion))
            {
                errors.Add(new TemplateError("Generator version is missing or empty.", "ERR_INVALID_GENERATOR_VERSION"));
            }

            if (template.Metadata.CreatedTimestamp == default)
            {
                errors.Add(new TemplateError("Created timestamp is invalid.", "ERR_INVALID_TIMESTAMP"));
            }
        }

        if (template.Settings == null)
        {
            errors.Add(new TemplateError("Settings section is missing.", "ERR_MISSING_SETTINGS"));
        }
        else
        {
            if (template.Settings.Theme == null)
            {
                errors.Add(new TemplateError("Theme configuration is missing in settings.", "ERR_MISSING_THEME"));
            }
            if (template.Settings.SearchEngine == null)
            {
                errors.Add(new TemplateError("Search engine configuration is missing in settings.", "ERR_MISSING_SEARCH_ENGINE"));
            }
            if (template.Settings.Sidebar == null)
            {
                errors.Add(new TemplateError("Sidebar configuration is missing in settings.", "ERR_MISSING_SIDEBAR"));
            }
            if (template.Settings.VerticalTabs == null)
            {
                errors.Add(new TemplateError("Vertical tabs configuration is missing in settings.", "ERR_MISSING_VERTICAL_TABS"));
            }
            if (template.Settings.BrowserPreferences == null)
            {
                errors.Add(new TemplateError("Browser preferences dictionary is missing in settings.", "ERR_MISSING_PREFERENCES"));
            }
        }

        if (template.Extensions == null)
        {
            errors.Add(new TemplateError("Extensions section is missing.", "ERR_MISSING_EXTENSIONS"));
        }
        else
        {
            for (int i = 0; i < template.Extensions.Count; i++)
            {
                var ext = template.Extensions[i];
                if (ext == null)
                {
                    errors.Add(new TemplateError($"Extension at index {i} is null.", "ERR_NULL_EXTENSION"));
                    continue;
                }

                if (string.IsNullOrWhiteSpace(ext.Id))
                {
                    errors.Add(new TemplateError($"Extension at index {i} has a missing or empty ID.", "ERR_INVALID_EXTENSION_ID"));
                }
                if (string.IsNullOrWhiteSpace(ext.Name))
                {
                    errors.Add(new TemplateError($"Extension at index {i} ({ext.Id}) has a missing or empty name.", "ERR_INVALID_EXTENSION_NAME"));
                }
                if (string.IsNullOrWhiteSpace(ext.Version))
                {
                    errors.Add(new TemplateError($"Extension at index {i} ({ext.Id}) has a missing or empty version.", "ERR_INVALID_EXTENSION_VERSION"));
                }
            }
        }

        if (template.Assets == null)
        {
            errors.Add(new TemplateError("Assets section is missing.", "ERR_MISSING_ASSETS"));
        }

        if (template.Warnings == null)
        {
            errors.Add(new TemplateError("Warnings section is missing.", "ERR_MISSING_WARNINGS_SECTION"));
        }
        if (template.ExcludedArtifacts == null)
        {
            errors.Add(new TemplateError("Excluded artifacts section is missing.", "ERR_MISSING_EXCLUSIONS_SECTION"));
        }
        else
        {
            for (int i = 0; i < template.ExcludedArtifacts.Count; i++)
            {
                var excl = template.ExcludedArtifacts[i];
                if (excl == null)
                {
                    errors.Add(new TemplateError($"Excluded artifact at index {i} is null.", "ERR_NULL_EXCLUDED_ARTIFACT"));
                    continue;
                }

                if (string.IsNullOrWhiteSpace(excl.Name))
                {
                    errors.Add(new TemplateError($"Excluded artifact at index {i} has a missing or empty name.", "ERR_INVALID_EXCLUSION_NAME"));
                }
                if (string.IsNullOrWhiteSpace(excl.Path))
                {
                    errors.Add(new TemplateError($"Excluded artifact at index {i} has a missing or empty path.", "ERR_INVALID_EXCLUSION_PATH"));
                }
            }
        }

        return new TemplateValidationResult(
            IsValid: errors.Count == 0,
            Errors: errors,
            Warnings: warnings
        );
    }
}
