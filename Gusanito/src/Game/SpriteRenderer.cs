using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Gusanito.Enum;
using Gusanito.Game;
using Gusanito.Interfaz;
using Gusanito.Models;

namespace Gusanito.Rendering;

public sealed class SpriteRenderer : ISnakeRenderer
{
    private readonly WriteableBitmap _bitmap;
    private readonly int _width;
    private readonly int _height;
    private readonly int _cellSize;
    private readonly SnakeTileMapper _tileMapper;
    private readonly bool _interpolated;
    private readonly int _tileSize;


    public WriteableBitmap Bitmap => _bitmap;

    public SpriteRenderer(int width, int height, int cellSize, int tileSize, SnakeTileMapper tileMapper,
        bool interpolated = true)
    {
        ArgumentNullException.ThrowIfNull(tileMapper);

        _tileSize = tileSize;
        _width = width;
        _height = height;
        _cellSize = cellSize;
        _tileMapper = tileMapper;
        _interpolated = interpolated;

        _bitmap = new WriteableBitmap(
            width * cellSize,
            height * cellSize,
            96, 96,
            PixelFormats.Bgra32,
            null);
    }

    public void Draw(GameEngine game, float tick)
    {
        _bitmap.Lock();

        unsafe
        {
            IntPtr buffer = _bitmap.BackBuffer;
            int stride = _bitmap.BackBufferStride;

            Clear(buffer, stride);
            DrawMap(game, buffer, stride);
            DrawSnake(game, buffer, stride, tick);
        }

        _bitmap.AddDirtyRect(new Int32Rect(0, 0, _bitmap.PixelWidth, _bitmap.PixelHeight));
        _bitmap.Unlock();
    }

    private unsafe void Clear(IntPtr buffer, int stride)
    {
        for (int y = 0; y < _height * _cellSize; y++)
        for (int x = 0; x < _width * _cellSize; x++)
        {
            int index = y * stride + x * 4;
            *((byte*)buffer + index + 0) = 0;
            *((byte*)buffer + index + 1) = 0;
            *((byte*)buffer + index + 2) = 0;
            *((byte*)buffer + index + 3) = 255;
        }
    }

    private unsafe void DrawMap(GameEngine game, IntPtr buffer, int stride)
    {
        for (int x = 0; x < _width; x++)
        for (int y = 0; y < _height; y++)
        {
            if (game.Map[x, y] == CellType.Food)
            {
                var pixels = _tileMapper.GetPixels(SnakeTile.Food);
                DrawTile(buffer, stride, x, y, pixels);
            }
            else if (game.Map[x, y] == CellType.Wall)
            {
                DrawSolidCell(buffer, stride, x, y, 120, 120, 120);
            }
        }
    }

    private unsafe void DrawSnake(GameEngine game, IntPtr buffer, int stride, float t)
    {
        var current = game.Snake.Body.ToList();
        var previous = game.Snake.PreviousBody;

        var segments = SnakeTileResolver.ResolveAll(current, previous, game.Snake.CurrentDirection);
        int count = segments.Count;

        for (int i = 0; i < count; i++)
        {
            var (_, tile) = segments[i];
            byte[] pixels = _tileMapper.GetPixels(tile);

            
            if (_interpolated)
            {
                float interpX = previous[i].X + (current[i].X - previous[i].X) * t;
                float interpY = previous[i].Y + (current[i].Y - previous[i].Y) * t;
                DrawTileInterpolated(buffer, stride, interpX, interpY, pixels);
            }
            else
            {
                DrawTile(buffer, stride, current[i].X, current[i].Y, pixels);
            }
        }
    }

    private unsafe void DrawTile(IntPtr buffer, int stride, int gridX, int gridY, byte[] tilePixels)
    {
        int startX = gridX * _cellSize;
        int startY = gridY * _cellSize;
        int tileStride = _tileSize * 4; // 👈 tamaño original del tile, no _cellSize

        for (int y = 0; y < _cellSize; y++)
        for (int x = 0; x < _cellSize; x++)
        {
            // Mapear píxel de destino (_cellSize) al píxel fuente (_tileSize)
            int srcX = x * _tileSize / _cellSize;
            int srcY = y * _tileSize / _cellSize;

            int px = startX + x;
            int py = startY + y;

            if (px < 0 || py < 0 || px >= _bitmap.PixelWidth || py >= _bitmap.PixelHeight)
                continue;

            int srcIndex = srcY * tileStride + srcX * 4;
            byte srcA = tilePixels[srcIndex + 3];

            if (srcA == 0) continue;

            int dstIndex = py * stride + px * 4;

            if (srcA == 255)
            {
                *((byte*)buffer + dstIndex + 0) = tilePixels[srcIndex + 0];
                *((byte*)buffer + dstIndex + 1) = tilePixels[srcIndex + 1];
                *((byte*)buffer + dstIndex + 2) = tilePixels[srcIndex + 2];
                *((byte*)buffer + dstIndex + 3) = 255;
            }
            else
            {
                float a = srcA / 255f;
                float ia = 1f - a;

                *((byte*)buffer + dstIndex + 0) =
                    (byte)(tilePixels[srcIndex + 0] * a + *((byte*)buffer + dstIndex + 0) * ia);
                *((byte*)buffer + dstIndex + 1) =
                    (byte)(tilePixels[srcIndex + 1] * a + *((byte*)buffer + dstIndex + 1) * ia);
                *((byte*)buffer + dstIndex + 2) =
                    (byte)(tilePixels[srcIndex + 2] * a + *((byte*)buffer + dstIndex + 2) * ia);
                *((byte*)buffer + dstIndex + 3) = 255;
            }
        }
    }

    private unsafe void DrawTileInterpolated(IntPtr buffer, int stride, float gridX, float gridY, byte[] tilePixels)
    {
        int startX = (int)(gridX * _cellSize);
        int startY = (int)(gridY * _cellSize);
        int tileStride = _tileSize * 4;

        for (int y = 0; y < _cellSize; y++)
        for (int x = 0; x < _cellSize; x++)
        {
            int srcX = x * _tileSize / _cellSize;
            int srcY = y * _tileSize / _cellSize;

            int px = startX + x;
            int py = startY + y;

            if (px < 0 || py < 0 || px >= _bitmap.PixelWidth || py >= _bitmap.PixelHeight)
                continue;

            int srcIndex = srcY * tileStride + srcX * 4;
            byte srcA = tilePixels[srcIndex + 3];

            if (srcA == 0) continue;

            int dstIndex = py * stride + px * 4;

            if (srcA == 255)
            {
                *((byte*)buffer + dstIndex + 0) = tilePixels[srcIndex + 0];
                *((byte*)buffer + dstIndex + 1) = tilePixels[srcIndex + 1];
                *((byte*)buffer + dstIndex + 2) = tilePixels[srcIndex + 2];
                *((byte*)buffer + dstIndex + 3) = 255;
            }
            else
            {
                float a = srcA / 255f;
                float ia = 1f - a;

                *((byte*)buffer + dstIndex + 0) =
                    (byte)(tilePixels[srcIndex + 0] * a + *((byte*)buffer + dstIndex + 0) * ia);
                *((byte*)buffer + dstIndex + 1) =
                    (byte)(tilePixels[srcIndex + 1] * a + *((byte*)buffer + dstIndex + 1) * ia);
                *((byte*)buffer + dstIndex + 2) =
                    (byte)(tilePixels[srcIndex + 2] * a + *((byte*)buffer + dstIndex + 2) * ia);
                *((byte*)buffer + dstIndex + 3) = 255;
            }
        }
    }

    private unsafe void DrawSolidCell(IntPtr buffer, int stride, int gridX, int gridY, byte r, byte g, byte b)
    {
        int startX = gridX * _cellSize;
        int startY = gridY * _cellSize;

        for (int y = 0; y < _cellSize; y++)
        for (int x = 0; x < _cellSize; x++)
        {
            int index = (startY + y) * stride + (startX + x) * 4;
            *((byte*)buffer + index + 0) = b;
            *((byte*)buffer + index + 1) = g;
            *((byte*)buffer + index + 2) = r;
            *((byte*)buffer + index + 3) = 255;
        }
    }
    
    private static bool IsCurve(SnakeTile tile) => tile switch
    {
        SnakeTile.CurveRightDown => true,
        SnakeTile.CurveDownLeft  => true,
        SnakeTile.CurveUpRight   => true,
        SnakeTile.CurveLeftUp    => true,
        _ => false
    };
}