using System.Threading.Tasks;
using Aevor.Core.Models;

namespace Aevor.Application.Interfaces;

public interface ITemplateSerializer
{
    string Serialize(AevorTemplate template, bool prettyPrint = true);
    AevorTemplate Deserialize(string json);
    Task SaveToFileAsync(string filePath, AevorTemplate template, bool prettyPrint = true);
    Task<AevorTemplate> LoadFromFileAsync(string filePath);
}
