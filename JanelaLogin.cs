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
            Height      = 380;
            ResizeMode  = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            WindowStyle = WindowStyle.SingleBorderWindow;

            Content = CriarLayout();
            Loaded += (_, _) => { AtualizarEstadoConfig(); _edUsuario.Focus(); };
        }

        private UIElement CriarLayout()
        {
            // StackPanel em vez de Grid com Star row — cada elemento ocupa
            // exatamente a altura que precisa, sem sobras nem espremimento.
            var stack = new StackPanel { Margin = new Thickness(28, 24, 28, 24) };

            var titulo = new TextBlock
            {
                Text = "Mapa de Máquinas",
                FontSize = 18, FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 2)
            };
            stack.Children.Add(titulo);

            var subtitulo = new TextBlock
            {
                Text = "BigCard / BigCash",
                FontSize = 11, Foreground = new SolidColorBrush(Color.FromRgb(120, 120, 130)),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 24)
            };
            stack.Children.Add(subtitulo);

            // ── Usuário ──────────────────────────────────────────────────────
            stack.Children.Add(new TextBlock
            {
                Text = "Usuário (4 dígitos)", FontSize = 11,
                Margin = new Thickness(0, 0, 0, 4)
            });
            _edUsuario = new TextBox
            {
                FontSize = 16, Height = 32, MaxLength = 4,
                VerticalContentAlignment = VerticalAlignment.Center,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                TextAlignment = TextAlignment.Left,
                Padding = new Thickness(8, 0, 0, 0),
                Margin = new Thickness(0, 0, 0, 16)
            };
            _edUsuario.PreviewTextInput += (_, e) => e.Handled = !ApenasDigitos(e.Text);
            _edUsuario.KeyDown += (_, e) => { if (e.Key == Key.Enter) _edSenha.Focus(); };
            stack.Children.Add(_edUsuario);

            // ── Senha ────────────────────────────────────────────────────────
            stack.Children.Add(new TextBlock
            {
                Text = "Senha", FontSize = 11,
                Margin = new Thickness(0, 0, 0, 4)
            });
            _edSenha = new PasswordBox
            {
                FontSize = 16, Height = 32, MaxLength = 6,
                VerticalContentAlignment = VerticalAlignment.Center,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Padding = new Thickness(8, 0, 0, 0),
                Margin = new Thickness(0, 0, 0, 4)
            };
            _edSenha.PreviewTextInput += (_, e) => e.Handled = !ApenasDigitos(e.Text);
            _edSenha.KeyDown += async (_, e) => { if (e.Key == Key.Enter) await TentarLogin(); };
            stack.Children.Add(_edSenha);

            // ── Mensagem de erro ────────────────────────────────────────────
            _lblErro = new TextBlock
            {
                Foreground = new SolidColorBrush(Color.FromRgb(190, 40, 40)),
                FontSize = 11, TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 6, 0, 0), Visibility = Visibility.Collapsed
            };
            stack.Children.Add(_lblErro);

            // ── Progresso ────────────────────────────────────────────────────
            _progresso = new ProgressBar
            {
                IsIndeterminate = true, Height = 3,
                Margin = new Thickness(0, 10, 0, 0), Visibility = Visibility.Collapsed
            };
            stack.Children.Add(_progresso);

            // ── Botão Entrar ─────────────────────────────────────────────────
            _btnEntrar = new Button
            {
                Content = "Entrar", Height = 34, FontSize = 13,
                Margin = new Thickness(0, 20, 0, 0), IsDefault = true
            };
            _btnEntrar.Click += async (_, _) => await TentarLogin();
            stack.Children.Add(_btnEntrar);

            // ── Link "Configurar conexão" ───────────────────────────────────
            _btnConfig = new Button
            {
                Content = "Configurar conexão...",
                Background = Brushes.Transparent, BorderThickness = new Thickness(0),
                Foreground = new SolidColorBrush(Color.FromRgb(70, 100, 180)),
                FontSize = 10, HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 14, 0, 0), Cursor = Cursors.Hand
            };
            _btnConfig.Click += (_, _) => AbrirConfigConexao();
            stack.Children.Add(_btnConfig);

            return stack;
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
                    // Close() é chamado implicitamente pelo DialogResult,
                    // mas o App.cs precisa ter ShutdownMode correto para
                    // não encerrar a aplicação inteira aqui — ver App.cs.
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
