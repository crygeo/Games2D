using Gusanito.Enum;
using Gusanito.Models;

namespace Gusanito.SAI;

/// <summary>
/// Measures the number of cells reachable from a given position using BFS flood fill.
/// Pure algorithm — no dependency on GameEngine, only on the map snapshot and occupied set.
/// This allows it to be used both for real-time evaluation and for simulated futures.
/// </summary>
public static class FloodFill
{
    /// <summary>
    /// Returns the count of empty cells reachable from <paramref name="start"/>.
    /// </summary>
    /// <param name="map">Current cell type map.</param>
    /// <param name="occupied">Set of positions occupied by the snake body (excluding tail if it will move).</param>
    /// <param name="width">Map width.</param>
    /// <param name="height">Map height.</param>
    /// <param name="start">Starting position for the fill.</param>
    /// <returns>Number of reachable empty cells.</returns>
    public static int CountReachable(
        CellType[,] map,
        HashSet<Position> occupied,
        int width,
        int height,
        Position start)
    {
        if (!IsTraversable(map, occupied, width, height, start))
            return 0;

        // Use a flat boolean array instead of HashSet<Position> for visited —
        // avoids boxing/hashing overhead on a hot path called every AI tick.
        var visited = new bool[width, height];
        var queue   = new Queue<Position>();

        queue.Enqueue(start);
        visited[start.X, start.Y] = true;

        int count = 0;

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            count++;

            foreach (var neighbor in GetNeighbors(current, width, height))
            {
                if (visited[neighbor.X, neighbor.Y])
                    continue;

                if (!IsTraversable(map, occupied, width, height, neighbor))
                    continue;

                visited[neighbor.X, neighbor.Y] = true;
                queue.Enqueue(neighbor);
            }
        }

        return count;
    }

    /// <summary>
    /// Returns the ratio of reachable cells to total available cells.
    /// Useful for normalizing the safety threshold regardless of board size.
    /// </summary>
    public static float ReachableRatio(
        CellType[,] map,
        HashSet<Position> occupied,
        int width,
        int height,
        Position start)
    {
        int total     = (width - 2) * (height - 2); // exclude walls
        int reachable = CountReachable(map, occupied, width, height, start);

        return total == 0 ? 0f : reachable / (float)total;
    }

    private static bool IsTraversable(
        CellType[,] map,
        HashSet<Position> occupied,
        int width,
        int height,
        Position pos)
    {
        if (pos.X < 0 || pos.Y < 0 || pos.X >= width || pos.Y >= height)
            return false;

        if (map[pos.X, pos.Y] == CellType.Wall)
            return false;

        if (occupied.Contains(pos))
            return false;

        return true;
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
}