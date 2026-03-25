using System.Windows.Media.Imaging;
using Gusanito.Game;

namespace Gusanito.Interfaz;

public interface ISnakeRenderer
{
    WriteableBitmap Bitmap { get; }
    void Draw(GameEngine game, float tick);
}