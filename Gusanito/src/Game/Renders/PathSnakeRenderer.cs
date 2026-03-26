using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Gusanito.Game;
using Gusanito.Enum;
using Gusanito.Interfaz;

public sealed class PathSnakeRenderer : ISnakeRenderer
{
    private readonly WriteableBitmap _bitmap;
    private readonly int _width;
    private readonly int _height;
    private readonly int _cellSize;
    private readonly int _radius;

    private readonly List<(float x, float y)> _path = new();

    public WriteableBitmap Bitmap => _bitmap;

    public PathSnakeRenderer(int width, int height, int cellSize, int thickness = 10)
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
            UpdatePath(game, t);
            DrawSnake(buffer, stride, game);
        }

        _bitmap.AddDirtyRect(new Int32Rect(0, 0, _bitmap.PixelWidth, _bitmap.PixelHeight));
        _bitmap.Unlock();
    }

    // 🔥 1. actualizar path
    private void UpdatePath(GameEngine game, float t)
    {
        var head = game.Snake.Body.First();
        var prev = game.Snake.PreviousBody.First();

        float cx = head.X * _cellSize + _cellSize / 2f;
        float cy = head.Y * _cellSize + _cellSize / 2f;

        float px = prev.X * _cellSize + _cellSize / 2f;
        float py = prev.Y * _cellSize + _cellSize / 2f;

        float smoothT = t * t * (3 - 2 * t);

        float hx = px + (cx - px) * smoothT;
        float hy = py + (cy - py) * smoothT;

        _path.Insert(0, (hx, hy));

        // limitar tamaño del path
        int maxPoints = game.Snake.Body.Count * 20;
        if (_path.Count > maxPoints)
            _path.RemoveAt(_path.Count - 1);
    }

    // 🔥 2. construir cuerpo por distancia
    private unsafe void DrawSnake(IntPtr buffer, int stride, GameEngine game)
    {
        if (_path.Count < 2) return;

        float spacing = _radius * 1.5f;
        int targetCount = game.Snake.Body.Count;

        var points = new List<(float x, float y)>();
        points.Add(_path[0]);

        float accumulated = 0f;

        for (int i = 1; i < _path.Count; i++)
        {
            var a = _path[i - 1];
            var b = _path[i];

            float dx = b.x - a.x;
            float dy = b.y - a.y;
            float dist = MathF.Sqrt(dx * dx + dy * dy);

            if (dist < 0.001f) continue;

            if (accumulated + dist >= spacing)
            {
                float t = (spacing - accumulated) / dist;

                float nx = a.x + dx * t;
                float ny = a.y + dy * t;

                points.Add((nx, ny));
                accumulated = 0f;

                if (points.Count >= targetCount)
                    break;
            }
            else
            {
                accumulated += dist;
            }
        }

        // 🔥 dibujar cápsulas
        for (int i = 0; i < points.Count - 1; i++)
        {
            DrawCapsule(buffer, stride,
                points[i].x, points[i].y,
                points[i + 1].x, points[i + 1].y,
                _radius,
                80, 200, 80);
        }

        // cabeza
        DrawCircle(buffer, stride,
            points[0].x, points[0].y,
            _radius + 2,
            100, 230, 100);
    }

    private unsafe void DrawCapsule(
        IntPtr buffer, int stride,
        float x0, float y0,
        float x1, float y1,
        int radius,
        byte r, byte g, byte b)
    {
        float dx = x1 - x0;
        float dy = y1 - y0;
        float len = MathF.Sqrt(dx * dx + dy * dy);

        float step = radius * 0.6f;
        int steps = Math.Max(1, (int)(len / step));

        for (int i = 0; i <= steps; i++)
        {
            float t = i / (float)steps;

            float cx = x0 + dx * t;
            float cy = y0 + dy * t;

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
        *((byte*)buffer + index + 2) = r;
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