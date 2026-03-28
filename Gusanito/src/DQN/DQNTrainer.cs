// src/DQN/DQNTrainer.cs

using Gusanito.Config;
using Gusanito.Game;
using Gusanito.Interfaz;

namespace Gusanito.DQN;


/// <summary>
/// Loop de entrenamiento headless — corre N episodios sin UI.
///
/// Responsabilidades:
///   - Gestionar el ciclo episodio → step → experience → optimize
///   - Emitir estadísticas por episodio para binding en la UI
///   - Respetar CancellationToken para parada limpia
///
/// No tiene dependencia de WPF — es puro dominio.
/// </summary>
public sealed class DQNTrainer : ITrainingLoop
{
    private readonly IDQNAgent         _agent;
    private readonly IRewardCalculator _rewardCalc;
    private readonly IStateEncoder     _encoder;
    private readonly GameSettings      _settings;
    private readonly DQNConfig         _config;

    private CancellationTokenSource? _cts;

    public event Action<TrainingStats>? EpisodeCompleted;

    public DQNTrainer(
        IDQNAgent         agent,
        IRewardCalculator rewardCalc,
        IStateEncoder     encoder,
        GameSettings      settings,
        DQNConfig         config)
    {
        _agent      = agent;
        _rewardCalc = rewardCalc;
        _encoder    = encoder;
        _settings   = settings;
        _config     = config;
    }

    public async Task RunAsync(int episodes, CancellationToken ct)
    {
        _agent.IsTraining = true;

        await Task.Run(() =>
        {
            for (int ep = 0; ep < episodes && !ct.IsCancellationRequested; ep++)
            {
                var stats = RunEpisode(ep, ct);
                EpisodeCompleted?.Invoke(stats);
            }
        }, ct);

        _agent.IsTraining = false;
    }

    public void Stop() => _cts?.Cancel();

    // ── Episodio ───────────────────────────────────────────────────────────

    private TrainingStats RunEpisode(int episode, CancellationToken ct)
    {
        var game = new GameEngine(_settings);
        game.NewGame();

        float totalReward = 0f;
        int   steps       = 0;
        var   started     = DateTime.UtcNow;

        // Límite de pasos por episodio — evita loops infinitos si el agente
        // aprende a sobrevivir sin comer. Proporcional al tamaño del tablero.
        int maxSteps = _settings.Width * _settings.Height * 4;

        while (!game.IsGameOver && steps < maxSteps && !ct.IsCancellationRequested)
        {
            var stateBefore = _encoder.Encode(game);
            var snapBefore  = game.Clone(); // para reward calculation

            var action    = _agent.GetNextMove(game);
            game.EnqueueDirection(action);
            game.Update();

            var stateAfter = _encoder.Encode(game);
            float reward   = _rewardCalc.Calculate(snapBefore, game, game.IsGameOver);

            _agent.PushExperience(new Experience(
                State:     stateBefore,
                Action:    (int)action,
                Reward:    reward,
                NextState: stateAfter,
                Done:      game.IsGameOver));

            _agent.OptimizeStep();

            totalReward += reward;
            steps++;
        }

        return new TrainingStats(
            Episode:     episode,
            Steps:       steps,
            TotalReward: totalReward,
            Score:       game.Score,
            Epsilon:     _agent.Epsilon,
            Duration:    DateTime.UtcNow - started);
    }
}