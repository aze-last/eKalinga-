using AttendanceShiftingManagement.ViewModels;

namespace AttendanceShiftingManagement.Tests;

public sealed class ConnectionSettingsViewModelTests
{
    [Fact]
    public void SelectionOnlyMode_HidesCredentialEditing()
    {
        var viewModel = new ConnectionSettingsViewModel(selectionOnly: true);

        Assert.True(viewModel.IsSelectionOnly);
        Assert.False(viewModel.ShowCredentialEditor);
        Assert.False(viewModel.ShowLanCredentialEditor);
        Assert.False(viewModel.ShowFixedPresetNotice);
        Assert.False(viewModel.CanEditSelectedPresetCredentials);
        Assert.True(viewModel.TestConnectionCommand.CanExecute(null));
    }

    [Fact]
    public void AdminMode_OnlyLanPresetIsEditable()
    {
        var viewModel = new ConnectionSettingsViewModel(selectionOnly: false);

        viewModel.SelectLocalPresetCommand.Execute(null);
        Assert.False(viewModel.CanEditSelectedPresetCredentials);
        Assert.False(viewModel.ShowLanCredentialEditor);
        Assert.True(viewModel.ShowFixedPresetNotice);

        viewModel.SelectLanPresetCommand.Execute(null);
        Assert.True(viewModel.IsLanSelected);
        Assert.True(viewModel.CanEditSelectedPresetCredentials);
        Assert.True(viewModel.ShowLanCredentialEditor);
        Assert.False(viewModel.ShowFixedPresetNotice);

        viewModel.SelectRemotePresetCommand.Execute(null);
        Assert.False(viewModel.CanEditSelectedPresetCredentials);
        Assert.False(viewModel.ShowLanCredentialEditor);
        Assert.True(viewModel.ShowFixedPresetNotice);
    }

    [Fact]
    public void AdminMode_ReadOnlyPresetsStillAllowTesting()
    {
        var viewModel = new ConnectionSettingsViewModel(selectionOnly: false);

        viewModel.SelectLocalPresetCommand.Execute(null);
        Assert.True(viewModel.TestConnectionCommand.CanExecute(null));

        viewModel.SelectRemotePresetCommand.Execute(null);
        Assert.True(viewModel.TestConnectionCommand.CanExecute(null));
    }
}
