using Gusanito.Enum;
using Gusanito.Game;
using Gusanito.Interfaz;
using Gusanito.Models;

namespace Gusanito.SAI;

public class SnakeAI : ISnakeAI
{
    public Direction GetNextMove(GameEngine game)
    {
        var start = game.Snake.Head;
        var target = game.Food;

        var path = AStar.FindPath(game, start, target);

        if (path.Count < 2)
            return game.Snake.CurrentDirection;

        var next = path[1];

        return GetDirection(start, next);
    }

    private Direction GetDirection(Position from, Position to)
    {
        if (to.X > from.X) return Direction.Right;
        if (to.X < from.X) return Direction.Left;
        if (to.Y > from.Y) return Direction.Down;
        return Direction.Up;
    }
}