using AttendanceShiftingManagement.ViewModels;
using System.Windows;

namespace AttendanceShiftingManagement.Views
{
    public partial class SettingsWindow : Window
    {
        private SettingsToolsViewModel ViewModel => (SettingsToolsViewModel)DataContext;

        public SettingsWindow()
        {
            InitializeComponent();
            DataContext = new SettingsToolsViewModel();
            ViewModel.AdvancedLoadTablesRequested += OpenAdvancedLoadTables;
        }

        private void OpenAdvancedLoadTables()
        {
            var window = new LoadTablesWindow
            {
                Owner = this
            };

            window.ShowDialog();
            ViewModel.RefreshPreviewCommand.Execute(null);
        }
    }
}
