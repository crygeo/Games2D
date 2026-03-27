using System.Drawing;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Gusanito.Config;
using Gusanito.Enum;
using Gusanito.Game;
using Gusanito.Helpers;
using Gusanito.Interfaz;
using Gusanito.Rendering;
using Gusanito.SAI;

namespace Gusanito.ViewModel;

public partial class MainVM : ObservableObject
{
    private DispatcherTimer _timer;
    private GameEngine _game;
    public GameSettings Settings { get; }

    //private GameRenderer _renderer;
    private ISnakeRenderer _renderer;


    private DateTime _lastFrameTime;
    private double _accumulator = 0;
    private double _tickRate; // en segundos

    public ICommand OnKeyDownCommand => new RelayCommand<KeyEventArgs>(OnKeyDown);

    public WriteableBitmap GameImage { get; }

    [ObservableProperty] private string scoreText;

    [ObservableProperty] private string timeText;

    [ObservableProperty] private bool isGameOver;

    [ObservableProperty] private bool isPaused;

    private bool _useAI = true;

    private readonly AsyncAIRunner _aiRunner;
    private readonly HamiltonianAI _hamiltonianAI;

    public MainVM()
    {
        Settings = GameSettingsLoader.LoadOrDefault("src/Config/gameSettings.json");

        _game = new GameEngine(Settings);

        #region IA

        _hamiltonianAI = new HamiltonianAI(
            shortcutEvaluator: new ShortcutEvaluator(dangerRatio: 0.6f, minReachableRatio: 0.35f),
            buildTimeout: TimeSpan.FromSeconds(5));

        _aiRunner = new AsyncAIRunner(_hamiltonianAI, tickBudget: TimeSpan.FromMilliseconds(80));

        _ = _aiRunner.RequestCycleRebuildAsync(_game);

        #endregion

        int cellSize = (int)(800 / ((Settings.Width + Settings.Height) / 2));


        // Solid: Pruevas
        _renderer = new SolidColorRenderer(Settings.Width, Settings.Height, cellSize, laneSize: Settings.LaneSize);

        // Sprite: Funciona pero sin interpolacion
        // var source    = new BitmapImage(new Uri("src/img/snake.png", UriKind.RelativeOrAbsolute));
        // var tilemap   = new Tilemap(source, 64, 64);
        // var mapper    = new SnakeTileMapper(tilemap);
        // _renderer     = new SpriteRenderer(Settings.Width, Settings.Height, GameConstants.CellSize, 64, mapper);

        //_renderer = new LineRenderer(Settings.Width, Settings.Height, GameConstants.CellSize, 15);

        //_renderer = new SnakeRendererPro(Settings.Width, Settings.Height, GameConstants.CellSize, 10);

        //_renderer = new CapsuleSnakeRenderer(Settings.Width, Settings.Height, GameConstants.CellSize, 10);

        //_renderer = new PathSnakeRenderer(Settings.Width, Settings.Height, GameConstants.CellSize, 10);

        GameImage = _renderer.Bitmap;
        ScoreText = _game.Score.ToString();
        TimeText = _game.ElapsedTime.ToString(@"mm\:ss");
        IsGameOver = _game.IsGameOver;
        IsPaused = _game.IsPaused;

        _tickRate = Settings.SpeedMs / 1000.0;

        _lastFrameTime = DateTime.Now;
        CompositionTarget.Rendering += GameLoop;
    }

    private void GameLoop(object sender, EventArgs e)
    {
        var now = DateTime.Now;
        var deltaTime = (now - _lastFrameTime).TotalSeconds;

        if (!_game.IsGameOver && !_game.IsPaused)
        {
            _game.ElapsedTime += TimeSpan.FromSeconds(deltaTime);
        }

        _lastFrameTime = now;

        _accumulator += deltaTime;

        while (_accumulator >= _tickRate)
        {
            if (!_game.IsGameOver && !_game.IsPaused)
            {
                if (_useAI)
                {
                    var newDirection = _aiRunner.GetNextMove(_game);

                    if (!DirectionHelper.IsOpposite(_game.Snake.CurrentDirection, newDirection))
                        _game.EnqueueDirection(newDirection);
                }
            }

            _game.Update(); // lógica en grid

            _accumulator -= _tickRate;
        }

        float t = _game.IsGameOver ? 1f : (float)(_accumulator / _tickRate);

        _renderer.Draw(_game, t); // 👈 ahora con interpolación

        ScoreText = $"Score: {_game.Score}";
        TimeText = $"Time: {_game.ElapsedTime:mm\\:ss}";
        IsGameOver = _game.IsGameOver;
        IsPaused = _game.IsPaused;
    }

    private void OnKeyDown(KeyEventArgs? e)
    {
        if (e == null)
            return;

        if (e.Key == Key.Enter)
        {
            _game.NewGame();
            _ = _aiRunner.RequestCycleRebuildAsync(_game);
            return;
        }

        if (e.Key == Key.Space)
        {
            _game.TogglePause();
            return;
        }

        if (_game.IsPaused || _game.IsGameOver)
            return;

        Direction newDirection = _game.Snake.CurrentDirection;
        switch (e.Key)
        {
            case Key.Up:
                newDirection = Direction.Up;
                break;
            case Key.Down:
                newDirection = Direction.Down;
                break;
            case Key.Left:
                newDirection = Direction.Left;
                break;
            case Key.Right:
                newDirection = Direction.Right;
                break;
        }

        // Evitamos que la serpiente se mueva en dirección opuesta a la actual, lo que causaría una colisión inmediata
        if (!DirectionHelper.IsOpposite(_game.Snake.CurrentDirection, newDirection))
        {
            _game.EnqueueDirection(newDirection);
        }
    }
    
    public void Dispose()
   {
       CompositionTarget.Rendering -= GameLoop;
       _aiRunner.Dispose();
   }
}