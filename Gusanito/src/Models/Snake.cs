using Gusanito.Enum;
using Gusanito.Helpers;

namespace Gusanito.Models;

public class Snake
{
    
    public Direction CurrentDirection { get; set; }
    public LinkedList<Position> Body { get; private set; }
    public IReadOnlyList<Position> PreviousBody { get; set; }
    public bool JustRespawned { get; set; }

    public Position Head => Body.First?.Value ?? throw new InvalidOperationException("Snake has no body");
    public Position PreviousHead => PreviousBody[0];
    
    public Snake(int x, int y)
    {
        Body = new LinkedList<Position>();
        
        Body.AddFirst(new Position(x, y));
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
        return GetNextHeadPosition(CurrentDirection);
    }
    
    public Position GetNextHeadPosition(Direction dir)
    {
        var direction = CurrentDirection.ToVector();

        return new Position(
            Head.X + direction.X,
            Head.Y + direction.Y
        );
    }
    
    public Snake Clone()
    {
        return new Snake(Head.X, Head.Y)
        {
            Body = new LinkedList<Position>( this.Body.Select(p => new Position(p.X, p.Y)).ToList()),
            CurrentDirection = this.CurrentDirection
        };
    }
}