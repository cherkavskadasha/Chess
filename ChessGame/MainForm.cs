// New class: GameInitializer.cs
using ChessGame.Classes;
using ChessGame.interfaces;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ChessGame
{
    public class GameInitializer
    {
        private readonly string _userName;
        private readonly BoardPanel _boardPanel;
        private NetworkClient _networkClient;
        private IGameMediator _mediator;
        private readonly CancellationTokenSource _cts;
        private readonly Action onConnectionError;
        private readonly Action onSuccess;

        public GameInitializer(string userName, BoardPanel boardPanel, CancellationTokenSource cts, Action onSuccess, Action onConnectionError)
        {
            _userName = userName;
            _boardPanel = boardPanel;
            _cts = cts;
            this.onSuccess = onSuccess;
            this.onConnectionError = onConnectionError;
        }

        public IGameMediator Mediator => _mediator;
        public NetworkClient NetworkClient => _networkClient;

        public async Task InitializeAsync()
        {
            try
            {
                await Task.Run(() =>
                {
                    GameControler.Instance.StartGame();

                    if (_cts.Token.IsCancellationRequested)
                        return;

                    _networkClient = new NetworkClient("127.0.0.1", 5000, _userName);

                    if (_cts.Token.IsCancellationRequested)
                    {
                        _networkClient.Disconnect();
                        return;
                    }

                    _mediator = new GameMediator(GameControler.Instance, _boardPanel, _networkClient, () => Application.OpenForms[0].Close());
                }, _cts.Token);

                onSuccess?.Invoke();
            }
            catch (Exception)
            {
                onConnectionError?.Invoke();
            }
        }
    }
}


// Updated MainForm.cs
using ChessGame.Classes;
using ChessGame.interfaces;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ChessGame
{
    public partial class MainForm : Form
    {
        private IGameMediator _mediator;
        private readonly string _userName;
        private readonly BoardPanel _boardPanel;
        private NetworkClient _networkClient;
        private readonly CancellationTokenSource _cts = new();

        public MainForm(string userName)
        {
            InitializeComponent();
            _userName = userName;
            _boardPanel = new BoardPanel();
        }

        private async void MainForm_Load(object sender, EventArgs e)
        {
            loadingLabel.SafeInvoke(() => loadingLabel.Visible = true);
            AnimateLoadingLabel();

            var initializer = new GameInitializer(
                _userName,
                _boardPanel,
                _cts,
                onSuccess: InitializeUIAfterConnection,
                onConnectionError: () => HandleInitializationError("Не вдалося підключитися до сервера")
            );

            await initializer.InitializeAsync();

            _networkClient = initializer.NetworkClient;
            _mediator = initializer.Mediator;
        }

        private void InitializeUIAfterConnection()
        {
            this.SafeInvoke(() =>
            {
                ChessPanel.Controls.Add(_boardPanel);
                SetupEventHandlers();
                InitializeLabels();
            });
        }

        private void HandleInitializationError(string message)
        {
            this.SafeInvoke(() =>
            {
                MessageBox.Show(message, "Помилка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                this.Close();
            });
        }

        private void InitializeLabels()
        {
            loadingLabel.Visible = false;
            playerName_label.Text = "Привіт, " + _userName;
            infoPanel.Visible = true;
            player_side_label.Text = _networkClient.IsLocalPlayerWhite ? "Білі" : "Чорні";
        }

        private async void AnimateLoadingLabel()
        {
            while (loadingLabel.Visible)
            {
                for (int i = 0; i <= 3; i++)
                {
                    loadingLabel.SafeInvoke(() => loadingLabel.Text = "Пошук гравця" + new string('.', i));
                    await Task.Delay(500);
                }
            }
        }

        private void SetupEventHandlers()
        {
            _networkClient.OpponentNameReceived += name =>
            {
                loadingLabel.Visible = false;
                oponentName_label.Text = "Ти граєш проти " + name;
            };

            GameControler.Instance.OnSideChanged += () =>
            {
                curSide_label.SafeInvoke(() =>
                {
                    curSide_label.Text = GameControler.Instance.IsWhiteTurn ? "Білі" : "Чорні";
                });
            };
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            _cts.Cancel();

            _mediator?.Disconnect();
            _networkClient?.Disconnect();
        }

        private void btn_exit_Click(object sender, EventArgs e)
        {
            this.Close();
        }
    }
}
