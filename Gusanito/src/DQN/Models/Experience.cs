namespace Gusanito.DQN;

public readonly record struct Experience(
    float[] State,
    int     Action,
    float   Reward,
    float[] NextState,
    bool    Done);