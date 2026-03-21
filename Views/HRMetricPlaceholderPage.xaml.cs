using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows.Controls;

namespace AttendanceShiftingManagement.Views
{
    public partial class HRMetricPlaceholderPage : UserControl
    {
        public HRMetricPlaceholderPage(string title, string description, IEnumerable<string> bulletPoints)
        {
            InitializeComponent();
            DataContext = new HRMetricPlaceholderData(title, description, bulletPoints);
        }
    }

    public sealed class HRMetricPlaceholderData
    {
        public HRMetricPlaceholderData(string title, string description, IEnumerable<string> bulletPoints)
        {
            Title = title;
            Description = description;
            BulletPoints = new ObservableCollection<string>(bulletPoints);
        }

        public string Title { get; }
        public string Description { get; }
        public ObservableCollection<string> BulletPoints { get; }
    }
}
