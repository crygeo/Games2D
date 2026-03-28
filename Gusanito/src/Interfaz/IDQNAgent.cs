using Gusanito.DQN;

namespace Gusanito.Interfaz;

public interface IDQNAgent : ISnakeAI
{
    bool  IsTraining { get; set; }
    float Epsilon    { get; }
    int   Steps      { get; }
 
    void PushExperience(in Experience exp);
    void OptimizeStep();
    void SyncTargetNetwork();
    void Save(string path);
    void Load(string path);
}
