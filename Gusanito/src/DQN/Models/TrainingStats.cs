namespace Gusanito.DQN;

public sealed record TrainingStats(
    int      Episode,      // Número de episodio (0-based)
    int      Steps,        // Pasos de optimización acumulados
    float    TotalReward,  // Suma de recompensas del episodio
    int      Score,        // Comidas recolectadas
    float    Epsilon,      // Valor de epsilon al finalizar
    TimeSpan Duration);    // Duración real del episodio
