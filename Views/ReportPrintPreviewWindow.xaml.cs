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
            PreviewViewer.Document = document ?? throw new ArgumentNullException(nameof(document));
            TitleTextBlock.Text = reportTitle;
            Title = reportTitle;
            WindowBrandingService.ApplyWindowIcon(this);
        }

        private void PrintButton_Click(object sender, RoutedEventArgs e)
        {
            if (PreviewViewer.Document == null)
            {
                return;
            }

            var dialog = new PrintDialog();
            if (dialog.ShowDialog() != true)
            {
                return;
            }

            PreviewViewer.Document.PageHeight = dialog.PrintableAreaHeight;
            PreviewViewer.Document.PageWidth = dialog.PrintableAreaWidth;
            PreviewViewer.Document.ColumnWidth = dialog.PrintableAreaWidth;
            dialog.PrintDocument(((IDocumentPaginatorSource)PreviewViewer.Document).DocumentPaginator, Title);
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
