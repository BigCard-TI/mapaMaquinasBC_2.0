using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using MapaMaquinas.Services;

namespace MapaMaquinas.Views
{
    /// <summary>
    /// Tela de login exibida antes do MainWindow.
    /// Usuário: exatamente 4 dígitos numéricos (CODIGO).
    /// Senha:   até 6 dígitos numéricos (cifrados via CifraNumerica antes
    ///          de comparar com a coluna SENHA do banco).
    /// </summary>
    public class JanelaLogin : Window
    {
        public bool LoginOk { get; private set; }
        public string CodigoAutenticado { get; private set; } = "";

        private TextBox      _edUsuario  = null!;
        private PasswordBox  _edSenha    = null!;
        private TextBlock    _lblErro    = null!;
        private Button       _btnEntrar  = null!;
        private Button       _btnConfig  = null!;
        private ProgressBar  _progresso  = null!;

        public JanelaLogin()
        {
            Title       = "MapaMaquinas — Login";
            Width       = 380;
            Height      = 350;
            ResizeMode  = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            WindowStyle = WindowStyle.SingleBorderWindow;

            Content = CriarLayout();
            Loaded += (_, _) => { AtualizarEstadoConfig(); _edUsuario.Focus(); };
        }

        private UIElement CriarLayout()
        {
            var grid = new Grid { Margin = new Thickness(24) };
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // título
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // usuário
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // senha
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // erro
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // progresso
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // botões
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // config

            var titulo = new TextBlock
            {
                Text = "Mapa Máquinas",
                FontSize = 18, FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 4)
            };
            Grid.SetRow(titulo, 0);
            grid.Children.Add(titulo);

            // ── Usuário ──────────────────────────────────────────────────────
            var pUsuario = new StackPanel { Margin = new Thickness(0, 0, 0, 12) };
            pUsuario.Children.Add(new TextBlock { Text = "Usuário (4 dígitos)", FontSize = 11, Margin = new Thickness(0, 0, 0, 4) });
            _edUsuario = new TextBox
            {
                FontSize = 16, Height = 32, MaxLength = 4,
                VerticalContentAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Center
            };
            _edUsuario.PreviewTextInput += (_, e) => e.Handled = !ApenasDigitos(e.Text);
            _edUsuario.KeyDown += (_, e) => { if (e.Key == Key.Enter) _edSenha.Focus(); };
            pUsuario.Children.Add(_edUsuario);
            Grid.SetRow(pUsuario, 1);
            grid.Children.Add(pUsuario);

            // ── Senha ────────────────────────────────────────────────────────
            var pSenha = new StackPanel { Margin = new Thickness(0, 0, 0, 8) };
            pSenha.Children.Add(new TextBlock { Text = "Senha", FontSize = 11, Margin = new Thickness(0, 0, 0, 4) });
            _edSenha = new PasswordBox
            {
                FontSize = 16, Height = 32, MaxLength = 6,
                VerticalContentAlignment = VerticalAlignment.Center
            };
            _edSenha.PreviewTextInput += (_, e) => e.Handled = !ApenasDigitos(e.Text);
            _edSenha.KeyDown += async (_, e) => { if (e.Key == Key.Enter) await TentarLogin(); };
            pSenha.Children.Add(_edSenha);
            Grid.SetRow(pSenha, 2);
            grid.Children.Add(pSenha);

            // ── Mensagem de erro ────────────────────────────────────────────
            _lblErro = new TextBlock
            {
                Foreground = new SolidColorBrush(Color.FromRgb(190, 40, 40)),
                FontSize = 11, TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 4, 0, 0), Visibility = Visibility.Collapsed
            };
            Grid.SetRow(_lblErro, 3);
            grid.Children.Add(_lblErro);

            // ── Progresso ────────────────────────────────────────────────────
            _progresso = new ProgressBar
            {
                IsIndeterminate = true, Height = 3,
                Margin = new Thickness(0, 8, 0, 0), Visibility = Visibility.Collapsed
            };
            Grid.SetRow(_progresso, 4);
            grid.Children.Add(_progresso);

            // ── Botão Entrar ─────────────────────────────────────────────────
            _btnEntrar = new Button
            {
                Content = "Entrar", Height = 34, FontSize = 13,
                Margin = new Thickness(0, 9, 0, 0), IsDefault = true
            };
            _btnEntrar.Click += async (_, _) => await TentarLogin();
            Grid.SetRow(_btnEntrar, 6);
            grid.Children.Add(_btnEntrar);

            // ── Link "Configurar conexão" ───────────────────────────────────
            _btnConfig = new Button
            {
                Content = "Configurar conexão...",
                Background = Brushes.Transparent, BorderThickness = new Thickness(0),
                Foreground = new SolidColorBrush(Color.FromRgb(70, 100, 180)),
                FontSize = 10, HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 5, 0, 0), Cursor = Cursors.Hand
            };
            _btnConfig.Click += (_, _) => AbrirConfigConexao();
            Grid.SetRow(_btnConfig, 7);
            grid.Children.Add(_btnConfig);

            return grid;
        }

        private static bool ApenasDigitos(string texto)
        {
            foreach (var c in texto)
                if (!char.IsDigit(c)) return false;
            return true;
        }

        private void AtualizarEstadoConfig()
        {
            // Se não há conexão configurada, avisa e direciona para o utilitário
            if (!ConexaoConfig.Existe())
            {
                MostrarErro("Nenhuma conexão configurada. Clique em 'Configurar conexão...' abaixo.");
                _btnEntrar.IsEnabled = false;
            }
            else
            {
                _btnEntrar.IsEnabled = true;
            }
        }

        private void MostrarErro(string mensagem)
        {
            _lblErro.Text = mensagem;
            _lblErro.Visibility = Visibility.Visible;
        }

        private void LimparErro() => _lblErro.Visibility = Visibility.Collapsed;

        private async System.Threading.Tasks.Task TentarLogin()
        {
            LimparErro();

            var usuario = _edUsuario.Text.Trim();
            var senha   = _edSenha.Password.Trim();

            if (usuario.Length != 4)
            {
                MostrarErro("O usuário deve ter exatamente 4 dígitos.");
                _edUsuario.Focus();
                return;
            }
            if (senha.Length == 0)
            {
                MostrarErro("Informe a senha.");
                _edSenha.Focus();
                return;
            }

            _btnEntrar.IsEnabled = false;
            _progresso.Visibility = Visibility.Visible;

            ResultadoLogin resultado;
            try
            {
                resultado = await AuthService.Autenticar(usuario, senha);
            }
            finally
            {
                _progresso.Visibility = Visibility.Collapsed;
                _btnEntrar.IsEnabled = true;
            }

            switch (resultado)
            {
                case ResultadoLogin.Sucesso:
                    LoginOk = true;
                    CodigoAutenticado = usuario;
                    DialogResult = true;
                    Close();
                    break;

                case ResultadoLogin.UsuarioNaoEncontrado:
                    MostrarErro("Usuário não encontrado.");
                    _edSenha.Password = "";
                    _edUsuario.Focus();
                    _edUsuario.SelectAll();
                    break;

                case ResultadoLogin.SenhaIncorreta:
                    MostrarErro("Senha incorreta.");
                    _edSenha.Password = "";
                    _edSenha.Focus();
                    break;

                case ResultadoLogin.ConexaoNaoConfigurada:
                    MostrarErro("Conexão não configurada. Use 'Configurar conexão...' abaixo.");
                    break;

                case ResultadoLogin.ErroConexao:
                    MostrarErro("Não foi possível conectar ao banco de dados. Verifique a configuração de conexão.");
                    break;
            }
        }

        private void AbrirConfigConexao()
        {
            var dlg = new JanelaConfigConexao(this);
            if (dlg.ShowDialog() == true)
            {
                LimparErro();
                AtualizarEstadoConfig();
            }
        }
    }
}
