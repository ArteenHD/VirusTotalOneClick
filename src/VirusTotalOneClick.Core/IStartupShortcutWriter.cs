namespace VirusTotalOneClick.Core;

public interface IStartupShortcutWriter
{
    void Create(ShortcutRequest request);

    void Delete();
}
