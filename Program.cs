using System.Text;

namespace SnakeGame;

internal static class Program
{
    private static void Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;

        if (args.Contains("--demo", StringComparer.OrdinalIgnoreCase))
        {
            GameRenderer.RenderDemoFrame();
            return;
        }

        Console.CursorVisible = false;
        var game = new Game();

        while (!game.ShouldExit)
        {
            while (Console.KeyAvailable)
            {
                game.HandleKey(Console.ReadKey(intercept: true).Key);
            }

            game.Update();
            GameRenderer.Render(game);
            Thread.Sleep(game.FrameDelay);
        }

        Console.CursorVisible = true;
        Console.Clear();
        Console.WriteLine("Игра завершена. Спасибо за игру!");
    }
}

internal enum Direction
{
    Up,
    Down,
    Left,
    Right
}

internal readonly record struct Cell(int X, int Y);

internal sealed class Game
{
    public const int Width = 40;
    public const int Height = 20;

    private readonly Random _random = new();
    private readonly LinkedList<Cell> _snake = new();
    private readonly HashSet<Cell> _occupiedCells = new();

    private Direction _direction = Direction.Right;
    private Direction _requestedDirection = Direction.Right;
    private Cell _food;

    public Game()
    {
        Restart();
    }

    public int Score { get; private set; }
    public bool IsPaused { get; private set; }
    public bool IsGameOver { get; private set; }
    public bool ShouldExit { get; private set; }
    public int FrameDelay => Math.Max(55, 150 - Score * 5);
    public Cell Food => _food;
    public Cell Head => _snake.First!.Value;
    public IEnumerable<Cell> Snake => _snake;

    public void HandleKey(ConsoleKey key)
    {
        switch (key)
        {
            case ConsoleKey.UpArrow:
            case ConsoleKey.W:
                RequestDirection(Direction.Up);
                break;
            case ConsoleKey.DownArrow:
            case ConsoleKey.S:
                RequestDirection(Direction.Down);
                break;
            case ConsoleKey.LeftArrow:
            case ConsoleKey.A:
                RequestDirection(Direction.Left);
                break;
            case ConsoleKey.RightArrow:
            case ConsoleKey.D:
                RequestDirection(Direction.Right);
                break;
            case ConsoleKey.P:
                IsPaused = !IsPaused;
                break;
            case ConsoleKey.R:
                Restart();
                break;
            case ConsoleKey.Escape:
                ShouldExit = true;
                break;
        }
    }

    public void Update()
    {
        if (IsPaused || IsGameOver)
        {
            return;
        }

        _direction = _requestedDirection;
        var nextHead = GetNextCell(Head, _direction);
        var eatsFood = nextHead == _food;
        var tail = _snake.Last!.Value;

        if (IsWallCollision(nextHead) || IsSelfCollision(nextHead, tail, eatsFood))
        {
            IsGameOver = true;
            return;
        }

        _snake.AddFirst(nextHead);
        _occupiedCells.Add(nextHead);

        if (eatsFood)
        {
            Score++;
            SpawnFood();
        }
        else
        {
            _snake.RemoveLast();
            _occupiedCells.Remove(tail);
        }
    }

    public bool ContainsSnake(Cell cell) => _occupiedCells.Contains(cell);

    public bool IsHead(Cell cell) => Head == cell;

    private void Restart()
    {
        _snake.Clear();
        _occupiedCells.Clear();

        var center = new Cell(Width / 2, Height / 2);
        var startCells = new[]
        {
            center,
            new Cell(center.X - 1, center.Y),
            new Cell(center.X - 2, center.Y)
        };

        foreach (var cell in startCells)
        {
            _snake.AddLast(cell);
            _occupiedCells.Add(cell);
        }

        Score = 0;
        IsPaused = false;
        IsGameOver = false;
        _direction = Direction.Right;
        _requestedDirection = Direction.Right;
        SpawnFood();
    }

    private void RequestDirection(Direction direction)
    {
        if (IsOpposite(direction, _direction))
        {
            return;
        }

        _requestedDirection = direction;
    }

    private void SpawnFood()
    {
        var freeCells = new List<Cell>();

        for (var y = 0; y < Height; y++)
        {
            for (var x = 0; x < Width; x++)
            {
                var cell = new Cell(x, y);
                if (!_occupiedCells.Contains(cell))
                {
                    freeCells.Add(cell);
                }
            }
        }

        if (freeCells.Count == 0)
        {
            IsGameOver = true;
            return;
        }

        _food = freeCells[_random.Next(freeCells.Count)];
    }

    private bool IsWallCollision(Cell cell)
    {
        return cell.X < 0 || cell.X >= Width || cell.Y < 0 || cell.Y >= Height;
    }

    private bool IsSelfCollision(Cell nextHead, Cell tail, bool eatsFood)
    {
        return _occupiedCells.Contains(nextHead) && (eatsFood || nextHead != tail);
    }

    private static Cell GetNextCell(Cell head, Direction direction)
    {
        return direction switch
        {
            Direction.Up => new Cell(head.X, head.Y - 1),
            Direction.Down => new Cell(head.X, head.Y + 1),
            Direction.Left => new Cell(head.X - 1, head.Y),
            Direction.Right => new Cell(head.X + 1, head.Y),
            _ => head
        };
    }

    private static bool IsOpposite(Direction requested, Direction current)
    {
        return requested switch
        {
            Direction.Up => current == Direction.Down,
            Direction.Down => current == Direction.Up,
            Direction.Left => current == Direction.Right,
            Direction.Right => current == Direction.Left,
            _ => false
        };
    }
}

internal static class GameRenderer
{
    public static void Render(Game game)
    {
        Console.SetCursorPosition(0, 0);
        Console.Write(BuildFrame(game));
    }

    public static void RenderDemoFrame()
    {
        var game = new Game();

        for (var i = 0; i < 8; i++)
        {
            game.Update();
        }

        Console.Write(BuildFrame(game));
    }

    private static string BuildFrame(Game game)
    {
        var output = new StringBuilder();
        output.AppendLine($"+{new string('-', Game.Width)}+");

        for (var y = 0; y < Game.Height; y++)
        {
            output.Append('|');

            for (var x = 0; x < Game.Width; x++)
            {
                var cell = new Cell(x, y);
                output.Append(GetCellSymbol(game, cell));
            }

            output.AppendLine("|");
        }

        output.AppendLine($"+{new string('-', Game.Width)}+");
        output.AppendLine($"Счет: {game.Score} | Управление: стрелки/WASD | P - пауза | R - заново | Esc - выход");

        if (game.IsPaused)
        {
            output.AppendLine("Пауза. Нажмите P, чтобы продолжить.");
        }
        else if (game.IsGameOver)
        {
            output.AppendLine("Игра окончена. Нажмите R для новой игры или Esc для выхода.");
        }
        else
        {
            output.AppendLine("Собирайте еду (*) и не сталкивайтесь со стенами или собственным телом.");
        }

        return output.ToString();
    }

    private static char GetCellSymbol(Game game, Cell cell)
    {
        if (game.IsHead(cell))
        {
            return '@';
        }

        if (game.ContainsSnake(cell))
        {
            return 'O';
        }

        if (game.Food == cell)
        {
            return '*';
        }

        return ' ';
    }
}
