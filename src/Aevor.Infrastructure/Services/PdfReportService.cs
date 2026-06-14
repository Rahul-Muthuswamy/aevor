using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Aevor.Application.Interfaces;

namespace Aevor.Infrastructure.Services;

public class PdfReportService : IPdfReportService
{
    public byte[] GenerateReport(string title, List<string> lines)
    {
        using var ms = new MemoryStream();
        var offsets = new List<long>();

        void WriteText(string text)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(text);
            ms.Write(bytes, 0, bytes.Length);
        }

        int StartObject()
        {
            int objId = offsets.Count + 1;
            offsets.Add(ms.Position);
            WriteText($"{objId} 0 obj\n");
            return objId;
        }

        void EndObject()
        {
            WriteText("endobj\n");
        }

        // Write header
        WriteText("%PDF-1.4\n");

        // We slice lines into pages of 50 lines each
        int linesPerPage = 50;
        var pagesList = new List<List<string>>();
        for (int i = 0; i < lines.Count; i += linesPerPage)
        {
            pagesList.Add(lines.GetRange(i, Math.Min(linesPerPage, lines.Count - i)));
        }
        if (pagesList.Count == 0)
        {
            pagesList.Add(new List<string>());
        }

        int pageCount = pagesList.Count;

        // Object 1: Catalog
        StartObject(); // Obj 1
        WriteText("<< /Type /Catalog /Pages 2 0 R >>\n");
        EndObject();

        // Object 2: Pages container
        var kidsBuilder = new StringBuilder();
        for (int p = 0; p < pageCount; p++)
        {
            int pageObjId = 4 + 2 * p;
            kidsBuilder.Append($"{pageObjId} 0 R ");
        }

        StartObject(); // Obj 2
        WriteText($"<< /Type /Pages /Kids [ {kidsBuilder.ToString().Trim()} ] /Count {pageCount} >>\n");
        EndObject();

        // Object 3: Font
        StartObject(); // Obj 3
        WriteText("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>\n");
        EndObject();

        // Write page objects and contents
        for (int p = 0; p < pageCount; p++)
        {
            int pageObjId = 4 + 2 * p;
            int contentObjId = 5 + 2 * p;

            // Page Object
            StartObject(); // Obj pageObjId
            WriteText($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources << /Font << /F1 3 0 R >> >> /Contents {contentObjId} 0 R >>\n");
            EndObject();

            // Prepare Content Stream
            var streamContent = new StringBuilder();
            streamContent.Append("BT\n/F1 10 Tf\n12 TL\n50 800 Td\n");

            if (p == 0)
            {
                // Title on the first page in Aevor pink/purple (0.796 0.424 0.902)
                streamContent.Append($"/F1 16 Tf\n0.796 0.424 0.902 rg\n({EscapePdfString(title)}) Tj\nT*\n0 g\nT*\n/F1 10 Tf\n");
            }

            foreach (var line in pagesList[p])
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    streamContent.Append("T*\n");
                }
                else
                {
                    string escaped = EscapePdfString(line);
                    if (line.StartsWith("===") || line.StartsWith("---"))
                    {
                        // Color separators in brand color
                        streamContent.Append($"0.796 0.424 0.902 rg\n({escaped}) Tj\nT*\n0 g\n");
                    }
                    else if (line.Contains("[Critical]") || line.Contains("[High]") || line.Contains("Severity: Critical") || line.StartsWith("Critical Findings"))
                    {
                        // Color critical alerts in red
                        streamContent.Append($"0.937 0.267 0.267 rg\n({escaped}) Tj\nT*\n0 g\n");
                    }
                    else if (line.Contains("[Warning]") || line.Contains("[Medium]") || line.Contains("Severity: Warning") || line.StartsWith("Warning Findings"))
                    {
                        // Color warnings in orange
                        streamContent.Append($"0.96 0.62 0.04 rg\n({escaped}) Tj\nT*\n0 g\n");
                    }
                    else if (line.Contains("Aevor Security Report") || line.Contains("Detailed Findings") || line.Contains("Profile Summaries"))
                    {
                        // Color sections in brand color
                        streamContent.Append($"0.796 0.424 0.902 rg\n({escaped}) Tj\nT*\n0 g\n");
                    }
                    else
                    {
                        streamContent.Append($"({escaped}) Tj\nT*\n");
                    }
                }
            }
            streamContent.Append("ET\n");

            byte[] streamBytes = Encoding.UTF8.GetBytes(streamContent.ToString());

            // Content Object
            StartObject(); // Obj contentObjId
            WriteText($"<< /Length {streamBytes.Length} >>\nstream\n");
            ms.Write(streamBytes, 0, streamBytes.Length);
            WriteText("\nendstream\n");
            EndObject();
        }

        // Cross-Reference Table
        long xrefPos = ms.Position;
        WriteText("xref\n");
        WriteText($"0 {offsets.Count + 1}\n");
        WriteText("0000000000 65535 f \n");
        foreach (var offset in offsets)
        {
            WriteText($"{offset:D10} 00000 n \n");
        }

        // Trailer
        WriteText("trailer\n");
        WriteText($"<< /Size {offsets.Count + 1} /Root 1 0 R >>\n");
        WriteText("startxref\n");
        WriteText($"{xrefPos}\n");
        WriteText("%%EOF\n");

        return ms.ToArray();
    }

    private string EscapePdfString(string input)
    {
        var sb = new StringBuilder();
        foreach (char c in input)
        {
            if (c == '(' || c == ')' || c == '\\')
            {
                sb.Append('\\').Append(c);
            }
            else if (c < 32 || c > 126)
            {
                sb.Append(' ');
            }
            else
            {
                sb.Append(c);
            }
        }
        return sb.ToString();
    }
}
