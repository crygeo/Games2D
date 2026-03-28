using Gusanito.DQN;

namespace Gusanito.Interfaz;

public interface ITrainingLoop
{
    event Action<TrainingStats> EpisodeCompleted;
    Task RunAsync(int episodes, CancellationToken ct);
    void Stop();
}