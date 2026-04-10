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
