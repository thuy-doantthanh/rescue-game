using SplashKitSDK;

namespace CooperativeRescue;

// A rescuee who cannot walk and must be carried to the exit by a responder.
public class InjuredRescuee : Rescuee
{
    private Responder? _carrier;

    // The player carrying this rescuee, if any.
    public override int? RescuerId => _carrier?.Id;

    // Creates an injured rescuee from coordinates.
    public InjuredRescuee(int id, float x, float y)
        : base(id, x, y)
    {
    }

    // Creates an injured rescuee from a point.
    public InjuredRescuee(int id, Point2D position)
        : base(id, position)
    {
    }

    // Identifies the responder carrying this rescuee, if any.
    public int? CarrierId => _carrier?.Id;

    public override RescueeState State
    {
        get
        {
            if (IsSafe)
            {
                return RescueeState.Safe;
            }

            return _carrier == null ? RescueeState.Waiting : RescueeState.Carried;
        }
    }

    public override bool TryAcceptHelp(Responder responder)
    {
        // Reject help if already safe, already carried, the responder is busy, or too far away.
        if (IsSafe || _carrier != null || responder.IsCarrying || !responder.IsNear(Position))
        {
            return false;
        }

        _carrier = responder;
        responder.PickUp(this);
        return true;
    }

    public override void Update(float deltaTime, RescueMap map)
    {
        // Follow the carrying responder's position while being carried.
        if (_carrier != null && !IsSafe)
        {
            Position = _carrier.Position;
        }
    }

    // Marks the rescuee as safe and releases the carrier, so they can help someone else.
    public override void ReachExit()
    {
        base.ReachExit();
        _carrier?.Drop();
        _carrier = null;
    }
}
