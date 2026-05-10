using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AmobaGame.UI.ViewModels;

public enum CellState
{
    Empty,
    X,
    O
}

public enum MatchStatus
{
    NotStarted,
    InProgress,
    XWon,
    OWon,
    Draw
}

public sealed class CellViewModel : ObservableObject
{
    private string _symbol = ".";

    public int Row { get; }
    public int Column { get; }

    public string Symbol
    {
        get => _symbol;
        set => SetProperty(ref _symbol, value);
    }

    public IRelayCommand ClickCommand { get; }

    public CellViewModel(int row, int column, Action<CellViewModel> onClick)
    {
        Row = row;
        Column = column;
        ClickCommand = new RelayCommand(() => onClick(this));
    }
}

public sealed class SaveModel
{
    public int Size { get; set; }
    public int RequiredToWin { get; set; }
    public string[] BoardRows { get; set; } = Array.Empty<string>();
    public string CurrentPlayer { get; set; } = "X";
    public string Status { get; set; } = nameof(MatchStatus.InProgress);
    public int XMoves { get; set; }
    public int OMoves { get; set; }
    public DateTime StartedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
}

public partial class MainWindowViewModel : ViewModelBase
{
    private const int MinSize = 3;
    private const int MaxSize = 25;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private CellState[,] _board = new CellState[0, 0];
    private int _boardSize;
    private int _requiredToWin;
    private CellState _currentPlayer;
    private MatchStatus _status;
    private int _xMoves;
    private int _oMoves;
    private DateTime _startedAtUtc;
    private DateTime? _completedAtUtc;

    [ObservableProperty]
    private string _boardSizeInput = "10";

    [ObservableProperty]
    private string _moveRowInput = "1";

    [ObservableProperty]
    private string _moveColumnInput = "1";

    [ObservableProperty]
    private string _savePath = "amoba-save.json";

    [ObservableProperty]
    private string _statusMessage = "Udv! Indits uj jatekot vagy jatsz a tablan.";

    [ObservableProperty]
    private string _currentPlayerText = "-";

    [ObservableProperty]
    private string _matchStatusText = "Nincs aktiv jatek";

    [ObservableProperty]
    private string _statisticsText = "-";

    [ObservableProperty]
    private int _boardColumns = 3;

    public ObservableCollection<CellViewModel> Cells { get; } = new();

    public MainWindowViewModel()
    {
        CreateNewGame(10);
    }

    [RelayCommand]
    private void NewGame()
    {
        if (!int.TryParse(BoardSizeInput, out int size))
        {
            StatusMessage = "Hibas meret. Egesz szamot adj meg.";
            return;
        }

        if (size < MinSize || size > MaxSize)
        {
            StatusMessage = $"A meret {MinSize} es {MaxSize} kozott lehet.";
            return;
        }

        CreateNewGame(size);
        StatusMessage = $"Uj jatek indult: {size}x{size}.";
    }

    [RelayCommand]
    private void SaveGame()
    {
        if (_status == MatchStatus.NotStarted)
        {
            StatusMessage = "Nincs aktiv jatek a menteshez.";
            return;
        }

        if (string.IsNullOrWhiteSpace(SavePath))
        {
            StatusMessage = "Adj meg mentesi fajlnevet.";
            return;
        }

        try
        {
            string fullPath = Path.GetFullPath(SavePath.Trim());
            SaveModel model = ToSaveModel();
            string json = JsonSerializer.Serialize(model, JsonOptions);
            File.WriteAllText(fullPath, json);
            StatusMessage = $"Mentes kesz: {fullPath}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Mentesi hiba: {ex.Message}";
        }
    }

    [RelayCommand]
    private void LoadGame()
    {
        if (string.IsNullOrWhiteSpace(SavePath))
        {
            StatusMessage = "Adj meg betoltendo fajlnevet.";
            return;
        }

        try
        {
            string fullPath = Path.GetFullPath(SavePath.Trim());
            if (!File.Exists(fullPath))
            {
                StatusMessage = "A fajl nem letezik.";
                return;
            }

            string json = File.ReadAllText(fullPath);
            SaveModel? model = JsonSerializer.Deserialize<SaveModel>(json, JsonOptions);
            if (model is null)
            {
                StatusMessage = "A mentes ures vagy serult.";
                return;
            }

            LoadFromSaveModel(model);
            StatusMessage = "Mentes sikeresen betoltve.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Betoltesi hiba: {ex.Message}";
        }
    }

    [RelayCommand]
    private void XMove()
    {
        TryMoveFromInputs(CellState.X);
    }

    [RelayCommand]
    private void OMove()
    {
        TryMoveFromInputs(CellState.O);
    }

    private void TryMoveFromInputs(CellState player)
    {
        if (!TryReadInputCoordinates(out int row, out int col))
        {
            return;
        }

        TryMakeMove(row, col, player, enforceTurn: true);
    }

    private bool TryReadInputCoordinates(out int row, out int col)
    {
        row = -1;
        col = -1;

        if (!int.TryParse(MoveRowInput, out int rowOneBased) || !int.TryParse(MoveColumnInput, out int colOneBased))
        {
            StatusMessage = "Sor/Oszlop mezokben egesz szam kell.";
            return false;
        }

        row = rowOneBased - 1;
        col = colOneBased - 1;

        if (!IsInsideBoard(row, col))
        {
            StatusMessage = "A megadott koordinata kivul esik a tablan.";
            return false;
        }

        return true;
    }

    private void OnCellClicked(CellViewModel cell)
    {
        MoveRowInput = (cell.Row + 1).ToString();
        MoveColumnInput = (cell.Column + 1).ToString();
        TryMakeMove(cell.Row, cell.Column, _currentPlayer, enforceTurn: true);
    }

    private void CreateNewGame(int size)
    {
        _boardSize = size;
        _requiredToWin = Math.Min(5, size);
        _board = new CellState[size, size];
        _currentPlayer = CellState.X;
        _status = MatchStatus.InProgress;
        _xMoves = 0;
        _oMoves = 0;
        _startedAtUtc = DateTime.UtcNow;
        _completedAtUtc = null;

        RebuildCells();
        RefreshUiState();
    }

    private void RebuildCells()
    {
        Cells.Clear();
        BoardColumns = _boardSize;

        for (int r = 0; r < _boardSize; r++)
        {
            for (int c = 0; c < _boardSize; c++)
            {
                Cells.Add(new CellViewModel(r, c, OnCellClicked));
            }
        }

        RefreshCellSymbols();
    }

    private void RefreshCellSymbols()
    {
        foreach (CellViewModel cell in Cells)
        {
            cell.Symbol = CellToChar(_board[cell.Row, cell.Column]).ToString();
        }
    }

    private void TryMakeMove(int row, int col, CellState requestedPlayer, bool enforceTurn)
    {
        if (_status != MatchStatus.InProgress)
        {
            StatusMessage = "A jatek mar veget ert. Indits uj jatekot vagy tolts be mentest.";
            return;
        }

        if (!IsInsideBoard(row, col))
        {
            StatusMessage = "A koordinata kivul esik a tablan.";
            return;
        }

        if (requestedPlayer == CellState.Empty)
        {
            StatusMessage = "Ures jellel nem lehet lepni.";
            return;
        }

        if (enforceTurn && requestedPlayer != _currentPlayer)
        {
            StatusMessage = $"Most {CellToChar(_currentPlayer)} kovetkezik.";
            return;
        }

        if (_board[row, col] != CellState.Empty)
        {
            StatusMessage = "Erre a mezore mar leptek.";
            return;
        }

        _board[row, col] = requestedPlayer;
        if (requestedPlayer == CellState.X)
        {
            _xMoves++;
        }
        else
        {
            _oMoves++;
        }

        if (HasWinningLine(row, col, requestedPlayer))
        {
            _status = requestedPlayer == CellState.X ? MatchStatus.XWon : MatchStatus.OWon;
            _completedAtUtc = DateTime.UtcNow;
            StatusMessage = $"Jatek vege: {CellToChar(requestedPlayer)} nyert.";
        }
        else if (_xMoves + _oMoves == _boardSize * _boardSize)
        {
            _status = MatchStatus.Draw;
            _completedAtUtc = DateTime.UtcNow;
            StatusMessage = "Jatek vege: dontetlen.";
        }
        else
        {
            _currentPlayer = requestedPlayer == CellState.X ? CellState.O : CellState.X;
            StatusMessage = $"Lepes rogzitve. Most {CellToChar(_currentPlayer)} jon.";
        }

        RefreshCellSymbols();
        RefreshUiState();
    }

    private bool HasWinningLine(int row, int col, CellState player)
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
            count += CountDirection(row, col, dr, dc, player);
            count += CountDirection(row, col, -dr, -dc, player);

            if (count >= _requiredToWin)
            {
                return true;
            }
        }

        return false;
    }

    private int CountDirection(int row, int col, int dr, int dc, CellState player)
    {
        int count = 0;
        int r = row + dr;
        int c = col + dc;

        while (IsInsideBoard(r, c) && _board[r, c] == player)
        {
            count++;
            r += dr;
            c += dc;
        }

        return count;
    }

    private bool IsInsideBoard(int row, int col)
    {
        return row >= 0 && row < _boardSize && col >= 0 && col < _boardSize;
    }

    private void RefreshUiState()
    {
        CurrentPlayerText = CellToChar(_currentPlayer).ToString();
        MatchStatusText = StatusToText(_status);

        int emptyCount = _boardSize * _boardSize - (_xMoves + _oMoves);
        TimeSpan elapsed = (_completedAtUtc ?? DateTime.UtcNow) - _startedAtUtc;

        StatisticsText =
            $"Tabla: {_boardSize}x{_boardSize} | " +
            $"Nyereshez kell: {_requiredToWin} | " +
            $"X lepes: {_xMoves} | O lepes: {_oMoves} | " +
            $"Ures mezo: {emptyCount} | " +
            $"Eltelt ido: {elapsed:hh\\:mm\\:ss}";
    }

    private SaveModel ToSaveModel()
    {
        string[] rows = new string[_boardSize];

        for (int r = 0; r < _boardSize; r++)
        {
            char[] chars = new char[_boardSize];
            for (int c = 0; c < _boardSize; c++)
            {
                chars[c] = CellToChar(_board[r, c]);
            }

            rows[r] = new string(chars);
        }

        return new SaveModel
        {
            Size = _boardSize,
            RequiredToWin = _requiredToWin,
            BoardRows = rows,
            CurrentPlayer = CellToChar(_currentPlayer).ToString(),
            Status = _status.ToString(),
            XMoves = _xMoves,
            OMoves = _oMoves,
            StartedAtUtc = _startedAtUtc,
            CompletedAtUtc = _completedAtUtc
        };
    }

    private void LoadFromSaveModel(SaveModel model)
    {
        if (model.Size < MinSize || model.Size > MaxSize)
        {
            throw new InvalidDataException($"Ervenytelen tabla meret: {model.Size}.");
        }

        if (model.BoardRows.Length != model.Size || model.BoardRows.Any(r => r.Length != model.Size))
        {
            throw new InvalidDataException("A mentett tabla formaja hibas.");
        }

        CellState[,] board = new CellState[model.Size, model.Size];
        int xCount = 0;
        int oCount = 0;

        for (int r = 0; r < model.Size; r++)
        {
            for (int c = 0; c < model.Size; c++)
            {
                CellState value = CharToCell(model.BoardRows[r][c]);
                board[r, c] = value;

                if (value == CellState.X)
                {
                    xCount++;
                }
                else if (value == CellState.O)
                {
                    oCount++;
                }
            }
        }

        _boardSize = model.Size;
        _requiredToWin = model.RequiredToWin < 3 || model.RequiredToWin > model.Size
            ? Math.Min(5, model.Size)
            : model.RequiredToWin;
        _board = board;
        _currentPlayer = CharToCell(model.CurrentPlayer.FirstOrDefault('X'));
        if (_currentPlayer == CellState.Empty)
        {
            _currentPlayer = CellState.X;
        }

        _status = Enum.TryParse(model.Status, out MatchStatus parsedStatus)
            ? parsedStatus
            : MatchStatus.InProgress;

        _xMoves = model.XMoves > 0 ? model.XMoves : xCount;
        _oMoves = model.OMoves > 0 ? model.OMoves : oCount;
        _startedAtUtc = model.StartedAtUtc == default ? DateTime.UtcNow : model.StartedAtUtc;
        _completedAtUtc = model.CompletedAtUtc;

        BoardSizeInput = _boardSize.ToString();
        RebuildCells();
        RefreshUiState();
    }

    private static char CellToChar(CellState cell)
    {
        return cell switch
        {
            CellState.X => 'X',
            CellState.O => 'O',
            _ => '.'
        };
    }

    private static CellState CharToCell(char c)
    {
        return char.ToUpperInvariant(c) switch
        {
            'X' => CellState.X,
            'O' => CellState.O,
            '.' => CellState.Empty,
            _ => throw new InvalidDataException($"Ismeretlen tabla karakter: '{c}'.")
        };
    }

    private static string StatusToText(MatchStatus status)
    {
        return status switch
        {
            MatchStatus.NotStarted => "Nincs aktiv jatek",
            MatchStatus.InProgress => "Folyamatban",
            MatchStatus.XWon => "X nyert",
            MatchStatus.OWon => "O nyert",
            MatchStatus.Draw => "Dontetlen",
            _ => "Ismeretlen"
        };
    }
}
