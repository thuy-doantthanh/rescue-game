using SplashKitSDK;

namespace CooperativeRescue;

// A player-controlled rescuer who moves around the map and helps rescuees.
public class Responder
{
    // Movement speeds, in pixels per second.
    private const float WalkingSpeed = 130f;
    private const float CarryingSpeed = 80f;

    // How close a responder must be to help someone, in pixels.
    private const float HelpRange = 48f;

    private InjuredRescuee? _carried;

    // Creates a responder at a point.
    public Responder(int id, Point2D position)
    {
        Id = id;
        Position = position;
    }

    // Creates a responder from coordinates.
    public Responder(int id, float x, float y)
        : this(id, SplashKit.PointAt(x, y))
    {
    }

    // Gets the player number.
    public int Id { get; }

    // Gets the current position. Only the responder itself can change it.
    public Point2D Position { get; private set; }

    // Gets the x coordinate of Position.
    public float X => (float)Position.X;

    // Gets the y coordinate of Position.
    public float Y => (float)Position.Y;

    // Gets whether the responder is carrying someone.
    public bool IsCarrying => _carried != null;

    // Checks whether a position is close enough to help someone standing there.
    public bool IsNear(Point2D targetPosition)
    {
        float dx = (float)(Position.X - targetPosition.X);
        float dy = (float)(Position.Y - targetPosition.Y);
        return (dx * dx) + (dy * dy) <= HelpRange * HelpRange;
    }

    // Checks whether a position is close enough to help someone standing there.
    public bool IsNear(float targetX, float targetY)
    {
        return IsNear(SplashKit.PointAt(targetX, targetY));
    }

    // Starts carrying an injured rescuee.
    public void PickUp(InjuredRescuee person)
    {
        _carried = person;
    }

    // Stops carrying. Called when the carried rescuee reaches the exit.
    public void Drop()
    {
        _carried = null;
    }

    // Moves for one frame. The direction is normalised, so diagonal movement is not faster.
    public void Move(
        float dx,
        float dy,
        float deltaTime,
        RescueMap map)
    {
        float distance = MathF.Sqrt(dx * dx + dy * dy);
        if (distance == 0f)
        {
            return;
        }

        float speed = IsCarrying ? CarryingSpeed : WalkingSpeed;
        float newX = (float)Position.X + dx / distance * speed * deltaTime;
        float newY = (float)Position.Y + dy / distance * speed * deltaTime;

        // Check each direction separately so the responder can slide around a rescuee.
        Point2D horizontalCheck = SplashKit.PointAt(newX, Position.Y);
        if (map.CanStandAt(horizontalCheck))
        {
            Position = horizontalCheck;
        }

        Point2D verticalCheck = SplashKit.PointAt(Position.X, newY);
        if (map.CanStandAt(verticalCheck))
        {
            Position = verticalCheck;
        }
    }

}
