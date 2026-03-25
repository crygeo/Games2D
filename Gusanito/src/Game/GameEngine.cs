using Gusanito.Config;
using Gusanito.Enum;
using Gusanito.Helpers;
using Gusanito.Models;

namespace Gusanito.Game;

public class GameEngine
{
    public bool IsGameOver { get; private set; } = true;
    public bool IsPaused { get; private set; }
    
    private readonly GameSettings _settings;
    
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
        // Consumir un input al inicio del tick
        if (_inputQueue.Count > 0)
            Snake.CurrentDirection = _inputQueue.Dequeue();

        var nextHead = Snake.GetNextHeadPosition(); // 👈 clave

        //Válida su proximo movimiento.
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

        // ✅ ahora sí mover con contexto correcto
        Snake.Move(shouldGrow);

        if (shouldGrow)
        {
            Map[nextHead.X, nextHead.Y] = CellType.Empty;
            GenerateFood();
        }
    }

    private void GenerateFood()
    {
        int x, y;

        do
        {
            x = _random.Next(_settings.Width);
            y = _random.Next(_settings.Height);
        }
        while (Map[x, y] != CellType.Empty);

        Map[x, y] = CellType.Food;
        Food = new Position(x, y);
    }

    public void NewGame()
    {
        if(!IsGameOver)
            return;
        
        IsGameOver = false;
        Snake = new Snake();

        InitializeMap();
        GenerateWalls();
        
        GenerateFood();
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

    
}