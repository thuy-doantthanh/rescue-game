using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Net.Sockets;
using System.Text.Json;
using SplashKitSDK;

namespace CooperativeRescue;

// Handles program start-up, network synchronisation, and the main game loop.
public static class Program
{
    private const int Port = 45827;
    private const int WindowWidth = 800;
    private const int WindowHeight = 600;

    // Screen positions that place the two windows side by side.
    private const int HostWindowX = 100;
    private const int ClientWindowX = 980;
    private const int WindowY = 120;

    // Caps one frame at 50 ms, even after a stall. At the fastest speed (130 px/s) that is a 6.5 px
    // step, far less than the 20 px wall thickness, so nothing can ever jump through a wall.
    private const double MaxFrameSeconds = 0.05;

    // Connects the two instances, opens the window, and runs the game loop.
    public static async Task Main(string[] args)
    {
        if (args.Length != 1 || (args[0] != "host" && args[0] != "client"))
        {
            Console.WriteLine("Usage: dotnet run -- [host|client]");
            return;
        }

        bool isHost = args[0] == "host";
        Console.WriteLine(isHost ? "Waiting for client on 127.0.0.1..." : "Connecting to host...");

        // Connects both instances over the loopback interface.
        Link link;
        try
        {
            link = isHost ? await Link.AcceptAsync(Port) : await Link.ConnectAsync(Port);
        }
        catch (SocketException exception)
        {
            Console.WriteLine($"Could not connect: {exception.Message}");
            Console.WriteLine("Start the host first, then the client.");
            return;
        }

        using (link)
        {
            link.StartReceiving();

            SplashKit.OpenWindow(isHost ? "Rescue - Host" : "Rescue - Client", WindowWidth, WindowHeight);
            GameSession game = new();
            SplashKit.MoveCurrentWindowTo(isHost ? HostWindowX : ClientWindowX, WindowY);

            Stopwatch clock = Stopwatch.StartNew();
            double previousTime = clock.Elapsed.TotalSeconds;
            PlayerAction remoteAction = new(0, 0, false);
            Snapshot visibleState = game.Capture();

            try
            {
                // Main processing, update and rendering loop.
                while (!SplashKit.QuitRequested())
                {
                    SplashKit.ProcessEvents();

                    double now = clock.Elapsed.TotalSeconds;
                    float seconds = (float)Math.Min(MaxFrameSeconds, now - previousTime);
                    previousTime = now;

                    PlayerAction localAction = ReadKeyboard();

                    if (isHost)
                    {
                        // Processes incoming player actions from the client.
                        while (link.TryRead(out string? message))
                        {
                            if (message == null)
                            {
                                Console.WriteLine("The other instance disconnected.");
                                return;
                            }

                            if (TryDeserialize(message, out PlayerAction? received))
                            {
                                // Several messages can arrive in one frame. Keeping a help press from any of them
                                // means it is not lost when a later message, without the press, arrives too.
                                remoteAction = received with { Help = remoteAction.Help || received.Help };
                            }
                        }

                        // Advances the simulation and sends the world snapshot to the client.
                        game.Apply(0, localAction, seconds);
                        game.Apply(1, remoteAction, seconds);

                        // A help press is used once; movement keeps its last value until the client sends a new one.
                        remoteAction = remoteAction with { Help = false };

                        game.Update(seconds);
                        visibleState = game.Capture();
                        link.Send(visibleState);
                    }
                    else
                    {
                        // Sends local input, then reads world state updates from the host.
                        link.Send(localAction);
                        while (link.TryRead(out string? message))
                        {
                            if (message == null)
                            {
                                Console.WriteLine("The other instance disconnected.");
                                return;
                            }

                            if (TryDeserialize(message, out Snapshot? received))
                            {
                                visibleState = received;
                            }
                        }
                    }

                    // RefreshScreen inside Draw limits the frame rate, so no extra sleep is needed.
                    game.Draw(visibleState);
                }
            }
            catch (IOException)
            {
                Console.WriteLine("The other instance disconnected.");
            }
            finally
            {
                SplashKit.CloseAllWindows();
            }
        }
    }

    // Reads movement from the arrow keys, and a help press from the space bar.
    private static PlayerAction ReadKeyboard()
    {
        int horizontal = 0;
        int vertical = 0;

        if (SplashKit.KeyDown(KeyCode.LeftKey))
        {
            horizontal--;
        }

        if (SplashKit.KeyDown(KeyCode.RightKey))
        {
            horizontal++;
        }

        if (SplashKit.KeyDown(KeyCode.UpKey))
        {
            vertical--;
        }

        if (SplashKit.KeyDown(KeyCode.DownKey))
        {
            vertical++;
        }

        bool helpPressed = SplashKit.KeyTyped(KeyCode.SpaceKey);
        return new PlayerAction(horizontal, vertical, helpPressed);
    }

    // Reads a message from the other instance. Malformed JSON is skipped instead of crashing the game.
    private static bool TryDeserialize<T>(string message, [NotNullWhen(true)] out T? value)
        where T : class
    {
        try
        {
            value = JsonSerializer.Deserialize<T>(message, Link.JsonOptions);
            return value != null;
        }
        catch (JsonException)
        {
            value = null;
            return false;
        }
    }
}
