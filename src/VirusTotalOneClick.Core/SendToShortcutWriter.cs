namespace VirusTotalOneClick.Core;

public sealed class SendToShortcutWriter : ISendToShortcutWriter
{
    private readonly string _sendToDirectory;

    public SendToShortcutWriter()
        : this(SendToShortcutService.GetSendToDirectory())
    {
    }

    public SendToShortcutWriter(string sendToDirectory)
    {
        _sendToDirectory = sendToDirectory;
    }

    public void Create(ShortcutRequest request)
    {
        Directory.CreateDirectory(_sendToDirectory);
        WindowsShortcutFile.Create(
            Path.Combine(_sendToDirectory, request.ShortcutName),
            request.ExecutablePath,
            request.Arguments);
    }

    public void Delete()
    {
        var shortcutPath = Path.Combine(_sendToDirectory, SendToShortcutService.ShortcutName);
        if (File.Exists(shortcutPath))
        {
            File.Delete(shortcutPath);
        }
    }
}
