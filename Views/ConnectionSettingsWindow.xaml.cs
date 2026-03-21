using AttendanceShiftingManagement.ViewModels;
using System.Windows;

namespace AttendanceShiftingManagement.Views
{
    public partial class ConnectionSettingsWindow : Window
    {
        public ConnectionSettingsWindow()
        {
            InitializeComponent();

            var viewModel = new ConnectionSettingsViewModel();
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
