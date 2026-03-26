
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Gusanito.Enum;
using Gusanito.Game;
using Gusanito.Interfaz;
using Gusanito.Models;

namespace Gusanito.Rendering;

public sealed class LineRenderer : ISnakeRenderer
{
    private readonly WriteableBitmap _bitmap;
    private readonly int _width;
    private readonly int _height;
    private readonly int _cellSize;
    private readonly int _thickness;

    public WriteableBitmap Bitmap => _bitmap;

    public LineRenderer(int width, int height, int cellSize, int thickness = 10)
    {
        _width     = width;
        _height    = height;
        _cellSize  = cellSize;
        _thickness = thickness;

        _bitmap = new WriteableBitmap(
            width  * cellSize,
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
            int stride    = _bitmap.BackBufferStride;

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
        for (int x = 0; x < _width  * _cellSize; x++)
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
            if (game.Map[x, y] == CellType.Wall)
                DrawSolidCell(buffer, stride, x, y, 120, 120, 120);
            else if (game.Map[x, y] == CellType.Food)
                DrawSolidCell(buffer, stride, x, y, 255, 50, 50);
        }
    }

    private unsafe void DrawSnake(GameEngine game, IntPtr buffer, int stride, float t)
    {
        var current  = game.Snake.Body.ToList();
        var previous = game.Snake.PreviousBody;
        int count    = Math.Min(current.Count, previous.Count);

        // Calcular centros interpolados — cada segmento interpola su propia posición
        var centers = new (float x, float y)[count];

        for (int i = 0; i < count; i++)
        {
            float cx = current[i].X  * _cellSize + _cellSize / 2f;
            float cy = current[i].Y  * _cellSize + _cellSize / 2f;
            float px = previous[i].X * _cellSize + _cellSize / 2f;
            float py = previous[i].Y * _cellSize + _cellSize / 2f;

            centers[i] = (
                px + (cx - px) * t,
                py + (cy - py) * t
            );
        }

        // En lugar de DrawThickLine directo entre centros
        for (int i = 0; i < count - 1; i++)
        {
            // Puntos vecinos para la tangente
            var p0 = centers[Math.Max(i - 1, 0)];
            var p1 = centers[i];
            var p2 = centers[i + 1];
            var p3 = centers[Math.Min(i + 2, count - 1)];

            // Subdividir la curva en pasos
            int steps = 8;
            for (int s = 0; s < steps; s++)
            {
                float t0 = s       / (float)steps;
                float t1 = (s + 1) / (float)steps;

                var from = CatmullRom(p0, p1, p2, p3, t0);
                var to   = CatmullRom(p0, p1, p2, p3, t1);

                DrawThickLine(buffer, stride, from.x, from.y, to.x, to.y, _thickness, 80, 200, 80);
            }
        }

        // Joints
        for (int i = 0; i < count; i++)
            DrawCircle(buffer, stride,
                centers[i].x, centers[i].y,
                _thickness / 2, 80, 200, 80);

        // Cabeza
        if (count > 0)
            DrawCircle(buffer, stride,
                centers[0].x, centers[0].y,
                _thickness / 2 + 2, 100, 230, 100);
    }

    private unsafe void DrawThickLine(
        IntPtr buffer, int stride,
        float x0, float y0, float x1, float y1,
        int thickness, byte r, byte g, byte b)
    {
        float dx  = x1 - x0;
        float dy  = y1 - y0;
        float len = MathF.Sqrt(dx * dx + dy * dy);

        if (len < 0.001f) return;

        // Vector perpendicular normalizado
        float nx = -dy / len;
        float ny =  dx / len;

        int half = thickness / 2;

        // Número de pasos a lo largo de la línea
        int steps = (int)len + 1;

        for (int s = 0; s <= steps; s++)
        {
            float t  = s / (float)steps;
            float cx = x0 + dx * t;
            float cy = y0 + dy * t;

            // Expandir perpendicular
            for (int p = -half; p <= half; p++)
            {
                int px = (int)(cx + nx * p);
                int py = (int)(cy + ny * p);

                SetPixel(buffer, stride, px, py, r, g, b);
            }
        }
    }

    private unsafe void DrawCircle(
        IntPtr buffer, int stride,
        float cx, float cy, int radius,
        byte r, byte g, byte b)
    {
        int r2 = radius * radius;

        for (int dy = -radius; dy <= radius; dy++)
        for (int dx = -radius; dx <= radius; dx++)
        {
            if (dx * dx + dy * dy <= r2)
                SetPixel(buffer, stride, (int)(cx + dx), (int)(cy + dy), r, g, b);
        }
    }

    private unsafe void SetPixel(IntPtr buffer, int stride, int px, int py, byte r, byte g, byte b)
    {
        if (px < 0 || py < 0 || px >= _bitmap.PixelWidth || py >= _bitmap.PixelHeight)
            return;

        int index = py * stride + px * 4;
        *((byte*)buffer + index + 0) = b;
        *((byte*)buffer + index + 1) = g;
        *((byte*)buffer + index + 2) = r;
        *((byte*)buffer + index + 3) = 255;
    }

    private unsafe void DrawSolidCell(IntPtr buffer, int stride, int gridX, int gridY, byte r, byte g, byte b)
    {
        int startX = gridX * _cellSize;
        int startY = gridY * _cellSize;

        for (int y = 0; y < _cellSize; y++)
        for (int x = 0; x < _cellSize; x++)
            SetPixel(buffer, stride, startX + x, startY + y, r, g, b);
    }
    
    private (float x, float y) CatmullRom(
        (float x, float y) p0,
        (float x, float y) p1,
        (float x, float y) p2,
        (float x, float y) p3,
        float t)
    {
        float t2 = t  * t;
        float t3 = t2 * t;

        float x = 0.5f * (
            2 * p1.x +
            (-p0.x + p2.x) * t +
            (2*p0.x - 5*p1.x + 4*p2.x - p3.x) * t2 +
            (-p0.x + 3*p1.x - 3*p2.x + p3.x) * t3);

        float y = 0.5f * (
            2 * p1.y +
            (-p0.y + p2.y) * t +
            (2*p0.y - 5*p1.y + 4*p2.y - p3.y) * t2 +
            (-p0.y + 3*p1.y - 3*p2.y + p3.y) * t3);

        return (x, y);
    }
    
}