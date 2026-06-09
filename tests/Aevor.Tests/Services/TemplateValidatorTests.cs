using System;
using System.Collections.Generic;
using FluentAssertions;
using Xunit;
using Aevor.Core.Models;
using Aevor.Infrastructure.Services;

namespace Aevor.Tests.Services;

public class TemplateValidatorTests
{
    private readonly TemplateValidator _validator = new();

    private AevorTemplate CreateValidTemplate()
    {
        return new AevorTemplate(
            Metadata: new TemplateMetadata(
                Name: "Valid Template",
                Description: "A valid test template",
                CreatedTimestamp: DateTime.UtcNow,
                TemplateVersion: TemplateVersion.V1_0,
                SourceBrowser: "Brave",
                SourceBrowserVersion: "1.0.0",
                SourceProfileName: "Default",
                GeneratorVersion: "1.0.0"
            ),
            Settings: new TemplateSettings(
                Theme: new ThemeInformation("id", "Dark", 0),
                SearchEngine: new SearchEngineInformation("Brave", "brave", "url"),
                Sidebar: new SidebarConfiguration(true, "Right"),
                VerticalTabs: new VerticalTabsConfiguration(false),
                BrowserPreferences: new Dictionary<string, object>()
            ),
            Extensions: new List<ExtensionInfo>
            {
                new("ext_id", "Ext Name", "1.0.0", true)
            },
            Assets: new TemplateAssets(null, null, new Dictionary<string, string>()),
            Warnings: new List<TemplateWarning>(),
            ExcludedArtifacts: new List<ExcludedArtifact>()
        );
    }

    [Fact]
    public void Validate_ValidTemplate_ShouldBeValidAndHaveNoErrors()
    {
        // Arrange
        var template = CreateValidTemplate();

        // Act
        var result = _validator.Validate(template);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
        result.Warnings.Should().BeEmpty();
    }

    [Fact]
    public void Validate_NullTemplate_ShouldReturnError()
    {
        // Act
        var result = _validator.Validate(null);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle().Which.Code.Should().Be("ERR_NULL_TEMPLATE");
    }

    [Fact]
    public void Validate_MissingMetadata_ShouldReturnError()
    {
        // Arrange
        var template = CreateValidTemplate() with { Metadata = null! };

        // Act
        var result = _validator.Validate(template);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Code == "ERR_MISSING_METADATA");
    }

    [Fact]
    public void Validate_MissingName_ShouldReturnError()
    {
        // Arrange
        var validTemplate = CreateValidTemplate();
        var template = validTemplate with { Metadata = validTemplate.Metadata with { Name = "" } };

        // Act
        var result = _validator.Validate(template);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Code == "ERR_INVALID_NAME");
    }

    [Fact]
    public void Validate_MissingVersion_ShouldReturnError()
    {
        // Arrange
        var validTemplate = CreateValidTemplate();
        var template = validTemplate with { Metadata = validTemplate.Metadata with { TemplateVersion = null! } };

        // Act
        var result = _validator.Validate(template);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Code == "ERR_MISSING_VERSION");
    }

    [Fact]
    public void Validate_InvalidVersion_ShouldReturnError()
    {
        // Arrange
        var validTemplate = CreateValidTemplate();
        var template = validTemplate with { Metadata = validTemplate.Metadata with { TemplateVersion = new TemplateVersion(2, 0) } };

        // Act
        var result = _validator.Validate(template);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Code == "ERR_UNSUPPORTED_VERSION");
    }

    [Fact]
    public void Validate_MissingSettings_ShouldReturnError()
    {
        // Arrange
        var template = CreateValidTemplate() with { Settings = null! };

        // Act
        var result = _validator.Validate(template);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Code == "ERR_MISSING_SETTINGS");
    }

    [Fact]
    public void Validate_MissingExtensions_ShouldReturnError()
    {
        // Arrange
        var template = CreateValidTemplate() with { Extensions = null! };

        // Act
        var result = _validator.Validate(template);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Code == "ERR_MISSING_EXTENSIONS");
    }

    [Fact]
    public void Validate_MissingExtensionFields_ShouldReturnErrors()
    {
        // Arrange
        var validTemplate = CreateValidTemplate();
        var template = validTemplate with { Extensions = new List<ExtensionInfo> { new("", "Name", "1.0", true) } };

        // Act
        var result = _validator.Validate(template);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Code == "ERR_INVALID_EXTENSION_ID");
    }
}
