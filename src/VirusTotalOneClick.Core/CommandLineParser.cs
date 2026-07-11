namespace VirusTotalOneClick.Core;

public static class CommandLineParser
{
    public static CommandLineOptions Parse(IReadOnlyList<string> args)
    {
        if (args.Count == 0)
        {
            return new CommandLineOptions(AppMode.Interactive, []);
        }

        if (IsToken(args[0], "settings"))
        {
            return new CommandLineOptions(AppMode.Settings, []);
        }

        if (IsToken(args[0], "history"))
        {
            return new CommandLineOptions(AppMode.History, []);
        }

        if (IsToken(args[0], "install-shell"))
        {
            return new CommandLineOptions(AppMode.InstallShell, []);
        }

        if (IsToken(args[0], "uninstall-shell"))
        {
            return new CommandLineOptions(AppMode.UninstallShell, []);
        }

        if (IsToken(args[0], "upload"))
        {
            return new CommandLineOptions(AppMode.Upload, ParsePathArguments(args));
        }

        // The app is launched by shell surfaces outside its control, so an
        // unrecognized verb opens the interactive window instead of erroring.
        return new CommandLineOptions(AppMode.Interactive, []);
    }

    private static bool IsToken(string argument, string expected)
        => string.Equals(argument, expected, StringComparison.OrdinalIgnoreCase);

    private static List<string> ParsePathArguments(IReadOnlyList<string> args)
    {
        var paths = new List<string>();

        for (var index = 1; index < args.Count; index++)
        {
            if (IsToken(args[index], "--path"))
            {
                if (index + 1 >= args.Count)
                {
                    break;
                }

                paths.Add(args[index + 1]);
                index++;
                continue;
            }

            if (args[index].StartsWith("--", StringComparison.Ordinal))
            {
                continue;
            }

            paths.Add(args[index]);
        }

        return paths;
    }
}
