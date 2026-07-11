using System.Collections.ObjectModel;

namespace VirusTotalOneClick.UI;

public sealed class FolderReviewNode : ObservableObject
{
    [ThreadStatic]
    private static int _cascadeDepth;

    private bool? _isChecked = true;
    private bool _isExpanded;
    private bool _suppressParentUpdate;

    /// <summary>
    /// True while a checked-state change is cascading through the tree.
    /// Listeners can skip per-node recomputation and wait for
    /// <see cref="CheckedCascadeCompleted"/> instead.
    /// </summary>
    public static bool IsCascadeInProgress => _cascadeDepth > 0;

    /// <summary>
    /// Raised once on the node whose <see cref="IsChecked"/> setter initiated
    /// a cascade, after every affected node has been updated.
    /// </summary>
    public event EventHandler? CheckedCascadeCompleted;

    public FolderReviewNode(string name, string fullPath, bool isDirectory, long? sizeBytes = null)
    {
        Name = name;
        FullPath = fullPath;
        IsDirectory = isDirectory;
        SizeBytes = sizeBytes;
        Children = [];
    }

    public string Name { get; }

    public string FullPath { get; }

    public bool IsDirectory { get; }

    public long? SizeBytes { get; }

    public ObservableCollection<FolderReviewNode> Children { get; }

    public FolderReviewNode? Parent { get; internal set; }

    public bool? IsChecked
    {
        get => _isChecked;
        set
        {
            if (_isChecked == value)
            {
                return;
            }

            _cascadeDepth++;
            try
            {
                SetIsChecked(value, updateChildren: true, updateParent: true);
            }
            finally
            {
                _cascadeDepth--;
            }

            if (_cascadeDepth == 0)
            {
                CheckedCascadeCompleted?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public bool IsExpanded
    {
        get => _isExpanded;
        set => SetProperty(ref _isExpanded, value);
    }

    public string DisplayDetail
    {
        get
        {
            if (IsDirectory)
            {
                var fileCount = CountFiles(this);
                return fileCount == 1 ? "1 file" : $"{fileCount} files";
            }

            return SizeBytes is { } bytes ? SizeFormat.Format(bytes) : string.Empty;
        }
    }

    public IEnumerable<FolderReviewNode> SelectedFiles()
    {
        if (!IsDirectory)
        {
            if (_isChecked == true)
            {
                yield return this;
            }

            yield break;
        }

        foreach (var child in Children)
        {
            foreach (var file in child.SelectedFiles())
            {
                yield return file;
            }
        }
    }

    private void SetIsChecked(bool? value, bool updateChildren, bool updateParent)
    {
        if (_isChecked == value)
        {
            return;
        }

        _isChecked = value;
        OnPropertyChanged(nameof(IsChecked));

        if (updateChildren && value.HasValue)
        {
            foreach (var child in Children)
            {
                child._suppressParentUpdate = true;
                try
                {
                    child.SetIsChecked(value, updateChildren: true, updateParent: false);
                }
                finally
                {
                    child._suppressParentUpdate = false;
                }
            }
        }

        if (updateParent && !_suppressParentUpdate)
        {
            Parent?.RecalculateFromChildren();
        }
    }

    internal void RecalculateFromChildren()
    {
        if (Children.Count == 0)
        {
            return;
        }

        var allChecked = true;
        var allUnchecked = true;
        foreach (var child in Children)
        {
            if (child._isChecked != true)
            {
                allChecked = false;
            }

            if (child._isChecked != false)
            {
                allUnchecked = false;
            }
        }

        bool? newState = allChecked ? true : allUnchecked ? false : null;
        if (_isChecked != newState)
        {
            _isChecked = newState;
            OnPropertyChanged(nameof(IsChecked));
            Parent?.RecalculateFromChildren();
        }
    }

    private static int CountFiles(FolderReviewNode node)
    {
        if (!node.IsDirectory)
        {
            return 1;
        }

        var sum = 0;
        foreach (var child in node.Children)
        {
            sum += CountFiles(child);
        }

        return sum;
    }
}
