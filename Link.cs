using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;

namespace CooperativeRescue;

// Handles thread-safe asynchronous TCP socket transport and JSON serialization.
public sealed class Link : IDisposable
{
    // Shared serializer options. IncludeFields is needed because SplashKit's Point2D uses public fields.
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        IncludeFields = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly TcpClient _tcp;
    private readonly StreamReader _reader;
    private readonly StreamWriter _writer;

    // A null entry means the connection has closed.
    private readonly ConcurrentQueue<string?> _incoming = new();

    // 1 once receiving has started. Changed with Interlocked, so it is safe across threads.
    private int _isReceiving;
    private bool _isDisposed;

    private Link(TcpClient tcp)
    {
        _tcp = tcp;
        NetworkStream stream = tcp.GetStream();
        _reader = new StreamReader(stream);
        _writer = new StreamWriter(stream) { AutoFlush = true };
    }

    // Waits for the other instance to connect on this computer.
    public static async Task<Link> AcceptAsync(int port)
    {
        TcpListener listener = new(IPAddress.Loopback, port);
        listener.Start();
        try
        {
            return new Link(await listener.AcceptTcpClientAsync());
        }
        finally
        {
            listener.Stop();
        }
    }

    // Connects to a host on this computer.
    public static async Task<Link> ConnectAsync(int port)
    {
        TcpClient tcp = new();
        try
        {
            await tcp.ConnectAsync(IPAddress.Loopback, port);
            return new Link(tcp);
        }
        catch
        {
            // Without this, a failed connection would leak the socket.
            tcp.Dispose();
            throw;
        }
    }

    // Starts reading incoming messages on a background task.
    public void StartReceiving()
    {
        // Two readers on one stream would corrupt messages, so this may only run once.
        if (Interlocked.Exchange(ref _isReceiving, 1) == 1)
        {
            throw new InvalidOperationException("This link is already receiving.");
        }

        _ = Task.Run(async () =>
        {
            try
            {
                string? line;
                while ((line = await _reader.ReadLineAsync()) != null)
                {
                    _incoming.Enqueue(line);
                }
            }
            catch (IOException)
            {
                // The connection was reset. This is treated the same as a normal close.
            }
            catch (ObjectDisposedException)
            {
                // Dispose() closed the stream while a read was waiting. This is expected on shutdown.
            }
            finally
            {
                // Tells the reader that the connection has closed.
                _incoming.Enqueue(null);
            }
        });
    }

    // Takes the next received message, if there is one. Never waits.
    public bool TryRead(out string? message) => _incoming.TryDequeue(out message);

    // Serializes a value to JSON and sends it as one line.
    public void Send<T>(T value) => _writer.WriteLine(JsonSerializer.Serialize(value, JsonOptions));

    // Closes the connection.
    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;

        try
        {
            _writer.Dispose();
        }
        catch (IOException)
        {
            // The connection was already broken, so there is nothing left to flush.
        }

        _reader.Dispose();
        _tcp.Dispose();
    }
}
