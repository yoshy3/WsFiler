using Avalonia.Controls;
using Avalonia.Threading;
using System;
using System.IO;
using System.IO.Pipes;
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
                if (window.WindowState == WindowState.Minimized)
                {
                    window.WindowState = WindowState.Normal;
                }

                window.Show();
                window.Activate();
            });
        }
    }
}
