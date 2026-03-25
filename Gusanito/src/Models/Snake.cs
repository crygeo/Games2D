using Gusanito.Enum;
using Gusanito.Helpers;

namespace Gusanito.Models;

public class Snake
{
    
    public Direction CurrentDirection { get; set; }
    public LinkedList<Position> Body { get; private set; }
    public IReadOnlyList<Position> PreviousBody { get; private set; }

    public Position Head => Body.First?.Value ?? throw new InvalidOperationException("Snake has no body");
    public Position PreviousHead => PreviousBody[0];
    
    public Snake()
    {
        Body = new LinkedList<Position>();
        Body.AddFirst(new Position(5, 5)); // posición inicial
        CurrentDirection = Direction.Right;
        PreviousBody = Body.ToList();
    }

    public void Move(bool grow = false)
    {
        PreviousBody = Body.ToList();
        
        var direction =  CurrentDirection.ToVector();
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