using Gusanito.Game;
using Gusanito.Interfaz;
using Gusanito.Models;

namespace Gusanito.DQN;

/// <summary>
/// Función de recompensa densa para DQN.
///
/// Señales:
///   +10.0  — comió comida
///   -10.0  — murió
///   +0.1   — se acercó a la comida (reward shaping)
///   -0.1   — se alejó de la comida (reward shaping)
///   -0.01  — penalización por paso (evita loops infinitos)
///
/// Trade-off vs Sparse:
///   Converge más rápido porque el agente recibe señal en cada paso.
///   Riesgo: puede aprender a orbitar la comida para acumular +0.1
///   sin comerla. Mitigado con la penalización por paso (-0.01).
///
/// Si se detecta el comportamiento de órbita, reducir DistanceReward
///   a 0.01 o cambiar a SparseRewardCalculator.
/// </summary>
public sealed class DenseRewardCalculator : IRewardCalculator
{
    // ── Magnitudes configurables ───────────────────────────────────────────
    private readonly float _foodReward;
    private readonly float _deathPenalty;
    private readonly float _distanceReward;
    private readonly float _stepPenalty;

    public DenseRewardCalculator(
        float foodReward    =  10.0f,
        float deathPenalty  = -10.0f,
        float distanceReward =  0.1f,
        float stepPenalty   =  -0.01f)
    {
        _foodReward     = foodReward;
        _deathPenalty   = deathPenalty;
        _distanceReward = distanceReward;
        _stepPenalty    = stepPenalty;
    }

    /// <inheritdoc />
    public float Calculate(GameEngine before, GameEngine after, bool died)
    {
        // ── 1. Muerte — señal dominante, termina el episodio ──────────────
        if (died)
            return _deathPenalty;

        // ── 2. Comió — señal positiva fuerte ──────────────────────────────
        if (after.Score > before.Score)
            return _foodReward;

        // ── 3. Reward shaping por distancia Manhattan a la comida ─────────
        //    Comparamos distancia antes y después del paso.
        //    Si se acercó → +distanceReward, si se alejó → -distanceReward.
        float distBefore = Manhattan(before.Snake.Head, before.Food);
        float distAfter  = Manhattan(after.Snake.Head,  after.Food);

        float shaping = distBefore > distAfter
            ?  _distanceReward   // se acercó
            : -_distanceReward;  // se alejó o se quedó igual

        // ── 4. Penalización por paso — desalienta loops ───────────────────
        return shaping + _stepPenalty;
    }

    // ── Helpers ───────────────────────────────────────────────────────────
    private static float Manhattan(Position a, Position b)
        => MathF.Abs(a.X - b.X) + MathF.Abs(a.Y - b.Y);
}