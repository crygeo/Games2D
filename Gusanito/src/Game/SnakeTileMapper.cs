using Gusanito.Helpers;

namespace Gusanito.Game;

using Gusanito.Enum;


public sealed class SnakeTileMapper
{
    private static readonly IReadOnlyDictionary<SnakeTile, (int col, int row)> Coords =
        new Dictionary<SnakeTile, (int, int)>
        {
            { SnakeTile.CurveRightDown,  (2, 0) },
            { SnakeTile.CurveDownLeft,   (0, 0) },
            { SnakeTile.CurveUpRight,    (0, 1) },//
            { SnakeTile.CurveLeftUp,     (2, 2) },//

            { SnakeTile.BodyHorizontal,  (1, 0) },
            { SnakeTile.BodyVertical,    (2, 1) },

            { SnakeTile.HeadRight,       (4, 0) },
            { SnakeTile.HeadUp,          (3, 0) },
            { SnakeTile.HeadLeft,        (3, 1) },
            { SnakeTile.HeadDown,        (4, 1) },

            { SnakeTile.TailRight,       (4, 2) },
            { SnakeTile.TailUp,          (3, 2) },
            { SnakeTile.TailLeft,        (3, 3) },
            { SnakeTile.TailDown,        (4, 3) },

            { SnakeTile.Food,            (0, 3) },
        };

    private readonly Tilemap _tilemap;

    public SnakeTileMapper(Tilemap tilemap)
    {
        ArgumentNullException.ThrowIfNull(tilemap);
        _tilemap = tilemap;
    }

    public byte[] GetPixels(SnakeTile tile)
    {
        if (!Coords.TryGetValue(tile, out var coord))
            throw new ArgumentOutOfRangeException(nameof(tile), $"Tile {tile} has no mapped coordinates.");

        return _tilemap.GetTilePixels(coord.col, coord.row);
    }
    
    
}