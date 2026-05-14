using Avalonia.Controls;
using Avalonia.Threading;
using System;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace WsFiler.App;

internal static class SingleInstanceCoordinator
{
    private const string MutexName = "yoshy3.wsfiler.single-instance";
    private const string LockFileName = "wsfiler.lock";
    private const string PipeName = "wsfiler";

    public static Mutex AcquireMutex(out bool createdNew)
    {
        return new Mutex(true, MutexName, out createdNew);
    }

    public static FileStream? TryAcquireLockFile()
    {
        try
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var dir = Path.Combine(appData, "yoshy3", "wsfiler");
            Directory.CreateDirectory(dir);

            return new FileStream(
                Path.Combine(dir, LockFileName),
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.None);
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    public static void RequestActivation()
    {
        try
        {
            using var client = new NamedPipeClientStream(
                ".",
                PipeName,
                PipeDirection.Out,
                PipeOptions.Asynchronous);
            client.Connect(500);
            client.WriteByte(1);
        }
        catch
        {
        }
    }

    public static IDisposable StartActivationServer(Window window)
    {
        var server = new ActivationServer(window);
        server.Start();
        return server;
    }

    private sealed class ActivationServer(Window window) : IDisposable
    {
        private readonly CancellationTokenSource cancellationTokenSource = new();
        private Task? serverTask;

        public void Start()
        {
            serverTask = Task.Run(() => RunAsync(cancellationTokenSource.Token));
        }

        public void Dispose()
        {
            cancellationTokenSource.Cancel();
            cancellationTokenSource.Dispose();
        }

        private async Task RunAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await using var pipe = new NamedPipeServerStream(
                        PipeName,
                        PipeDirection.In,
                        maxNumberOfServerInstances: 1,
                        PipeTransmissionMode.Byte,
                        PipeOptions.Asynchronous);

                    await pipe.WaitForConnectionAsync(cancellationToken);
                    ActivateWindow();
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch
                {
                }
            }
        }

        private void ActivateWindow()
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (OperatingSystem.IsWindows() && TryActivateWindowsWindow(window))
                {
                    return;
                }

                ActivateAvaloniaWindow(window);
            });
        }

        private static void ActivateAvaloniaWindow(Window window)
        {
            if (window.WindowState == WindowState.Minimized)
            {
                window.WindowState = WindowState.Normal;
            }

            window.Show();
            window.Activate();
        }

        private static bool TryActivateWindowsWindow(Window window)
        {
            var handle = window.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
            if (handle == IntPtr.Zero)
            {
                return false;
            }

            if (NativeMethods.IsIconic(handle))
            {
                NativeMethods.ShowWindow(handle, NativeMethods.SwRestore);
            }
            else
            {
                window.Show();
                NativeMethods.ShowWindow(handle, NativeMethods.SwShow);
            }

            // Avalonia's Activate() can be ignored by Windows foreground-lock rules when
            // the existing instance is behind another app. A short topmost pulse reliably
            // raises the already-running main window without leaving it pinned on top.
            NativeMethods.SetWindowPos(
                handle,
                NativeMethods.HwndTopmost,
                0,
                0,
                0,
                0,
                NativeMethods.SwpNoMove | NativeMethods.SwpNoSize | NativeMethods.SwpShowWindow);
            NativeMethods.SetWindowPos(
                handle,
                NativeMethods.HwndNotopmost,
                0,
                0,
                0,
                0,
                NativeMethods.SwpNoMove | NativeMethods.SwpNoSize | NativeMethods.SwpShowWindow);

            return NativeMethods.SetForegroundWindow(handle);
        }
    }

    private static class NativeMethods
    {
        public const int SwShow = 5;
        public const int SwRestore = 9;
        public static readonly IntPtr HwndTopmost = new(-1);
        public static readonly IntPtr HwndNotopmost = new(-2);
        public const uint SwpNoSize = 0x0001;
        public const uint SwpNoMove = 0x0002;
        public const uint SwpShowWindow = 0x0040;

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetWindowPos(
            IntPtr hWnd,
            IntPtr hWndInsertAfter,
            int x,
            int y,
            int cx,
            int cy,
            uint uFlags);
    }
}
