using AttendanceShiftingManagement.ViewModels;

namespace AttendanceShiftingManagement.Tests;

public sealed class ConnectionSettingsViewModelTests
{
    [Fact]
    public void SelectionOnlyMode_OnlyLanRemainsEditableBeforeSignIn()
    {
        var viewModel = new ConnectionSettingsViewModel(selectionOnly: true);

        Assert.True(viewModel.IsSelectionOnly);
        Assert.False(viewModel.ShowCredentialEditor);
        Assert.False(viewModel.ShowLanCredentialEditor);
        Assert.True(viewModel.ShowFixedPresetNotice);
        Assert.False(viewModel.CanEditSelectedPresetCredentials);
        Assert.True(viewModel.TestConnectionCommand.CanExecute(null));

        viewModel.SelectLanPresetCommand.Execute(null);

        Assert.True(viewModel.IsLanSelected);
        Assert.True(viewModel.ShowCredentialEditor);
        Assert.True(viewModel.ShowLanCredentialEditor);
        Assert.False(viewModel.ShowFixedPresetNotice);
        Assert.True(viewModel.CanEditSelectedPresetCredentials);

        viewModel.SelectRemotePresetCommand.Execute(null);

        Assert.True(viewModel.IsRemoteSelected);
        Assert.False(viewModel.ShowCredentialEditor);
        Assert.False(viewModel.ShowLanCredentialEditor);
        Assert.True(viewModel.ShowFixedPresetNotice);
        Assert.False(viewModel.CanEditSelectedPresetCredentials);
    }

    [Fact]
    public void SelectionOnlyMode_RaisesShowCredentialEditorChange_WhenLanIsSelected()
    {
        var viewModel = new ConnectionSettingsViewModel(selectionOnly: true);
        var changedProperties = new List<string>();

        viewModel.PropertyChanged += (_, args) =>
        {
            if (!string.IsNullOrWhiteSpace(args.PropertyName))
            {
                changedProperties.Add(args.PropertyName);
            }
        };

        viewModel.SelectLanPresetCommand.Execute(null);

        Assert.Contains(nameof(ConnectionSettingsViewModel.ShowCredentialEditor), changedProperties);
        Assert.Contains(nameof(ConnectionSettingsViewModel.ShowLanCredentialEditor), changedProperties);
    }

    [Fact]
    public void AdminMode_OnlyLanPresetIsEditable()
    {
        var viewModel = new ConnectionSettingsViewModel(selectionOnly: false);

        viewModel.SelectLocalPresetCommand.Execute(null);
        Assert.False(viewModel.CanEditSelectedPresetCredentials);
        Assert.False(viewModel.ShowCredentialEditor);
        Assert.False(viewModel.ShowLanCredentialEditor);
        Assert.True(viewModel.ShowFixedPresetNotice);

        viewModel.SelectLanPresetCommand.Execute(null);
        Assert.True(viewModel.IsLanSelected);
        Assert.True(viewModel.CanEditSelectedPresetCredentials);
        Assert.True(viewModel.ShowCredentialEditor);
        Assert.True(viewModel.ShowLanCredentialEditor);
        Assert.False(viewModel.ShowFixedPresetNotice);

        viewModel.SelectRemotePresetCommand.Execute(null);
        Assert.False(viewModel.CanEditSelectedPresetCredentials);
        Assert.False(viewModel.ShowCredentialEditor);
        Assert.False(viewModel.ShowLanCredentialEditor);
        Assert.True(viewModel.ShowFixedPresetNotice);
    }

    [Fact]
    public void AdminMode_AllPresetsStillAllowTesting()
    {
        var viewModel = new ConnectionSettingsViewModel(selectionOnly: false);

        viewModel.SelectLocalPresetCommand.Execute(null);
        Assert.True(viewModel.TestConnectionCommand.CanExecute(null));

        viewModel.SelectRemotePresetCommand.Execute(null);
        Assert.True(viewModel.TestConnectionCommand.CanExecute(null));
    }
}
