using Gusanito.Enum;
using Gusanito.Game;
using Gusanito.Models;

namespace Gusanito.SAI;

/// <summary>
/// Evaluates whether the snake can safely take a shortcut on the Hamiltonian cycle.
///
/// A shortcut means moving to a position ahead in the cycle order, skipping intermediate nodes.
/// This dramatically increases speed without abandoning the safety guarantees of the cycle.
///
/// Safety contract: a shortcut is only allowed when:
///   1. The target position is strictly ahead of the snake's current position in the cycle.
///   2. After taking the shortcut, Flood Fill confirms sufficient reachable space.
///   3. The snake body length to board ratio is below the configured danger threshold.
///      (A very long snake leaves little room for shortcuts.)
/// </summary>
public sealed class ShortcutEvaluator
{
    // When the snake occupies more than this fraction of the board, disable shortcuts entirely.
    private readonly float _dangerRatio;

    // Minimum flood-fill reachable ratio after taking the shortcut.
    private readonly float _minReachableRatio;

    public ShortcutEvaluator(float dangerRatio = 0.6f, float minReachableRatio = 0.35f)
    {
        _dangerRatio       = dangerRatio;
        _minReachableRatio = minReachableRatio;
    }

    /// <summary>
    /// Determines whether the snake should take a direct move to <paramref name="candidate"/>
    /// instead of following the next node in the Hamiltonian cycle.
    /// </summary>
    public bool IsSafeShortcut(
        GameEngine game,
        Dictionary<Position, int> cycleOrder,
        int currentCycleIndex,
        Position candidate,
        int cycleLength)
    {
        if (!cycleOrder.TryGetValue(candidate, out int candidateIndex))
            return false;

        int totalCells  = (game.Width - 2) * (game.Height - 2);
        int snakeLength = game.Snake.Body.Count;
        float occupancy = snakeLength / (float)totalCells;

        if (occupancy >= _dangerRatio)
            return false;

        // Candidate must be strictly ahead in the cycle (modular)
        int distanceInCycle = (candidateIndex - currentCycleIndex + cycleLength) % cycleLength;

        if (distanceInCycle <= 1)
            return false;

        var occupied = BuildOccupied(game);

        float reachable = FloodFill.ReachableRatio(
            game.Map,
            occupied,
            game.Width,
            game.Height,
            candidate);

        return reachable >= _minReachableRatio;
    }

    private static HashSet<Position> BuildOccupied(GameEngine game)
    {
        var body = game.Snake.Body;
        var set  = new HashSet<Position>(body.Count);

        int i = 0;
        foreach (var pos in body)
        {
            if (i < body.Count - 1)
                set.Add(pos);
            i++;
        }

        return set;
    }
}
