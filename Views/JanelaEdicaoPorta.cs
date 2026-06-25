using System;
using System.Windows;
using System.Windows.Controls;
using MapaMaquinas.Models;

namespace MapaMaquinas.Views
{
    public class JanelaEdicaoPorta : Window
    {
        private readonly Empresa _empresa;
        private PortaSwitch? _porta;

        private TextBox _edNumero      = null!;
        private TextBox _edDescricao   = null!;
        private TextBox _edLocalizacao = null!;
        private TextBox _edObs         = null!;

        public PortaSwitch? Porta => _porta;

        public JanelaEdicaoPorta(Window owner, Empresa empresa, PortaSwitch? porta = null)
        {
            Owner   = owner;
            _empresa = empresa;
            _porta   = porta;

            Title         = porta?.Numero is { Length: > 0 } n ? $"Editar Porta — {n}" : "Nova Porta de Switch";
            Width         = 380;
            Height        = 280;
            ResizeMode    = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;

            Content = CriarLayout();
            PreencherDados();
        }

        private UIElement CriarLayout()
        {
            var stack = new StackPanel { Margin = new Thickness(12) };

            stack.Children.Add(new TextBlock { Text = "Número da Porta *", Margin = new Thickness(0, 0, 0, 2) });
            _edNumero = new TextBox { Margin = new Thickness(0, 0, 0, 8) };
            stack.Children.Add(_edNumero);

            stack.Children.Add(new TextBlock { Text = "Descrição", Margin = new Thickness(0, 0, 0, 2) });
            _edDescricao = new TextBox { Margin = new Thickness(0, 0, 0, 8) };
            stack.Children.Add(_edDescricao);

            stack.Children.Add(new TextBlock { Text = "Localização", Margin = new Thickness(0, 0, 0, 2) });
            _edLocalizacao = new TextBox { Margin = new Thickness(0, 0, 0, 8) };
            stack.Children.Add(_edLocalizacao);

            stack.Children.Add(new TextBlock { Text = "Observações", Margin = new Thickness(0, 0, 0, 2) });
            _edObs = new TextBox { Margin = new Thickness(0, 0, 0, 16) };
            stack.Children.Add(_edObs);

            var btnRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var btnSalvar   = new Button { Content = "Salvar",   Width = 90, Margin = new Thickness(0, 0, 8, 0), IsDefault = true };
            var btnCancelar = new Button { Content = "Cancelar", Width = 90, IsCancel = true };
            btnSalvar.Click   += OnSalvar;
            btnCancelar.Click += (_, _) => DialogResult = false;
            btnRow.Children.Add(btnSalvar);
            btnRow.Children.Add(btnCancelar);
            stack.Children.Add(btnRow);

            return stack;
        }

        private void PreencherDados()
        {
            if (_porta == null) return;
            _edNumero.Text      = _porta.Numero;
            _edDescricao.Text   = _porta.Descricao;
            _edLocalizacao.Text = _porta.Localizacao;
            _edObs.Text         = _porta.Observacoes;
        }

        private string? NumeroDuplicado(string num)
        {
            foreach (var p in _empresa.Portas)
            {
                if (_porta != null && string.Equals(p.Id, _porta.Id, StringComparison.OrdinalIgnoreCase)) continue;
                if (string.Equals(p.Numero.Trim(), num.Trim(), StringComparison.OrdinalIgnoreCase))
                    return p.Numero;
            }
            return null;
        }

        private static string GerarId(string numero) =>
            "porta_" + numero.Trim().ToLower().Replace(' ', '_');

        private void OnSalvar(object sender, RoutedEventArgs e)
        {
            var num = _edNumero.Text.Trim();
            if (string.IsNullOrEmpty(num))
            {
                MessageBox.Show("Número da porta é obrigatório!", "Validação", MessageBoxButton.OK, MessageBoxImage.Warning);
                _edNumero.Focus();
                return;
            }

            var dup = NumeroDuplicado(num);
            if (dup != null)
            {
                MessageBox.Show($"Já existe uma porta com o número \"{dup}\"!", "Validação", MessageBoxButton.OK, MessageBoxImage.Warning);
                _edNumero.Focus();
                return;
            }

            _porta ??= new PortaSwitch();
            _porta.Id          = GerarId(num);
            _porta.Numero      = num;
            _porta.Descricao   = _edDescricao.Text.Trim();
            _porta.Localizacao = _edLocalizacao.Text.Trim();
            _porta.Observacoes = _edObs.Text.Trim();

            DialogResult = true;
        }
    }
}
