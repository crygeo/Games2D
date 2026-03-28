// src/DQN/DQNAgent.cs

using Gusanito.Enum;
using Gusanito.Game;
using Gusanito.Interfaz;
using TorchSharp;
using static TorchSharp.torch;
using static TorchSharp.torch.nn;

namespace Gusanito.DQN;

/// <summary>
/// Agente DQN con experience replay y target network.
///
/// Implementa ISnakeAI → es un drop-in replacement para HamiltonianAI.
///
/// Thread safety:
/// - GetNextMove() puede llamarse desde el UI thread.
/// - OptimizeStep() y PushExperience() se llaman desde el training thread.
/// - _onlineNet NO se comparte entre threads durante optimización.
///   Durante inferencia se usa _inferenceNet, que se sincroniza
///   periódicamente desde el training thread bajo _inferenceLock.
/// </summary>
public sealed class DQNAgent : IDQNAgent
{
    // ── Configuración ──────────────────────────────────────────────────────
    private readonly DQNConfig     _config;
    private readonly IStateEncoder _encoder;
    private readonly Device        _device;

    // ── Redes ──────────────────────────────────────────────────────────────
    private readonly QNetwork _onlineNet;   // recibe gradientes
    private readonly QNetwork _targetNet;   // frozen, se sincroniza periódicamente
    private readonly QNetwork _inferenceNet;// copia para el UI thread
    private readonly object   _inferenceLock = new();

    // ── Optimizador ────────────────────────────────────────────────────────
    private readonly optim.Optimizer _optimizer;

    // ── Replay buffer ──────────────────────────────────────────────────────
    private readonly ReplayBuffer _buffer;

    // ── Estado de entrenamiento ────────────────────────────────────────────
    private int   _steps;
    private float _epsilon;
    private readonly Random _rng = new();

    public bool  IsTraining { get; set; } = true;
    public float Epsilon
    {
        get => _epsilon;
        set => _epsilon = value;
    }

    public int   Steps      => _steps;

    public DQNAgent(DQNConfig config, IStateEncoder encoder)
    {
        _config  = config;
        _encoder = encoder;
        _device  = cuda.is_available() ? CUDA : CPU;

        _onlineNet    = new QNetwork(encoder.StateSize, config.ActionCount).to(_device);
        _targetNet    = new QNetwork(encoder.StateSize, config.ActionCount).to(_device);
        _inferenceNet = new QNetwork(encoder.StateSize, config.ActionCount).to(CPU);

        SyncTargetNetwork();
        SyncInferenceNetwork();

        _optimizer = optim.Adam(_onlineNet.parameters(), lr: config.LearningRate);
        _buffer    = new ReplayBuffer(config.ReplayCapacity);
        _epsilon   = config.EpsilonStart;
    }

    // ── ISnakeAI ───────────────────────────────────────────────────────────

    /// <summary>
    /// Llamado desde el UI thread. Usa _inferenceNet (CPU, no recibe gradientes).
    /// Epsilon-greedy solo activo durante entrenamiento.
    /// </summary>
    public Direction GetNextMove(GameEngine game)
    {
        if (IsTraining && _rng.NextDouble() < _epsilon)
            return (Direction)_rng.Next(_config.ActionCount);

        var state = _encoder.Encode(game);
        return GetBestAction(state);
    }

    // ── IDQNAgent ──────────────────────────────────────────────────────────

    public void PushExperience(in Experience exp) => _buffer.Push(exp);

    /// <summary>
    /// Un paso de optimización con un minibatch del replay buffer.
    /// Solo llamar desde el training thread.
    /// </summary>
    public void OptimizeStep()
    {
        if (!_buffer.IsReady(_config.MinBufferSize)) return;

        var batch = _buffer.Sample(_config.BatchSize, _rng);

        using var scope = NewDisposeScope();

        // ── Construir tensors del batch ───────────────────────────────────
        var states      = BuildTensor(batch, e => e.State);
        var nextStates  = BuildTensor(batch, e => e.NextState);
        var actions     = tensor(batch.Select(e => (long)e.Action).ToArray()).to(_device);
        var rewards     = tensor(batch.Select(e => e.Reward).ToArray()).to(_device);
        var dones       = tensor(batch.Select(e => e.Done ? 1f : 0f).ToArray()).to(_device);

        // ── Q(s, a) — online network ──────────────────────────────────────
        var qValues     = _onlineNet.forward(states)
                            .gather(1, actions.unsqueeze(1))
                            .squeeze(1);

        // ── Q target: r + γ · max Q'(s', a') · (1 - done) ───────────────
        using var noGrad = no_grad();
        var nextQ        = _targetNet.forward(nextStates).amax(1);
        var targets      = rewards + _config.Gamma * nextQ * (1f - dones);

        // ── Huber loss (más estable que MSE con outliers) ─────────────────
        var loss = functional.smooth_l1_loss(qValues, targets.detach());

        _optimizer.zero_grad();
        loss.backward();

        // Gradient clipping — evita explosión de gradientes, común en DQN
        nn.utils.clip_grad_norm_(_onlineNet.parameters(), 10.0);

        _optimizer.step();

        _steps++;

        // ── Sync target network ───────────────────────────────────────────
        if (_steps % _config.TargetSyncSteps == 0)
            SyncTargetNetwork();

        // ── Sync inference network (UI thread) ───────────────────────────
        if (_steps % _config.InferenceSyncSteps == 0)
            SyncInferenceNetwork();

        // ── Decay epsilon ─────────────────────────────────────────────────
        _epsilon = Math.Max(_config.EpsilonEnd,
                            _epsilon * _config.EpsilonDecay);
    }

    public void SyncTargetNetwork()
    {
        _targetNet.load_state_dict(_onlineNet.state_dict());
        _targetNet.eval();
    }

    public void Save(string path)
    {
        _onlineNet.save(path);
    }

    public void Load(string path)
    {
        _onlineNet.load(path);
        SyncTargetNetwork();
        SyncInferenceNetwork();
    }

    // ── Privados ───────────────────────────────────────────────────────────

    private Direction GetBestAction(float[] state)
    {
        lock (_inferenceLock)
        {
            using var noGrad = no_grad();
            using var t      = tensor(state).unsqueeze(0);
            var qValues      = _inferenceNet.forward(t);
            return (Direction)(int)qValues.argmax(1).item<long>();
        }
    }

    private void SyncInferenceNetwork()
    {
        lock (_inferenceLock)
        {
            // Mover state_dict a CPU antes de cargar en inferenceNet
            var stateDict = _onlineNet.state_dict();
            _inferenceNet.load_state_dict(stateDict);
            _inferenceNet.eval();
        }
    }

    private Tensor BuildTensor(Experience[] batch, Func<Experience, float[]> selector)
    {
        int size = selector(batch[0]).Length;
        var flat = new float[batch.Length * size];

        for (int i = 0; i < batch.Length; i++)
        {
            var arr = selector(batch[i]);
            Array.Copy(arr, 0, flat, i * size, size);
        }

        return tensor(flat).reshape(batch.Length, size).to(_device);
    }
}