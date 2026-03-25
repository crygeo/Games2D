using Gusanito.Enum;
using Gusanito.Models;

namespace Gusanito.Helpers;

public static class DirectionHelper
{
    public static bool IsOpposite(Direction a, Direction b)
    {
        var va = ToVector(a);
        var vb = ToVector(b);

        return va.X == -vb.X && va.Y == -vb.Y;
    }
    
    public static Position ToVector(this Direction d) => d switch
    {
        Direction.Up => new Position(0, -1),
        Direction.Down => new Position(0, 1),
        Direction.Left => new Position(-1, 0),
        Direction.Right => new Position(1, 0),
        _ => new Position(0, 0)
    };
}