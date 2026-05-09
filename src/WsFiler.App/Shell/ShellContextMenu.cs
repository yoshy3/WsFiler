using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace WsFiler.App.Shell;

[SupportedOSPlatform("windows")]
internal static class ShellContextMenu
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

    private static void ShowMenu(IntPtr ownerHwnd, string folderPath, IReadOnlyList<string> childNames, int screenX, int screenY)
    {
        if (SHGetDesktopFolder(out var desktopFolder) != 0 || desktopFolder is null)
        {
            return;
        }

        var allocatedPidls = new List<IntPtr>();
        IShellFolder? targetFolder = null;
        IContextMenu? contextMenu = null;
        var hMenu = IntPtr.Zero;

        try
        {
            uint eaten = 0;
            uint attrs = 0;
            if (desktopFolder.ParseDisplayName(IntPtr.Zero, IntPtr.Zero, folderPath, ref eaten, out var folderPidl, ref attrs) != 0)
            {
                return;
            }
            allocatedPidls.Add(folderPidl);

            var shellFolderIid = IID_IShellFolder;
            if (desktopFolder.BindToObject(folderPidl, IntPtr.Zero, ref shellFolderIid, out var folderPtr) != 0)
            {
                return;
            }

            try
            {
                targetFolder = (IShellFolder)Marshal.GetObjectForIUnknown(folderPtr);
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

                if (targetFolder.GetUIObjectOf(ownerHwnd, (uint)childPidls.Length, childPidls, ref contextIid, IntPtr.Zero, out menuPtr) != 0)
                {
                    return;
                }
            }
            else
            {
                if (targetFolder.CreateViewObject(ownerHwnd, ref contextIid, out menuPtr) != 0)
                {
                    return;
                }
            }

            try
            {
                contextMenu = (IContextMenu)Marshal.GetObjectForIUnknown(menuPtr);
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
                cbSize = Marshal.SizeOf<CMINVOKECOMMANDINFO>(),
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
            if (contextMenu is not null)
            {
                Marshal.ReleaseComObject(contextMenu);
            }
            if (targetFolder is not null)
            {
                Marshal.ReleaseComObject(targetFolder);
            }
            Marshal.ReleaseComObject(desktopFolder);
            foreach (var pidl in allocatedPidls)
            {
                if (pidl != IntPtr.Zero)
                {
                    ILFree(pidl);
                }
            }
        }
    }

    [DllImport("shell32.dll")]
    private static extern int SHGetDesktopFolder(out IShellFolder ppshf);

    [DllImport("shell32.dll")]
    private static extern void ILFree(IntPtr pidl);

    [DllImport("user32.dll")]
    private static extern IntPtr CreatePopupMenu();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyMenu(IntPtr hMenu);

    [DllImport("user32.dll")]
    private static extern uint TrackPopupMenuEx(IntPtr hmenu, uint flags, int x, int y, IntPtr hwnd, IntPtr lptpm);

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("000214E6-0000-0000-C000-000000000046")]
    private interface IShellFolder
    {
        [PreserveSig] int ParseDisplayName(IntPtr hwnd, IntPtr pbc, [MarshalAs(UnmanagedType.LPWStr)] string pszDisplayName, ref uint pchEaten, out IntPtr ppidl, ref uint pdwAttributes);
        [PreserveSig] int EnumObjects(IntPtr hwnd, int grfFlags, out IntPtr ppenumIDList);
        [PreserveSig] int BindToObject(IntPtr pidl, IntPtr pbc, ref Guid riid, out IntPtr ppv);
        [PreserveSig] int BindToStorage(IntPtr pidl, IntPtr pbc, ref Guid riid, out IntPtr ppv);
        [PreserveSig] int CompareIDs(IntPtr lParam, IntPtr pidl1, IntPtr pidl2);
        [PreserveSig] int CreateViewObject(IntPtr hwndOwner, ref Guid riid, out IntPtr ppv);
        [PreserveSig] int GetAttributesOf(
            uint cidl,
            [In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] IntPtr[] apidl,
            ref uint rgfInOut);
        [PreserveSig] int GetUIObjectOf(
            IntPtr hwndOwner,
            uint cidl,
            [In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] IntPtr[] apidl,
            ref Guid riid,
            IntPtr rgfReserved,
            out IntPtr ppv);
        [PreserveSig] int GetDisplayNameOf(IntPtr pidl, uint uFlags, IntPtr lpName);
        [PreserveSig] int SetNameOf(IntPtr hwnd, IntPtr pidl, [MarshalAs(UnmanagedType.LPWStr)] string pszName, uint uFlags, out IntPtr ppidlOut);
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("000214E4-0000-0000-C000-000000000046")]
    private interface IContextMenu
    {
        [PreserveSig] int QueryContextMenu(IntPtr hMenu, uint indexMenu, uint idCmdFirst, uint idCmdLast, uint uFlags);
        [PreserveSig] int InvokeCommand(ref CMINVOKECOMMANDINFO pici);
        [PreserveSig] int GetCommandString(IntPtr idcmd, uint uflags, IntPtr pwReserved, IntPtr commandstring, int cch);
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    private struct CMINVOKECOMMANDINFO
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
