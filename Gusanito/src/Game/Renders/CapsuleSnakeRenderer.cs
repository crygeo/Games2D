using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Gusanito.Enum;
using Gusanito.Interfaz;

namespace Gusanito.Game.Renders;

public sealed class CapsuleSnakeRenderer : ISnakeRenderer
{
    private readonly WriteableBitmap _bitmap;
    private readonly int _width;
    private readonly int _height;
    private readonly int _cellSize;
    private readonly int _radius;

    public WriteableBitmap Bitmap => _bitmap;

    public CapsuleSnakeRenderer(int width, int height, int cellSize, int thickness = 10)
    {
        _width = width;
        _height = height;
        _cellSize = cellSize;
        _radius = thickness / 2;

        _bitmap = new WriteableBitmap(
            width * cellSize,
            height * cellSize,
            96, 96,
            PixelFormats.Bgra32,
            null);
    }

    public void Draw(GameEngine game, float t)
    {
        _bitmap.Lock();

        unsafe
        {
            var buffer = _bitmap.BackBuffer;
            int stride = _bitmap.BackBufferStride;

            Clear(buffer, stride);
            DrawMap(game, buffer, stride);
            DrawSnake(game, buffer, stride, t);
        }

        _bitmap.AddDirtyRect(new Int32Rect(0, 0, _bitmap.PixelWidth, _bitmap.PixelHeight));
        _bitmap.Unlock();
    }

    private unsafe void DrawSnake(GameEngine game, IntPtr buffer, int stride, float t)
    {
        var current  = game.Snake.Body.ToList();
        var previous = game.Snake.PreviousBody;

        int count = Math.Min(current.Count, previous.Count);
        if (count == 0) return;

        var centers = new (float x, float y)[count];

        float delayPerSegment = 0.08f;

        for (int i = 0; i < count; i++)
        {
            float cx = current[i].X * _cellSize + _cellSize / 2f;
            float cy = current[i].Y * _cellSize + _cellSize / 2f;
            float px = previous[i].X * _cellSize + _cellSize / 2f;
            float py = previous[i].Y * _cellSize + _cellSize / 2f;

            float localT;

            if (i == 0)
            {
                float smooth = t * t * (3 - 2 * t);
                localT = smooth;
            }
            else
            {
                float delay = i * delayPerSegment;
                localT = Math.Clamp(t - delay, 0f, 1f);
            }

            centers[i] = (
                px + (cx - px) * localT,
                py + (cy - py) * localT
            );
        }

        // 🔥 dibujar cápsulas
        for (int i = 0; i < count - 1; i++)
        {
            DrawCapsule(
                buffer, stride,
                centers[i].x, centers[i].y,
                centers[i + 1].x, centers[i + 1].y,
                _radius,
                t,          // 👈 el t del GameLoop
                i,          // 👈 índice del segmento
                80, 200, 80);
        }

        // cabeza
        DrawCircle(buffer, stride,
            centers[0].x, centers[0].y,
            _radius + 2,
            100, 230, 100);
    }

    private unsafe void DrawCapsule(
        IntPtr buffer, int stride,
        float x0, float y0,
        float x1, float y1,
        int radius,
        float globalT,          // 👈 el t del GameLoop
        int segmentIndex,       // 👈 índice del segmento
        byte r, byte g, byte b)
    {
        float dx = x1 - x0;
        float dy = y1 - y0;
        float len = MathF.Sqrt(dx * dx + dy * dy);

        if (len < 0.001f)
        {
            DrawCircle(buffer, stride, x0, y0, radius, r, g, b);
            return;
        }

        float step = radius * 0.6f;
        int steps = Math.Max(1, (int)(len / step));

        for (int i = 0; i <= steps; i++)
        {
            float baseT = i / (float)steps;

            // 🔥 delay jerárquico
            float segmentDelay  = segmentIndex * 0.08f;
            float internalDelay = baseT * 0.08f;

            float finalT = Math.Clamp(globalT - segmentDelay - internalDelay, 0f, 1f);

            // interpolación con desfase
            float cx = x0 + dx * finalT;
            float cy = y0 + dy * finalT;

            DrawCircle(buffer, stride, cx, cy, radius, r, g, b);
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
        *((byte*)buffer + index + index + 2 - index) = r;
        *((byte*)buffer + index + 3) = 255;
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
            if (game.Map[x, y] == CellType.Wall)
                DrawSolidCell(buffer, stride, x, y, 120, 120, 120);
            else if (game.Map[x, y] == CellType.Food)
                DrawSolidCell(buffer, stride, x, y, 255, 50, 50);
        }
    }

    private unsafe void DrawSolidCell(IntPtr buffer, int stride, int gridX, int gridY, byte r, byte g, byte b)
    {
        int startX = gridX * _cellSize;
        int startY = gridY * _cellSize;

        for (int y = 0; y < _cellSize; y++)
        for (int x = 0; x < _cellSize; x++)
            SetPixel(buffer, stride, startX + x, startY + y, r, g, b);
    }
}