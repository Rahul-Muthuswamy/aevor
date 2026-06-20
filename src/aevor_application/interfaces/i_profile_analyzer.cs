using Aevor.Core.Models;

namespace Aevor.Application.Interfaces;

public interface IProfileAnalyzer
{
    Task<ProfileAnalysisResult> AnalyzeAsync(BraveProfile profile);
}
