using Gusanito.Game;

namespace Gusanito.Interfaz;

public interface IRewardCalculator
{
    float Calculate(GameEngine before, GameEngine after, bool died);
}