using AssetsTools.NET.Extra;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Dock.Model.Mvvm.Controls;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using UABEANext4.AssetWorkspace;
using UABEANext4.Logic;
using UABEANext4.Logic.Scene;

namespace UABEANext4.ViewModels.Tools;

public partial class SceneViewToolViewModel : Tool
{
    const string TOOL_TITLE = "Scene View";
    const string DEFAULT_SCENE_NAME = "level0";

    public Workspace Workspace { get; }

    [ObservableProperty]
    private SceneData? _sceneData;

    [ObservableProperty]
    private SceneObject? _selectedObject;

    [ObservableProperty]
    private string _statusText = "No scene loaded. Load a scene or open a file.";

    [ObservableProperty]
    private bool _isSceneLoaded = false;

    [ObservableProperty]
    private string _sceneName = DEFAULT_SCENE_NAME;

    [ObservableProperty]
    private bool _autoLoad = true;

    private List<AssetsFileInstance>? _currentFileInsts;
    private bool _hasAutoLoaded = false;

    /// <summary>
    /// Action to reset the camera. Set by the view to connect to the SceneViewControl.
    /// </summary>
    public Action? ResetCameraAction { get; set; }

    [Obsolete("This constructor is for the designer only and should not be used directly.", true)]
    public SceneViewToolViewModel()
    {
        Workspace = new();
        Id = TOOL_TITLE.Replace(" ", "");
        Title = TOOL_TITLE;
    }

    public SceneViewToolViewModel(Workspace workspace)
    {
        Workspace = workspace;
        Id = TOOL_TITLE.Replace(" ", "");
        Title = TOOL_TITLE;

        WeakReferenceMessenger.Default.Register<SelectedWorkspaceItemChangedMessage>(this, OnWorkspaceItemSelected);
        WeakReferenceMessenger.Default.Register<WorkspaceClosingMessage>(this, OnWorkspaceClosing);

        // Subscribe to workspace items being added
        Workspace.RootItems.CollectionChanged += OnRootItemsChanged;
    }

    private void OnRootItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Add && e.NewItems != null)
        {
            // When new items are added, try auto-load if enabled
            if (AutoLoad && !_hasAutoLoaded && !IsSceneLoaded)
            {
                // Start auto-load with retries to handle bundle files that need time to extract
                StartAutoLoadWithRetry();
            }
        }
    }

    private void StartAutoLoadWithRetry()
    {
        System.Threading.Tasks.Task.Run(async () =>
        {
            // Try multiple times with increasing delays to handle bundle extraction
            int[] delays = [500, 1000, 2000];

            foreach (var delay in delays)
            {
                await System.Threading.Tasks.Task.Delay(delay);

                bool shouldTry = false;
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    if (AutoLoad && !_hasAutoLoaded && !IsSceneLoaded)
                    {
                        shouldTry = true;
                        TryAutoLoadScene();
                    }
                });

                // Wait a bit to see if it loaded
                await System.Threading.Tasks.Task.Delay(200);

                // Check if we succeeded
                bool loaded = false;
                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                {
                    loaded = IsSceneLoaded || _hasAutoLoaded;
                });

                if (loaded)
                {
                    break;
                }
            }
        });
    }

    private void OnWorkspaceItemSelected(object recipient, SelectedWorkspaceItemChangedMessage message)
    {
        var items = message.Value;
        if (items.Count == 0) return;

        var fileInsts = new List<AssetsFileInstance>();
        foreach (var item in items)
        {
            if (item.ObjectType == WorkspaceItemType.AssetsFile && item.Object is AssetsFileInstance fileInst)
            {
                fileInsts.Add(fileInst);
            }
            else if (item.ObjectType == WorkspaceItemType.BundleFile)
            {
                // If a bundle file is selected, get all its child assets files
                foreach (var child in item.Children)
                {
                    if (child.ObjectType == WorkspaceItemType.AssetsFile && child.Object is AssetsFileInstance childFileInst)
                    {
                        fileInsts.Add(childFileInst);
                    }
                }
            }
        }

        if (fileInsts.Count > 0)
        {
            _currentFileInsts = fileInsts;
            StatusText = $"Ready to load scene from {_currentFileInsts.Count} file(s). Click 'Load Scene' to begin.";

            // Auto-load if enabled and not already loaded
            if (AutoLoad && !_hasAutoLoaded && !IsSceneLoaded)
            {
                TryAutoLoadScene();
            }
        }
    }

    private void TryAutoLoadScene()
    {
        // Try to find the scene by name in the workspace
        var sceneFile = FindSceneFileByName(SceneName);
        if (sceneFile != null)
        {
            StatusText = $"Auto-loading scene '{SceneName}'...";
            _currentFileInsts = new List<AssetsFileInstance> { sceneFile };
            _hasAutoLoaded = true;
            LoadScene();
        }
        else
        {
            // Update status to show we're still waiting for the scene file
            StatusText = $"Waiting for scene '{SceneName}'... (Auto-load enabled)";
        }
    }

    private AssetsFileInstance? FindSceneFileByName(string sceneName)
    {
        // Search all workspace items for a file matching the scene name
        foreach (var rootItem in Workspace.RootItems)
        {
            var result = FindSceneInItem(rootItem, sceneName);
            if (result != null) return result;
        }
        return null;
    }

    private AssetsFileInstance? FindSceneInItem(WorkspaceItem item, string sceneName)
    {
        // Check if this item matches the scene name
        if (item.ObjectType == WorkspaceItemType.AssetsFile && item.Object is AssetsFileInstance fileInst)
        {
            var name = item.Name.ToLowerInvariant();
            var searchName = sceneName.ToLowerInvariant();

            // Match by exact name, name without extension, or if the file name contains the scene name
            if (name == searchName ||
                name == searchName + ".assets" ||
                name.Contains(searchName))
            {
                return fileInst;
            }
        }

        // Search children (e.g., assets files inside bundle files)
        foreach (var child in item.Children)
        {
            var result = FindSceneInItem(child, sceneName);
            if (result != null) return result;
        }

        return null;
    }

    private void OnWorkspaceClosing(object recipient, WorkspaceClosingMessage message)
    {
        SceneData = null;
        SelectedObject = null;
        IsSceneLoaded = false;
        StatusText = "No scene loaded. Load a scene or open a file.";
        _currentFileInsts = null;
        _hasAutoLoaded = false;
    }

    [RelayCommand]
    private void LoadScene()
    {
        // If no file is currently selected, try to find the scene by name
        if (_currentFileInsts == null || _currentFileInsts.Count == 0)
        {
            var sceneFile = FindSceneFileByName(SceneName);
            if (sceneFile != null)
            {
                _currentFileInsts = new List<AssetsFileInstance> { sceneFile };
            }
            else
            {
                // If still no file, try to get the first available file from the workspace
                var firstFile = GetFirstAvailableFile();
                if (firstFile != null)
                {
                    _currentFileInsts = new List<AssetsFileInstance> { firstFile };
                }
                else
                {
                    StatusText = $"No scene file '{SceneName}' found. Open a file or bundle first.";
                    return;
                }
            }
        }

        StatusText = "Loading scene...";

        try
        {
            var sceneData = new SceneData(Workspace);

            // Load from first file for now
            var fileInst = _currentFileInsts[0];
            sceneData.LoadFromFile(fileInst);

            SceneData = sceneData;
            IsSceneLoaded = true;

            var objectCount = sceneData.AllObjects.Count;
            var meshCount = sceneData.AllObjects.Count(o => o.HasMesh);
            var texturedCount = sceneData.AllObjects.Count(o => o.HasTexture);

            StatusText = $"Loaded {objectCount} objects ({meshCount} with meshes, {texturedCount} with textures)";
        }
        catch (Exception ex)
        {
            StatusText = $"Error loading scene: {ex.Message}";
            IsSceneLoaded = false;
        }
    }

    private AssetsFileInstance? GetFirstAvailableFile()
    {
        foreach (var rootItem in Workspace.RootItems)
        {
            var file = GetFirstFileInItem(rootItem);
            if (file != null) return file;
        }
        return null;
    }

    private AssetsFileInstance? GetFirstFileInItem(WorkspaceItem item)
    {
        if (item.ObjectType == WorkspaceItemType.AssetsFile && item.Object is AssetsFileInstance fileInst)
        {
            return fileInst;
        }

        foreach (var child in item.Children)
        {
            var file = GetFirstFileInItem(child);
            if (file != null) return file;
        }

        return null;
    }

    [RelayCommand]
    private void ResetCamera()
    {
        ResetCameraAction?.Invoke();
        StatusText = "Camera reset to default position.";
    }

    public void OnObjectSelected(SceneObject? obj)
    {
        SelectedObject = obj;

        if (obj != null)
        {
            StatusText = $"Selected: {obj.Name} (PathId: {obj.PathId})";

            // Send message to select corresponding asset in inspector
            if (obj.GameObjectAsset != null)
            {
                WeakReferenceMessenger.Default.Send(new AssetsSelectedMessage([obj.GameObjectAsset]));
            }
        }
        else
        {
            StatusText = IsSceneLoaded
                ? "No object selected. Click on an object to select it."
                : "No scene loaded.";
        }
    }
}
