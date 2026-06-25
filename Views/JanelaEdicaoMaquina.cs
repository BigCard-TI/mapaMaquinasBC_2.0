using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MapaMaquinas.Models;

namespace MapaMaquinas.Views
{
    public class JanelaEdicaoMaquina : Window
    {
        private readonly Empresa _empresa;
        private Maquina?  _maquina;

        private TextBox   _edHostname    = null!;
        private TextBox   _edProcessador = null!;
        private TextBox   _edRam         = null!;
        private TextBox   _edIp          = null!;
        private TextBox   _edStorage     = null!;
        private TextBox   _edPorta       = null!;
        private TextBox   _edRamal       = null!;
        private TextBox   _edObs         = null!;
        private ComboBox  _cbSetor       = null!;
        private ComboBox  _cbTipo        = null!;
        private Border    _corPreview    = null!;

        public Maquina? Maquina => _maquina;

        public JanelaEdicaoMaquina(Window owner, Empresa empresa, Maquina? maquina)
        {
            Owner = owner;
            _empresa = empresa;
            _maquina = maquina;

            Title         = maquina?.Hostname is { Length: > 0 } h ? $"Editar — {h}" : "Nova Máquina";
            Width         = 420;
            Height        = 560;
            ResizeMode    = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;

            Content = CriarLayout();
            PreencherDados();
        }

        private UIElement CriarLayout()
        {
            var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            var stack  = new StackPanel { Margin = new Thickness(12) };
            scroll.Content = stack;

            _edHostname    = AddCampo(stack, "Hostname *");
            _edProcessador = AddCampo(stack, "Processador");
            _edRam         = AddCampo(stack, "RAM");
            _edStorage     = AddCampo(stack, "Storage");
            _edIp          = AddCampo(stack, "IP");
            _edPorta       = AddCampo(stack, "Porta Switch");
            _edRamal       = AddCampo(stack, "Ramal");
            _edObs         = AddCampo(stack, "Observações");

            // Setor + preview de cor
            stack.Children.Add(new TextBlock { Text = "Setor *", Margin = new Thickness(0, 8, 0, 2) });
            var setorRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 4) };
            _cbSetor = new ComboBox { Width = 220, DisplayMemberPath = "Nome" };
            foreach (var s in _empresa.Setores)
                _cbSetor.Items.Add(s);
            _cbSetor.SelectionChanged += (_, _) => AtualizarPreviewCor();
            setorRow.Children.Add(_cbSetor);

            _corPreview = new Border
            {
                Width = 60, Height = 22, Margin = new Thickness(8, 0, 0, 0),
                BorderBrush = Brushes.Gray, BorderThickness = new Thickness(1),
                Background = Brushes.Silver
            };
            setorRow.Children.Add(_corPreview);
            stack.Children.Add(setorRow);

            // Tipo
            stack.Children.Add(new TextBlock { Text = "Tipo *", Margin = new Thickness(0, 8, 0, 2) });
            _cbTipo = new ComboBox { Width = 220 };
            foreach (var t in new[] { "Desktop", "Notebook", "Mac", "Servidor", "Impressora" })
                _cbTipo.Items.Add(t);
            _cbTipo.SelectedIndex = 0;
            stack.Children.Add(_cbTipo);

            // Botões
            var btnRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 16, 0, 0)
            };
            var btnSalvar  = new Button { Content = "Salvar",   Width = 90, Margin = new Thickness(0, 0, 8, 0), IsDefault = true };
            var btnCancelar = new Button { Content = "Cancelar", Width = 90, IsCancel = true };
            btnSalvar.Click += OnSalvar;
            btnCancelar.Click += (_, _) => DialogResult = false;
            btnRow.Children.Add(btnSalvar);
            btnRow.Children.Add(btnCancelar);
            stack.Children.Add(btnRow);

            return scroll;
        }

        private static TextBox AddCampo(StackPanel parent, string label)
        {
            parent.Children.Add(new TextBlock { Text = label, Margin = new Thickness(0, 8, 0, 2) });
            var tb = new TextBox { Margin = new Thickness(0, 0, 0, 4) };
            parent.Children.Add(tb);
            return tb;
        }

        private void AtualizarPreviewCor()
        {
            if (_cbSetor.SelectedItem is Setor s)
                _corPreview.Background = s.CorAsBrush();
            else
                _corPreview.Background = Brushes.Silver;
        }

        private void PreencherDados()
        {
            if (_maquina == null) return;
            _edHostname.Text    = _maquina.Hostname;
            _edProcessador.Text = _maquina.Processador;
            _edRam.Text         = _maquina.Ram;
            _edIp.Text          = _maquina.Ip;
            _edStorage.Text     = _maquina.Storage;
            _edPorta.Text       = _maquina.PortaSwitch;
            _edRamal.Text       = _maquina.Ramal;
            _edObs.Text         = _maquina.Observacoes;

            for (int i = 0; i < _cbSetor.Items.Count; i++)
            {
                if (_cbSetor.Items[i] is Setor s &&
                    string.Equals(s.Id, _maquina.SetorId, StringComparison.OrdinalIgnoreCase))
                {
                    _cbSetor.SelectedIndex = i;
                    break;
                }
            }
            AtualizarPreviewCor();

            var tipos = new[] { "desktop", "notebook", "mac", "servidor", "impressora" };
            var idx = Array.FindIndex(tipos, t => string.Equals(t, _maquina.Tipo.Trim(), StringComparison.OrdinalIgnoreCase));
            _cbTipo.SelectedIndex = idx >= 0 ? idx : 0;
        }


        private void OnSalvar(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_edHostname.Text))
            {
                MessageBox.Show("Hostname é obrigatório!", "Validação", MessageBoxButton.OK, MessageBoxImage.Warning);
                _edHostname.Focus();
                return;
            }

            if (_cbSetor.SelectedItem is not Setor setor)
            {
                MessageBox.Show("Selecione um setor!", "Validação", MessageBoxButton.OK, MessageBoxImage.Warning);
                _cbSetor.Focus();
                return;
            }


            _maquina ??= new Maquina();

            var tipos = new[] { "desktop", "notebook", "mac", "servidor", "impressora" };
            _maquina.Cor         = "";
            _maquina.Id          = _edHostname.Text.Trim();
            _maquina.Hostname    = _edHostname.Text.Trim();
            _maquina.Processador = _edProcessador.Text;
            _maquina.Ram         = _edRam.Text;
            _maquina.Ip          = _edIp.Text;
            _maquina.Storage     = _edStorage.Text;
            _maquina.PortaSwitch = _edPorta.Text;
            _maquina.Ramal       = _edRamal.Text;
            _maquina.Observacoes = _edObs.Text;
            _maquina.SetorId     = setor.Id;
            _maquina.Tipo        = tipos[_cbTipo.SelectedIndex >= 0 ? _cbTipo.SelectedIndex : 0];

            DialogResult = true;
        }
    }
}
