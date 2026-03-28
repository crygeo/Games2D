using Gusanito.Enum;
using Gusanito.Game;
using Gusanito.Models;

namespace Gusanito.SAI;

/// <summary>
/// Temporary diagnostic — call CloneDiff.Assert(game) in AsyncAIRunner
/// before firing the Task to print any mismatch between game and its clone.
/// Remove once the bug is confirmed fixed.
/// </summary>
public static class CloneDiff
{
    public static void Assert(GameEngine game)
    {
        var clone = game.Clone();
        var issues = new List<string>();

        // ── Food ──────────────────────────────────────────────────────────────
        if (game.Food.X != clone.Food.X  || game.Food.Y != clone.Food.Y)
            issues.Add($"Food: original={game.Food.X},{game.Food.Y}  clone={clone.Food.X},{clone.Food.Y}");

        // ── Map food cell ──────────────────────────────────────────────────────
        // The Map[food.X, food.Y] must be CellType.Food in both
        if (game.Map[game.Food.X, game.Food.Y] != CellType.Food)
            issues.Add($"Map[food] original is NOT CellType.Food — it is {game.Map[game.Food.X, game.Food.Y]}");

        if (clone.Map[clone.Food.X, clone.Food.Y] != CellType.Food)
            issues.Add($"Map[food] clone is NOT CellType.Food — it is {clone.Map[clone.Food.X, clone.Food.Y]}");

        // ── Snake head ────────────────────────────────────────────────────────
        if (game.Snake.Head.X != clone.Snake.Head.X || game.Snake.Head.Y != clone.Snake.Head.Y)
            issues.Add($"Head: original={game.Snake.Head.X},{game.Snake.Head.Y}  clone={clone.Snake.Head.X},{clone.Snake.Head.Y}");

        // ── Snake direction ───────────────────────────────────────────────────
        if (game.Snake.CurrentDirection != clone.Snake.CurrentDirection)
            issues.Add($"Direction: original={game.Snake.CurrentDirection}  clone={clone.Snake.CurrentDirection}");

        // ── Body length ───────────────────────────────────────────────────────
        if (game.Snake.Body.Count != clone.Snake.Body.Count)
            issues.Add($"Body.Count: original={game.Snake.Body.Count}  clone={clone.Snake.Body.Count}");

        // ── Body positions ─────────────────────────────────────────────────────
        var origBody  = game.Snake.Body.ToList();
        var cloneBody = clone.Snake.Body.ToList();
        int minLen    = Math.Min(origBody.Count, cloneBody.Count);

        for (int i = 0; i < minLen; i++)
        {
            if (origBody[i].X != cloneBody[i].X || origBody[i].Y != cloneBody[i].Y)
                issues.Add($"Body[{i}]: original={origBody[i].X},{origBody[i].Y}  clone={cloneBody[i].X},{cloneBody[i].Y}");
        }

        // ── PreviousBody length ───────────────────────────────────────────────
        if (game.Snake.PreviousBody.Count != clone.Snake.PreviousBody.Count)
            issues.Add($"PreviousBody.Count: original={game.Snake.PreviousBody.Count}  clone={clone.Snake.PreviousBody.Count}");

        // ── IsGameOver ────────────────────────────────────────────────────────
        if (game.IsGameOver != clone.IsGameOver)
            issues.Add($"IsGameOver: original={game.IsGameOver}  clone={clone.IsGameOver}");

        // ── Width / Height ────────────────────────────────────────────────────
        if (game.Width != clone.Width || game.Height != clone.Height)
            issues.Add($"Dimensions: original={game.Width}x{game.Height}  clone={clone.Width}x{clone.Height}");

        // ── Report ────────────────────────────────────────────────────────────
        if (issues.Count == 0)
        {
            Console.WriteLine("[CloneDiff] ✅ Clone is identical to original.");
        }
        else
        {
            Console.WriteLine($"[CloneDiff] ❌ {issues.Count} difference(s) found:");
            foreach (var issue in issues)
                Console.WriteLine($"  → {issue}");
        }
    }
}