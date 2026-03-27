using Gusanito.Enum;
using Gusanito.Game;

namespace Gusanito.Interfaz;

public interface ISnakeAI
{
    Direction GetNextMove(GameEngine game);
}