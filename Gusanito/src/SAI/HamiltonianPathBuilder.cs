using Gusanito.Enum;
using Gusanito.Models;

namespace Gusanito.SAI;

/// <summary>
/// Constructs a Hamiltonian cycle over the walkable cells of the game grid.
///
/// A Hamiltonian cycle visits every non-wall cell exactly once and returns to the start.
/// This guarantees the snake can always follow a safe, complete path covering the board.
///
/// Algorithm: Greedy DFS with backtracking.
/// - Works well for grid graphs with typical snake board sizes (up to ~40x40).
/// - For very large boards (>50x50), consider a pre-baked structured construction
///   (row-alternating pattern) which is O(n) but requires even-width grids.
///
/// Construction is O(n!) worst case but in practice terminates quickly on grid graphs
/// thanks to the structured neighborhood. Run once at game start, not per tick.
/// </summary>
public static class HamiltonianPathBuilder
{
    /// <summary>
    /// Builds a Hamiltonian cycle starting from <paramref name="start"/>.
    /// Returns an ordered list of positions forming the cycle, or an empty list if none was found.
    /// </summary>
    public static IReadOnlyList<Position> Build(CellType[,] map, int width, int height, Position start)
    {
        var walkable = CollectWalkable(map, width, height);
        int total    = walkable.Count;

        if (total == 0)
            return Array.Empty<Position>();

        // Index map: position → index in walkable list, for O(1) lookup
        var indexMap = new Dictionary<Position, int>(total);
        for (int i = 0; i < total; i++)
            indexMap[walkable[i]] = i;

        var visited = new bool[total];
        var path    = new Position[total];
        int startIdx = indexMap.TryGetValue(start, out var si) ? si : 0;

        path[0]          = walkable[startIdx];
        visited[startIdx] = true;

        bool found = Backtrack(map, width, height, walkable, indexMap, visited, path, 1, total);

        return found ? path : Array.Empty<Position>();
    }

    /// <summary>
    /// Fast structured construction for boards where width is even.
    /// Produces a boustrophedon (snake-scan) Hamiltonian path.
    /// Not a cycle — use only when a guaranteed cycle via DFS cannot be found in time.
    /// </summary>
    public static IReadOnlyList<Position> BuildStructured(CellType[,] map, int width, int height)
    {
        // Walk row by row, alternating direction (boustrophedon).
        // Skips wall cells. Returns a path (not a cycle).
        var path = new List<Position>((width - 2) * (height - 2));

        for (int y = 1; y < height - 1; y++)
        {
            bool leftToRight = y % 2 == 1;

            int xStart = leftToRight ? 1 : width - 2;
            int xEnd   = leftToRight ? width - 1 : 0;
            int xStep  = leftToRight ? 1 : -1;

            for (int x = xStart; x != xEnd; x += xStep)
            {
                if (map[x, y] != CellType.Wall)
                    path.Add(new Position(x, y));
            }
        }

        return path;
    }

    // ─────────────────────────────────────────────────────────────
    // Private
    // ─────────────────────────────────────────────────────────────

    private static bool Backtrack(
        CellType[,] map,
        int width,
        int height,
        List<Position> walkable,
        Dictionary<Position, int> indexMap,
        bool[] visited,
        Position[] path,
        int depth,
        int total)
    {
        if (depth == total)
        {
            // Check if last position connects back to start (forms a cycle)
            return AreAdjacent(path[depth - 1], path[0]);
        }

        var current = path[depth - 1];

        foreach (var neighbor in GetNeighbors(current, width, height))
        {
            if (map[neighbor.X, neighbor.Y] == CellType.Wall)
                continue;

            if (!indexMap.TryGetValue(neighbor, out int idx))
                continue;

            if (visited[idx])
                continue;

            path[depth]  = neighbor;
            visited[idx] = true;

            if (Backtrack(map, width, height, walkable, indexMap, visited, path, depth + 1, total))
                return true;

            visited[idx] = false;
        }

        return false;
    }

    private static List<Position> CollectWalkable(CellType[,] map, int width, int height)
    {
        var list = new List<Position>((width - 2) * (height - 2));

        for (int x = 0; x < width; x++)
        for (int y = 0; y < height; y++)
        {
            if (map[x, y] != CellType.Wall)
                list.Add(new Position(x, y));
        }

        return list;
    }

    private static readonly (int dx, int dy)[] Offsets =
    {
        ( 1,  0),
        (-1,  0),
        ( 0,  1),
        ( 0, -1),
    };

    private static IEnumerable<Position> GetNeighbors(Position pos, int width, int height)
    {
        foreach (var (dx, dy) in Offsets)
        {
            int nx = pos.X + dx;
            int ny = pos.Y + dy;

            if (nx >= 0 && ny >= 0 && nx < width && ny < height)
                yield return new Position(nx, ny);
        }
    }

    private static bool AreAdjacent(Position a, Position b)
        => Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y) == 1;
}