using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Gusanito.Config;
using Gusanito.Enum;

namespace Gusanito.Game;

public class GameRenderer
{
    private readonly Canvas _canvas;
    private readonly GameSettings _settings;

    public GameRenderer(Canvas canvas, GameSettings settings)
    {
        _canvas = canvas;
        _settings = settings;
    }

    public void Draw(GameEngine game)
    {
        _canvas.Children.Clear();

        DrawMap(game);
        DrawSnake(game);
    }
    
    private void DrawMap(GameEngine game)
    {
        for (int x = 0; x < _settings.Width; x++)
        {
            for (int y = 0; y < _settings.Height; y++)
            {
                var cell = game.Map[x, y];

                if (cell == CellType.Empty)
                    continue;

                var rect = new Rectangle
                {
                    Width = GameConstants.CellSize,
                    Height = GameConstants.CellSize,
                    Fill = cell switch
                    {
                        CellType.Wall => Brushes.Gray,
                        CellType.Food => Brushes.Red,
                        _ => Brushes.Transparent
                    }
                };

                Canvas.SetLeft(rect, x * GameConstants.CellSize);
                Canvas.SetTop(rect, y * GameConstants.CellSize);

                _canvas.Children.Add(rect);
            }
        }
    }
    
    private void DrawSnake(GameEngine game)
    {
        foreach (var part in game.Snake.Body)
        {
            var rect = new Rectangle
            {
                Width = GameConstants.CellSize,
                Height = GameConstants.CellSize,
                Fill = Brushes.Green
            };

            Canvas.SetLeft(rect, part.X * GameConstants.CellSize);
            Canvas.SetTop(rect, part.Y * GameConstants.CellSize);

            _canvas.Children.Add(rect);
        }
    }
}