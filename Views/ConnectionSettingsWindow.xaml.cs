using AttendanceShiftingManagement.ViewModels;
using System.Windows;

namespace AttendanceShiftingManagement.Views
{
    public partial class ConnectionSettingsWindow : Window
    {
        public ConnectionSettingsWindow(bool selectionOnly = true, bool requireOtpOnSave = false)
        {
            InitializeComponent();

            var viewModel = new ConnectionSettingsViewModel(selectionOnly, requireOtpOnSave);
            viewModel.CloseRequested += OnCloseRequested;
            DataContext = viewModel;
        }

        private void OnCloseRequested(bool? dialogResult)
        {
            DialogResult = dialogResult;
            Close();
        }
    }
}
