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
        Body             = new LinkedList<Position>();
        Body.AddFirst(new Position(x, y));
        CurrentDirection = Direction.Right;
        PreviousBody     = Body.ToList();
    }

    public Snake(bool skipInit)
    {
    }

    public void Move(bool grow = false)
    {
        PreviousBody = Body.ToList();

        var direction = CurrentDirection.ToVector();
        var newHead   = new Position(
            Head.X + direction.X,
            Head.Y + direction.Y);

        Body.AddFirst(newHead);

        if (!grow)
            Body.RemoveLast();
    }

    public Position GetNextHeadPosition() => GetNextHeadPosition(CurrentDirection);

    // ── BUG FIX: usaba CurrentDirection.ToVector() ignorando el parámetro ──
    public Position GetNextHeadPosition(Direction dir)
    {
        var direction = dir.ToVector();                    // ← era CurrentDirection.ToVector()
        return new Position(
            Head.X + direction.X,
            Head.Y + direction.Y);
    }

    public Snake Clone()
    {
        // Construimos manualmente sin pasar por el constructor público
        // para no ejecutar lógica de inicialización innecesaria.
        var clone = new Snake(skipInit: true)
        {
            // ── BUG FIX: copia completa del body (antes era correcto, pero lo dejamos explícito)
            Body = new LinkedList<Position>(Body.Select(p => new Position(p.X, p.Y))),
            
            CurrentDirection = CurrentDirection,

            // ── BUG FIX: PreviousBody no se copiaba → interpolación rota en el snapshot
            PreviousBody = PreviousBody,

            // ── BUG FIX: JustRespawned no se copiaba → podía suprimir interpolación al arrancar
            JustRespawned = JustRespawned,
        };

        return clone;
    }
}