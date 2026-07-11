namespace VirusTotalOneClick.UI;

public sealed class UploadQueueItem : ObservableObject
{
    private string _status = "Waiting";
    private string? _submittedName;
    private string? _resultUrl;
    private string? _errorMessage;

    public UploadQueueItem(string originalPath, string? submittedName = null)
    {
        OriginalPath = originalPath ?? string.Empty;
        _submittedName = submittedName;
    }

    public string OriginalPath { get; }

    public string? SubmittedName
    {
        get => _submittedName;
        set => SetProperty(ref _submittedName, value);
    }

    public string Status
    {
        get => _status;
        set
        {
            if (SetProperty(ref _status, value))
            {
                OnPropertyChanged(nameof(DisplayStatus));
            }
        }
    }

    public string? ResultUrl
    {
        get => _resultUrl;
        set
        {
            if (SetProperty(ref _resultUrl, value))
            {
                OnPropertyChanged(nameof(HasResultUrl));
            }
        }
    }

    public bool HasResultUrl => !string.IsNullOrWhiteSpace(_resultUrl);

    public string? ErrorMessage
    {
        get => _errorMessage;
        set
        {
            if (SetProperty(ref _errorMessage, value))
            {
                OnPropertyChanged(nameof(HasErrorMessage));
                OnPropertyChanged(nameof(DisplayStatus));
            }
        }
    }

    public bool HasErrorMessage => !string.IsNullOrWhiteSpace(_errorMessage);

    public string DisplayStatus => Status == "Failed" && HasErrorMessage
        ? $"Failed: {ErrorMessage}"
        : Status;
}
