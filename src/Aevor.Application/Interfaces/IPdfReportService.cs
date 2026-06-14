using System.Collections.Generic;

namespace Aevor.Application.Interfaces;

public interface IPdfReportService
{
    byte[] GenerateReport(string title, List<string> lines);
}
