using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MapaMaquinas.Models;

namespace MapaMaquinas.Views
{
    public class JanelaVisualizacao : Window
    {
        public JanelaVisualizacao(Window owner, Maquina maquina, Setor? setor)
        {
            Owner  = owner;
            Title  = $"Detalhes — {maquina.Hostname}";
            Width  = 420;
            Height = 480;
            ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;

            var corFundo = setor?.CorAsColor() ?? Color.FromRgb(136, 136, 136);
            var nomeSetor = setor?.Nome ?? maquina.SetorId;

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(70) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Topo colorido
            var topo = new Border { Background = new SolidColorBrush(corFundo), Padding = new Thickness(16, 10, 16, 10) };
            var topoStack = new StackPanel();
            topoStack.Children.Add(new TextBlock
            {
                Text = maquina.Hostname, FontSize = 16, FontWeight = FontWeights.Bold,
                Foreground = Brushes.Black
            });
            topoStack.Children.Add(new TextBlock
            {
                Text = nomeSetor, FontSize = 9, Foreground = Brushes.Black, Margin = new Thickness(2, 4, 0, 0)
            });
            topo.Child = topoStack;
            Grid.SetRow(topo, 0);
            grid.Children.Add(topo);

            // Conteúdo com scroll
            var scroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Background = Brushes.White,
                Padding = new Thickness(16, 12, 16, 12)
            };
            var content = new StackPanel();

            void AddLinha(string label, string valor, bool destaque = false)
            {
                if (string.IsNullOrEmpty(valor)) return;
                var row = new Grid { Margin = new Thickness(0, 0, 0, 2) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                var lbl = new TextBlock
                {
                    Text = label, FontSize = 8,
                    Foreground = new SolidColorBrush(Color.FromRgb(136, 136, 136)),
                    VerticalAlignment = VerticalAlignment.Top
                };
                var val = new TextBlock
                {
                    Text = valor, FontSize = destaque ? 9 : 8,
                    FontWeight = destaque ? FontWeights.Bold : FontWeights.Normal,
                    TextWrapping = TextWrapping.Wrap
                };
                Grid.SetColumn(lbl, 0);
                Grid.SetColumn(val, 1);
                row.Children.Add(lbl);
                row.Children.Add(val);
                content.Children.Add(row);
                content.Children.Add(new Border
                {
                    BorderBrush = new SolidColorBrush(Color.FromRgb(220, 220, 220)),
                    BorderThickness = new Thickness(0, 0, 0, 1),
                    Margin = new Thickness(0, 2, 0, 4)
                });
            }

            AddLinha("Hostname",     maquina.Hostname,    destaque: true);
            AddLinha("IP",           maquina.Ip,          destaque: true);
            if (!string.IsNullOrEmpty(maquina.Ramal))
                AddLinha("Ramal",    maquina.Ramal,       destaque: true);
            AddLinha("Tipo",         TipoAmigavel(maquina.Tipo));
            AddLinha("Porta Switch", maquina.PortaSwitch);
            AddLinha("Processador",  maquina.Processador);
            AddLinha("RAM",          maquina.Ram);
            AddLinha("Storage",      maquina.Storage);
            AddLinha("Setor",        nomeSetor);
            AddLinha("Observações",  maquina.Observacoes);

            scroll.Content = content;
            Grid.SetRow(scroll, 1);
            grid.Children.Add(scroll);

            // Botão fechar
            var btnFechar = new Button
            {
                Content = "Fechar", Width = 90, Margin = new Thickness(8),
                HorizontalAlignment = HorizontalAlignment.Right,
                IsCancel = true
            };
            btnFechar.Click += (_, _) => Close();
            Grid.SetRow(btnFechar, 2);
            grid.Children.Add(btnFechar);

            Content = grid;
        }

        private static string TipoAmigavel(string tipo) => tipo.ToLower() switch
        {
            "desktop"    => "Desktop",
            "notebook"   => "Notebook",
            "mac"        => "Mac",
            "servidor"   => "Servidor",
            "impressora" => "Impressora",
            _            => string.IsNullOrEmpty(tipo) ? "—" : tipo
        };
    }
}
