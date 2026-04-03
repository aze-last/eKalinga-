using System.Windows;

namespace AttendanceShiftingManagement.Tests;

internal static class WpfTestHost
{
    private static readonly object SyncRoot = new();

    public static void EnsureApplication()
    {
        lock (SyncRoot)
        {
            if (Application.Current != null)
            {
                return;
            }

            try
            {
                var app = new global::AttendanceShiftingManagement.App();
                app.InitializeComponent();
            }
            catch (InvalidOperationException)
            {
                // Another test may have created the singleton application first.
            }
        }
    }
}
