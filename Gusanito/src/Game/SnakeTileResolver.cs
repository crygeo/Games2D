
using Gusanito.Enum;
using Gusanito.Models;

namespace Gusanito.Game;

public static class SnakeTileResolver
{
    public static SnakeTile ResolveHead(Direction direction) => direction switch
    {
        Direction.Up    => SnakeTile.HeadUp,
        Direction.Down  => SnakeTile.HeadDown,
        Direction.Left  => SnakeTile.HeadLeft,
        Direction.Right => SnakeTile.HeadRight,
        _ => throw new ArgumentOutOfRangeException(nameof(direction))
    };

    public static SnakeTile ResolveTail(Direction direction) => direction switch
    {
        Direction.Up    => SnakeTile.TailUp,
        Direction.Down  => SnakeTile.TailDown,
        Direction.Left  => SnakeTile.TailLeft,
        Direction.Right => SnakeTile.TailRight,
        _ => throw new ArgumentOutOfRangeException(nameof(direction))
    };

    public static SnakeTile ResolveBody(Direction from, Direction to) => (from, to) switch
    {
        (Direction.Right, Direction.Right) => SnakeTile.BodyHorizontal,
        (Direction.Left,  Direction.Left)  => SnakeTile.BodyHorizontal,
        (Direction.Up,    Direction.Up)    => SnakeTile.BodyVertical,
        (Direction.Down,  Direction.Down)  => SnakeTile.BodyVertical,

        (Direction.Right, Direction.Down)  => SnakeTile.CurveRightDown,
        (Direction.Up,    Direction.Left)  => SnakeTile.CurveRightDown,

        (Direction.Left,  Direction.Down)  => SnakeTile.CurveDownLeft,
        (Direction.Up,    Direction.Right) => SnakeTile.CurveDownLeft,

        (Direction.Down,  Direction.Right) => SnakeTile.CurveUpRight,
        (Direction.Left,  Direction.Up)    => SnakeTile.CurveUpRight,

        (Direction.Right, Direction.Up)    => SnakeTile.CurveLeftUp,
        (Direction.Down,  Direction.Left)  => SnakeTile.CurveLeftUp,

        _ => throw new ArgumentException($"Invalid combination: {from} → {to}")
    };

    /// <summary>
    /// Calcula la dirección desde posición 'from' hacia posición 'to'.
    /// </summary>
    public static Direction GetDirection(Position from, Position to)
    {
        int dx = to.X - from.X;
        int dy = to.Y - from.Y;

        return (dx, dy) switch
        {
            (1,  0) => Direction.Right,
            (-1, 0) => Direction.Left,
            (0,  1) => Direction.Down,
            (0, -1) => Direction.Up,
            _ => throw new ArgumentException($"Positions are not adjacent: {from} → {to}")
        };
    }

    /// <summary>
    /// Resuelve el tile correcto para cada segmento dado el body completo.
    /// Devuelve una lista de (Position, SnakeTile) lista para renderizar.
    /// </summary>
    public static IReadOnlyList<(Position position, SnakeTile tile)> ResolveAll(
        IReadOnlyList<Position> current,
        IReadOnlyList<Position> previous,
        Direction currentDirection) // 👈 agregar parámetro
    {
        int count = Math.Min(current.Count, previous.Count);
        var result = new List<(Position, SnakeTile)>(count);

        for (int i = 0; i < count; i++)
        {
            SnakeTile tile;

            if (i == 0)
            {
                // Si no se ha movido aún, usar la dirección actual
                var dir = current[i] == previous[i]
                    ? currentDirection
                    : GetDirection(previous[i], current[i]);

                tile = ResolveHead(dir);
            }
            else if (i == count - 1)
            {
                var dir = current[i] == previous[i]
                    ? currentDirection
                    : GetDirection(current[i], current[i - 1]);

                tile = ResolveTail(dir);
            }
            else
            {
                var from = GetDirection(current[i + 1], current[i]);
                var to   = GetDirection(current[i],     current[i - 1]);
                tile = ResolveBody(from, to);
            }

            result.Add((current[i], tile));
        }

        return result;
    }
}
