using System.Windows;
using System.Windows.Media.Imaging;

namespace Gusanito.Helpers;

public sealed class Tilemap
{
    private readonly BitmapSource _source;
    private readonly int _tileWidth;
    private readonly int _tileHeight;
    private readonly int _columns;
    private readonly int _rows;

    public int Columns => _columns;
    public int Rows => _rows;

    public Tilemap(BitmapSource source, int tileWidth, int tileHeight)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (tileWidth  <= 0) throw new ArgumentOutOfRangeException(nameof(tileWidth));
        if (tileHeight <= 0) throw new ArgumentOutOfRangeException(nameof(tileHeight));

        if (source.PixelWidth  % tileWidth  != 0) throw new ArgumentException($"Image width ({source.PixelWidth}) is not divisible by tileWidth ({tileWidth}).");
        if (source.PixelHeight % tileHeight != 0) throw new ArgumentException($"Image height ({source.PixelHeight}) is not divisible by tileHeight ({tileHeight}).");

        _source     = source;
        _tileWidth  = tileWidth;
        _tileHeight = tileHeight;
        _columns    = source.PixelWidth  / tileWidth;
        _rows       = source.PixelHeight / tileHeight;
    }

    public CroppedBitmap GetTile(int col, int row)
    {
        ValidateCoords(col, row);

        var rect = new Int32Rect(
            col * _tileWidth,
            row * _tileHeight,
            _tileWidth,
            _tileHeight
        );

        return new CroppedBitmap(_source, rect);
    }

    public byte[] GetTilePixels(int col, int row)
    {
        ValidateCoords(col, row);

        var cropped = new CroppedBitmap(_source, new Int32Rect(
            col * _tileWidth,
            row * _tileHeight,
            _tileWidth,
            _tileHeight
        ));

        int stride = _tileWidth * 4; // Bgra32
        byte[] pixels = new byte[stride * _tileHeight];
        cropped.CopyPixels(pixels, stride, 0);
        return pixels;
    }

    private void ValidateCoords(int col, int row)
    {
        if (col < 0 || col >= _columns)
            throw new ArgumentOutOfRangeException(nameof(col), $"Col {col} is out of range [0, {_columns - 1}].");

        if (row < 0 || row >= _rows)
            throw new ArgumentOutOfRangeException(nameof(row), $"Row {row} is out of range [0, {_rows - 1}].");
    }
}