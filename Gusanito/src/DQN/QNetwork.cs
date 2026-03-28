// src/DQN/QNetwork.cs
using TorchSharp;
using TorchSharp.Modules;
using static TorchSharp.torch;
using static TorchSharp.torch.nn;

namespace Gusanito.DQN;

/// <summary>
/// Red neuronal para aproximar Q(s, a).
///
/// Arquitectura: FC 512 → 256 → |actions|
/// Sin BatchNorm — introduce varianza problemática con replay buffer pequeño.
/// Sin Dropout durante entrenamiento DQN estándar — degrada estabilidad.
///
/// Dos instancias en DQNAgent: online network y target network.
/// Solo la online network recibe gradientes. La target se sincroniza
/// periódicamente con hard copy (no soft update — más simple y suficiente).
/// </summary>
public sealed class QNetwork : Module<Tensor, Tensor>
{
    private readonly Linear _fc1;
    private readonly Linear _fc2;
    private readonly Linear _fc3;

    public QNetwork(int stateSize, int actionCount, string name = "QNetwork")
        : base(name)
    {
        _fc1 = Linear(stateSize, 512);
        _fc2 = Linear(512,       256);
        _fc3 = Linear(256,       actionCount);

        RegisterComponents();
    }

    public override Tensor forward(Tensor x)
    {
        x = functional.relu(_fc1.forward(x));
        x = functional.relu(_fc2.forward(x));
        return _fc3.forward(x); // Q-values crudos, sin activación final
    }
}