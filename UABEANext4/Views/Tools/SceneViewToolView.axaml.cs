using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using UABEANext4.ViewModels.Tools;
using System;
using System.ComponentModel;
using System.Threading.Tasks;

namespace UABEANext4.Views.Tools;

public partial class SceneViewToolView : UserControl
{
    private SceneViewToolViewModel? _viewModel;

    public SceneViewToolView()
    {
        InitializeComponent();
        sceneViewControl.SelectionChanged += OnSceneSelectionChanged;
        sceneViewControl.DuplicateRequested += OnDuplicateRequested;
        sceneViewControl.DeleteRequested += OnDeleteRequested;
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_viewModel != null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        if (DataContext is SceneViewToolViewModel vm)
        {
            _viewModel = vm;
            vm.ResetCameraAction = () => sceneViewControl.ResetCamera();
            vm.SaveFileDialogAction = ShowSaveFileDialog;
            vm.PropertyChanged += OnViewModelPropertyChanged;
        }
    }

    private async Task<string?> ShowSaveFileDialog(string defaultName, string filter)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return null;

        var options = new FilePickerSaveOptions
        {
            Title = "Export FBX",
            SuggestedFileName = defaultName + ".fbx",
            FileTypeChoices = new[]
            {
                new FilePickerFileType("FBX Files") { Patterns = new[] { "*.fbx" } },
                new FilePickerFileType("All Files") { Patterns = new[] { "*" } }
            }
        };

        var result = await topLevel.StorageProvider.SaveFilePickerAsync(options);
        return result?.TryGetLocalPath();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SceneViewToolViewModel.SceneData))
        {
            sceneViewControl.MarkDirty();
        }
    }

    private void OnSceneSelectionChanged(object? sender, Logic.Scene.SceneObject? obj)
    {
        if (DataContext is SceneViewToolViewModel vm)
        {
            vm.OnObjectSelected(obj);
        }
    }

    private void OnDuplicateRequested(object? sender, EventArgs e)
    {
        if (DataContext is SceneViewToolViewModel vm)
        {
            vm.DuplicateObjectCommand.Execute(null);
        }
    }

    private void OnDeleteRequested(object? sender, EventArgs e)
    {
        if (DataContext is SceneViewToolViewModel vm)
        {
            vm.DeleteObjectCommand.Execute(null);
        }
    }

    private void OnSceneTabClick(object? sender, RoutedEventArgs e)
    {
        sceneTabButton.IsChecked = true;
        logTabButton.IsChecked = false;
        hierarchyTabButton.IsChecked = false;
        scenePanel.IsVisible = true;
        logPanel.IsVisible = false;
        hierarchyPanel.IsVisible = false;
        logControls.IsVisible = false;
    }

    private void OnLogTabClick(object? sender, RoutedEventArgs e)
    {
        sceneTabButton.IsChecked = false;
        logTabButton.IsChecked = true;
        hierarchyTabButton.IsChecked = false;
        scenePanel.IsVisible = false;
        logPanel.IsVisible = true;
        hierarchyPanel.IsVisible = false;
        logControls.IsVisible = true;
    }

    private void OnHierarchyTabClick(object? sender, RoutedEventArgs e)
    {
        sceneTabButton.IsChecked = false;
        logTabButton.IsChecked = false;
        hierarchyTabButton.IsChecked = true;
        scenePanel.IsVisible = false;
        logPanel.IsVisible = false;
        hierarchyPanel.IsVisible = true;
        logControls.IsVisible = false;
    }
}
