using Gusanito.Config;
using Gusanito.Enum;
using Gusanito.Helpers;
using Gusanito.Models;

namespace Gusanito.Game;

public class GameEngine
{
    public bool IsGameOver { get; private set; } = true;
    public bool IsPaused { get; private set; }
    
    public int Score { get; private set; }
    public TimeSpan ElapsedTime { get; set; } = TimeSpan.Zero;
    
    private readonly GameSettings _settings;
    public int Width => _settings.Width;
    public int Height => _settings.Height;
    
    public Snake Snake { get; private set; }
    public Position Food { get; private set; }

    public Direction Direction { get; set; } = Direction.Left;
    private readonly Random _random = new();
    
    public CellType[,] Map { get; private set; }

    private readonly Queue<Direction> _inputQueue = new();
    private const int MaxQueueSize = 2; // evita buffering excesivo
    
    public GameEngine(GameSettings settings)
    {
        _settings = settings;
        
        NewGame();
    }

    private void InitializeMap()
    {
        Map = new CellType[_settings.Width, _settings.Height];

        for (int x = 0; x < _settings.Width; x++)
        {
            for (int y = 0; y < _settings.Height; y++)
            {
                Map[x, y] = CellType.Empty;
            }
        }
    }
    
    private void GenerateWalls()
    {
        for (int x = 0; x < _settings.Width; x++)
        {
            Map[x, 0] = CellType.Wall;
            Map[x, _settings.Height - 1] = CellType.Wall;
        }

        for (int y = 0; y < _settings.Height; y++)
        {
            Map[0, y] = CellType.Wall;
            Map[_settings.Width - 1, y] = CellType.Wall;
        }
    }
    
    public void Update()
    {
        
        // Consumir input solo cuando estamos en un múltiplo de 5
        bool canTurn = Snake.Head.X % _settings.LaneSize == 0 && Snake.Head.Y % _settings.LaneSize == 0;
    
        if (canTurn && _inputQueue.Count > 0)
            Snake.CurrentDirection = _inputQueue.Dequeue();

        var nextHead = Snake.GetNextHeadPosition();

        if (IsSelfCollision(nextHead))
        {
            IsGameOver = true;
            return;
        }

        var cell = Map[nextHead.X, nextHead.Y];

        if (cell == CellType.Wall)
        {
            IsGameOver = true;
            return;
        }

        bool shouldGrow = cell == CellType.Food;

        Snake.Move(shouldGrow);

        if (shouldGrow)
        {
            Map[nextHead.X, nextHead.Y] = CellType.Empty;
            for (int i = 0; i < _settings.LaneSize - 1; i++)
                Snake.Move(grow: true);
            
            GenerateFood();
            Score++;
        }

        Snake.JustRespawned = false;
    }

    private void GenerateFood()
    {
        int x, y;

        do
        {
            x = _random.Next(1, _settings.WidthMap  / _settings.LaneSize) * _settings.LaneSize + _settings.Walls;
            y = _random.Next(1, _settings.HeightMap / _settings.LaneSize) * _settings.LaneSize + _settings.Walls;
        }
        while (Map[x, y] != CellType.Empty || IsOnSnake(x, y));

        Map[x, y] = CellType.Food;
        Food = new Position(x, y);
    }

    private bool IsOnSnake(int x, int y)
    {
        return Snake.Body.Any(p => p.X == x && p.Y == y);
    }

    

    public void NewGame()
    {
        Score = 0;
        ElapsedTime = TimeSpan.Zero;
        
        if(!IsGameOver)
            return;
    
        IsGameOver = false;

        // Snake.cs
        int cx = 5;
        int cy = (_settings.Height / 2 / _settings.LaneSize) * _settings.LaneSize;
        
        Snake = new Snake(cx, cy);

        InitializeMap();
        GenerateWalls();
    
        GenerateFood();
    
        for (int i = 0; i < (_settings.ScorePerFood - 1) * _settings.LaneSize; i++)
        {
            Snake.Move(grow: true);
        }

        // 🔴 CLAVE: sincronizar estados
        Snake.PreviousBody = Snake.Body
            .Select(p => new Position(p.X, p.Y))
            .ToList();

        Snake.JustRespawned = true;
    }
    
    
    private bool IsSelfCollision(Position head)
    {
        return Snake.Body
            .Skip(1)
            .Any(p => p.X == head.X && p.Y == head.Y);
    }
   
    public void TogglePause()
    {
        if (IsGameOver) return;

        IsPaused = !IsPaused;
    }
    
    public void EnqueueDirection(Direction newDirection)
    {
        // La dirección a comparar es la última encolada, o la actual si la cola está vacía
        var lastDirection = _inputQueue.Count > 0 
            ? _inputQueue.Last() 
            : Snake.CurrentDirection;

        if (!DirectionHelper.IsOpposite(lastDirection, newDirection) && lastDirection != newDirection)
        {
            if (_inputQueue.Count < MaxQueueSize)
                _inputQueue.Enqueue(newDirection);
        }
    }

    public GameEngine Clone()
    {
        var clone = new GameEngine(this._settings);

        // copiar mapa
        clone.Map = (CellType[,])this.Map.Clone();

        // copiar snake
        clone.Snake = this.Snake.Clone();

        // copiar estado
        clone.Score = this.Score;
        clone.IsGameOver = this.IsGameOver;
        clone.IsPaused = this.IsPaused;

        return clone;
    }
}