using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Gusanito.Config;
using Gusanito.DQN;
using Gusanito.Enum;
using Gusanito.Game;
using Gusanito.Helpers;
using Gusanito.Interfaz;
using Gusanito.Rendering;

namespace Gusanito.ViewModel;

public partial class MainVM : ObservableObject
{
    // ── Infraestructura ────────────────────────────────────────────────────
    private readonly GameEngine    _game;
    private readonly ISnakeRenderer _renderer;
    public  readonly GameSettings  Settings;

    // ── Game loop ──────────────────────────────────────────────────────────
    private DateTime _lastFrameTime;
    private double   _accumulator;
    private double   _tickRate;

    // ── AI — el resto de MainVM solo conoce ISnakeAI ──────────────────────
    private readonly ISnakeAI    _ai;

    // ── DQN — referencias concretas solo para controlar entrenamiento ──────
    // Estas dos propiedades existen ÚNICAMENTE para poder llamar
    // StartTraining / StopTraining / guardar pesos.
    // Si usas HamiltonianAI en vez de DQNAgent, simplemente son null.
    private readonly IDQNAgent?    _dqnAgent;
    private readonly ITrainingLoop? _trainer;
    private CancellationTokenSource? _trainCts;

    // ── Bindings ───────────────────────────────────────────────────────────
    public WriteableBitmap GameImage { get; }

    [ObservableProperty] private string  scoreText    = "Score: 0";
    [ObservableProperty] private string  timeText     = "Time: 00:00";
    [ObservableProperty] private string  trainingText = "";
    [ObservableProperty] private bool    isGameOver   = true;
    [ObservableProperty] private bool    isPaused;
    [ObservableProperty] private bool    isTraining;

    // ── Commands ───────────────────────────────────────────────────────────
    public ICommand OnKeyDownCommand     => new RelayCommand<KeyEventArgs>(OnKeyDown);
    public ICommand StartTrainingCommand => new RelayCommand(StartTraining,  () => !IsTraining);
    public ICommand StopTrainingCommand  => new RelayCommand(StopTraining,   () =>  IsTraining);
    public ICommand SaveWeightsCommand   => new RelayCommand(SaveWeights,    () =>  _dqnAgent != null);

    // ── Constructor ────────────────────────────────────────────────────────
    public MainVM()
    {
        Settings = GameSettingsLoader.LoadOrDefault("src/Config/gameSettings.json");
        _game    = new GameEngine(Settings);

        // ── Elegir AI ──────────────────────────────────────────────────────
        // Para volver a HamiltonianAI: comenta el bloque DQN y descomenta
        // el bloque Hamiltonian. MainVM no cambia en absoluto — solo estas líneas.

        // ── Opción A: HamiltonianAI ────────────────────────────────────────
        // var hamAI    = new HamiltonianAI(new ShortcutEvaluator(), TimeSpan.FromSeconds(5));
        // var runner   = new SyncAIRunner(hamAI);
        // _ai          = runner;
        // _dqnAgent    = null;
        // _trainer     = null;

        // ── Opción B: DQN ─────────────────────────────────────────────────
        var encoder  = new GridStateEncoder(Settings.Width, Settings.Height);
        var config   = new DQNConfig();
        var agent    = new DQNAgent(config, encoder);
        var trainer  = new DQNTrainer(
            agent:       agent,
            rewardCalc:  new DenseRewardCalculator(),
            encoder:     encoder,
            settings:    Settings,
            config:      config);

        // El game loop usa _ai — no sabe si es DQN o Hamiltonian
        _ai       = agent;
        // Las referencias concretas solo para control de entrenamiento
        _dqnAgent = agent;
        _trainer  = trainer;

        // Suscribir stats ANTES de arrancar — evita race condition
        _trainer.EpisodeCompleted += OnEpisodeCompleted;

        // ── Renderer ───────────────────────────────────────────────────────
        int cellSize = (int)(800.0 / ((Settings.Width + Settings.Height) / 2.0));
        _renderer    = new SolidColorRenderer(
            Settings.Width, Settings.Height, cellSize, Settings.LaneSize);

        GameImage = _renderer.Bitmap;

        // ── Arrancar juego ─────────────────────────────────────────────────
        StartNewGame();

        // ── Game loop ──────────────────────────────────────────────────────
        _tickRate      = Settings.SpeedMs / 1000.0;
        _lastFrameTime = DateTime.Now;
        CompositionTarget.Rendering += GameLoop;
    }

    // ── Game lifecycle ─────────────────────────────────────────────────────

    private void StartNewGame()
    {
        if (!_game.NewGame())
            return;

        ScoreText  = "Score: 0";
        TimeText   = "Time: 00:00";
        IsGameOver = false;
        IsPaused   = false;
    }

    // ── Game loop ──────────────────────────────────────────────────────────

    private void GameLoop(object? sender, EventArgs e)
    {
        var now       = DateTime.Now;
        var deltaTime = (now - _lastFrameTime).TotalSeconds;
        _lastFrameTime = now;

        if (!_game.IsGameOver && !_game.IsPaused)
            _game.ElapsedTime += TimeSpan.FromSeconds(deltaTime);

        _accumulator += deltaTime;

        while (_accumulator >= _tickRate)
        {
            if (!_game.IsGameOver && !_game.IsPaused)
                TickAI();

            _game.Update();
            _accumulator -= _tickRate;
        }

        float t = _game.IsGameOver ? 1f : (float)(_accumulator / _tickRate);

        _renderer.Draw(_game, t);

        ScoreText  = $"Score: {_game.Score}";
        TimeText   = $"Time: {_game.ElapsedTime:mm\\:ss}";
        IsGameOver = _game.IsGameOver;
        IsPaused   = _game.IsPaused;
    }

    private void TickAI()
    {
        // _ai es ISnakeAI — no importa si es DQN, Hamiltonian u otro
        var dir = _ai.GetNextMove(_game);

        if (!DirectionHelper.IsOpposite(_game.Snake.CurrentDirection, dir))
            _game.EnqueueDirection(dir);
    }

    // ── Training control ───────────────────────────────────────────────────

    /// <summary>
    /// Arranca el entrenamiento headless en background.
    /// El agente sigue respondiendo en el GameLoop via _inferenceNet
    /// mientras la online network se optimiza en el background Task.
    /// </summary>
    private async void StartTraining()
    {
        if (_trainer == null || IsTraining) return;

        IsTraining = true;
        _trainCts  = new CancellationTokenSource();

        try
        {
            // 5000 episodios — ajusta según tiempo disponible
            await _trainer.RunAsync(episodes: 5000, ct: _trainCts.Token);
        }
        catch (OperationCanceledException)
        {
            // Parada limpia — normal
        }
        finally
        {
            IsTraining = false;
            _trainCts.Dispose();
            _trainCts = null;
        }
    }

    private void StopTraining()
    {
        _trainCts?.Cancel();
        _trainer?.Stop();
    }

    private void SaveWeights()
    {
        _dqnAgent?.Save("src/DQN/weights.bin");
    }

    // ── EpisodeCompleted — viene del training thread ───────────────────────

    private void OnEpisodeCompleted(TrainingStats stats)
    {
        // CRÍTICO: este callback llega desde el background Task,
        // no desde el UI thread. Dispatcher.Invoke para tocar bindings.
        Application.Current.Dispatcher.Invoke(() =>
        {
            TrainingText = $"Ep {stats.Episode} | " +
                           $"Score {stats.Score} | "  +
                           $"ε {stats.Epsilon:F3} | "  +
                           $"R {stats.TotalReward:F1}";
        });
    }

    // ── Input ──────────────────────────────────────────────────────────────

    private void OnKeyDown(KeyEventArgs? e)
    {
        if (e == null) return;

        switch (e.Key)
        {
            case Key.Enter:
                StartNewGame();
                return;

            case Key.Space:
                _game.TogglePause();
                return;

            case Key.T:
                // Toggle entrenamiento con la tecla T
                if (IsTraining) StopTraining();
                else            StartTraining();
                return;

            case Key.S:
                SaveWeights();
                return;
        }

        if (_game.IsPaused || _game.IsGameOver) return;

        // Movimiento manual — desactiva AI si el jugador toma el control
        Direction? dir = e.Key switch
        {
            Key.Up    => Direction.Up,
            Key.Down  => Direction.Down,
            Key.Left  => Direction.Left,
            Key.Right => Direction.Right,
            _         => null
        };

        if (dir.HasValue &&
            !DirectionHelper.IsOpposite(_game.Snake.CurrentDirection, dir.Value))
        {
            _game.EnqueueDirection(dir.Value);
        }
    }
}