using AttendanceShiftingManagement.Services;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;

namespace AttendanceShiftingManagement.Views
{
    public partial class ReportPrintPreviewWindow : Window
    {
        public ReportPrintPreviewWindow(FlowDocument document, string reportTitle)
        {
            InitializeComponent();
            var previewDocument = document ?? throw new ArgumentNullException(nameof(document));
            previewDocument.IsOptimalParagraphEnabled = false;
            previewDocument.ColumnWidth = double.PositiveInfinity;
            PreviewViewer.Document = previewDocument;
            TitleTextBlock.Text = reportTitle;
            Title = reportTitle;
            WindowBrandingService.ApplyWindowIcon(this);
        }

        private void PrintButton_Click(object sender, RoutedEventArgs e)
        {
            if (PreviewViewer.Document is not FlowDocument document)
            {
                return;
            }

            var dialog = new PrintDialog();
            if (dialog.ShowDialog() != true)
            {
                return;
            }

            document.PageHeight = dialog.PrintableAreaHeight;
            document.PageWidth = dialog.PrintableAreaWidth;
            document.ColumnWidth = double.PositiveInfinity;
            dialog.PrintDocument(((IDocumentPaginatorSource)document).DocumentPaginator, Title);
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
