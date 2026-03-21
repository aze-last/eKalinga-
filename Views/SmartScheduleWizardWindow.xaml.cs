using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using AttendanceShiftingManagement.Services;

namespace AttendanceShiftingManagement.Views
{
    public partial class SmartScheduleWizardWindow : Window
    {
        public SchedulingResult? Result { get; private set; }
        private readonly Func<SchedulingResult> _generator;

        public SmartScheduleWizardWindow(Func<SchedulingResult> generator)
        {
            InitializeComponent();
            _generator = generator;
            StartGeneration();
        }

        private async void StartGeneration()
        {
            try
            {
                // Animated delay to simulate "AI processing"
                StatusTitle.Text = "Analyzing employee constraints...";
                await Task.Delay(1200);
                Check1.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10B981"));

                StatusTitle.Text = "Optimizing manager & crew coverage...";
                await Task.Delay(1500);
                Check2.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10B981"));

                StatusTitle.Text = "Balancing fairness distribution...";
                await Task.Delay(1000);
                Check3.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10B981"));

                // Run actual calculation
                Result = await Task.Run(() => _generator());

                // Show Results
                if (Result != null && Result.Shifts.Count > 0)
                {
                    StatusTitle.Text = "Optimization complete!";
                    FairnessValue.Text = $"{Result.FairnessScore:F1}%";
                    DraftSummary.Text =
                        $"{Result.Shifts.Count} shifts generated for {Result.TotalManagers + Result.TotalCrew} active employees. " +
                        $"Average load: {Result.AverageAssignedShifts:F1} shifts per employee.";
                    FullSummaryText.Text = Result.DistributionSummary;

                    ProcessingStep.Visibility = Visibility.Collapsed;
                    ResultStep.Visibility = Visibility.Visible;
                    ApplyButton.IsEnabled = true;
                }
                else
                {
                    MessageBox.Show("No shifts could be generated. Please check for active employees or conflicting data.", "Scheduling Issue", MessageBoxButton.OK, MessageBoxImage.Warning);
                    this.Close();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error during generation: {ex.Message}", "Error");
                this.Close();
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Result = null;
            DialogResult = false;
            this.Close();
        }

        private void ApplyButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            this.Close();
        }
    }
}
