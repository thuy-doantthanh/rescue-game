using SplashKitSDK;

namespace CooperativeRescue;

// The level layout (boundary, walls and exit), with collision checks and A* pathfinding.
public class RescueMap
{
    // The width and height of one grid cell, in pixels.
    public const float CellSize = 20f;

    // The number of grid columns: the map is 760 pixels wide, so 760 / 20 = 38.
    public const int Columns = 38;

    // The number of grid rows: the map is 520 pixels high, so 520 / 20 = 26.
    public const int Rows = 26;

    // The exit bitmap
    private readonly Bitmap _exitImage;

    // The walls
    private readonly List<Rectangle> _walls;

    // Creates a map with the given walls.
    public RescueMap(IEnumerable<Rectangle> walls, Bitmap exitImage)
    {
        _walls = walls.ToList();
        _exitImage = exitImage;
    }

    // Gets the playable area.
    public Rectangle MapBounds { get; } = SplashKit.RectangleFrom(20, 60, 760, 520);

    // Gets the exit area.
    public Rectangle ExitArea { get; } = SplashKit.RectangleFrom(670, 490, 90, 90);

    // Gets the walls, as a read-only list.
    public IReadOnlyList<Rectangle> Walls => _walls;

    // Checks whether a point is inside the map and not inside any wall.
    public bool CanStandAt(Point2D position)
    {
        // Must be inside the outer map boundary.
        if (!SplashKit.PointInRectangle(position, MapBounds))
        {
            return false;
        }

        // Must not touch any wall.
        foreach (Rectangle wall in _walls)
        {
            if (SplashKit.PointInRectangle(position, wall))
            {
                return false;
            }
        }

        return true;
    }

    // Checks whether a point is inside the map and not inside any wall.
    public bool CanStandAt(float x, float y)
    {
        return CanStandAt(SplashKit.PointAt(x, y));
    }

    // Checks whether a point is inside the exit area.
    public bool AtExit(Point2D position)
    {
        return SplashKit.PointInRectangle(position, ExitArea);
    }

    // Checks whether a point is inside the exit area.
    public bool AtExit(float x, float y)
    {
        return AtExit(SplashKit.PointAt(x, y));
    }

    // Converts a position into the grid cell (column, row) that contains it.
    public (int x, int y) ToCell(Point2D position)
    {
        int column = Math.Clamp((int)((position.X - MapBounds.X) / CellSize), 0, Columns - 1);
        int row = Math.Clamp((int)((position.Y - MapBounds.Y) / CellSize), 0, Rows - 1);
        return (column, row);
    }

    // Converts a position into the grid cell (column, row) that contains it.
    public (int x, int y) ToCell(float x, float y)
    {
        return ToCell(SplashKit.PointAt(x, y));
    }

    // Converts a grid cell (column, row) into the point at its centre.
    public Point2D CellCentre((int x, int y) cell)
    {
        double centreX = MapBounds.X + (CellSize / 2f) + (cell.x * CellSize);
        double centreY = MapBounds.Y + (CellSize / 2f) + (cell.y * CellSize);
        return SplashKit.PointAt(centreX, centreY);
    }

    // Finds the shortest path between two positions using A* pathfinding.
    public List<(int x, int y)> FindPath(Point2D from, Point2D to)
    {
        (int x, int y) start = ToCell(from);
        (int x, int y) goal = ToCell(to);

        // There is no route if the start or goal is blocked.
        if (!Passable(start) || !Passable(goal))
        {
            return new List<(int x, int y)>();
        }

        // The frontier is explored in order of estimated total cost: cost so far + heuristic.
        PriorityQueue<(int x, int y), int> frontier = new();
        Dictionary<(int x, int y), int> costSoFar = new() { [start] = 0 };
        Dictionary<(int x, int y), (int x, int y)> cameFrom = new();

        frontier.Enqueue(start, 0);

        while (frontier.TryDequeue(out (int x, int y) current, out _))
        {
            // Goal reached: walk back through cameFrom to rebuild the path.
            if (current == goal)
            {
                List<(int x, int y)> path = new() { goal };
                while (path[^1] != start)
                {
                    path.Add(cameFrom[path[^1]]);
                }

                path.Reverse();
                return path;
            }

            // The four neighbouring cells: right, left, down, up.
            (int x, int y)[] neighbours =
            {
                (current.x + 1, current.y),
                (current.x - 1, current.y),
                (current.x, current.y + 1),
                (current.x, current.y - 1)
            };

            foreach ((int x, int y) next in neighbours)
            {
                if (!Passable(next))
                {
                    continue;
                }

                int newCost = costSoFar[current] + 1;

                // TryGetValue looks the cell up once, instead of ContainsKey followed by the indexer.
                if (!costSoFar.TryGetValue(next, out int knownCost) || newCost < knownCost)
                {
                    costSoFar[next] = newCost;
                    cameFrom[next] = current;
                    frontier.Enqueue(next, newCost + Heuristic(next, goal));
                }
            }
        }

        // No route exists.
        return new List<(int x, int y)>();
    }

    // Calculates the shortest path between two positions using A* pathfinding.
    public List<(int x, int y)> FindPath(float fromX, float fromY, float toX, float toY)
    {
        return FindPath(SplashKit.PointAt(fromX, fromY), SplashKit.PointAt(toX, toY));
    }

    // Draws the walls and the exit.
    public void Draw()
    {
        foreach (Rectangle wall in _walls)
        {
            SplashKit.FillRectangle(Color.DarkOrange, wall);
        }

        SplashKit.DrawBitmap(
            _exitImage,
            ExitArea.X,
            ExitArea.Y,
            SplashKit.OptionScaleBmp(
                ExitArea.Width / _exitImage.Width,
                ExitArea.Height / _exitImage.Height
            )
        );
    }

    // Manhattan distance: the A* heuristic. With 4-directional movement it never overestimates,
    // so A* is guaranteed to find a shortest path.
    private static int Heuristic((int x, int y) a, (int x, int y) b)
    {
        return Math.Abs(a.x - b.x) + Math.Abs(a.y - b.y);
    }

    // Checks whether a cell is inside the grid and walkable.
    private bool Passable((int x, int y) cell)
    {
        if (cell.x < 0 || cell.x >= Columns || cell.y < 0 || cell.y >= Rows)
        {
            return false;
        }

        return CanStandAt(CellCentre(cell));
    }
}
