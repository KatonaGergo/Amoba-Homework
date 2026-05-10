using System.Text.Json;

internal enum Cell
{
    Empty,
    X,
    O
}

internal enum GameStatus
{
    InProgress,
    XWon,
    OWon,
    Draw
}

internal sealed class GameState
{
    public int Size { get; init; }
    public int RequiredToWin { get; init; }
    public Cell[,] Board { get; init; } = new Cell[0, 0];
    public Cell CurrentPlayer { get; set; }
    public GameStatus Status { get; set; }
    public int XMoves { get; set; }
    public int OMoves { get; set; }
    public DateTime StartedAtUtc { get; init; }
    public DateTime? CompletedAtUtc { get; set; }
}

internal sealed class SaveModel
{
    public int Size { get; set; }
    public int RequiredToWin { get; set; }
    public string[] BoardRows { get; set; } = Array.Empty<string>();
    public string CurrentPlayer { get; set; } = "X";
    public string Status { get; set; } = nameof(GameStatus.InProgress);
    public int XMoves { get; set; }
    public int OMoves { get; set; }
    public DateTime StartedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
}

internal static class Program
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private static GameState? _game;

    private static void Main()
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        while (true)
        {
            PrintMenu();
            Console.Write("Választás: ");
            string? input = Console.ReadLine();

            if (!int.TryParse(input, out int menuItem))
            {
                WriteError("Számot adj meg a menüből.");
                continue;
            }

            Console.WriteLine();

            switch (menuItem)
            {
                case 1:
                    StartNewGame();
                    break;
                case 2:
                    SaveGame();
                    break;
                case 3:
                    LoadGame();
                    break;
                case 4:
                    MakeMove(Cell.X);
                    break;
                case 5:
                    MakeMove(Cell.O);
                    break;
                case 6:
                    PrintStatistics();
                    break;
                case 7:
                    PrintBoard();
                    break;
                case 0:
                    Console.WriteLine("Kilépés...");
                    return;
                default:
                    WriteError("Nincs ilyen menüpont.");
                    break;
            }

            Console.WriteLine();
        }
    }

    private static void PrintMenu()
    {
        Console.WriteLine("===== AMŐBA =====");
        Console.WriteLine("1 - Új játék");
        Console.WriteLine("2 - Játék mentése");
        Console.WriteLine("3 - Játék betöltése");
        Console.WriteLine("4 - X lépés");
        Console.WriteLine("5 - O lépés");
        Console.WriteLine("6 - Statisztika");
        Console.WriteLine("7 - Tábla megjelenítése");
        Console.WriteLine("0 - Kilépés");
    }

    private static void StartNewGame()
    {
        int size = ReadInt(
            "Tábla mérete (N, legalább 3): ",
            value => value >= 3,
            "N legalább 3 legyen.");

        int requiredToWin = Math.Min(5, size);

        _game = new GameState
        {
            Size = size,
            RequiredToWin = requiredToWin,
            Board = new Cell[size, size],
            CurrentPlayer = Cell.X,
            Status = GameStatus.InProgress,
            XMoves = 0,
            OMoves = 0,
            StartedAtUtc = DateTime.UtcNow,
            CompletedAtUtc = null
        };

        Console.WriteLine($"Új játék indult: {size}x{size}, győzelemhez {requiredToWin} jel kell egy vonalban.");
        PrintBoard();
    }

    private static void MakeMove(Cell requestedPlayer)
    {
        if (!EnsureGame())
        {
            return;
        }

        if (_game!.Status != GameStatus.InProgress)
        {
            WriteError("A játék már véget ért. Indíts új játékot vagy tölts be egy mentést.");
            return;
        }

        if (requestedPlayer != _game.CurrentPlayer)
        {
            WriteError($"Most {CellToChar(_game.CurrentPlayer)} következik.");
            return;
        }

        int row = ReadInt(
            $"Sor (1..{_game.Size}): ",
            value => value >= 1 && value <= _game.Size,
            "A sor kívül esik a táblán.") - 1;

        int col = ReadInt(
            $"Oszlop (1..{_game.Size}): ",
            value => value >= 1 && value <= _game.Size,
            "Az oszlop kívül esik a táblán.") - 1;

        if (_game.Board[row, col] != Cell.Empty)
        {
            WriteError("Erre a mezőre már léptek.");
            return;
        }

        _game.Board[row, col] = requestedPlayer;

        if (requestedPlayer == Cell.X)
        {
            _game.XMoves++;
        }
        else
        {
            _game.OMoves++;
        }

        EvaluateGameAfterMove(row, col, requestedPlayer);
        PrintBoard();

        if (_game.Status == GameStatus.InProgress)
        {
            _game.CurrentPlayer = requestedPlayer == Cell.X ? Cell.O : Cell.X;
            Console.WriteLine($"Következő játékos: {CellToChar(_game.CurrentPlayer)}");
        }
    }

    private static void EvaluateGameAfterMove(int row, int col, Cell player)
    {
        if (!EnsureGame())
        {
            return;
        }

        if (HasWinningLine(_game!, row, col, player))
        {
            _game!.Status = player == Cell.X ? GameStatus.XWon : GameStatus.OWon;
            _game.CompletedAtUtc = DateTime.UtcNow;
            Console.WriteLine($"Játék vége: {CellToChar(player)} győzött!");
            return;
        }

        if (_game!.XMoves + _game.OMoves == _game.Size * _game.Size)
        {
            _game.Status = GameStatus.Draw;
            _game.CompletedAtUtc = DateTime.UtcNow;
            Console.WriteLine("Játék vége: döntetlen.");
        }
    }

    private static bool HasWinningLine(GameState game, int row, int col, Cell player)
    {
        (int dr, int dc)[] directions =
        {
            (0, 1),
            (1, 0),
            (1, 1),
            (1, -1)
        };

        foreach ((int dr, int dc) in directions)
        {
            int count = 1;
            count += CountDirection(game, row, col, dr, dc, player);
            count += CountDirection(game, row, col, -dr, -dc, player);

            if (count >= game.RequiredToWin)
            {
                return true;
            }
        }

        return false;
    }

    private static int CountDirection(GameState game, int row, int col, int dr, int dc, Cell player)
    {
        int count = 0;
        int r = row + dr;
        int c = col + dc;

        while (r >= 0 && r < game.Size && c >= 0 && c < game.Size && game.Board[r, c] == player)
        {
            count++;
            r += dr;
            c += dc;
        }

        return count;
    }

    private static void SaveGame()
    {
        if (!EnsureGame())
        {
            return;
        }

        Console.Write("Mentés fájlneve (pl. mentes.json): ");
        string? fileName = Console.ReadLine();

        if (string.IsNullOrWhiteSpace(fileName))
        {
            WriteError("A fájlnév nem lehet üres.");
            return;
        }

        string fullPath = Path.GetFullPath(fileName);
        SaveModel saveModel = ToSaveModel(_game!);
        string json = JsonSerializer.Serialize(saveModel, JsonOptions);
        File.WriteAllText(fullPath, json);

        Console.WriteLine($"Sikeres mentés: {fullPath}");
    }

    private static void LoadGame()
    {
        Console.Write("Betöltendő fájl neve (pl. mentes.json): ");
        string? fileName = Console.ReadLine();

        if (string.IsNullOrWhiteSpace(fileName))
        {
            WriteError("A fájlnév nem lehet üres.");
            return;
        }

        string fullPath = Path.GetFullPath(fileName);

        if (!File.Exists(fullPath))
        {
            WriteError("A megadott mentésfájl nem létezik.");
            return;
        }

        try
        {
            string json = File.ReadAllText(fullPath);
            SaveModel? model = JsonSerializer.Deserialize<SaveModel>(json, JsonOptions);

            if (model is null)
            {
                WriteError("A mentésfájl tartalma üres vagy hibás.");
                return;
            }

            _game = FromSaveModel(model);
            Console.WriteLine("Mentés sikeresen betöltve.");
            PrintBoard();
        }
        catch (Exception ex)
        {
            WriteError($"Betöltési hiba: {ex.Message}");
        }
    }

    private static SaveModel ToSaveModel(GameState game)
    {
        string[] rows = new string[game.Size];

        for (int r = 0; r < game.Size; r++)
        {
            char[] rowChars = new char[game.Size];
            for (int c = 0; c < game.Size; c++)
            {
                rowChars[c] = CellToChar(game.Board[r, c]);
            }

            rows[r] = new string(rowChars);
        }

        return new SaveModel
        {
            Size = game.Size,
            RequiredToWin = game.RequiredToWin,
            BoardRows = rows,
            CurrentPlayer = CellToChar(game.CurrentPlayer).ToString(),
            Status = game.Status.ToString(),
            XMoves = game.XMoves,
            OMoves = game.OMoves,
            StartedAtUtc = game.StartedAtUtc,
            CompletedAtUtc = game.CompletedAtUtc
        };
    }

    private static GameState FromSaveModel(SaveModel model)
    {
        if (model.Size < 3)
        {
            throw new InvalidDataException("A mentésben a tábla mérete érvénytelen.");
        }

        if (model.BoardRows.Length != model.Size || model.BoardRows.Any(r => r.Length != model.Size))
        {
            throw new InvalidDataException("A mentés táblaformátuma hibás.");
        }

        Cell[,] board = new Cell[model.Size, model.Size];
        int xCount = 0;
        int oCount = 0;

        for (int r = 0; r < model.Size; r++)
        {
            for (int c = 0; c < model.Size; c++)
            {
                Cell cell = CharToCell(model.BoardRows[r][c]);
                board[r, c] = cell;

                if (cell == Cell.X)
                {
                    xCount++;
                }
                else if (cell == Cell.O)
                {
                    oCount++;
                }
            }
        }

        Cell currentPlayer = CharToCell(model.CurrentPlayer.FirstOrDefault('X'));
        GameStatus status = Enum.TryParse<GameStatus>(model.Status, out GameStatus parsed) ? parsed : GameStatus.InProgress;

        return new GameState
        {
            Size = model.Size,
            RequiredToWin = model.RequiredToWin < 3 || model.RequiredToWin > model.Size
                ? Math.Min(5, model.Size)
                : model.RequiredToWin,
            Board = board,
            CurrentPlayer = currentPlayer == Cell.Empty ? Cell.X : currentPlayer,
            Status = status,
            XMoves = model.XMoves > 0 ? model.XMoves : xCount,
            OMoves = model.OMoves > 0 ? model.OMoves : oCount,
            StartedAtUtc = model.StartedAtUtc == default ? DateTime.UtcNow : model.StartedAtUtc,
            CompletedAtUtc = model.CompletedAtUtc
        };
    }

    private static void PrintBoard()
    {
        if (!EnsureGame())
        {
            return;
        }

        Console.WriteLine("Tábla:");
        Console.Write("    ");
        for (int c = 0; c < _game!.Size; c++)
        {
            Console.Write($"{(c + 1),2} ");
        }

        Console.WriteLine();

        for (int r = 0; r < _game.Size; r++)
        {
            Console.Write($"{(r + 1),2}: ");
            for (int c = 0; c < _game.Size; c++)
            {
                Console.Write($" {CellToChar(_game.Board[r, c])} ");
            }

            Console.WriteLine();
        }
    }

    private static void PrintStatistics()
    {
        if (!EnsureGame())
        {
            return;
        }

        DateTime endTime = _game!.CompletedAtUtc ?? DateTime.UtcNow;
        TimeSpan elapsed = endTime - _game.StartedAtUtc;

        Console.WriteLine("Statisztika:");
        Console.WriteLine($"- Tábla mérete: {_game.Size}x{_game.Size}");
        Console.WriteLine($"- Győzelemhez szükséges jelek száma: {_game.RequiredToWin}");
        Console.WriteLine($"- X lépések száma: {_game.XMoves}");
        Console.WriteLine($"- O lépések száma: {_game.OMoves}");
        Console.WriteLine($"- Üres mezők száma: {_game.Size * _game.Size - (_game.XMoves + _game.OMoves)}");
        Console.WriteLine($"- Következő játékos: {CellToChar(_game.CurrentPlayer)}");
        Console.WriteLine($"- Állapot: {StatusToHungarian(_game.Status)}");
        Console.WriteLine($"- Eltelt idő: {elapsed:hh\\:mm\\:ss}");
    }

    private static bool EnsureGame()
    {
        if (_game is not null)
        {
            return true;
        }

        WriteError("Nincs aktív játék. Először indíts új játékot vagy tölts be mentést.");
        return false;
    }

    private static int ReadInt(string prompt, Func<int, bool> validator, string validationError)
    {
        while (true)
        {
            Console.Write(prompt);
            string? input = Console.ReadLine();

            if (!int.TryParse(input, out int value))
            {
                WriteError("Kérlek egy egész számot adj meg.");
                continue;
            }

            if (!validator(value))
            {
                WriteError(validationError);
                continue;
            }

            return value;
        }
    }

    private static char CellToChar(Cell cell)
    {
        return cell switch
        {
            Cell.X => 'X',
            Cell.O => 'O',
            _ => '.'
        };
    }

    private static Cell CharToCell(char c)
    {
        return char.ToUpperInvariant(c) switch
        {
            'X' => Cell.X,
            'O' => Cell.O,
            '.' => Cell.Empty,
            _ => throw new InvalidDataException($"Ismeretlen tábla jel: '{c}'")
        };
    }

    private static string StatusToHungarian(GameStatus status)
    {
        return status switch
        {
            GameStatus.InProgress => "Folyamatban",
            GameStatus.XWon => "X nyert",
            GameStatus.OWon => "O nyert",
            GameStatus.Draw => "Döntetlen",
            _ => "Ismeretlen"
        };
    }

    private static void WriteError(string message)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine(message);
        Console.ResetColor();
    }
}
