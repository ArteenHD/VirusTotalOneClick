using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;

namespace VirusTotalOneClick.Core.Tests;

internal static class ShellLinkReader
{
    public static (string TargetPath, string Arguments, string IconPath, int IconIndex) Read(string shortcutPath)
    {
        object shellLinkObject = new CShellLink();
        try
        {
            var persistFile = (IPersistFile)shellLinkObject;
            persistFile.Load(shortcutPath, 0);

            var shellLink = (IShellLinkW)shellLinkObject;
            var target = new StringBuilder(260);
            shellLink.GetPath(target, target.Capacity, IntPtr.Zero, 0);
            var arguments = new StringBuilder(1024);
            shellLink.GetArguments(arguments, arguments.Capacity);
            var icon = new StringBuilder(260);
            shellLink.GetIconLocation(icon, icon.Capacity, out var iconIndex);

            return (target.ToString(), arguments.ToString(), icon.ToString(), iconIndex);
        }
        finally
        {
            Marshal.ReleaseComObject(shellLinkObject);
        }
    }

    [ComImport]
    [Guid("00021401-0000-0000-C000-000000000046")]
    private sealed class CShellLink
    {
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("000214F9-0000-0000-C000-000000000046")]
    private interface IShellLinkW
    {
        void GetPath([Out][MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszFile, int cchMaxPath, IntPtr pfd, uint fFlags);

        void GetIDList(out IntPtr ppidl);

        void SetIDList(IntPtr pidl);

        void GetDescription([Out][MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszName, int cchMaxName);

        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);

        void GetWorkingDirectory([Out][MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszDir, int cchMaxPath);

        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);

        void GetArguments([Out][MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszArgs, int cchMaxPath);

        void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);

        void GetHotkey(out short pwHotkey);

        void SetHotkey(short wHotkey);

        void GetShowCmd(out int piShowCmd);

        void SetShowCmd(int iShowCmd);

        void GetIconLocation([Out][MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszIconPath, int cchIconPath, out int piIcon);

        void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);

        void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, uint dwReserved);

        void Resolve(IntPtr hwnd, uint fFlags);

        void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
    }
}
