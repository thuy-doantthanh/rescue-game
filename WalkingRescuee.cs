using SplashKitSDK;

namespace CooperativeRescue;

// A rescuee who follows their responder, using A* pathfinding to get around walls.
public class WalkingRescuee : Rescuee
{
    // Walking speed, in pixels per second.
    private const float MovementSpeed = 100f;

    // Replans at least this often, so the route stays fresh while the leader stays inside one cell.
    private const float ReplanIntervalSeconds = 0.25f;

    // How close counts as having reached a waypoint, in pixels.
    private const float WaypointTolerance = 2f;

    private Responder? _leader;

    // The player this rescuee is following, if any.
    public override int? RescuerId => _leader?.Id;

    private List<(int x, int y)> _route = new();
    private int _nextCell;
    private (int x, int y) _lastTarget = (-1, -1);
    private float _replanTimer;

    // Creates a walking rescuee at a point.
    public WalkingRescuee(int id, Point2D position)
        : base(id, position)
    {
    }

    // Creates a walking rescuee from coordinates.
    public WalkingRescuee(int id, float x, float y)
        : base(id, x, y)
    {
    }

    public override RescueeState State
    {
        get
        {
            if (IsSafe)
            {
                return RescueeState.Safe;
            }

            return _leader == null ? RescueeState.Waiting : RescueeState.Following;
        }
    }

    // Accepts a nearby responder as a leader, unless already safe or following someone.
    public override bool TryAcceptHelp(Responder responder)
    {
        if (IsSafe || _leader != null || !responder.IsNear(Position))
        {
            return false;
        }

        _leader = responder;
        return true;
    }

    // Updates pathfinding, then moves one step towards the leader.
    public override void Update(float deltaTime, RescueMap map)
    {
        // Nothing to do once safe, or while waiting for help.
        if (IsSafe || _leader == null)
        {
            return;
        }

        UpdateRouteToLeader(deltaTime, map);

        // Stop once the end of the current route is reached.
        if (_nextCell >= _route.Count)
        {
            return;
        }

        MoveAlongRoute(deltaTime, map);
    }

    // Recalculates the path to the leader when the leader changes cell, or when the replan timer runs out.
    private void UpdateRouteToLeader(float deltaTime, RescueMap map)
    {
        if (_leader == null)
        {
            return;
        }

        (int x, int y) leaderCell = map.ToCell(_leader.Position);
        _replanTimer -= deltaTime;

        bool leaderMovedCells = leaderCell != _lastTarget;
        bool timerExpired = _replanTimer <= 0;

        if (leaderMovedCells || timerExpired)
        {
            _route = map.FindPath(Position, _leader.Position);

            // Index 0 is the cell we are already in, so aim for index 1 when there is one.
            _nextCell = _route.Count > 1 ? 1 : 0;

            _lastTarget = leaderCell;
            _replanTimer = ReplanIntervalSeconds;
        }
    }

    // Moves towards the centre of the next cell on the route, sliding along walls if blocked.
    private void MoveAlongRoute(float deltaTime, RescueMap map)
    {
        Point2D targetPoint = map.CellCentre(_route[_nextCell]);

        float deltaX = (float)(targetPoint.X - Position.X);
        float deltaY = (float)(targetPoint.Y - Position.Y);
        float distanceToTarget = MathF.Sqrt((deltaX * deltaX) + (deltaY * deltaY));

        // Move on to the next cell once close enough to this one's centre.
        if (distanceToTarget < WaypointTolerance)
        {
            _nextCell++;
            return;
        }

        // Never overshoot the waypoint.
        float stepDistance = MathF.Min(MovementSpeed * deltaTime, distanceToTarget);

        float nextX = (float)Position.X + (deltaX / distanceToTarget * stepDistance);
        float nextY = (float)Position.Y + (deltaY / distanceToTarget * stepDistance);

        // Move X and Y separately, so the rescuee slides along walls.
        Point2D horizontalCheck = SplashKit.PointAt(nextX, Position.Y);
        if (map.CanStandAt(horizontalCheck))
        {
            Position = horizontalCheck;
        }

        Point2D verticalCheck = SplashKit.PointAt(Position.X, nextY);
        if (map.CanStandAt(verticalCheck))
        {
            Position = verticalCheck;
        }
    }
}
