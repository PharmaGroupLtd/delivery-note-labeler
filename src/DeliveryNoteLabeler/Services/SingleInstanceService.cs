using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using DeliveryNoteLabeler.Core.Services;

namespace DeliveryNoteLabeler.Services;

public sealed class SingleInstanceService : IDisposable
{
    public const string MutexName = "DeliveryNoteLabeler_SingleInstance";
    public const string PipeName = "DeliveryNoteLabeler";
    private const string Magic = "DNL1\n";
    private const int ConnectRetryCount = 60;
    private const int ConnectRetryDelayMs = 150;

    private readonly Mutex _mutex;
    private readonly bool _isPrimary;
    private CancellationTokenSource? _listenerCts;
    private Task? _listenerTask;
    private TaskCompletionSource<bool>? _listenerReady;

    public SingleInstanceService()
    {
        _mutex = new Mutex(true, MutexName, out _isPrimary);
    }

    public bool IsPrimaryInstance => _isPrimary;

    public bool TryForwardToExistingInstance(IReadOnlyList<string> pdfPaths)
    {
        if (_isPrimary)
        {
            return false;
        }

        var payload = Magic + JsonSerializer.Serialize(pdfPaths);
        var bytes = Encoding.UTF8.GetBytes(payload);

        for (var attempt = 0; attempt < ConnectRetryCount; attempt++)
        {
            try
            {
                using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
                client.Connect(500);
                client.Write(bytes, 0, bytes.Length);
                client.Flush();
                return true;
            }
            catch (TimeoutException)
            {
                Thread.Sleep(ConnectRetryDelayMs);
            }
            catch (IOException)
            {
                Thread.Sleep(ConnectRetryDelayMs);
            }
        }

        return false;
    }

    public void StartListening(Action<IReadOnlyList<string>> onPathsReceived)
    {
        if (!_isPrimary)
        {
            return;
        }

        _listenerReady = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _listenerCts = new CancellationTokenSource();
        var token = _listenerCts.Token;
        _listenerTask = Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    await using var server = new NamedPipeServerStream(
                        PipeName,
                        PipeDirection.In,
                        NamedPipeServerStream.MaxAllowedServerInstances,
                        PipeTransmissionMode.Byte,
                        PipeOptions.Asynchronous);

                    _listenerReady.TrySetResult(true);

                    await server.WaitForConnectionAsync(token).ConfigureAwait(false);

                    using var memory = new MemoryStream();
                    await server.CopyToAsync(memory, token).ConfigureAwait(false);
                    var data = Encoding.UTF8.GetString(memory.ToArray());
                    if (!data.StartsWith(Magic, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    var json = data[Magic.Length..];
                    var rawPaths = JsonSerializer.Deserialize<List<string>>(json) ?? [];
                    var paths = rawPaths.Count == 0
                        ? []
                        : PdfPathParser.ParseForwardedPdfPaths(rawPaths);

                    onPathsReceived(paths);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (IOException)
                {
                    // Retry on transient pipe errors.
                }
            }
        }, token);
    }

    public async Task WaitForListenerReadyAsync(TimeSpan timeout)
    {
        if (_listenerReady is null)
        {
            return;
        }

        using var cts = new CancellationTokenSource(timeout);
        var completed = await Task.WhenAny(
            _listenerReady.Task,
            Task.Delay(Timeout.InfiniteTimeSpan, cts.Token)).ConfigureAwait(false);

        if (completed != _listenerReady.Task)
        {
            throw new TimeoutException("Single-instance listener did not start in time.");
        }
    }

    public void Dispose()
    {
        _listenerCts?.Cancel();
        try
        {
            _listenerTask?.Wait(TimeSpan.FromSeconds(1));
        }
        catch
        {
            // Ignore shutdown errors.
        }

        _listenerCts?.Dispose();
        if (_isPrimary)
        {
            try
            {
                _mutex.ReleaseMutex();
            }
            catch
            {
                // Ignore if already released.
            }
        }

        _mutex.Dispose();
    }
}
