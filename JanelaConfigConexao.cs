using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MapaMaquinas.Services;

namespace MapaMaquinas.Views
{
    /// <summary>
    /// Utilitário para configurar a conexão com o SQL Server.
    /// Monta a connection string a partir dos campos, testa o acesso à
    /// tabela USUARIOS, e só então salva criptografado via DPAPI no Registro.
    /// </summary>
    public class JanelaConfigConexao : Window
    {
        private TextBox     _edServidor   = null!;
        private TextBox     _edBanco      = null!;
        private RadioButton _rbWindowsAuth = null!;
        private RadioButton _rbSqlAuth     = null!;
        private TextBox     _edUsuarioSql  = null!;
        private PasswordBox _edSenhaSql    = null!;
        private TextBlock   _lblResultado  = null!;
        private Button      _btnTestar     = null!;
        private Button      _btnSalvar     = null!;
        private ProgressBar _progresso     = null!;

        public JanelaConfigConexao(Window owner)
        {
            Owner = owner;
            Title = "Configurar conexão com o banco";
            Width = 420;
            Height = 420;
            ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;

            Content = CriarLayout();
            PreencherSeJaExistir();
        }

        private UIElement CriarLayout()
        {
            var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            var stack  = new StackPanel { Margin = new Thickness(16) };
            scroll.Content = stack;

            stack.Children.Add(new TextBlock
            {
                Text = "Configuração da conexão SQL Server",
                FontSize = 14, FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 4)
            });
            stack.Children.Add(new TextBlock
            {
                Text = "A conexão é criptografada (DPAPI) e salva apenas neste computador, " +
                       "vinculada ao seu usuário do Windows. Nunca é gravada em arquivo.",
                FontSize = 10, TextWrapping = TextWrapping.Wrap,
                Foreground = new SolidColorBrush(Color.FromRgb(110, 110, 120)),
                Margin = new Thickness(0, 0, 0, 16)
            });

            _edServidor = AddCampo(stack, "Servidor (ex: 192.168.2.10\\SQLEXPRESS)");
            _edBanco    = AddCampo(stack, "Banco de dados");

            stack.Children.Add(new TextBlock { Text = "Autenticação", Margin = new Thickness(0, 8, 0, 4), FontWeight = FontWeights.SemiBold });
            _rbWindowsAuth = new RadioButton { Content = "Windows (usuário atual)", GroupName = "auth", Margin = new Thickness(0, 0, 0, 4) };
            _rbSqlAuth     = new RadioButton { Content = "SQL Server (usuário e senha)", GroupName = "auth", IsChecked = true };
            _rbWindowsAuth.Checked += (_, _) => AtualizarCamposSql();
            _rbSqlAuth.Checked     += (_, _) => AtualizarCamposSql();
            stack.Children.Add(_rbWindowsAuth);
            stack.Children.Add(_rbSqlAuth);

            _edUsuarioSql = AddCampo(stack, "Usuário SQL");
            stack.Children.Add(new TextBlock { Text = "Senha SQL", Margin = new Thickness(0, 8, 0, 2) });
            _edSenhaSql = new PasswordBox { Margin = new Thickness(0, 0, 0, 8), Height = 26 };
            stack.Children.Add(_edSenhaSql);

            _lblResultado = new TextBlock
            {
                FontSize = 11, TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 8, 0, 0), Visibility = Visibility.Collapsed
            };
            stack.Children.Add(_lblResultado);

            _progresso = new ProgressBar
            {
                IsIndeterminate = true, Height = 3,
                Margin = new Thickness(0, 8, 0, 0), Visibility = Visibility.Collapsed
            };
            stack.Children.Add(_progresso);

            var btnRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 20, 0, 0)
            };

            _btnTestar = new Button { Content = "Testar conexão", Width = 120, Height = 28, Margin = new Thickness(0, 0, 8, 0) };
            _btnTestar.Click += async (_, _) => await TestarConexao();
            btnRow.Children.Add(_btnTestar);

            _btnSalvar = new Button { Content = "Salvar", Width = 90, Height = 28, Margin = new Thickness(0, 0, 8, 0), IsEnabled = false };
            _btnSalvar.Click += (_, _) => Salvar();
            btnRow.Children.Add(_btnSalvar);

            var btnCancelar = new Button { Content = "Cancelar", Width = 80, Height = 28, IsCancel = true };
            btnRow.Children.Add(btnCancelar);

            stack.Children.Add(btnRow);

            return scroll;
        }

        private static TextBox AddCampo(StackPanel parent, string label)
        {
            parent.Children.Add(new TextBlock { Text = label, Margin = new Thickness(0, 8, 0, 2) });
            var tb = new TextBox { Height = 26, Margin = new Thickness(0, 0, 0, 4) };
            parent.Children.Add(tb);
            return tb;
        }

        private void AtualizarCamposSql()
        {
            var habilitado = _rbSqlAuth.IsChecked == true;
            _edUsuarioSql.IsEnabled = habilitado;
            _edSenhaSql.IsEnabled   = habilitado;
        }

        private void PreencherSeJaExistir()
        {
            AtualizarCamposSql();
            // Por segurança, não pré-preenchemos servidor/usuário extraídos
            // da connection string salva — o usuário deve reconfigurar do zero
            // se quiser alterar. Evita reexibir dados sensíveis na tela.
        }

        private string MontarConnectionString()
        {
            var servidor = _edServidor.Text.Trim();
            var banco    = _edBanco.Text.Trim();

            if (string.IsNullOrEmpty(servidor) || string.IsNullOrEmpty(banco))
                throw new InvalidOperationException("Preencha Servidor e Banco de dados.");

            if (_rbWindowsAuth.IsChecked == true)
            {
                return $"Server={servidor};Database={banco};Integrated Security=True;TrustServerCertificate=True;";
            }
            else
            {
                var usuario = _edUsuarioSql.Text.Trim();
                var senha   = _edSenhaSql.Password;
                if (string.IsNullOrEmpty(usuario))
                    throw new InvalidOperationException("Preencha o usuário SQL.");

                return $"Server={servidor};Database={banco};User Id={usuario};Password={senha};TrustServerCertificate=True;";
            }
        }

        private async System.Threading.Tasks.Task TestarConexao()
        {
            _lblResultado.Visibility = Visibility.Collapsed;
            string connStr;
            try
            {
                connStr = MontarConnectionString();
            }
            catch (InvalidOperationException ex)
            {
                MostrarResultado(ex.Message, sucesso: false);
                return;
            }

            _btnTestar.IsEnabled = false;
            _progresso.Visibility = Visibility.Visible;

            var (ok, mensagem) = await AuthService.TestarConexao(connStr);

            _progresso.Visibility = Visibility.Collapsed;
            _btnTestar.IsEnabled = true;

            MostrarResultado(mensagem, ok);
            _btnSalvar.IsEnabled = ok;
            _ultimaConnStringTestada = ok ? connStr : null;
        }

        private string? _ultimaConnStringTestada;

        private void MostrarResultado(string mensagem, bool sucesso)
        {
            _lblResultado.Text = mensagem;
            _lblResultado.Foreground = sucesso
                ? new SolidColorBrush(Color.FromRgb(30, 140, 60))
                : new SolidColorBrush(Color.FromRgb(190, 40, 40));
            _lblResultado.Visibility = Visibility.Visible;
        }

        private void Salvar()
        {
            if (string.IsNullOrEmpty(_ultimaConnStringTestada))
            {
                MostrarResultado("Teste a conexão com sucesso antes de salvar.", sucesso: false);
                return;
            }

            try
            {
                ConexaoConfig.Salvar(_ultimaConnStringTestada);
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MostrarResultado($"Erro ao salvar: {ex.Message}", sucesso: false);
            }
        }
    }
}
