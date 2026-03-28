using Gusanito.Enum;
using Gusanito.Game;
using Gusanito.Interfaz;

namespace Gusanito.DQN;

/// <summary>
/// Codifica el estado del juego como 4 canales binarios aplanados.
///
/// Canal 0 — cabeza:   1.0 en la celda de la cabeza
/// Canal 1 — cuerpo:   1.0 en cada segmento del cuerpo
/// Canal 2 — comida:   1.0 en la celda de la comida
/// Canal 3 — paredes:  1.0 en cada celda de tipo Wall
///
/// Total: 4 × W × H floats.
/// Para un tablero 27×27 (25 + 2 paredes) → 2916 inputs.
///
/// Diseño deliberado:
/// - 4 canales separados en vez de one-hot plano → la red puede aprender
///   filtros espaciales independientes por tipo de celda.
/// - Sin normalización adicional — valores ya están en [0, 1].
/// - StateSize es fijo por instancia → la red se construye con este valor.
/// </summary>
public sealed class GridStateEncoder : IStateEncoder
{
    private readonly int _width;
    private readonly int _height;

    public int StateSize => 4 * _width * _height;

    public GridStateEncoder(int width, int height)
    {
        _width  = width;
        _height = height;
    }

    public float[] Encode(GameEngine game)
    {
        var state = new float[StateSize];
        int layer = _width * _height;

        // Canal 0 — cabeza
        var head = game.Snake.Head;
        state[0 * layer + head.Y * _width + head.X] = 1f;

        // Canal 1 — cuerpo (excluye cabeza para no solapar señales)
        bool first = true;
        foreach (var segment in game.Snake.Body)
        {
            if (first) { first = false; continue; }
            state[1 * layer + segment.Y * _width + segment.X] = 1f;
        }

        // Canal 2 — comida
        var food = game.Food;
        state[2 * layer + food.Y * _width + food.X] = 1f;

        // Canal 3 — paredes
        for (int x = 0; x < _width; x++)
        for (int y = 0; y < _height; y++)
        {
            if (game.Map[x, y] == CellType.Wall)
                state[3 * layer + y * _width + x] = 1f;
        }

        return state;
    }
}