
using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Gusanito.Enum;
using Gusanito.Game;
using Gusanito.Interfaz;

namespace Gusanito.Rendering;

public sealed class SnakeRendererPro : ISnakeRenderer
{
    private readonly WriteableBitmap _bitmap;
    private readonly int _width;
    private readonly int _height;
    private readonly int _cellSize;
    private readonly int _thickness;

    public WriteableBitmap Bitmap => _bitmap;

    public SnakeRendererPro(int width, int height, int cellSize, int thickness = 12)
    {
        _width     = width;
        _height    = height;
        _cellSize  = cellSize;
        _thickness = thickness;

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
            IntPtr buffer = _bitmap.BackBuffer;
            int stride = _bitmap.BackBufferStride;

            Clear(buffer, stride);
            DrawMap(game, buffer, stride);
            DrawSnake(game, buffer, stride, t);
        }

        _bitmap.AddDirtyRect(new Int32Rect(0, 0, _bitmap.PixelWidth, _bitmap.PixelHeight));
        _bitmap.Unlock();
    }

    // ===============================
    // 🧠 SNAKE RENDER
    // ===============================
    private unsafe void DrawSnake(GameEngine game, IntPtr buffer, int stride, float t)
    {
        var body = game.Snake.Body.ToList();
        var prev = game.Snake.PreviousBody;

        int count = Math.Min(body.Count, prev.Count);
        if (count == 0) return;

        // 🔴 IMPORTANTE: evitar interpolación en respawn
        bool noInterp = game.Snake.JustRespawned;

        var centers = new (float x, float y)[count];

        for (int i = 0; i < count; i++)
        {
            float cx = body[i].X * _cellSize + _cellSize / 2f;
            float cy = body[i].Y * _cellSize + _cellSize / 2f;

            if (noInterp)
            {
                centers[i] = (cx, cy);
            }
            else
            {
                float px = prev[i].X * _cellSize + _cellSize / 2f;
                float py = prev[i].Y * _cellSize + _cellSize / 2f;

                float delayPerSegment = 0.0001f; // 🔥 ajustable

                float localT;

                if (i == 0)
                {
                    // cabeza con easing
                    float smoothT = t * t * (3f - 2f * t);
                    localT = smoothT;
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
        }

        // 🔥 Dibujar cuerpo como cápsulas
        for (int i = 0; i < count - 1; i++)
        {
            var to = centers[i];
            var from = centers[i + 1];
            
            
            float dx = to.x - from.x;
            float dy = to.y - from.y;
            float len = MathF.Sqrt(dx * dx + dy * dy);

            if (len < 0.001f)
                continue;

// normalizar dirección
            float nx = dx / len;
            float ny = dy / len;

// 🔥 recorte en ambos extremos
            float cut = _thickness * 0.01f; // 🔥 AJUSTABLE

            float startX = from.x + nx * cut;
            float startY = from.y + ny * cut;

            float endX   = to.x   - nx * cut;
            float endY   = to.y   - ny * cut;

            DrawThickLine(buffer, stride,
                startX, startY,
                endX,   endY,
                _thickness,
                80, 200, 80);
        }

        // 🔵 joints (círculos)
        for (int i = 0; i < count; i++)
        {
            DrawCircle(
                buffer, stride,
                centers[i].x, centers[i].y,
                _thickness / 2,
                80, 200, 80);
        }

        // 🟢 cabeza
        DrawCircle(
            buffer, stride,
            centers[0].x, centers[0].y,
            _thickness / 2 + 2,
            100, 230, 100);
    }

    // ===============================
    // 🧱 MAP
    // ===============================
    private unsafe void DrawMap(GameEngine game, IntPtr buffer, int stride)
    {
        for (int x = 0; x < _width; x++)
        for (int y = 0; y < _height; y++)
        {
            if (game.Map[x, y] == CellType.Wall)
                DrawSolidCell(buffer, stride, x, y, 120, 120, 120);

            if (game.Map[x, y] == CellType.Food)
                DrawSolidCell(buffer, stride, x, y, 255, 50, 50);
        }
    }

    // ===============================
    // 🧹 CLEAR
    // ===============================
    private unsafe void Clear(IntPtr buffer, int stride)
    {
        int total = _bitmap.PixelHeight * stride;
        Span<byte> span = new Span<byte>((void*)buffer, total);
        span.Fill(0);

        for (int i = 3; i < total; i += 4)
            span[i] = 255;
    }

    // ===============================
    // ✏️ DRAW PRIMITIVES
    // ===============================
    private unsafe void DrawThickLine(
        IntPtr buffer, int stride,
        float x0, float y0, float x1, float y1,
        int thickness, byte r, byte g, byte b)
    {
        float dx = x1 - x0;
        float dy = y1 - y0;
        float len = MathF.Sqrt(dx * dx + dy * dy);

        if (len < 0.001f) return;

        float nx = -dy / len;
        float ny = dx / len;

        int half = thickness / 2;
        int steps = (int)len + 1;

        for (int s = 0; s <= steps; s++)
        {
            float t = s / (float)steps;
            float cx = x0 + dx * t;
            float cy = y0 + dy * t;

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

    private unsafe void DrawSolidCell(IntPtr buffer, int stride, int gx, int gy, byte r, byte g, byte b)
    {
        int startX = gx * _cellSize;
        int startY = gy * _cellSize;

        for (int y = 0; y < _cellSize; y++)
        for (int x = 0; x < _cellSize; x++)
            SetPixel(buffer, stride, startX + x, startY + y, r, g, b);
    }

    private unsafe void SetPixel(IntPtr buffer, int stride, int px, int py, byte r, byte g, byte b)
    {
        if (px < 0 || py < 0 || px >= _bitmap.PixelWidth || py >= _bitmap.PixelHeight)
            return;

        int index = py * stride + px * 4;
        byte* ptr = (byte*)buffer + index;

        ptr[0] = b;
        ptr[1] = g;
        ptr[2] = r;
        ptr[3] = 255;
    }
}