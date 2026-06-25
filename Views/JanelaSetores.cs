using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MapaMaquinas.Models;

namespace MapaMaquinas.Views
{
    public class JanelaSetores : Window
    {
        private readonly Empresa _empresa;
        public bool Alterado { get; private set; }

        private ListBox   _lista     = null!;
        private TextBox   _edNome    = null!;
        private TextBox   _edCorHex  = null!;
        private Border    _corPreview = null!;
        private TextBlock _lblTitulo = null!;
        private Button    _btnSalvar = null!;
        private Button    _btnExcluir = null!;
        private Setor?    _setorEditando;

        public JanelaSetores(Window owner, Empresa empresa)
        {
            Owner    = owner;
            _empresa = empresa;
            Title    = $"Setores — {empresa.Nome}";
            Width    = 560;
            Height   = 480;
            ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;

            Content = CriarLayout();
            CarregarLista();
        }

        private UIElement CriarLayout()
        {
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(240) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(44) });

            // ── Lista ─────────────────────────────────────────────────────────
            _lista = new ListBox { DisplayMemberPath = "Nome", Margin = new Thickness(0) };
            _lista.SelectionChanged += OnListaSelecionar;
            Grid.SetColumn(_lista, 0);
            Grid.SetRow(_lista, 0);
            grid.Children.Add(_lista);

            // ── Painel de edição ──────────────────────────────────────────────
            var pnlEd = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(248, 248, 248)),
                Padding = new Thickness(16)
            };
            var edStack = new StackPanel();

            _lblTitulo = new TextBlock { Text = "Selecione um setor", FontSize = 10, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 16) };
            edStack.Children.Add(_lblTitulo);

            edStack.Children.Add(new TextBlock { Text = "Nome *", Margin = new Thickness(0, 0, 0, 2) });
            _edNome = new TextBox { IsEnabled = false, Margin = new Thickness(0, 0, 0, 12) };
            edStack.Children.Add(_edNome);

            edStack.Children.Add(new TextBlock { Text = "Cor (hex)", Margin = new Thickness(0, 0, 0, 2) });
            var corRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 12) };
            _edCorHex = new TextBox { Width = 90, IsEnabled = false };
            _edCorHex.TextChanged += (_, _) => AtualizarPreviewCor();
            corRow.Children.Add(_edCorHex);

            _corPreview = new Border
            {
                Width = 50, Height = 22, Margin = new Thickness(8, 0, 0, 0),
                BorderBrush = Brushes.Gray, BorderThickness = new Thickness(1),
                Background = Brushes.Silver
            };
            corRow.Children.Add(_corPreview);

            var btnPicker = new Button { Content = "...", Width = 28, Height = 22, Margin = new Thickness(4, 0, 0, 0), IsEnabled = false };
            btnPicker.Click += OnColorPicker;
            corRow.Children.Add(btnPicker);
            edStack.Children.Add(corRow);

            _btnSalvar = new Button { Content = "Salvar setor", Width = 110, Height = 28, IsEnabled = false, HorizontalAlignment = HorizontalAlignment.Left, IsDefault = true };
            _btnSalvar.Click += OnSalvarSetor;
            edStack.Children.Add(_btnSalvar);

            pnlEd.Child = edStack;
            Grid.SetColumn(pnlEd, 1);
            Grid.SetRow(pnlEd, 0);
            grid.Children.Add(pnlEd);

            // ── Rodapé ────────────────────────────────────────────────────────
            var rodape = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(236, 236, 236)),
                BorderBrush = Brushes.LightGray, BorderThickness = new Thickness(0, 1, 0, 0)
            };
            var rodapeRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 8, 8, 8)
            };

            var btnNovo = new Button { Content = "Novo setor", Width = 100, Height = 28, Margin = new Thickness(0, 0, 8, 0) };
            btnNovo.Click += (_, _) => { _lista.SelectedItem = null; LimparEdicao(novoSetor: true); };
            rodapeRow.Children.Add(btnNovo);

            _btnExcluir = new Button { Content = "Excluir", Width = 80, Height = 28, IsEnabled = false };
            _btnExcluir.Click += OnExcluir;
            rodapeRow.Children.Add(_btnExcluir);

            var btnFechar = new Button
            {
                Content = "Fechar", Width = 80, Height = 28,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0),
                IsCancel = true
            };
            btnFechar.Click += (_, _) => Close();

            var rodapeFill = new DockPanel();
            rodapeFill.Children.Add(rodapeRow);
            DockPanel.SetDock(rodapeRow, Dock.Left);
            rodapeFill.Children.Add(btnFechar);
            DockPanel.SetDock(btnFechar, Dock.Right);
            rodape.Child = rodapeFill;

            Grid.SetColumnSpan(rodape, 2);
            Grid.SetRow(rodape, 1);
            grid.Children.Add(rodape);

            // Guarda referência para habilitar o botão color picker
            btnPicker.Tag = btnPicker;
            _edNome.TextChanged += (_, _) => { };

            // Remapeia o botão picker via campo para poder ativá-lo
            _edCorHex.Tag = btnPicker;

            return grid;
        }

        private void CarregarLista()
        {
            _lista.Items.Clear();
            foreach (var s in _empresa.Setores)
                _lista.Items.Add(s);
        }

        private void OnListaSelecionar(object sender, SelectionChangedEventArgs e)
        {
            if (_lista.SelectedItem is Setor s)
                SelecionarSetor(s);
        }

        private void SelecionarSetor(Setor s)
        {
            _setorEditando     = s;
            _lblTitulo.Text    = $"Editar: {s.Nome}";
            _edNome.Text       = s.Nome;
            _edNome.IsEnabled  = true;
            _edCorHex.Text     = s.Cor;
            _edCorHex.IsEnabled = true;
            _btnSalvar.IsEnabled  = true;
            _btnExcluir.IsEnabled = true;
            if (_edCorHex.Tag is Button bp) bp.IsEnabled = true;
            AtualizarPreviewCor();
        }

        private void LimparEdicao(bool novoSetor)
        {
            _setorEditando = null;
            _lblTitulo.Text = novoSetor ? "Novo setor" : "Selecione um setor";
            _edNome.Text    = "";
            _edNome.IsEnabled = novoSetor;
            _edCorHex.Text  = "#808080";
            _edCorHex.IsEnabled = novoSetor;
            _btnSalvar.IsEnabled  = novoSetor;
            _btnExcluir.IsEnabled = false;
            if (_edCorHex.Tag is Button bp) bp.IsEnabled = novoSetor;
            AtualizarPreviewCor();
            if (novoSetor) _edNome.Focus();
        }

        private void AtualizarPreviewCor()
        {
            try
            {
                var hex = _edCorHex.Text.TrimStart('#');
                if (hex.Length == 6)
                {
                    var r = Convert.ToByte(hex.Substring(0, 2), 16);
                    var g = Convert.ToByte(hex.Substring(2, 2), 16);
                    var b = Convert.ToByte(hex.Substring(4, 2), 16);
                    _corPreview.Background = new SolidColorBrush(Color.FromRgb(r, g, b));
                    return;
                }
            }
            catch { }
            _corPreview.Background = Brushes.Silver;
        }

        private void OnColorPicker(object sender, RoutedEventArgs e)
        {
            var dlg = new System.Windows.Forms.ColorDialog();
            try
            {
                var hex = _edCorHex.Text.TrimStart('#');
                if (hex.Length == 6)
                {
                    var r = Convert.ToByte(hex.Substring(0, 2), 16);
                    var g = Convert.ToByte(hex.Substring(2, 2), 16);
                    var b = Convert.ToByte(hex.Substring(4, 2), 16);
                    dlg.Color = System.Drawing.Color.FromArgb(r, g, b);
                }
            }
            catch { }

            if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                var c = dlg.Color;
                _edCorHex.Text = $"#{c.R:X2}{c.G:X2}{c.B:X2}";
            }
        }

        private void OnSalvarSetor(object sender, RoutedEventArgs e)
        {
            var nome = _edNome.Text.Trim();
            var cor  = _edCorHex.Text.Trim();

            if (string.IsNullOrEmpty(nome))
            {
                MessageBox.Show("Nome do setor é obrigatório!", "Validação", MessageBoxButton.OK, MessageBoxImage.Warning);
                _edNome.Focus();
                return;
            }

            if (cor.Length != 4 && cor.Length != 7) cor = "#808080";

            if (_setorEditando == null)
            {
                // Novo
                if (_empresa.Setores.Any(s => string.Equals(s.Nome, nome, StringComparison.OrdinalIgnoreCase)))
                {
                    MessageBox.Show("Já existe um setor com esse nome!", "Validação", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                var novoSetor = new Setor { Id = IdUnico(nome), Nome = nome, Cor = cor };
                _empresa.Setores.Add(novoSetor);
                _lista.Items.Add(novoSetor);
                _lista.SelectedItem = novoSetor;
                _setorEditando = novoSetor;
                _lblTitulo.Text = $"Editar: {nome}";
                _btnExcluir.IsEnabled = true;
            }
            else
            {
                // Editar
                if (_empresa.Setores.Any(s => string.Equals(s.Nome, nome, StringComparison.OrdinalIgnoreCase) && s != _setorEditando))
                {
                    MessageBox.Show("Já existe um setor com esse nome!", "Validação", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                _setorEditando.Nome = nome;
                _setorEditando.Cor  = cor;
                _lista.Items.Refresh();
                _lblTitulo.Text = $"Editar: {nome}";
            }

            Alterado = true;
            MessageBox.Show("Setor salvo! Feche esta janela para aplicar as cores no mapa.", "Sucesso", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void OnExcluir(object sender, RoutedEventArgs e)
        {
            if (_setorEditando == null) return;

            var qtd = _empresa.Maquinas.Count(m => string.Equals(m.SetorId, _setorEditando.Id, StringComparison.OrdinalIgnoreCase));
            if (qtd > 0)
            {
                MessageBox.Show(
                    $"Não é possível excluir o setor \"{_setorEditando.Nome}\".\n" +
                    $"Ele possui {qtd} máquina(s) vinculada(s).\n\n" +
                    "Reatribua as máquinas a outro setor antes de excluir.",
                    "Não permitido", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (MessageBox.Show($"Excluir o setor \"{_setorEditando.Nome}\"?", "Confirmar",
                    MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            _lista.Items.Remove(_setorEditando);
            _empresa.Setores.Remove(_setorEditando);
            _setorEditando = null;
            Alterado       = true;
            LimparEdicao(novoSetor: false);
        }

        private string IdUnico(string nome)
        {
            var base_ = nome.Trim().ToLower().Replace(' ', '_');
            var candidato = base_;
            int n = 2;
            while (_empresa.Setores.Any(s => string.Equals(s.Id, candidato, StringComparison.OrdinalIgnoreCase)))
                candidato = $"{base_}_{n++}";
            return candidato;
        }
    }
}
