using AttendanceShiftingManagement.Services;
using AttendanceShiftingManagement.ViewModels;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;

namespace AttendanceShiftingManagement.Views
{
    public partial class SessionAnnouncementWindow : Window
    {
        public SessionAnnouncementWindow(SessionAnnouncementSnapshot snapshot)
        {
            InitializeComponent();
            DataContext = new SessionAnnouncementViewModel(snapshot);
            WindowBrandingService.ApplyWindowIcon(this);
            Loaded += SessionAnnouncementWindow_Loaded;
        }

        private void SessionAnnouncementWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (Resources["PanelEntranceStoryboard"] is Storyboard storyboard)
            {
                storyboard.Begin(this);
            }

            ContinueButton.Focus();
        }

        private void Continue_Click(object sender, RoutedEventArgs e)
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
