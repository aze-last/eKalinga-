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
    }
}
