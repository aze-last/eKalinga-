using AttendanceShiftingManagement.Services;
using System.Data;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;

namespace AttendanceShiftingManagement.Tests;

public sealed class ReportDocumentServiceTests
{
    [Fact]
    public void ReportPdfExportService_SavesSnapshotAsPdfFile()
    {
        var table = new DataTable();
        table.Columns.Add("Beneficiary", typeof(string));
        table.Columns.Add("Amount", typeof(string));
        table.Rows.Add("Maria Santos", "PHP 1,500.00");

        var snapshot = new ReportsSnapshot
        {
            Title = "Distribution Claims",
            Subtitle = "Claim log rows captured in scope.",
            ExportFilePrefix = "distribution-claims",
            RangeLabel = "Apr 01, 2026 to Apr 24, 2026",
            ProgramLabel = "All Programs",
            Table = table,
            Metrics =
            [
                new ReportsMetricItem { Label = "Claims", Value = "1", Note = "Rows captured" }
            ]
        };

        var filePath = Path.Combine(Path.GetTempPath(), $"report-export-{Guid.NewGuid():N}.pdf");
        try
        {
            new ReportPdfExportService().Save(snapshot, filePath, new ReportPdfExportOptions
            {
                PreparedBy = "Admin",
                GeneratedAt = new DateTime(2026, 4, 24, 9, 0, 0)
            });

            var bytes = File.ReadAllBytes(filePath);
            var pdfText = System.Text.Encoding.ASCII.GetString(bytes);
            Assert.True(bytes.Length > 100);
            Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(bytes, 0, 4));
            Assert.Contains("/SystemLogo", pdfText, StringComparison.Ordinal);
            Assert.Contains("/AppLogo", pdfText, StringComparison.Ordinal);
            Assert.Contains("/Subtype /Image", pdfText, StringComparison.Ordinal);
        }
        finally
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
    }

    [Fact]
    public void BuildDocument_UsesCompactHeaderWithCountAndGeneratedLine()
    {
        Exception? capturedException = null;

        var thread = new Thread(() =>
        {
            try
            {
                WpfTestHost.EnsureApplication();

                var table = new DataTable();
                table.Columns.Add("Name", typeof(string));
                table.Rows.Add("Scholar 1");
                table.Rows.Add("Scholar 2");

                var snapshot = new ReportsSnapshot
                {
                    Title = "Approved Scholars Report",
                    Subtitle = "Approved scholars included in the current release batch.",
                    ExportFilePrefix = "validated-beneficiaries",
                    RangeLabel = "Apr 01, 2026 to Apr 07, 2026",
                    ProgramLabel = "All Programs",
                    Table = table
                };

                var generatedAt = new DateTime(2026, 4, 7, 13, 18, 0);
                var document = new ReportDocumentService().BuildDocument(snapshot, new ReportDocumentOptions
                {
                    IncludeLogo = false,
                    PreparedBy = "Admin",
                    GeneratedAt = generatedAt
                });

                var firstBlock = Assert.IsType<BlockUIContainer>(document.Blocks.FirstBlock);
                var headerGrid = Assert.IsType<Grid>(firstBlock.Child);
                var headerTexts = CollectTextBlocks(headerGrid);

                Assert.Contains("Approved Scholars Report", headerTexts);
                Assert.Contains("Total: 2 beneficiaries | Generated: Apr 07, 2026 13:18", headerTexts);

                var metadataParagraph = Assert.IsType<Paragraph>(document.Blocks.Cast<Block>().ElementAt(1));
                var metadataText = new TextRange(metadataParagraph.ContentStart, metadataParagraph.ContentEnd).Text;

                Assert.Contains("Report Range:", metadataText);
                Assert.Contains("Program Scope:", metadataText);
                Assert.Contains("Prepared By:", metadataText);
                Assert.DoesNotContain("Generated:", metadataText);
            }
            catch (Exception ex)
            {
                capturedException = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        Assert.True(capturedException is null, capturedException?.ToString());
    }

    private static List<string> CollectTextBlocks(DependencyObject root)
    {
        var texts = new List<string>();
        CollectTextBlocks(root, texts);
        return texts;
    }

    private static void CollectTextBlocks(DependencyObject root, ICollection<string> texts)
    {
        if (root is TextBlock textBlock && !string.IsNullOrWhiteSpace(textBlock.Text))
        {
            texts.Add(textBlock.Text);
        }

        foreach (var child in LogicalTreeHelper.GetChildren(root))
        {
            if (child is DependencyObject childDependencyObject)
            {
                CollectTextBlocks(childDependencyObject, texts);
            }
        }
    }
}
