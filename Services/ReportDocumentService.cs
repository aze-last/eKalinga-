using AttendanceShiftingManagement.Helpers;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace AttendanceShiftingManagement.Services
{
    public sealed class ReportDocumentOptions
    {
        public string PreparedBy { get; init; } = string.Empty;
        public bool IncludeLogo { get; init; } = true;
        public DateTime? GeneratedAt { get; init; }
    }

    public sealed class ReportDocumentService
    {
        public FlowDocument BuildDocument(ReportsSnapshot snapshot, ReportDocumentOptions? options = null)
        {
            options ??= new ReportDocumentOptions();
            var branding = SystemProfileSettingsService.BuildLoginBranding(SystemProfileSettingsService.Load());
            var document = new FlowDocument
            {
                PagePadding = new Thickness(42),
                ColumnGap = 0,
                ColumnWidth = double.PositiveInfinity,
                PageWidth = string.Equals(snapshot.SuggestedOrientation, "Landscape", StringComparison.OrdinalIgnoreCase) ? 1122 : 793,
                PageHeight = string.Equals(snapshot.SuggestedOrientation, "Landscape", StringComparison.OrdinalIgnoreCase) ? 793 : 1122,
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 12,
                Foreground = CreateBrush("#0F172A")
            };

            var logoSource = options.IncludeLogo ? LocalImageLoader.Load(branding.LogoPath) : null;
            var generatedAt = options.GeneratedAt ?? DateTime.Now;

            document.Blocks.Add(BuildHeaderBlock(snapshot, branding, logoSource, generatedAt));

            document.Blocks.Add(BuildMetadataParagraph(snapshot, options.PreparedBy));
            document.Blocks.Add(BuildMetricTable(snapshot.Metrics));

            if (snapshot.Highlights.Count > 0)
            {
                document.Blocks.Add(new Paragraph(new Run("Executive Summary"))
                {
                    FontSize = 14,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = CreateBrush("#0F172A"),
                    Margin = new Thickness(0, 16, 0, 8)
                });

                var list = new List { MarkerStyle = TextMarkerStyle.Disc, Margin = new Thickness(18, 0, 0, 12) };
                foreach (var highlight in snapshot.Highlights)
                {
                    list.ListItems.Add(new ListItem(new Paragraph(new Run(highlight)) { Margin = new Thickness(0, 0, 0, 4), Foreground = CreateBrush("#334155") }));
                }

                document.Blocks.Add(list);
            }

            document.Blocks.Add(new Paragraph(new Run("Detailed Table"))
            {
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                Foreground = CreateBrush("#0F172A"),
                Margin = new Thickness(0, 12, 0, 8)
            });

            document.Blocks.Add(BuildPreviewTable(snapshot.Table));
            document.Blocks.Add(new Paragraph(new Run("Prepared by: ____________________     Reviewed by: ____________________     Approved by: ____________________"))
            {
                Margin = new Thickness(0, 24, 0, 6),
                Foreground = CreateBrush("#334155")
            });

            document.Blocks.Add(new Paragraph(new Run("System-generated report. Use Print and choose Microsoft Print to PDF when a PDF file is needed."))
            {
                FontSize = 10,
                Foreground = CreateBrush("#64748B"),
                Margin = new Thickness(0)
            });

            return document;
        }

        private static BlockUIContainer BuildHeaderBlock(
            ReportsSnapshot snapshot,
            SystemLoginBrandingSnapshot branding,
            ImageSource? logoSource,
            DateTime generatedAt)
        {
            var headerGrid = new Grid
            {
                Margin = new Thickness(0, 0, 0, 10),
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            if (logoSource != null)
            {
                var logo = new Image
                {
                    Source = logoSource,
                    Width = 66,
                    Height = 66,
                    Stretch = Stretch.Uniform,
                    Margin = new Thickness(0, 0, 16, 0),
                    VerticalAlignment = VerticalAlignment.Top
                };

                Grid.SetColumn(logo, 0);
                headerGrid.Children.Add(logo);
            }

            var textStack = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Top
            };
            Grid.SetColumn(textStack, 1);

            textStack.Children.Add(new TextBlock
            {
                Text = (branding.Title ?? "Local Government Unit").Trim().ToUpperInvariant(),
                FontSize = 11,
                FontWeight = FontWeights.Medium,
                Foreground = CreateBrush("#8A5A2B")
            });

            textStack.Children.Add(new TextBlock
            {
                Text = string.IsNullOrWhiteSpace(branding.Subtitle) ? "eKalinga+" : branding.Subtitle.Trim(),
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Foreground = CreateBrush("#1877F2"),
                Margin = new Thickness(0, 2, 0, 6)
            });

            textStack.Children.Add(new TextBlock
            {
                Text = snapshot.Title,
                FontSize = 26,
                FontWeight = FontWeights.Bold,
                Foreground = CreateBrush("#111827")
            });

            textStack.Children.Add(new TextBlock
            {
                Text = $"Total: {BuildRecordCountText(snapshot)} | Generated: {generatedAt.ToString("MMM dd, yyyy HH:mm", CultureInfo.InvariantCulture)}",
                FontSize = 12,
                Foreground = CreateBrush("#6B7280"),
                Margin = new Thickness(0, 4, 0, 0)
            });

            textStack.Children.Add(new Border
            {
                Width = 220,
                Height = 3,
                Background = CreateBrush("#1D8CFF"),
                CornerRadius = new CornerRadius(2),
                Margin = new Thickness(0, 10, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Left
            });

            headerGrid.Children.Add(textStack);

            return new BlockUIContainer(headerGrid)
            {
                Margin = new Thickness(0, 0, 0, 6)
            };
        }

        private static Paragraph BuildMetadataParagraph(ReportsSnapshot snapshot, string preparedBy)
        {
            var paragraph = new Paragraph { Margin = new Thickness(0, 0, 0, 12), Foreground = CreateBrush("#475569") };
            paragraph.Inlines.Add(new Bold(new Run("Report Range: ")));
            paragraph.Inlines.Add(new Run(snapshot.RangeLabel));
            paragraph.Inlines.Add(new LineBreak());
            paragraph.Inlines.Add(new Bold(new Run("Program Scope: ")));
            paragraph.Inlines.Add(new Run(snapshot.ProgramLabel));
            paragraph.Inlines.Add(new LineBreak());
            paragraph.Inlines.Add(new Bold(new Run("Prepared By: ")));
            paragraph.Inlines.Add(new Run(string.IsNullOrWhiteSpace(preparedBy) ? "--" : preparedBy));
            return paragraph;
        }

        private static string BuildRecordCountText(ReportsSnapshot snapshot)
        {
            var count = snapshot.Table.Rows.Count;
            return snapshot.ExportFilePrefix switch
            {
                "aid-request-summary" => count == 1 ? "1 request" : $"{count:N0} requests",
                "validated-beneficiaries" => count == 1 ? "1 beneficiary" : $"{count:N0} beneficiaries",
                "budget-utilization" => count == 1 ? "1 program" : $"{count:N0} programs",
                "distribution-claims" => count == 1 ? "1 claim" : $"{count:N0} claims",
                _ => count == 1 ? "1 record" : $"{count:N0} records"
            };
        }

        private static Table BuildMetricTable(IReadOnlyList<ReportsMetricItem> metrics)
        {
            var table = new Table { CellSpacing = 0, Margin = new Thickness(0, 0, 0, 12) };
            for (var index = 0; index < Math.Max(metrics.Count, 1); index++)
            {
                table.Columns.Add(new TableColumn());
            }

            var row = new TableRow();
            foreach (var metric in metrics)
            {
                var cell = new TableCell
                {
                    BorderBrush = CreateBrush("#D9E6FA"),
                    BorderThickness = new Thickness(1),
                    Padding = new Thickness(12),
                    Background = CreateBrush("#F8FBFF")
                };

                cell.Blocks.Add(new Paragraph(new Run(metric.Label)) { FontSize = 11, FontWeight = FontWeights.SemiBold, Foreground = CreateBrush("#64748B"), Margin = new Thickness(0, 0, 0, 4) });
                cell.Blocks.Add(new Paragraph(new Run(metric.Value)) { FontSize = 18, FontWeight = FontWeights.Bold, Foreground = CreateBrush("#0D2B6E"), Margin = new Thickness(0, 0, 0, 4) });
                cell.Blocks.Add(new Paragraph(new Run(metric.Note)) { FontSize = 10, Foreground = CreateBrush("#64748B"), Margin = new Thickness(0) });
                row.Cells.Add(cell);
            }

            var group = new TableRowGroup();
            group.Rows.Add(row);
            table.RowGroups.Add(group);
            return table;
        }

        private static Table BuildPreviewTable(System.Data.DataTable source)
        {
            var table = new Table { CellSpacing = 0 };
            foreach (System.Data.DataColumn _ in source.Columns)
            {
                table.Columns.Add(new TableColumn());
            }

            var rowGroup = new TableRowGroup();
            var headerRow = new TableRow();
            foreach (System.Data.DataColumn column in source.Columns)
            {
                headerRow.Cells.Add(new TableCell(new Paragraph(new Run(column.ColumnName)))
                {
                    Padding = new Thickness(8),
                    Background = CreateBrush("#EAF3FF"),
                    BorderBrush = CreateBrush("#CFE2FF"),
                    BorderThickness = new Thickness(0.8),
                    Foreground = CreateBrush("#0D2B6E")
                });
            }

            rowGroup.Rows.Add(headerRow);
            var rowIndex = 0;
            foreach (System.Data.DataRow dataRow in source.Rows)
            {
                var row = new TableRow();
                var background = rowIndex % 2 == 0 ? "#FFFFFF" : "#F8FBFF";
                foreach (System.Data.DataColumn column in source.Columns)
                {
                    var value = Convert.ToString(dataRow[column], CultureInfo.CurrentCulture) ?? string.Empty;
                    row.Cells.Add(new TableCell(new Paragraph(new Run(value)))
                    {
                        Padding = new Thickness(8),
                        Background = CreateBrush(background),
                        BorderBrush = CreateBrush("#E2E8F0"),
                        BorderThickness = new Thickness(0.6),
                        Foreground = CreateBrush("#334155")
                    });
                }

                rowGroup.Rows.Add(row);
                rowIndex++;
            }

            table.RowGroups.Add(rowGroup);
            return table;
        }

        private static SolidColorBrush CreateBrush(string colorCode)
        {
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString(colorCode));
        }
    }
}
