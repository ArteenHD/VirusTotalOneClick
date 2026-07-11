namespace VirusTotalOneClick.Core;

public interface ISendToShortcutWriter
{
    void Create(ShortcutRequest request);

    void Delete();
}
