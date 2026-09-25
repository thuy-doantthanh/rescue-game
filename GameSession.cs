using SplashKitSDK;

namespace CooperativeRescue;

// Manages game state, entity updates, player actions, and rendering for a rescue session.
public class GameSession
{
    // Length of a round, in seconds.
    private const float RoundLengthSeconds = 300f;

    // Bitmap of Player1, Player2, WalkingRescuee, InjuredRescuee, and InjuredRescuee after Rescured
    private readonly Bitmap _player1Image;
    private readonly Bitmap _player2Image;
    private readonly Bitmap _walkingImage;
    private readonly Bitmap _injuredImage;
    private readonly Bitmap _player1CarryingImage;
    private readonly Bitmap _player2CarryingImage;

    // The walking rescuee is drawn orange, and everyone else red.
    private const int WalkingRescueeId = 1;

    private readonly RescueMap _map;

    private readonly Responder[] _players =
    {
        new(1, SplashKit.PointAt(70, 100)),
        new(2, SplashKit.PointAt(70, 470))
    };

    private int _player1Score;
    private int _player2Score;

    private readonly List<Rescuee> _people = new()
{
    new WalkingRescuee(1, SplashKit.PointAt(250, 160)),
    new WalkingRescuee(2, SplashKit.PointAt(540, 140)),
    new WalkingRescuee(3, SplashKit.PointAt(180, 430)),

    new InjuredRescuee(4, SplashKit.PointAt(600, 400)),
    new InjuredRescuee(5, SplashKit.PointAt(450, 480)),
    new InjuredRescuee(6, SplashKit.PointAt(650, 250))
};

    private float _timeLeft = RoundLengthSeconds;

    // Initialises the map with the level's walls.
    public GameSession()
    {
        List<Rectangle> walls = new()
        {
            SplashKit.RectangleFrom(360, 60, 20, 340),
            SplashKit.RectangleFrom(100, 200, 150, 20)
            };

        // Both game instances use the same seed, so they display the same walls.
        Random random = new(2026);

        Rectangle[] possibleWalls =
        {
            SplashKit.RectangleFrom(300, 100, 20, 100),
            SplashKit.RectangleFrom(500, 100, 20, 100),
            SplashKit.RectangleFrom(200, 300, 100, 20),
            SplashKit.RectangleFrom(460, 300, 100, 20),
            SplashKit.RectangleFrom(120, 500, 100, 20),
            SplashKit.RectangleFrom(300, 450, 20, 80),
            SplashKit.RectangleFrom(620, 330, 20, 100),
            SplashKit.RectangleFrom(560, 220, 100, 20)
        };

        Point2D exitPoint = SplashKit.PointAt(750, 530);
        Bitmap exitImage = SplashKit.LoadBitmap("exit-image", "exit.png");

        foreach (Rectangle candidate in possibleWalls.OrderBy(_ => random.Next()))
        {
            if (walls.Count >= 6) break; // Two original walls plus four more.

            walls.Add(candidate);
            RescueMap testMap = new(walls, exitImage);

            // Reject a wall if it blocks a starting position or the route to the exit.
            bool everyoneCanReachExit = _players.All(p =>
                testMap.CanStandAt(p.Position) &&
                testMap.FindPath(p.Position, exitPoint).Count > 0)
                && _people.All(p =>
                    testMap.CanStandAt(p.Position) &&
                    testMap.FindPath(p.Position, exitPoint).Count > 0);

            if (!everyoneCanReachExit)
            {
                walls.RemoveAt(walls.Count - 1);
            }
        }

        _map = new RescueMap(walls, exitImage);

        _player1Image = SplashKit.LoadBitmap("player-1", "player-1.png");
        _player2Image = SplashKit.LoadBitmap("player-2", "player-2.png");
        _walkingImage = SplashKit.LoadBitmap("walking-rescuee", "walking-rescuee.png");
        _injuredImage = SplashKit.LoadBitmap("injured-rescuee", "injured-rescuee.png");
        _player1CarryingImage = SplashKit.LoadBitmap(
            "player-1-carrying", "player-1-injured-rescuee.png");

        _player2CarryingImage = SplashKit.LoadBitmap(
            "player-2-carrying", "player-2-injured-rescuee.png");
    }

    // Applies a local or remote player's input to their responder.
    public void Apply(int index, PlayerAction action, float deltaTime)
    {
        // The index comes from our own code, so a bad one is a programming error: fail loudly.
        if (index < 0 || index >= _players.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(index), index, "There is no player with this index.");
        }

        if (IsGameOver())
        {
            return;
        }

        // The action may come from the network. NaN or infinity would corrupt the position maths,
        // and Math.Clamp does not remove them, so such input is ignored.
        if (!float.IsFinite(action.DeltaX) || !float.IsFinite(action.DeltaY))
        {
            return;
        }

        Responder player = _players[index];

        float clampedDx = Math.Clamp(action.DeltaX, -1f, 1f);
        float clampedDy = Math.Clamp(action.DeltaY, -1f, 1f);
        player.Move(clampedDx, clampedDy, deltaTime, _map);

        if (action.Help)
        {
            // Each press helps at most one person.
            foreach (Rescuee person in _people)
            {
                if (person.TryAcceptHelp(player))
                {
                    break;
                }
            }
        }
    }

    // Updates the timer and every rescuee, and checks who has reached the exit.
    public void Update(float deltaTime)
    {
        if (IsGameOver())
        {
            return;
        }

        _timeLeft = MathF.Max(0f, _timeLeft - deltaTime);

        foreach (Rescuee person in _people)
        {
            person.Update(deltaTime, _map);

            // Only someone being helped can reach safety; a waiting rescuee stays put.
            bool isMoving = person.State != RescueeState.Waiting;
            if (!person.IsSafe && isMoving && _map.AtExit(person.Position))
            {
                if (!person.IsSafe && isMoving && _map.AtExit(person.Position))
                {
                    int points = person is InjuredRescuee ? 2 : 1;

                    if (person.RescuerId == 1)
                    {
                        _player1Score += points;
                    }
                    else if (person.RescuerId == 2)
                    {
                        _player2Score += points;
                    }

                    person.ReachExit();
                }
            }
        }
    }

    // Captures the current world state into an immutable snapshot for the network.
    public Snapshot Capture()
    {
        List<PersonState> personStates = _people
        .Select(p => new PersonState(
            p.Id,
            p.Position,
            p.IsSafe,
            p.State,
            p is InjuredRescuee injured ? injured.CarrierId : null
    ))
    .ToList();

        return new Snapshot(
            _players[0].Position,
            _players[1].Position,
            personStates,
            _timeLeft,
            _player1Score,
            _player2Score
        );
    }

    // Draws the map, players, rescuees and status text for a snapshot.
    public void Draw(Snapshot state)
    {
        SplashKit.ClearScreen(Color.White);

        bool finished = state.TimeLeft <= 0 || state.People.All(p => p.Safe);

        if (finished)
        {
            string result = state.Player1Score > state.Player2Score
                ? "PLAYER 1 WINS!"
                : state.Player2Score > state.Player1Score
                    ? "PLAYER 2 WINS!"
                    : "DRAW!";

            SplashKit.DrawText(result, Color.Red, 12, 15);
            SplashKit.DrawText(
                $"Player 1: {state.Player1Score}  Player 2: {state.Player2Score}",
                Color.Black, 12, 35);

            SplashKit.RefreshScreen(60);
            return;
        }
        
        _map.Draw();

        // A carried rescuee and their responder appear as one image.
        PersonState? carried = state.People.FirstOrDefault(
            p => p.State == RescueeState.Carried
        );

        DrawCentred(
            carried?.CarrierId == 1 ? _player1CarryingImage : _player1Image,
            state.Player1Position,
            48, 48
        );

        DrawCentred(
            carried?.CarrierId == 2 ? _player2CarryingImage : _player2Image,
            state.Player2Position,
            48, 48
        );

        foreach (PersonState person in state.People)
        {
            // The combined image already represents a carried rescuee.
            if (person.Safe || person.State == RescueeState.Carried)
            {
                continue;
            }

            Bitmap image = person.Id <= 3 ? _walkingImage : _injuredImage;

            DrawCentred(image, person.Position, 42, 42);
        }

        // Status and controls text.
        SplashKit.DrawText(StatusText(state), Color.Black, 12, 15);
        SplashKit.DrawText("Arrows move | Space help | Red = host | Blue = client", Color.Black, 12, 35);

        SplashKit.RefreshScreen(60);
    }

    // Draws an image centred on a game position at the requested size.
    private static void DrawCentred(Bitmap image, Point2D position, double width, double height)
    {
        SplashKit.DrawBitmap(
            image,
            position.X - width / 2,
            position.Y - height / 2,
            SplashKit.OptionScaleBmp(width / image.Width, height / image.Height)
        );
    }
    // Describes the round using only the snapshot. The client's own session never updates, so reading
    // this session's rescuees here meant the client never showed "ALL RESCUED!".
    private static string StatusText(Snapshot state)
    {
        string scores = $"P1: {state.Player1Score}  P2: {state.Player2Score}";
        bool finished = state.TimeLeft <= 0 || state.People.All(p => p.Safe);

        if (!finished)
        {
            return $"Time: {state.TimeLeft:0}  {scores}";
        }

        string winner = state.Player1Score > state.Player2Score
            ? "PLAYER 1 WINS!"
            : state.Player2Score > state.Player1Score
                ? "PLAYER 2 WINS!"
                : "DRAW!";

        return $"{winner}  {scores}";
    }

    // The round ends when time runs out or everyone is safe.
    private bool IsGameOver() => _timeLeft <= 0 || AreAllPeopleSafe();

    // Checks whether every rescuee has reached the exit.
    private bool AreAllPeopleSafe() => _people.All(p => p.IsSafe);
}
