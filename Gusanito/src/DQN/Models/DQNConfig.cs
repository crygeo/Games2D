namespace Gusanito.DQN;

public sealed record DQNConfig
{
    public int   ActionCount       { get; init; } = 4;
    public float Gamma             { get; init; } = 0.99f;
    public float LearningRate      { get; init; } = 1e-4f;
    public float EpsilonStart      { get; init; } = 1.0f;
    public float EpsilonEnd        { get; init; } = 0.01f;
    public float EpsilonDecay      { get; init; } = 0.995f;
    public int   BatchSize         { get; init; } = 64;
    public int   ReplayCapacity    { get; init; } = 100_000;
    public int   MinBufferSize     { get; init; } = 10_000;
    public int   TargetSyncSteps   { get; init; } = 1_000;
    public int   InferenceSyncSteps{ get; init; } = 100;
}