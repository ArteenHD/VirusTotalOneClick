namespace VirusTotalOneClick.Core;

public interface IStartMenuShortcutWriter
{
    void Create(ShortcutRequest request);

    void DeleteAll();
}
