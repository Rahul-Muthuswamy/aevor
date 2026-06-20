using Aevor.Core.Models;

namespace Aevor.Application.Interfaces;

public interface ITemplateBuilder
{
    AevorTemplate Build(
        ProfileAnalysisResult analysisResult,
        SecurityScanResult scanResult,
        string templateName,
        string templateDescription,
        string generatorVersion = "1.0.0"
    );
}
