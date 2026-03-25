using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Gusanito.Enum;
using Gusanito.Game;

public class WriteableBitmapRenderer
{
    private readonly WriteableBitmap _bitmap;
    private readonly int _width;
    private readonly int _height;
    private readonly int _cellSize;

    public WriteableBitmap Bitmap => _bitmap;

    public WriteableBitmapRenderer(int width, int height, int cellSize)
    {
        _width = width;
        _height = height;
        _cellSize = cellSize;

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

            // Limpiar (fondo negro)
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

                *((byte*)buffer + index + 0) = 0; // B
                *((byte*)buffer + index + 1) = 0; // G
                *((byte*)buffer + index + 2) = 0; // R
                *((byte*)buffer + index + 3) = 255; // A
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
        {
            for (int y = 0; y < _height; y++)
            {
                var cell = game.Map[x, y];

                if (cell == CellType.Empty)
                    continue;

                switch (cell)
                {
                    case CellType.Wall:
                        DrawCell(buffer, stride, x, y, 120, 120, 120);
                        break;

                    case CellType.Food:
                        DrawCell(buffer, stride, x, y, 255, 0, 0);
                        break;
                }
            }
        }
    }

    private unsafe void DrawSnake(GameEngine game, IntPtr buffer, int stride, float t)
    {
        var snake = game.Snake;

        var prev = snake.PreviousHead;
        var head = snake.Head;

        float interpX = prev.X + (head.X - prev.X) * t;
        float interpY = prev.Y + (head.Y - prev.Y) * t;

        DrawCellInterpolated(buffer, stride, interpX, interpY, 0, 255, 0);

        // dibujar resto del cuerpo (por ahora sin interpolar)
        bool skipHead = true;
        foreach (var part in snake.Body)
        {
            if (skipHead)
            {
                skipHead = false;
                continue;
            }

            DrawCell(buffer, stride, part.X, part.Y, 0, 200, 0);
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