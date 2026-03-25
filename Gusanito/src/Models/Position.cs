namespace Gusanito.Models;

public struct Position
{
    public int X { get; set; }
    public int Y { get; set; }

    public Position(int x, int y) { X = x; Y = y; }

    public static bool operator ==(Position a, Position b) => a.X == b.X && a.Y == b.Y;
    public static bool operator !=(Position a, Position b) => !(a == b);
    public override bool Equals(object? obj) => obj is Position p && this == p;
    public override int GetHashCode() => HashCode.Combine(X, Y);
}