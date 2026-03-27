using Gusanito.Enum;
using Gusanito.Game;
using Gusanito.Helpers;
using Gusanito.Interfaz;
using Gusanito.Models;

namespace Gusanito.SAI;

/// <summary>
/// Snake AI based on a precomputed Hamiltonian cycle with intelligent shortcuts.
///
/// Strategy:
///   1. At game start, compute a Hamiltonian cycle covering all walkable cells.
///      If the DFS cycle fails (large board timeout), fall back to the structured boustrophedon path.
///   2. Each tick, follow the next node in the cycle.
///   3. Evaluate adjacent cells: if any is strictly ahead AND passes the Flood Fill safety check,
///      take the shortcut to reach food faster.
///
/// This class is pure logic — it does not touch the UI thread.
/// It is designed to be called from AsyncAIRunner on a background Task.
/// </summary>
public sealed class HamiltonianAI : ISnakeAI
{
    private IReadOnlyList<Position> _cycle = Array.Empty<Position>();
    private Dictionary<Position, int> _cycleOrder = new();
    private readonly ShortcutEvaluator _shortcutEvaluator;
    private bool _cycleReady;
    private readonly TimeSpan _buildTimeout;

    public HamiltonianAI(
        ShortcutEvaluator? shortcutEvaluator = null,
        TimeSpan buildTimeout = default)
    {
        _shortcutEvaluator = shortcutEvaluator ?? new ShortcutEvaluator();
        _buildTimeout      = buildTimeout == default ? TimeSpan.FromSeconds(5) : buildTimeout;
    }

    /// <summary>
    /// Call once after NewGame() to rebuild the cycle for the current board.
    /// Safe to call from background thread.
    /// </summary>
    public void RebuildCycle(GameEngine game)
    {
        _cycleReady = false;
        _cycle      = Array.Empty<Position>();
        _cycleOrder = new Dictionary<Position, int>();

        var start = game.Snake.Head;

        IReadOnlyList<Position> cycle = Array.Empty<Position>();

        using var cts = new CancellationTokenSource(_buildTimeout);
        var task = Task.Run(
            () => HamiltonianPathBuilder.Build(game.Map, game.Width, game.Height, start),
            cts.Token);

        try
        {
            cycle = task.GetAwaiter().GetResult();
        }
        catch (OperationCanceledException) { /* timeout — use fallback */ }

        if (cycle.Count == 0)
            cycle = HamiltonianPathBuilder.BuildStructured(game.Map, game.Width, game.Height);

        _cycle      = cycle;
        _cycleOrder = BuildOrderMap(_cycle);
        _cycleReady = true;
    }

    /// <inheritdoc />
    public Direction GetNextMove(GameEngine game)
    {
        if (!_cycleReady || _cycle.Count == 0)
            return game.Snake.CurrentDirection;

        var head = game.Snake.Head;

        if (!_cycleOrder.TryGetValue(head, out int currentIndex))
            return GetSafeDirection(game);

        int cycleLength = _cycle.Count;
        int nextIndex   = (currentIndex + 1) % cycleLength;
        var nextPos     = _cycle[nextIndex];

        // Evaluate shortcuts: adjacent cells ahead in the cycle
        foreach (var candidate in GetAdjacent(head, game.Width, game.Height))
        {
            if (candidate == nextPos)
                continue; // normal next step, not a shortcut

            if (_shortcutEvaluator.IsSafeShortcut(game, _cycleOrder, currentIndex, candidate, cycleLength))
                return DirectionBetween(head, candidate);
        }

        return DirectionBetween(head, nextPos);
    }

    // ─────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────

    private static Dictionary<Position, int> BuildOrderMap(IReadOnlyList<Position> cycle)
    {
        var map = new Dictionary<Position, int>(cycle.Count);
        for (int i = 0; i < cycle.Count; i++)
            map[cycle[i]] = i;
        return map;
    }

    private Direction GetSafeDirection(GameEngine game)
    {
        foreach (var dir in AllDirections)
        {
            if (DirectionHelper.IsOpposite(game.Snake.CurrentDirection, dir))
                continue;

            var next = ApplyDirection(game.Snake.Head, dir);

            if (next.X < 0 || next.Y < 0 || next.X >= game.Width || next.Y >= game.Height)
                continue;

            if (game.Map[next.X, next.Y] == CellType.Wall)
                continue;

            if (game.Snake.Body.Contains(next))
                continue;

            return dir;
        }

        return game.Snake.CurrentDirection;
    }

    private static Direction DirectionBetween(Position from, Position to) =>
        (to.X - from.X, to.Y - from.Y) switch
        {
            ( 1,  0) => Direction.Right,
            (-1,  0) => Direction.Left,
            ( 0,  1) => Direction.Down,
            ( 0, -1) => Direction.Up,
            _        => Direction.Right
        };

    private static readonly (int dx, int dy)[] NeighborOffsets =
        { (1, 0), (-1, 0), (0, 1), (0, -1) };

    private static readonly Direction[] AllDirections =
        { Direction.Up, Direction.Down, Direction.Left, Direction.Right };

    private static IEnumerable<Position> GetAdjacent(Position pos, int width, int height)
    {
        foreach (var (dx, dy) in NeighborOffsets)
        {
            int nx = pos.X + dx;
            int ny = pos.Y + dy;
            if (nx >= 0 && ny >= 0 && nx < width && ny < height)
                yield return new Position(nx, ny);
        }
    }

    private static Position ApplyDirection(Position pos, Direction dir) => dir switch
    {
        Direction.Up    => new Position(pos.X,     pos.Y - 1),
        Direction.Down  => new Position(pos.X,     pos.Y + 1),
        Direction.Left  => new Position(pos.X - 1, pos.Y),
        Direction.Right => new Position(pos.X + 1, pos.Y),
        _               => pos
    };
}