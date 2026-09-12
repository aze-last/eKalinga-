using AttendanceShiftingManagement.Services;
using AttendanceShiftingManagement.ViewModels;
using System.Windows;
using System.Windows.Input;

namespace AttendanceShiftingManagement.Views
{
    public partial class WhatsNewWindow : Window
    {
        public WhatsNewWindow(IReadOnlyList<WhatsNewEntry> entries)
        {
            InitializeComponent();
            DataContext = new WhatsNewViewModel(entries, AppVersionService.GetCurrentVersion());
            WindowBrandingService.ApplyWindowIcon(this);
            Loaded += WhatsNewWindow_Loaded;
        }

        private void WhatsNewWindow_Loaded(object sender, RoutedEventArgs e)
        {
            GotItButton.Focus();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                DialogResult = true;
                Close();
                e.Handled = true;
                return;
            }

            base.OnPreviewKeyDown(e);
        }
    }
}
