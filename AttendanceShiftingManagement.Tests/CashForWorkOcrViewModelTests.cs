using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using AttendanceShiftingManagement.Models;
using AttendanceShiftingManagement.Services;
using AttendanceShiftingManagement.ViewModels;

namespace AttendanceShiftingManagement.Tests;

public sealed class CashForWorkOcrViewModelTests
{
    [Fact]
    public void OpenCreateEventPanel_WhenCashForWorkEventIsSelected_ResetsEditorForNewEvent()
    {
        Exception? failure = null;

        var thread = new Thread(() =>
        {
            try
            {
                WpfTestHost.EnsureApplication();

                var viewModel = (CashForWorkOcrViewModel)RuntimeHelpers.GetUninitializedObject(typeof(CashForWorkOcrViewModel));
                SetPrivateField(viewModel, "_selectedEvent", new CashForWorkEvent
                {
                    Title = "Existing Clean-Up",
                    Location = "Covered Court",
                    EventDate = new DateTime(2026, 4, 10),
                    StartTime = new TimeSpan(8, 0, 0),
                    EndTime = new TimeSpan(12, 0, 0),
                    Notes = "Existing notes",
                    EventKind = CashForWorkEventKind.CashForWork
                });

                var openEventEditorPanel = typeof(CashForWorkOcrViewModel).GetMethod(
                    "OpenEventEditorPanel",
                    BindingFlags.Instance | BindingFlags.NonPublic);

                Assert.NotNull(openEventEditorPanel);

                openEventEditorPanel!.Invoke(viewModel, [CashForWorkEventKind.CashForWork]);

                Assert.Equal(string.Empty, viewModel.EventTitle);
                Assert.Equal(string.Empty, viewModel.EventLocation);
                Assert.Equal(DateTime.Today, viewModel.EventDate.Date);
                Assert.Equal(DateTime.Today.AddHours(7), viewModel.EventStartTime);
                Assert.Equal(DateTime.Today.AddHours(12), viewModel.EventEndTime);
                Assert.Equal(string.Empty, viewModel.EventNotes);
                Assert.Equal("CREATE EVENT", viewModel.EventEditorSubmitLabel);
                Assert.Equal("Create Event", viewModel.DrawerTitle);
                Assert.DoesNotContain("update", viewModel.DrawerSubtitle, StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        Assert.True(failure is null, failure?.ToString());
    }

    [Fact]
    public void OpenEditEventPanel_WhenEventIsSelected_LoadsExistingValuesForUpdate()
    {
        Exception? failure = null;

        var thread = new Thread(() =>
        {
            try
            {
                WpfTestHost.EnsureApplication();

                var existingEvent = new CashForWorkEvent
                {
                    Title = "Existing Clean-Up",
                    Location = "Covered Court",
                    EventDate = new DateTime(2026, 4, 10),
                    StartTime = new TimeSpan(8, 0, 0),
                    EndTime = new TimeSpan(12, 0, 0),
                    Notes = "Existing notes",
                    EventKind = CashForWorkEventKind.CashForWork
                };

                var viewModel = (CashForWorkOcrViewModel)RuntimeHelpers.GetUninitializedObject(typeof(CashForWorkOcrViewModel));
                SetPrivateField(viewModel, "_selectedEvent", existingEvent);

                var openEditEventEditorPanel = typeof(CashForWorkOcrViewModel).GetMethod(
                    "OpenEditEventPanel",
                    BindingFlags.Instance | BindingFlags.NonPublic);

                Assert.NotNull(openEditEventEditorPanel);

                openEditEventEditorPanel!.Invoke(viewModel, null);

                Assert.Equal(existingEvent.Title, viewModel.EventTitle);
                Assert.Equal(existingEvent.Location, viewModel.EventLocation);
                Assert.Equal(existingEvent.EventDate.Date, viewModel.EventDate.Date);
                Assert.Equal(DateTime.Today.Date.Add(existingEvent.StartTime), viewModel.EventStartTime);
                Assert.Equal(DateTime.Today.Date.Add(existingEvent.EndTime), viewModel.EventEndTime);
                Assert.Equal(existingEvent.Notes, viewModel.EventNotes);
                Assert.Equal("UPDATE EVENT", viewModel.EventEditorSubmitLabel);
                Assert.Equal("Edit Event", viewModel.DrawerTitle);
                Assert.Contains("update", viewModel.DrawerSubtitle, StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        Assert.True(failure is null, failure?.ToString());
    }

    [Fact]
    public void OpenScanAttendancePanel_OpensScannerWorkflowPanel()
    {
        Exception? failure = null;

        var thread = new Thread(() =>
        {
            try
            {
                WpfTestHost.EnsureApplication();

                var viewModel = (CashForWorkOcrViewModel)RuntimeHelpers.GetUninitializedObject(typeof(CashForWorkOcrViewModel));
                SetPrivateField(viewModel, "_selectedEvent", new CashForWorkEvent
                {
                    Title = "Scanner Ready Event",
                    EventKind = CashForWorkEventKind.CashForWork
                });

                var openScanAttendancePanel = typeof(CashForWorkOcrViewModel).GetMethod(
                    "OpenScanAttendancePanel",
                    BindingFlags.Instance | BindingFlags.NonPublic);

                Assert.NotNull(openScanAttendancePanel);

                openScanAttendancePanel!.Invoke(viewModel, null);

                Assert.Equal(CashForWorkWorkspacePanel.ScanAttendance, viewModel.ActivePanel);
                Assert.Equal("Scan Attendance", viewModel.DrawerTitle);
                Assert.Contains("scanner session", viewModel.DrawerSubtitle, StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        Assert.True(failure is null, failure?.ToString());
    }

    [Fact]
    public void AttendanceScannerSessionGuard_WhenEventIsReleased_ReturnsBlockReason()
    {
        var guardMethod = typeof(CashForWorkOcrViewModel).GetMethod(
            "GetAttendanceScannerSessionBlockReason",
            BindingFlags.Static | BindingFlags.NonPublic);

        Assert.NotNull(guardMethod);

        var reason = (string?)guardMethod!.Invoke(null, [new CashForWorkEvent
        {
            Title = "Released Event",
            Status = CashForWorkEventStatus.Completed,
            EventDate = DateTime.Today
        }, 1]);

        Assert.NotNull(reason);
        Assert.Contains("released", reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AttendanceScannerSessionGuard_WhenEventIsInFuture_ReturnsBlockReason()
    {
        var guardMethod = typeof(CashForWorkOcrViewModel).GetMethod(
            "GetAttendanceScannerSessionBlockReason",
            BindingFlags.Static | BindingFlags.NonPublic);

        Assert.NotNull(guardMethod);

        var reason = (string?)guardMethod!.Invoke(null, [new CashForWorkEvent
        {
            Title = "Future Event",
            Status = CashForWorkEventStatus.Open,
            EventDate = DateTime.Today.AddDays(1)
        }, 1]);

        Assert.NotNull(reason);
        Assert.Contains("event date", reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ReleaseSummaryPresentation_WhenSomeParticipantsArePending_ShowsPartialPayoutReady()
    {
        var presentationMethod = typeof(CashForWorkOcrViewModel).GetMethod(
            "BuildReleaseSummaryPresentation",
            BindingFlags.Static | BindingFlags.NonPublic);

        Assert.NotNull(presentationMethod);

        var presentation = presentationMethod!.Invoke(null, [
            new CashForWorkEvent
            {
                Title = "Canal Clearing",
                EventDate = DateTime.Today,
                Status = CashForWorkEventStatus.Open
            },
            new CashForWorkReleaseReadySummary(
                41,
                "Canal Clearing",
                DateTime.Today,
                "Sitio Uno",
                CashForWorkEventStatus.Open,
                5,
                3,
                2,
                3,
                1,
                0m,
                Array.Empty<CashForWorkReleaseReadyParticipant>())
        ]);

        Assert.NotNull(presentation);
        Assert.Equal("Partial payout ready", GetPresentationProperty(presentation!, "StatusText"));
        Assert.Contains("can be released now", GetPresentationProperty(presentation!, "Detail"), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("excluded", GetPresentationProperty(presentation!, "Detail"), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ReleaseSummaryPresentation_WhenAttendanceIsComplete_ShowsReadyForPayout()
    {
        var presentationMethod = typeof(CashForWorkOcrViewModel).GetMethod(
            "BuildReleaseSummaryPresentation",
            BindingFlags.Static | BindingFlags.NonPublic);

        Assert.NotNull(presentationMethod);

        var presentation = presentationMethod!.Invoke(null, [
            new CashForWorkEvent
            {
                Title = "Canal Clearing",
                EventDate = DateTime.Today,
                Status = CashForWorkEventStatus.Open
            },
            new CashForWorkReleaseReadySummary(
                41,
                "Canal Clearing",
                DateTime.Today,
                "Sitio Uno",
                CashForWorkEventStatus.Open,
                5,
                5,
                0,
                5,
                2,
                0m,
                Array.Empty<CashForWorkReleaseReadyParticipant>())
        ]);

        Assert.NotNull(presentation);
        Assert.Equal("Ready for payout", GetPresentationProperty(presentation!, "StatusText"));
    }

    private static void SetPrivateField(object instance, string fieldName, object? value)
    {
        var field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        field!.SetValue(instance, value);
    }

    private static string GetPresentationProperty(object presentation, string propertyName)
    {
        var property = presentation.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
        Assert.NotNull(property);
        return Assert.IsType<string>(property!.GetValue(presentation));
    }
}
