using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using MapaMaquinas.Controls;
using MapaMaquinas.Models;
using MapaMaquinas.Services;

namespace MapaMaquinas.Views
{
    /// <summary>
    /// Lista todas as máquinas em tabela com hostname, IP, setor e status de ping.
    /// Duplo clique ou Enter navega até o card no mapa.
    /// </summary>
    public class JanelaListaMaquinas : Window
    {
        private readonly List<CardMaquina> _cards;
        private readonly Action<CardMaquina> _navegarAte;
        private ListView _lista = null!;
        private TextBox  _edFiltro = null!;

        private record LinhaLista(
            CardMaquina Card,
            string Hostname,
            string Ip,
            string Setor,
            string Tipo,
            string Ramal,
            string StatusHostname,
            string StatusIp,
            Color CorStatusHost,
            Color CorStatusIp);

        public JanelaListaMaquinas(Window owner, List<CardMaquina> cards,
                                   Action<CardMaquina> navegarAte)
        {
            _cards     = cards;
            _navegarAte = navegarAte;
            Owner      = owner;
            Title      = $"Máquinas — {cards.Count} cadastradas";
            Width      = 780;
            Height     = 500;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Content    = CriarLayout();
            Loaded    += (_, _) => { _edFiltro.Focus(); PopularLista(""); };
        }

        private UIElement CriarLayout()
        {
            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // ── Filtro ────────────────────────────────────────────────────────
            var filtroBar = new DockPanel { Margin = new Thickness(8, 8, 8, 4) };
            filtroBar.Children.Add(new TextBlock
            {
                Text = "Filtrar:", VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 6, 0)
            });
            _edFiltro = new TextBox { Height = 26 };
            _edFiltro.TextChanged += (_, _) => PopularLista(_edFiltro.Text);
            _edFiltro.KeyDown += (_, e) =>
            {
                if (e.Key == Key.Down && _lista.Items.Count > 0)
                { _lista.SelectedIndex = 0; _lista.Focus(); }
                if (e.Key == Key.Escape) Close();
            };
            filtroBar.Children.Add(_edFiltro);
            Grid.SetRow(filtroBar, 0);
            grid.Children.Add(filtroBar);

            // ── Lista ─────────────────────────────────────────────────────────
            _lista = new ListView { Margin = new Thickness(8, 0, 8, 0) };
            var view = new GridView();

            view.Columns.Add(ColStatus("Host", 36));
            view.Columns.Add(ColTexto("Hostname",  nameof(LinhaLista.Hostname),  180));
            view.Columns.Add(ColStatus("IP",  36));
            view.Columns.Add(ColTexto("IP",         nameof(LinhaLista.Ip),        120));
            view.Columns.Add(ColTexto("Setor",      nameof(LinhaLista.Setor),     110));
            view.Columns.Add(ColTexto("Tipo",       nameof(LinhaLista.Tipo),       80));
            view.Columns.Add(ColTexto("Ramal",      nameof(LinhaLista.Ramal),      60));

            _lista.View = view;
            _lista.MouseDoubleClick += (_, _) => Navegar();
            _lista.KeyDown += (_, e) =>
            {
                if (e.Key == Key.Enter)  Navegar();
                if (e.Key == Key.Escape) Close();
            };

            Grid.SetRow(_lista, 1);
            grid.Children.Add(_lista);

            // ── Rodapé ────────────────────────────────────────────────────────
            var rodape = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(8)
            };
            var btnIr = new Button
                { Content = "Ir para máquina", Width = 130, Height = 28,
                  Margin = new Thickness(0, 0, 8, 0), IsDefault = true };
            btnIr.Click += (_, _) => Navegar();
            var btnFechar = new Button
                { Content = "Fechar", Width = 80, Height = 28, IsCancel = true };
            btnFechar.Click += (_, _) => Close();
            rodape.Children.Add(btnIr);
            rodape.Children.Add(btnFechar);
            Grid.SetRow(rodape, 2);
            grid.Children.Add(rodape);

            return grid;
        }

        private void PopularLista(string filtro)
        {
            _lista.Items.Clear();
            var termo = filtro.Trim();

            foreach (var card in _cards)
            {
                var m = card.Maquina;
                if (m == null) continue;

                if (!string.IsNullOrEmpty(termo) &&
                    !m.Hostname.Contains(termo, StringComparison.OrdinalIgnoreCase) &&
                    !m.Ip.Contains(termo, StringComparison.OrdinalIgnoreCase) &&
                    !m.SetorId.Contains(termo, StringComparison.OrdinalIgnoreCase) &&
                    !m.Ramal.Contains(termo, StringComparison.OrdinalIgnoreCase) &&
                    !m.Tipo.Contains(termo, StringComparison.OrdinalIgnoreCase))
                    continue;

                var linha = new LinhaLista(
                    Card:            card,
                    Hostname:        m.Hostname,
                    Ip:              m.Ip,
                    Setor:           m.SetorId,
                    Tipo:            m.Tipo,
                    Ramal:           m.Ramal,
                    StatusHostname:  StatusStr(card.PingStatusHostname),
                    StatusIp:        StatusStr(card.PingStatusIp),
                    CorStatusHost:   StatusCor(card.PingStatusHostname),
                    CorStatusIp:     StatusCor(card.PingStatusIp));

                _lista.Items.Add(linha);
            }

            Title = $"Máquinas — {_lista.Items.Count} exibidas / {_cards.Count} total";
        }

        private void Navegar()
        {
            if (_lista.SelectedItem is not LinhaLista linha) return;
            _navegarAte(linha.Card);
            Close();
        }

        private static GridViewColumn ColTexto(string header, string campo, double largura)
        {
            return new GridViewColumn
            {
                Header           = header,
                Width            = largura,
                DisplayMemberBinding = new Binding(campo)
            };
        }

        private static GridViewColumn ColStatus(string header, double largura)
        {
            var col = new GridViewColumn { Header = header, Width = largura };
            var template = new DataTemplate();
            var factory = new FrameworkElementFactory(typeof(Ellipse));
            factory.SetValue(Ellipse.WidthProperty, 10.0);
            factory.SetValue(Ellipse.HeightProperty, 10.0);
            factory.SetValue(Ellipse.MarginProperty, new Thickness(4, 0, 0, 0));
            var fieldName = header == "Host"
                ? nameof(LinhaLista.CorStatusHost)
                : nameof(LinhaLista.CorStatusIp);
            factory.SetBinding(Ellipse.FillProperty,
                new Binding(fieldName) { Converter = new ColorToBrushConverter() });
            template.VisualTree = factory;
            col.CellTemplate = template;
            return col;
        }

        private static string StatusStr(StatusPing s) => s switch
        {
            StatusPing.Online     => "OK",
            StatusPing.Offline    => "Offline",
            StatusPing.Aguardando => "...",
            _                     => "—"
        };

        private static Color StatusCor(StatusPing s) => s switch
        {
            StatusPing.Online     => Color.FromRgb(50,  205, 50),
            StatusPing.Offline    => Color.FromRgb(210, 50,  50),
            StatusPing.Aguardando => Color.FromRgb(255, 190,  0),
            _                     => Color.FromRgb(110, 110, 110)
        };
    }

    // Converte Color para SolidColorBrush no binding
    public class ColorToBrushConverter : System.Windows.Data.IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter,
                              System.Globalization.CultureInfo culture)
            => value is Color c ? new SolidColorBrush(c) : Brushes.Gray;

        public object ConvertBack(object value, Type targetType, object parameter,
                                  System.Globalization.CultureInfo culture)
            => throw new NotImplementedException();
    }

    // Ellipse helper (não existe no WPF sem referência extra — usa Border arredondado)
    public class Ellipse : Border
    {
        public static readonly DependencyProperty FillProperty =
            DependencyProperty.Register("Fill", typeof(Brush), typeof(Ellipse),
                new PropertyMetadata(Brushes.Gray, OnFillChanged));

        public Brush Fill
        {
            get => (Brush)GetValue(FillProperty);
            set => SetValue(FillProperty, value);
        }

        private static void OnFillChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is Ellipse el) el.Background = (Brush)e.NewValue;
        }

        public Ellipse()
        {
            CornerRadius = new CornerRadius(5);
            VerticalAlignment = VerticalAlignment.Center;
        }
    }
}
