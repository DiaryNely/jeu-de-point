using jeuPoint.Models;
using jeuPoint.Services.Rules;
using jeuPoint.Data;
using Microsoft.Extensions.Logging;

namespace jeuPoint.UI;

public sealed partial class MainForm : Form
{
    private readonly IGameRepository _gameRepository;
    private readonly ILogger<MainForm> _logger;
    private readonly Player _playerOne;
    private readonly Player _playerTwo;

    private Game? _game;
    private GameEngine? _gameEngine;
    private bool _isGameStarted;

    private readonly Dictionary<GridCell, long> _boardPoints = new();
    private readonly List<ValidatedLine> _validatedLines = new();

    private readonly GameBoardControl _boardControl;
    private readonly Label _currentPlayerLabel;
    private readonly Label _scoreLabel;
    private readonly PowerGaugeControl _powerGauge;
    private readonly Label _statusLabel;
    private readonly Button _modeButton;
    private readonly Button _startGameButton;
    private readonly Button _saveGameButton;
    private readonly Button _loadGameButton;
    private readonly Button _confirmShotButton;
    private readonly TrackBar _powerTrackBar;
    private readonly NumericUpDown _gridWidthInput;
    private readonly NumericUpDown _gridHeightInput;

    private GameInteractionMode _mode = GameInteractionMode.Place;
    private GridCell? _lockedTarget;
    private readonly System.Windows.Forms.Timer _shotAnimationTimer;
    private int _shotAnimationStep;
    private int _shotAnimationStepsTotal;
    private long _shotAnimationShooterId;
    private GridCell? _shotAnimationTarget;
    private GameActionResult? _pendingShotResult;
    private bool _isShotAnimating;

    public MainForm(IGameRepository gameRepository, ILogger<MainForm> logger)
    {
        _gameRepository = gameRepository;
        _logger = logger;
        _playerOne = new Player(1, "Joueur 1", 0);
        _playerTwo = new Player(2, "Joueur 2", 0);

        Text = "Jeu de grille - WinForms GDI+";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(1100, 780);
        BackColor = Color.FromArgb(248, 249, 251);
        Font = new Font("Segoe UI", 9.5f, FontStyle.Regular, GraphicsUnit.Point);
        KeyPreview = true;
        KeyDown += OnMainFormKeyDown;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Padding = new Padding(16)
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 74));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 26));

        _boardControl = new GameBoardControl
        {
            Dock = DockStyle.Fill,
            GridWidth = 16,
            GridHeight = 10,
            CurrentPlayerId = _playerOne.Id,
            Mode = _mode
        };
        _boardControl.CellClicked += async (_, cell) => await HandleBoardClickAsync(cell);

        var rightPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 15,
            BackColor = Color.White,
            Padding = new Padding(14)
        };

        rightPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
        rightPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        rightPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        rightPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        rightPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        rightPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));
        rightPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        rightPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
        rightPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        rightPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        rightPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        rightPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        rightPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        rightPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var titleLabel = new Label
        {
            Dock = DockStyle.Fill,
            Text = "Contrôle de partie",
            Font = new Font("Segoe UI", 12f, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft
        };

        var sizeLabel = new Label
        {
            Dock = DockStyle.Fill,
            Text = "Taille de grille (L x H)",
            TextAlign = ContentAlignment.MiddleLeft
        };

        var sizeLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1
        };
        sizeLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));
        sizeLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20));
        sizeLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));

        _gridWidthInput = new NumericUpDown
        {
            Dock = DockStyle.Fill,
            Minimum = 8,
            Maximum = 40,
            Value = 16
        };

        _gridHeightInput = new NumericUpDown
        {
            Dock = DockStyle.Fill,
            Minimum = 8,
            Maximum = 40,
            Value = 10
        };

        var separatorLabel = new Label
        {
            Dock = DockStyle.Fill,
            Text = "x",
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI", 10f, FontStyle.Bold)
        };

        sizeLayout.Controls.Add(_gridWidthInput, 0, 0);
        sizeLayout.Controls.Add(separatorLabel, 1, 0);
        sizeLayout.Controls.Add(_gridHeightInput, 2, 0);

        _startGameButton = new Button
        {
            Dock = DockStyle.Fill,
            Text = "Démarrer partie",
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(25, 135, 84),
            ForeColor = Color.White
        };
        _startGameButton.FlatAppearance.BorderSize = 0;
        _startGameButton.Click += (_, _) => StartGame();

        _saveGameButton = new Button
        {
            Dock = DockStyle.Fill,
            Text = "Sauvegarder",
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(32, 201, 151),
            ForeColor = Color.White,
            Enabled = false
        };
        _saveGameButton.FlatAppearance.BorderSize = 0;
        _saveGameButton.Click += async (_, _) => await SaveCurrentGameAsync();

        _loadGameButton = new Button
        {
            Dock = DockStyle.Fill,
            Text = "Charger",
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(108, 117, 125),
            ForeColor = Color.White
        };
        _loadGameButton.FlatAppearance.BorderSize = 0;
        _loadGameButton.Click += async (_, _) => await LoadSavedGameAsync();

        _currentPlayerLabel = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Segoe UI", 10f, FontStyle.Bold)
        };

        _scoreLabel = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft
        };

        _modeButton = new Button
        {
            Dock = DockStyle.Fill,
            Text = "Mode : Pose",
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(33, 37, 41),
            ForeColor = Color.White
        };
        _modeButton.FlatAppearance.BorderSize = 0;
        _modeButton.Click += (_, _) => ToggleMode();
        _modeButton.Enabled = false;

        var powerLabel = new Label
        {
            Dock = DockStyle.Fill,
            Text = "Puissance canon",
            TextAlign = ContentAlignment.MiddleLeft
        };

        var powerLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 1
        };
        powerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        _powerTrackBar = new TrackBar
        {
            Dock = DockStyle.Fill,
            Minimum = 1,
            Maximum = 9,
            TickStyle = TickStyle.BottomRight,
            Value = 5,
            SmallChange = 1,
            LargeChange = 1
        };
        _powerTrackBar.Enabled = false;

        _powerGauge = new PowerGaugeControl
        {
            Dock = DockStyle.Fill,
            MinPower = 1,
            MaxPower = 9,
            Power = _powerTrackBar.Value
        };

        _confirmShotButton = new Button
        {
            Dock = DockStyle.Fill,
            Text = "Confirmer tir",
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(13, 110, 253),
            ForeColor = Color.White,
            Enabled = false
        };
        _confirmShotButton.FlatAppearance.BorderSize = 0;
        _confirmShotButton.Click += (_, _) => ConfirmShot();

        _powerTrackBar.ValueChanged += (_, _) =>
        {
            _powerGauge.Power = _powerTrackBar.Value;

            if (_isGameStarted && _mode == GameInteractionMode.Shoot && !_isShotAnimating)
            {
                RenderState($"Puissance sélectionnée : {_powerTrackBar.Value}. Point d'impact mis à jour.");
            }
        };

        powerLayout.Controls.Add(_powerTrackBar, 0, 0);

        var hintLabel = new Label
        {
            Dock = DockStyle.Fill,
            Text = "Clic sur la grille pour jouer",
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Color.FromArgb(108, 117, 125)
        };

        _statusLabel = new Label
        {
            Dock = DockStyle.Fill,
            Text = "Configurez la grille puis démarrez la partie.",
            Font = new Font("Segoe UI", 10f, FontStyle.Italic),
            TextAlign = ContentAlignment.TopLeft
        };

        rightPanel.Controls.Add(titleLabel, 0, 0);
        rightPanel.Controls.Add(sizeLabel, 0, 1);
        rightPanel.Controls.Add(sizeLayout, 0, 2);
        rightPanel.Controls.Add(_startGameButton, 0, 3);
        rightPanel.Controls.Add(_currentPlayerLabel, 0, 4);
        rightPanel.Controls.Add(_scoreLabel, 0, 5);
        rightPanel.Controls.Add(_modeButton, 0, 6);
        rightPanel.Controls.Add(powerLabel, 0, 7);
        rightPanel.Controls.Add(powerLayout, 0, 8);
        rightPanel.Controls.Add(_powerGauge, 0, 9);
        rightPanel.Controls.Add(_confirmShotButton, 0, 10);
        rightPanel.Controls.Add(_saveGameButton, 0, 11);
        rightPanel.Controls.Add(_loadGameButton, 0, 12);
        rightPanel.Controls.Add(hintLabel, 0, 13);
        rightPanel.Controls.Add(_statusLabel, 0, 14);

        root.Controls.Add(_boardControl, 0, 0);
        root.Controls.Add(rightPanel, 1, 0);

        Controls.Add(root);

        _shotAnimationTimer = new System.Windows.Forms.Timer { Interval = 110 };
        _shotAnimationTimer.Tick += OnShotAnimationTick;

        RenderState("Configurez la grille puis démarrez la partie.");
    }
}
