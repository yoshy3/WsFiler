using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using System.Runtime.Versioning;

namespace WsFiler.App.Shell;

[SupportedOSPlatform("windows")]
internal static partial class ShellContextMenu
{
    private const uint CMF_NORMAL = 0x00000000;
    private const uint CMF_EXPLORE = 0x00000020;
    private const uint TPM_RETURNCMD = 0x0100;
    private const uint TPM_RIGHTBUTTON = 0x0002;
    private const int SW_SHOWNORMAL = 1;
    private const uint MIN_CMD_ID = 1;
    private const uint MAX_CMD_ID = 0x7FFF;

    private static readonly Guid IID_IShellFolder = new("000214E6-0000-0000-C000-000000000046");
    private static readonly Guid IID_IContextMenu = new("000214E4-0000-0000-C000-000000000046");

    private static readonly StrategyBasedComWrappers ComWrappers = new();

    public static void ShowForFiles(IntPtr ownerHwnd, IReadOnlyList<string> fullPaths, int screenX, int screenY)
    {
        if (fullPaths.Count == 0)
        {
            return;
        }

        var groups = fullPaths
            .Select(p => (Folder: Path.GetDirectoryName(p) ?? string.Empty, Name: Path.GetFileName(p)))
            .Where(t => !string.IsNullOrEmpty(t.Folder) && !string.IsNullOrEmpty(t.Name))
            .GroupBy(t => t.Folder, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (groups.Count != 1)
        {
            return;
        }

        var group = groups[0];
        ShowMenu(ownerHwnd, group.Key, group.Select(g => g.Name).ToList(), screenX, screenY);
    }

    public static void ShowForFolderBackground(IntPtr ownerHwnd, string folderPath, int screenX, int screenY)
    {
        ShowMenu(ownerHwnd, folderPath, [], screenX, screenY);
    }

    private static unsafe void ShowMenu(IntPtr ownerHwnd, string folderPath, IReadOnlyList<string> childNames, int screenX, int screenY)
    {
        if (SHGetDesktopFolder(out var desktopPtr) != 0 || desktopPtr == IntPtr.Zero)
        {
            return;
        }

        IShellFolder? desktopFolder = null;
        IShellFolder? targetFolder = null;
        IContextMenu? contextMenu = null;
        var allocatedPidls = new List<IntPtr>();
        var hMenu = IntPtr.Zero;

        try
        {
            desktopFolder = (IShellFolder)ComWrappers.GetOrCreateObjectForComInstance(desktopPtr, CreateObjectFlags.UniqueInstance);

            uint eaten = 0;
            uint attrs = 0;
            if (desktopFolder.ParseDisplayName(IntPtr.Zero, IntPtr.Zero, folderPath, ref eaten, out var folderPidl, ref attrs) != 0)
            {
                return;
            }
            allocatedPidls.Add(folderPidl);

            var shellFolderIid = IID_IShellFolder;
            if (desktopFolder.BindToObject(folderPidl, IntPtr.Zero, ref shellFolderIid, out var folderPtr) != 0 || folderPtr == IntPtr.Zero)
            {
                return;
            }

            try
            {
                targetFolder = (IShellFolder)ComWrappers.GetOrCreateObjectForComInstance(folderPtr, CreateObjectFlags.UniqueInstance);
            }
            finally
            {
                Marshal.Release(folderPtr);
            }

            IntPtr menuPtr;
            var contextIid = IID_IContextMenu;
            if (childNames.Count > 0)
            {
                var childPidls = new IntPtr[childNames.Count];
                for (var i = 0; i < childNames.Count; i++)
                {
                    uint e = 0;
                    uint a = 0;
                    if (targetFolder.ParseDisplayName(ownerHwnd, IntPtr.Zero, childNames[i], ref e, out childPidls[i], ref a) != 0)
                    {
                        return;
                    }
                    allocatedPidls.Add(childPidls[i]);
                }

                fixed (IntPtr* pidlsPtr = childPidls)
                {
                    if (targetFolder.GetUIObjectOf(ownerHwnd, (uint)childPidls.Length, pidlsPtr, ref contextIid, IntPtr.Zero, out menuPtr) != 0 || menuPtr == IntPtr.Zero)
                    {
                        return;
                    }
                }
            }
            else
            {
                if (targetFolder.CreateViewObject(ownerHwnd, ref contextIid, out menuPtr) != 0 || menuPtr == IntPtr.Zero)
                {
                    return;
                }
            }

            try
            {
                contextMenu = (IContextMenu)ComWrappers.GetOrCreateObjectForComInstance(menuPtr, CreateObjectFlags.UniqueInstance);
            }
            finally
            {
                Marshal.Release(menuPtr);
            }

            hMenu = CreatePopupMenu();
            if (hMenu == IntPtr.Zero)
            {
                return;
            }

            if (contextMenu.QueryContextMenu(hMenu, 0, MIN_CMD_ID, MAX_CMD_ID, CMF_NORMAL | CMF_EXPLORE) < 0)
            {
                return;
            }

            var selected = TrackPopupMenuEx(hMenu, TPM_RETURNCMD | TPM_RIGHTBUTTON, screenX, screenY, ownerHwnd, IntPtr.Zero);
            if (selected == 0)
            {
                return;
            }

            var info = new CMINVOKECOMMANDINFO
            {
                cbSize = sizeof(CMINVOKECOMMANDINFO),
                fMask = 0,
                hwnd = ownerHwnd,
                lpVerb = (IntPtr)(selected - MIN_CMD_ID),
                lpParameters = IntPtr.Zero,
                lpDirectory = IntPtr.Zero,
                nShow = SW_SHOWNORMAL,
                dwHotKey = 0,
                hIcon = IntPtr.Zero,
            };
            contextMenu.InvokeCommand(ref info);
        }
        finally
        {
            if (hMenu != IntPtr.Zero)
            {
                DestroyMenu(hMenu);
            }
            (contextMenu as IDisposable)?.Dispose();
            (targetFolder as IDisposable)?.Dispose();
            (desktopFolder as IDisposable)?.Dispose();
            Marshal.Release(desktopPtr);
            foreach (var pidl in allocatedPidls)
            {
                if (pidl != IntPtr.Zero)
                {
                    ILFree(pidl);
                }
            }
        }
    }

    [LibraryImport("shell32.dll")]
    private static partial int SHGetDesktopFolder(out IntPtr ppshf);

    [LibraryImport("shell32.dll")]
    private static partial void ILFree(IntPtr pidl);

    [LibraryImport("user32.dll")]
    private static partial IntPtr CreatePopupMenu();

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool DestroyMenu(IntPtr hMenu);

    [LibraryImport("user32.dll")]
    private static partial uint TrackPopupMenuEx(IntPtr hmenu, uint flags, int x, int y, IntPtr hwnd, IntPtr lptpm);

    [GeneratedComInterface]
    [Guid("000214E6-0000-0000-C000-000000000046")]
    internal partial interface IShellFolder
    {
        [PreserveSig] int ParseDisplayName(IntPtr hwnd, IntPtr pbc, [MarshalAs(UnmanagedType.LPWStr)] string pszDisplayName, ref uint pchEaten, out IntPtr ppidl, ref uint pdwAttributes);
        [PreserveSig] int EnumObjects(IntPtr hwnd, int grfFlags, out IntPtr ppenumIDList);
        [PreserveSig] int BindToObject(IntPtr pidl, IntPtr pbc, ref Guid riid, out IntPtr ppv);
        [PreserveSig] int BindToStorage(IntPtr pidl, IntPtr pbc, ref Guid riid, out IntPtr ppv);
        [PreserveSig] int CompareIDs(IntPtr lParam, IntPtr pidl1, IntPtr pidl2);
        [PreserveSig] int CreateViewObject(IntPtr hwndOwner, ref Guid riid, out IntPtr ppv);
        [PreserveSig] unsafe int GetAttributesOf(uint cidl, IntPtr* apidl, ref uint rgfInOut);
        [PreserveSig] unsafe int GetUIObjectOf(IntPtr hwndOwner, uint cidl, IntPtr* apidl, ref Guid riid, IntPtr rgfReserved, out IntPtr ppv);
        [PreserveSig] int GetDisplayNameOf(IntPtr pidl, uint uFlags, IntPtr lpName);
        [PreserveSig] int SetNameOf(IntPtr hwnd, IntPtr pidl, [MarshalAs(UnmanagedType.LPWStr)] string pszName, uint uFlags, out IntPtr ppidlOut);
    }

    [GeneratedComInterface]
    [Guid("000214E4-0000-0000-C000-000000000046")]
    internal partial interface IContextMenu
    {
        [PreserveSig] int QueryContextMenu(IntPtr hMenu, uint indexMenu, uint idCmdFirst, uint idCmdLast, uint uFlags);
        [PreserveSig] int InvokeCommand(ref CMINVOKECOMMANDINFO pici);
        [PreserveSig] int GetCommandString(IntPtr idcmd, uint uflags, IntPtr pwReserved, IntPtr commandstring, int cch);
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct CMINVOKECOMMANDINFO
    {
        public int cbSize;
        public uint fMask;
        public IntPtr hwnd;
        public IntPtr lpVerb;
        public IntPtr lpParameters;
        public IntPtr lpDirectory;
        public int nShow;
        public uint dwHotKey;
        public IntPtr hIcon;
    }
}
