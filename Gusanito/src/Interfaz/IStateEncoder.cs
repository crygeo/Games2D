using Gusanito.Game;

namespace Gusanito.Interfaz;

public interface IStateEncoder
{
    int StateSize { get; }
    float[] Encode(GameEngine game);
}