using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using Gusanito.Config;
using Gusanito.Enum;
using Gusanito.Game;
using Gusanito.Helpers;
using Gusanito.Models;

namespace Gusanito;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private DispatcherTimer _timer;
    private GameEngine _game;
    public GameSettings Settings { get; }
    
    //private GameRenderer _renderer;
    private WriteableBitmapRenderer _renderer;
    

    private DateTime _lastFrameTime;
    private double _accumulator = 0;
    private double _tickRate; // en segundos
    
    public MainWindow()
    {
        InitializeComponent();
        
        
        Settings = GameSettingsLoader.LoadOrDefault("src/Config/gameSettings.json");
        
        _game = new GameEngine(Settings);

        //_renderer = new GameRenderer(GameImage, Settings);
        _renderer = new WriteableBitmapRenderer(
            Settings.Width,
            Settings.Height,
            GameConstants.CellSize
        );

        GameImage.Source = _renderer.Bitmap;
        
        _tickRate = Settings.SpeedMs / 1000.0;

        _lastFrameTime = DateTime.Now;
        CompositionTarget.Rendering += GameLoop;
    }
    
    private void GameLoop(object sender, EventArgs e)
    {
        var now = DateTime.Now;
        var deltaTime = (now - _lastFrameTime).TotalSeconds;
        _lastFrameTime = now;

        _accumulator += deltaTime;

        while (_accumulator >= _tickRate)
        {
            if (!_game.IsGameOver && !_game.IsPaused)
            {
                _game.Update(); // lógica en grid
            }

            _accumulator -= _tickRate;
        }

        float t = _game.IsGameOver ? 1f : (float)(_accumulator / _tickRate);

        _renderer.Draw(_game, t); // 👈 ahora con interpolación
    }
    
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        
        if (e.Key == Key.Enter)
        {
            _game.NewGame();
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
            _game.Snake.CurrentDirection = newDirection;
        }
    }
}