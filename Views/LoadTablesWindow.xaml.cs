using AttendanceShiftingManagement.ViewModels;
using System.Windows;

namespace AttendanceShiftingManagement.Views
{
    public partial class LoadTablesWindow : Window
    {
        public LoadTablesWindow()
        {
            InitializeComponent();
            DataContext = new LoadTablesViewModel();
        }

        private void Minimize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
