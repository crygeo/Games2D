using Gusanito.Enum;
using Gusanito.Game;
using Gusanito.Models;

namespace Gusanito.SAI;

public static class AStar
{
    public static List<Position> FindPath(GameEngine game, Position start, Position goal)
    {
        var open = new PriorityQueue<Position, int>();
        var cameFrom = new Dictionary<Position, Position>();
        var cost = new Dictionary<Position, int>();

        open.Enqueue(start, 0);
        cost[start] = 0;

        while (open.Count > 0)
        {
            var current = open.Dequeue();

            if (current.Equals(goal))
                break;

            foreach (var next in GetNeighbors(current))
            {
                if (!IsWalkable(game, next))
                    continue;

                int newCost = cost[current] + 1;

                if (!cost.ContainsKey(next) || newCost < cost[next])
                {
                    cost[next] = newCost;
                    int priority = newCost + Heuristic(goal, next);
                    open.Enqueue(next, priority);
                    cameFrom[next] = current;
                }
            }
        }

        return ReconstructPath(cameFrom, start, goal);
    }
    
    static int Heuristic(Position a, Position b)
        => Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y);

    public static bool IsWalkable(GameEngine game, Position pos)
    {
        // 🔴 1. Validar límites PRIMERO
        if (pos.X < 0 || pos.Y < 0 ||
            pos.X >= game.Width || pos.Y >= game.Height)
            return false;

        // 🔴 2. Luego mapa
        if (game.Map[pos.X, pos.Y] == CellType.Wall)
            return false;

        // 🔴 3. Luego cuerpo
        if (game.Snake.Body.Contains(pos))
            return false;

        return true;
    }
    
    private static readonly Position[] Directions =
    {
        new Position(1, 0),   // Right
        new Position(-1, 0),  // Left
        new Position(0, 1),   // Down
        new Position(0, -1),  // Up
    };
    
    static IEnumerable<Position> GetNeighbors(Position pos)
    {
        foreach (var dir in Directions)
        {
            yield return new Position(pos.X + dir.X, pos.Y + dir.Y);
        }
    }
    
    static List<Position> ReconstructPath(
        Dictionary<Position, Position> cameFrom,
        Position start,
        Position goal)
    {
        var path = new List<Position>();

        if (!cameFrom.ContainsKey(goal))
            return path; // no hay camino

        var current = goal;
        path.Add(current);

        while (!current.Equals(start))
        {
            current = cameFrom[current];
            path.Add(current);
        }

        path.Reverse();
        return path;
    }
}