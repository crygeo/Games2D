using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Gusanito.Enum;
using Gusanito.Game;
using Gusanito.Interfaz;

namespace Gusanito.Rendering;

public sealed class SolidColorRenderer : ISnakeRenderer
{
    private readonly WriteableBitmap _bitmap;
    private readonly int _width;
    private readonly int _height;
    private readonly int _cellSize;
    private readonly   int _laneSize;
    private readonly float _laneOffset;  // offset al centro del carril

    /// <summary>
    /// Enables or disables sub-cell interpolation between game ticks.
    /// When false, snake segments snap to their logical grid position.
    /// Can be toggled at runtime without re-creating the renderer.
    /// </summary>
    public bool InterpolationEnabled { get; set; }
    
    

    public WriteableBitmap Bitmap => _bitmap;

    public SolidColorRenderer(int width, int height, int cellSize, int laneSize, bool interpolacion = false)
    {
        _width    = width;
        _height   = height;
        _cellSize = cellSize;
        _laneSize = laneSize;
        _laneOffset = laneSize / 2f;

        // Inicializa desde el parámetro del constructor,
        // pero puede cambiarse después via la propiedad pública.
        InterpolationEnabled = interpolacion;

        _bitmap = new WriteableBitmap(
            (width * cellSize) + (cellSize * 3),
            (height * cellSize) + (cellSize * 3),
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
        {
            for (int x = 0; x < _width * _cellSize; x++)
            {
                int index = y * stride + x * 4;

                *((byte*)buffer + index + 0) = 0;
                *((byte*)buffer + index + 1) = 0;
                *((byte*)buffer + index + 2) = 0;
                *((byte*)buffer + index + 3) = 255;
            }
        }
    }

    private unsafe void DrawCell(IntPtr buffer, int stride, int gridX, int gridY, byte r, byte g, byte b)
    {
        int startX = gridX * _cellSize;
        int startY = gridY * _cellSize;

        for (int y = 0; y < _cellSize; y++)
        {
            for (int x = 0; x < _cellSize; x++)
            {
                int px = startX + x;
                int py = startY + y;

                int index = py * stride + px * 4;

                *((byte*)buffer + index + 0) = b;
                *((byte*)buffer + index + 1) = g;
                *((byte*)buffer + index + 2) = r;
                *((byte*)buffer + index + 3) = 255;
            }
        }
    }

    private unsafe void DrawMap(GameEngine game, IntPtr buffer, int stride)
    {
        for (int x = 0; x < _width; x++)
        for (int y = 0; y < _height; y++)
        {
            var cell = game.Map[x, y];

            if (cell == CellType.Wall)
                DrawPixelThick(buffer, stride, x + _laneOffset, y + _laneOffset, _laneSize, 120, 120, 120);
            else if (cell == CellType.Food)
                DrawPixelThick(buffer, stride, x + _laneOffset, y + _laneOffset, _laneSize, 255, 0, 0);
        }
    }

    private unsafe void DrawSnake(GameEngine game, IntPtr buffer, int stride, float t)
    {
        var snake    = game.Snake;
        var current  = snake.Body.ToList();
        var previous = snake.PreviousBody;

        int count = Math.Min(current.Count, previous.Count);

        for (int i = 0; i < count; i++)
        {
            byte r = (byte)0;
            byte g = i == 0 ? (byte)255 : (byte)200;
            byte b = (byte)0;

            if (InterpolationEnabled)
            {
                float interpX = previous[i].X + (current[i].X - previous[i].X) * t;
                float interpY = previous[i].Y + (current[i].Y - previous[i].Y) * t;

                // Centrar en el carril
                interpX += _laneOffset;
                interpY += _laneOffset;

                DrawPixelThick(buffer, stride, interpX, interpY, _laneSize, r, g, b);
            }
            else
            {
                DrawPixelThick(buffer, stride,
                    current[i].X + _laneOffset,
                    current[i].Y + _laneOffset,
                    _laneSize, r, g, b);
            }
        }
    }
    
    private unsafe void DrawPixelThick(
        IntPtr buffer, int stride,
        float cx, float cy,
        int size,
        byte r, byte g, byte b)
    {
        // Convertir coordenadas lógicas a píxeles
        int centerX = (int)(cx * _cellSize);
        int centerY = (int)(cy * _cellSize);
        
        int thicknessPx = size * _cellSize;


        int half   = thicknessPx / 2;
        int startX = centerX - half;
        int startY = centerY - half;

        for (int y = 0; y < thicknessPx; y++)
        for (int x = 0; x < thicknessPx; x++)
        {
            int px = startX + x;
            int py = startY + y;

            if (px < 0 || py < 0 || px >= _bitmap.PixelWidth || py >= _bitmap.PixelHeight)
                continue;

            int index = py * stride + px * 4;
            *((byte*)buffer + index + 0) = b;
            *((byte*)buffer + index + 1) = g;
            *((byte*)buffer + index + 2) = r;
            *((byte*)buffer + index + 3) = 255;
        }
    }

    private unsafe void DrawCellInterpolated(IntPtr buffer, int stride, float gridX, float gridY, byte r, byte g, byte b)
    {
        int startX = (int)(gridX * _cellSize);
        int startY = (int)(gridY * _cellSize);

        for (int y = 0; y < _cellSize; y++)
        {
            for (int x = 0; x < _cellSize; x++)
            {
                int px = startX + x;
                int py = startY + y;

                if (px < 0 || py < 0 || px >= _bitmap.PixelWidth || py >= _bitmap.PixelHeight)
                    continue;

                int index = py * stride + px * 4;

                *((byte*)buffer + index + 0) = b;
                *((byte*)buffer + index + 1) = g;
                *((byte*)buffer + index + 2) = r;
                *((byte*)buffer + index + 3) = 255;
            }
        }
    }
}