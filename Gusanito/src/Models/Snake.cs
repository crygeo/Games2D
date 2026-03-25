using Gusanito.Enum;
using Gusanito.Helpers;

namespace Gusanito.Models;

public class Snake
{
    
    public Direction CurrentDirection { get; set; }
    public LinkedList<Position> Body { get; set; }

    public Position Head => Body.First?.Value ?? throw new InvalidOperationException("Snake has no body");
    public Position PreviousHead { get; private set; }
    
    public Snake()
    {
        Body = new LinkedList<Position>();
        Body.AddFirst(new Position(5, 5)); // posición inicial
        CurrentDirection = Direction.Right;
    }

    public void Move(bool grow = false)
    {
        var direction =  CurrentDirection.ToVector();

        PreviousHead = Head; // 👈 guardar antes de mover

        var newHead = new Position(
            Head.X + direction.X,
            Head.Y + direction.Y
        );

        Body.AddFirst(newHead);

        if (!grow)
            Body.RemoveLast();
    }
    
    public Position GetNextHeadPosition()
    {
        var direction = CurrentDirection.ToVector();

        return new Position(
            Head.X + direction.X,
            Head.Y + direction.Y
        );
    }
}