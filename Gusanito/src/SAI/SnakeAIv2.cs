using Gusanito.Enum;
using Gusanito.Game;
using Gusanito.Interfaz;
using Gusanito.Models;

namespace Gusanito.SAI;

public class SnakeAIv2 : ISnakeAI
{
    public Direction GetNextMove(GameEngine game)
    {
        var start = game.Snake.Head;
        var food = game.Food;

        var pathToFood = AStar.FindPath(game, start, food);

        // 🧠 intentar comida
        if (pathToFood.Count > 1)
        {
            var clone = game.Clone();

            Simulate(clone, pathToFood);

            if (!clone.IsGameOver && IsSafe(clone))
            {
                return GetDirection(start, pathToFood[1]);
            }
        }

        // 🔁 fallback: seguir cola
        var tail = game.Snake.Body.Last();
        var pathToTail = AStar.FindPath(game, start, tail);

        if (pathToTail.Count > 1)
        {
            return GetDirection(start, pathToTail[1]);
        }

        // 💀 fallback final (evitar morir inmediato)
        return GetSafeRandomMove(game);
    }

    private Direction GetDirection(Position from, Position to)
    {
        if (to.X > from.X) return Direction.Right;
        if (to.X < from.X) return Direction.Left;
        if (to.Y > from.Y) return Direction.Down;
        return Direction.Up;
    }
    
    void Simulate(GameEngine game, List<Position> path)
    {
        for (int i = 1; i < path.Count; i++)
        {
            var from = game.Snake.Head;
            var to = path[i];

            var dir = GetDirection(from, to);

            game.EnqueueDirection(dir);
            game.Update();

            if (game.IsGameOver)
                break;
        }
    }
    
    bool IsSafe(GameEngine game)
    {
        var tail = game.Snake.Body.Last();

        var pathToTail = AStar.FindPath(game, game.Snake.Head, tail);

        return pathToTail.Count > 0;
    }
    
    Direction GetSafeRandomMove(GameEngine game)
    {
        var dirs = new[] { Direction.Up, Direction.Down, Direction.Left, Direction.Right };

        foreach (var dir in dirs)
        {
            var next = game.Snake.GetNextHeadPosition(dir);

            if (AStar.IsWalkable(game, next))
                return dir;
        }

        return game.Snake.CurrentDirection;
    }
}