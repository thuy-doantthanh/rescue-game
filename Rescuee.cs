using SplashKitSDK;

namespace CooperativeRescue;

// The base class for people who need to be rescued.
public abstract class Rescuee
{
    // The player who is helping this rescuee.
    public abstract int? RescuerId { get; }

    // Initialises identity and starting position from coordinates.
    protected Rescuee(int id, float x, float y)
        : this(id, SplashKit.PointAt(x, y))
    {
    }

    // Initialises identity and starting position from a point.
    protected Rescuee(int id, Point2D position)
    {
        Id = id;
        Position = position;
    }

    // Gets the rescuee's identifier.
    public int Id { get; }

    // Gets the current position. Only the rescuee itself can change it.
    public Point2D Position { get; protected set; }

    // Gets the x coordinate of Position.
    public float X => (float)Position.X;

    // Gets the y coordinate of Position.
    public float Y => (float)Position.Y;

    // Gets whether the rescuee has reached the exit.
    public bool IsSafe { get; private set; }

    // Gets the rescuee's current assistance status.
    public abstract RescueeState State { get; }

    // Offers help from a responder.
    public abstract bool TryAcceptHelp(Responder responder);

    // Updates the rescuee for one frame.
    public abstract void Update(float deltaTime, RescueMap map);

    // Marks the rescuee as safe. Overrides must call the base method.
    public virtual void ReachExit()
    {
        IsSafe = true;
    }
}
