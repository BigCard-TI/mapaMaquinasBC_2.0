using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using MapaMaquinas.Controls;
using MapaMaquinas.Services;
using MapaMaquinas.Models;
using MapaMaquinas.Services;
using MapaMaquinas.Views;

namespace MapaMaquinas
{
    public class MainWindow : Window
    {
        // ── Infraestrutura ────────────────────────────────────────────────────
        private readonly Repositorio  _repositorio = new();
        private readonly JsonManager  _jsonManager;
        private readonly Config       _config;

        // ── Estado ────────────────────────────────────────────────────────────
        private Empresa? _empresaAtual;
        private bool     _alterado;

        // ── UI ────────────────────────────────────────────────────────────────
        private TabControl        _tabEmpresas     = null!;
        private Canvas            _mapaCanvas      = null!;
        private ScrollViewer      _mapaScroll      = null!;
        private Image             _imgFundo        = null!;
        private TextBox           _edBusca         = null!;
        private TextBlock         _lblStatus       = null!;
        private MenuItem          _menuSalvar      = null!;
        private MenuItem          _menuExportarPng = null!;

        private readonly List<CardMaquina> _cards      = new();
        private readonly List<CardPorta>   _cardsPorta = new();
        private readonly List<CardMaquina> _highlight  = new();

        // ── Fila de ping ──────────────────────────────────────────────────────
        private PingQueue? _pingQueue;

        // ── Zoom ──────────────────────────────────────────────────────────────
        private double         _escala         = 1.0;
        private const double   EscalaMin       = 0.25;
        private const double   EscalaMax       = 3.0;
        private const double   EscalaStep      = 0.1;
        private ScaleTransform _scaleTransform = new(1, 1);
        private TextBlock      _lblZoom        = null!;

        public MainWindow()
        {
            _config      = new Config();
            _jsonManager = new JsonManager(_repositorio);

            Title        = "Mapa Máquinas — BigCard TI v2.0";
            Width        = 1280;
            Height       = 780;
            MinWidth     = 800;
            MinHeight    = 500;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;

            Content = CriarLayout();
            _pingQueue = new PingQueue(Dispatcher);
            _pingQueue.ProgressoAtualizado += OnPingProgresso;
            _pingQueue.CicloCompleto       += OnPingCicloCompleto;
            Loaded += OnLoaded;
        }

        // ── Layout ────────────────────────────────────────────────────────────
        private UIElement CriarLayout()
        {
            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });  // Menu
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });  // Toolbar
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Conteúdo
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });  // Status

            // ── Menu ──────────────────────────────────────────────────────────
            var menuBar = CriarMenu();
            Grid.SetRow(menuBar, 0);
            grid.Children.Add(menuBar);

            // ── Toolbar ───────────────────────────────────────────────────────
            var toolbar = CriarToolbar();
            Grid.SetRow(toolbar, 1);
            grid.Children.Add(toolbar);

            // ── Corpo ─────────────────────────────────────────────────────────
            var splitter = new Grid();
            splitter.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(160) });
            splitter.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(4) });
            splitter.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // Lateral: abas de empresas + legenda de status
            var painelLateral = new Grid();
            painelLateral.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            painelLateral.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            _tabEmpresas = new TabControl { Margin = new Thickness(0) };
            _tabEmpresas.SelectionChanged += OnEmpresaChanged;
            Grid.SetRow(_tabEmpresas, 0);
            painelLateral.Children.Add(_tabEmpresas);

            painelLateral.Children.Add(CriarLegenda());
            Grid.SetRow(painelLateral.Children[1], 1);

            Grid.SetColumn(painelLateral, 0);
            splitter.Children.Add(painelLateral);

            // Divisor
            var gs = new GridSplitter
            {
                Width = 4, HorizontalAlignment = HorizontalAlignment.Stretch,
                Background = Brushes.LightGray
            };
            Grid.SetColumn(gs, 1);
            splitter.Children.Add(gs);

            // Mapa
            _mapaScroll = new ScrollViewer
            {
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility   = ScrollBarVisibility.Auto
            };
            _mapaCanvas = new Canvas
            {
                Background = new SolidColorBrush(Color.FromRgb(230, 230, 235)),
                ClipToBounds = true
            };
            _imgFundo = new Image
            {
                Stretch = Stretch.None,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top
            };
            _mapaCanvas.Children.Add(_imgFundo);

            // Zoom via ScaleTransform no canvas
            _scaleTransform = new ScaleTransform(1, 1);
            _mapaCanvas.RenderTransform = _scaleTransform;
            _mapaCanvas.RenderTransformOrigin = new Point(0, 0);
            _mapaScroll.PreviewMouseWheel += OnMapaMouseWheel;

            _mapaScroll.Content = _mapaCanvas;
            Grid.SetColumn(_mapaScroll, 2);
            splitter.Children.Add(_mapaScroll);

            Grid.SetRow(splitter, 2);
            grid.Children.Add(splitter);

            // ── Status bar ────────────────────────────────────────────────────
            var statusBar = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(240, 240, 240)),
                BorderBrush = Brushes.LightGray, BorderThickness = new Thickness(0, 1, 0, 0),
                Padding = new Thickness(8, 3, 8, 3)
            };
            _lblStatus = new TextBlock { FontSize = 11 };
            statusBar.Child = _lblStatus;
            Grid.SetRow(statusBar, 3);
            grid.Children.Add(statusBar);

            return grid;
        }

        private UIElement CriarLegenda()
        {
            var border = new Border
            {
                Background      = new SolidColorBrush(Color.FromRgb(245, 245, 248)),
                BorderBrush     = new SolidColorBrush(Color.FromRgb(210, 210, 215)),
                BorderThickness = new Thickness(0, 1, 0, 0),
                Padding         = new Thickness(10, 8, 10, 10)
            };

            var stack = new StackPanel { Orientation = Orientation.Vertical };

            // Título
            stack.Children.Add(new TextBlock
            {
                Text       = "STATUS DO PING",
                FontSize   = 9,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(100, 100, 110)),
                Margin     = new Thickness(0, 0, 0, 6)
            });

            // Explicação da barra dividida
            stack.Children.Add(new TextBlock
            {
                Text         = "A barra é dividida em duas metades:",
                FontSize     = 8,
                Foreground   = new SolidColorBrush(Color.FromRgb(90, 90, 100)),
                TextWrapping = TextWrapping.Wrap,
                Margin       = new Thickness(0, 0, 0, 6)
            });

            AdicionarItemLegenda(stack, Color.FromRgb(50,  205, 50),  "Verde",
                "Ping respondeu");
            AdicionarItemLegenda(stack, Color.FromRgb(210, 50,  50),  "Vermelho",
                "Sem resposta");
            AdicionarItemLegenda(stack, Color.FromRgb(255, 190, 0),   "Amarelo",
                "Aguardando verificação");
            AdicionarItemLegenda(stack, Color.FromRgb(110, 110, 110), "Cinza",
                "Sem dado cadastrado");

            // Separador
            stack.Children.Add(new Border
            {
                BorderBrush = new SolidColorBrush(Color.FromRgb(210, 210, 215)),
                BorderThickness = new Thickness(0, 1, 0, 0),
                Margin = new Thickness(0, 4, 0, 6)
            });

            // Exemplo visual da barra dividida
            stack.Children.Add(new TextBlock
            {
                Text         = "Exemplo:",
                FontSize     = 8,
                FontWeight   = FontWeights.SemiBold,
                Foreground   = new SolidColorBrush(Color.FromRgb(90, 90, 100)),
                Margin       = new Thickness(0, 0, 0, 4)
            });

            AdicionarItemLegendaDupla(stack,
                Color.FromRgb(50,  205, 50),
                Color.FromRgb(50,  205, 50),
                "Tudo OK",
                "Hostname e IP respondem");

            AdicionarItemLegendaDupla(stack,
                Color.FromRgb(50,  205, 50),
                Color.FromRgb(210, 50,  50),
                "IP errado",
                "Hostname OK, IP sem resposta");

            AdicionarItemLegendaDupla(stack,
                Color.FromRgb(210, 50,  50),
                Color.FromRgb(50,  205, 50),
                "Nome errado",
                "IP OK, hostname sem resposta");

            AdicionarItemLegendaDupla(stack,
                Color.FromRgb(210, 50,  50),
                Color.FromRgb(210, 50,  50),
                "Offline",
                "Nenhum respondeu");

            border.Child = stack;
            return border;
        }

        private static void AdicionarItemLegenda(StackPanel parent, Color cor,
                                                  string titulo, string descricao)
        {
            var row = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin      = new Thickness(0, 0, 0, 5)
            };

            row.Children.Add(new Border
            {
                Width             = 5,
                Height            = 20,
                Background        = new SolidColorBrush(cor),
                CornerRadius      = new CornerRadius(1),
                Margin            = new Thickness(0, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Center
            });

            var textos = new StackPanel { Orientation = Orientation.Vertical,
                                          VerticalAlignment = VerticalAlignment.Center };
            textos.Children.Add(new TextBlock
            {
                Text       = titulo,
                FontSize   = 10,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.Black
            });
            textos.Children.Add(new TextBlock
            {
                Text         = descricao,
                FontSize     = 9,
                Foreground   = new SolidColorBrush(Color.FromRgb(90, 90, 100)),
                TextWrapping = TextWrapping.Wrap
            });
            row.Children.Add(textos);
            parent.Children.Add(row);
        }

        /// <summary>Item de legenda com barra dividida — metade superior e inferior.</summary>
        private static void AdicionarItemLegendaDupla(StackPanel parent,
            Color corCima, Color corBaixo, string titulo, string descricao)
        {
            var row = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin      = new Thickness(0, 0, 0, 5)
            };

            // Barra dividida verticalmente
            var barra = new Grid { Width = 5, Height = 28, Margin = new Thickness(0, 0, 8, 0) };
            barra.RowDefinitions.Add(new RowDefinition());
            barra.RowDefinitions.Add(new RowDefinition());

            var top = new Border { Background = new SolidColorBrush(corCima),
                                   CornerRadius = new CornerRadius(1, 1, 0, 0) };
            var bot = new Border { Background = new SolidColorBrush(corBaixo),
                                   CornerRadius = new CornerRadius(0, 0, 1, 1) };
            Grid.SetRow(top, 0);
            Grid.SetRow(bot, 1);
            barra.Children.Add(top);
            barra.Children.Add(bot);
            row.Children.Add(barra);

            var textos = new StackPanel { Orientation = Orientation.Vertical,
                                          VerticalAlignment = VerticalAlignment.Center };
            textos.Children.Add(new TextBlock
            {
                Text       = titulo,
                FontSize   = 10,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.Black
            });
            textos.Children.Add(new TextBlock
            {
                Text         = descricao,
                FontSize     = 9,
                Foreground   = new SolidColorBrush(Color.FromRgb(90, 90, 100)),
                TextWrapping = TextWrapping.Wrap
            });
            row.Children.Add(textos);
            parent.Children.Add(row);
        }

        private Menu CriarMenu()
        {
            var menu = new Menu();

            // ── Arquivo ───────────────────────────────────────────────────────
            var mArquivo = new MenuItem { Header = "_Arquivo" };

            var mAbrir = new MenuItem { Header = "_Abrir dados...", InputGestureText = "Ctrl+O" };
            mAbrir.Click += (_, _) => AbrirArquivo();
            mArquivo.Items.Add(mAbrir);

            _menuSalvar = new MenuItem { Header = "_Salvar", InputGestureText = "Ctrl+S", IsEnabled = false };
            _menuSalvar.Click += (_, _) => Salvar();
            mArquivo.Items.Add(_menuSalvar);

            mArquivo.Items.Add(new Separator());

            var mConfig = new MenuItem { Header = "_Configurar caminho..." };
            mConfig.Click += (_, _) => ConfigurarCaminho();
            mArquivo.Items.Add(mConfig);

            mArquivo.Items.Add(new Separator());

            _menuExportarPng = new MenuItem { Header = "_Exportar PNG...", IsEnabled = false };
            _menuExportarPng.Click += (_, _) => ExportarPng();
            mArquivo.Items.Add(_menuExportarPng);

            mArquivo.Items.Add(new Separator());
            var mSair = new MenuItem { Header = "Sai_r" };
            mSair.Click += (_, _) => Close();
            mArquivo.Items.Add(mSair);

            menu.Items.Add(mArquivo);

            // ── Máquinas ──────────────────────────────────────────────────────
            var mMaq = new MenuItem { Header = "_Máquinas" };

            var mNovaMaq = new MenuItem { Header = "_Nova máquina...", InputGestureText = "Ins" };
            mNovaMaq.Click += (_, _) => NovaMaquina();
            mMaq.Items.Add(mNovaMaq);

            mMaq.Items.Add(new Separator());

            var mSetores = new MenuItem { Header = "Gerenciar _setores..." };
            mSetores.Click += (_, _) => GerenciarSetores();
            mMaq.Items.Add(mSetores);

            menu.Items.Add(mMaq);

            // ── Portas ────────────────────────────────────────────────────────
            var mPortas = new MenuItem { Header = "_Portas" };
            var mNovaPorta = new MenuItem { Header = "_Nova porta de switch..." };
            mNovaPorta.Click += (_, _) => NovaPorta();
            mPortas.Items.Add(mNovaPorta);
            menu.Items.Add(mPortas);

            return menu;
        }

        private UIElement CriarToolbar()
        {
            var panel = new ToolBar { Height = 34 };

            var btnAbrir = new Button { Content = "📂 Abrir", Margin = new Thickness(2), ToolTip = "Abrir arquivo de dados" };
            btnAbrir.Click += (_, _) => AbrirArquivo();
            panel.Items.Add(btnAbrir);

            var btnSalvar = new Button { Content = "💾 Salvar", Margin = new Thickness(2), ToolTip = "Salvar alterações (Ctrl+S)" };
            btnSalvar.Click += (_, _) => Salvar();
            panel.Items.Add(btnSalvar);

            panel.Items.Add(new Separator());

            var btnNovaMaq = new Button { Content = "+ Máquina", Margin = new Thickness(2) };
            btnNovaMaq.Click += (_, _) => NovaMaquina();
            panel.Items.Add(btnNovaMaq);

            var btnNovaPorta = new Button { Content = "+ Porta", Margin = new Thickness(2) };
            btnNovaPorta.Click += (_, _) => NovaPorta();
            panel.Items.Add(btnNovaPorta);

            var btnSetores = new Button { Content = "⚙ Setores", Margin = new Thickness(2) };
            btnSetores.Click += (_, _) => GerenciarSetores();
            panel.Items.Add(btnSetores);

            panel.Items.Add(new Separator());

            // Busca
            var lblBusca = new TextBlock { Text = "Buscar:", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0, 4, 0) };
            panel.Items.Add(lblBusca);

            _edBusca = new TextBox { Width = 180, Margin = new Thickness(2), VerticalContentAlignment = VerticalAlignment.Center };
            _edBusca.KeyDown += (_, e) => { if (e.Key == Key.Enter) BuscarMaquina(); };
            panel.Items.Add(_edBusca);

            var btnBuscar = new Button { Content = "🔍", Margin = new Thickness(2), ToolTip = "Buscar máquina" };
            btnBuscar.Click += (_, _) => BuscarMaquina();
            panel.Items.Add(btnBuscar);

            var btnLimpar = new Button { Content = "✕", Margin = new Thickness(2), ToolTip = "Limpar busca" };
            btnLimpar.Click += (_, _) => LimparBusca();
            panel.Items.Add(btnLimpar);

            panel.Items.Add(new Separator());

            // ── Controles de Zoom ─────────────────────────────────────────────
            var btnZoomOut = new Button { Content = "−", Width = 24, Margin = new Thickness(2), ToolTip = "Diminuir zoom (Ctrl+−)" };
            btnZoomOut.Click += (_, _) => AjustarZoom(-EscalaStep);
            panel.Items.Add(btnZoomOut);

            _lblZoom = new TextBlock
            {
                Text = "100%", Width = 38, TextAlignment = TextAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 11, Margin = new Thickness(2, 0, 2, 0),
                ToolTip = "Clique para resetar zoom"
            };
            _lblZoom.MouseLeftButtonDown += (_, _) => ResetarZoom();
            _lblZoom.Cursor = Cursors.Hand;
            panel.Items.Add(_lblZoom);

            var btnZoomIn = new Button { Content = "+", Width = 24, Margin = new Thickness(2), ToolTip = "Aumentar zoom (Ctrl++)" };
            btnZoomIn.Click += (_, _) => AjustarZoom(+EscalaStep);
            panel.Items.Add(btnZoomIn);

            return panel;
        }

        // ── Carregamento ──────────────────────────────────────────────────────
        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            KeyDown += OnKeyDown;
            if (_config.CaminhoValido())
            {
                var arquivo = _config.Arquivo("mapa_maquinas.json");
                if (File.Exists(arquivo))
                    CarregarArquivo(arquivo);
                else
                    AtualizarStatus("Arquivo mapa_maquinas.json não encontrado. Use Arquivo > Abrir para localizar.");
            }
            else
            {
                AtualizarStatus("Caminho de dados não configurado. Use Arquivo > Configurar caminho...");
            }
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.S && (Keyboard.Modifiers & ModifierKeys.Control) != 0) Salvar();
            if (e.Key == Key.O && (Keyboard.Modifiers & ModifierKeys.Control) != 0) AbrirArquivo();
            if (e.Key == Key.Insert) NovaMaquina();
            if ((Keyboard.Modifiers & ModifierKeys.Control) != 0)
            {
                if (e.Key == Key.OemPlus  || e.Key == Key.Add)      AjustarZoom(+EscalaStep);
                if (e.Key == Key.OemMinus || e.Key == Key.Subtract)  AjustarZoom(-EscalaStep);
                if (e.Key == Key.D0       || e.Key == Key.NumPad0)   ResetarZoom();
            }
        }

        private void AbrirArquivo()
        {
            var dlg = new OpenFileDialog
            {
                Filter = "JSON|*.json|Todos|*.*",
                Title  = "Abrir arquivo de dados"
            };
            if (dlg.ShowDialog() != true) return;
            CarregarArquivo(dlg.FileName);
        }

        private void CarregarArquivo(string caminho)
        {
            try
            {
                _jsonManager.CarregarDoArquivo(caminho);
                PopularAbas();
                _alterado = false;
                _menuSalvar.IsEnabled = true;
                _menuExportarPng.IsEnabled = true;
                AtualizarStatus($"Carregado: {caminho}  |  {_repositorio.Empresas.Count} empresa(s)");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erro ao carregar:\n{ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void PopularAbas()
        {
            // Desconecta o evento para não disparar durante a montagem
            _tabEmpresas.SelectionChanged -= OnEmpresaChanged;
            _tabEmpresas.Items.Clear();

            foreach (var emp in _repositorio.Empresas)
            {
                var tab = new TabItem { Header = emp.Nome, Tag = emp };
                _tabEmpresas.Items.Add(tab);
            }

            // Reconecta e carrega a primeira empresa manualmente
            _tabEmpresas.SelectionChanged += OnEmpresaChanged;

            if (_tabEmpresas.Items.Count > 0)
            {
                _tabEmpresas.SelectedIndex = 0;
                var primeiraTab = (TabItem)_tabEmpresas.Items[0];
                _empresaAtual = (Empresa)primeiraTab.Tag;
                CarregarMapa(_empresaAtual);
            }
        }

        private void OnEmpresaChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_tabEmpresas.SelectedItem is TabItem tab && tab.Tag is Empresa emp)
            {
                _empresaAtual = emp;
                CarregarMapa(emp);
            }
        }

        // ── Mapa ──────────────────────────────────────────────────────────────
        private void CarregarMapa(Empresa empresa)
        {
            LimparCards();

            // Imagem de fundo
            _imgFundo.Source = null;
            if (!string.IsNullOrEmpty(empresa.MapaArquivo))
            {
                var caminhoImg = Path.IsPathRooted(empresa.MapaArquivo)
                    ? empresa.MapaArquivo
                    : _config.Arquivo(empresa.MapaArquivo);

                if (File.Exists(caminhoImg))
                {
                    try
                    {
                        var bmp = new BitmapImage(new Uri(caminhoImg, UriKind.Absolute));
                        _imgFundo.Source = bmp;
                        _mapaCanvas.Width  = bmp.PixelWidth;
                        _mapaCanvas.Height = bmp.PixelHeight;
                    }
                    catch { }
                }
            }

            if (_mapaCanvas.Width < 200)  _mapaCanvas.Width  = 2000;
            if (_mapaCanvas.Height < 200) _mapaCanvas.Height = 1200;

            // Cards de máquinas — posicionados exatamente onde estão salvos no JSON
            foreach (var m in empresa.Maquinas)
            {
                var setor = empresa.BuscarSetor(m.SetorId);
                var card  = new CardMaquina(m, setor);
                card.Editar     += (s, _) => EditarMaquina(card);
                card.Remover    += (s, _) => RemoverMaquina(card);
                card.Visualizar += (s, _) => VisualizarMaquina(card);
                card.MouseLeftButtonUp += (_, _) => MarcarAlterado();

                Canvas.SetLeft(card, m.PosX);
                Canvas.SetTop(card, m.PosY);

                _mapaCanvas.Children.Add(card);
                _cards.Add(card);

                // Registra na fila de ping e conecta o "Verificar agora"
                _pingQueue!.AdicionarCard(card);
                card.OnPingarAgora = () => _pingQueue.PingarAgora(card);
            }

            // Inicia a fila: pinga uma máquina por vez, aguarda 2 min entre ciclos
            _pingQueue!.Iniciar(_cards);

            // Cards de portas
            foreach (var p in empresa.Portas)
            {
                var cardP = new CardPorta(p);
                cardP.Editar  += (_, _) => EditarPorta(cardP);
                cardP.Remover += (_, _) => RemoverPorta(cardP);
                cardP.MouseLeftButtonUp += (_, _) => MarcarAlterado();

                Canvas.SetLeft(cardP, p.PosX);
                Canvas.SetTop(cardP, p.PosY);
                _mapaCanvas.Children.Add(cardP);
                _cardsPorta.Add(cardP);
            }

            AtualizarStatus($"{empresa.Nome}  |  {empresa.Maquinas.Count} máquina(s)  |  {empresa.Portas.Count} porta(s)");
        }

        private void LimparCards()
        {
            // Para a fila centralizada antes de descartar os cards
            _pingQueue?.Parar();

            foreach (var c in _cards)      _mapaCanvas.Children.Remove(c);
            foreach (var c in _cardsPorta) _mapaCanvas.Children.Remove(c);
            _cards.Clear();
            _cardsPorta.Clear();
            _highlight.Clear();
        }

        // ── CRUD Máquinas ─────────────────────────────────────────────────────
        private void NovaMaquina()
        {
            if (_empresaAtual == null) { MessageBox.Show("Abra um arquivo primeiro."); return; }
            if (_empresaAtual.Setores.Count == 0) { MessageBox.Show("Cadastre ao menos um setor antes de adicionar máquinas."); return; }

            var dlg = new JanelaEdicaoMaquina(this, _empresaAtual, null);
            if (dlg.ShowDialog() != true || dlg.Maquina == null) return;

            var m = dlg.Maquina;
            m.PosX = 20; m.PosY = 20;
            _empresaAtual.Maquinas.Add(m);

            var setor = _empresaAtual.BuscarSetor(m.SetorId);
            var card  = new CardMaquina(m, setor);
            card.Editar     += (_, _) => EditarMaquina(card);
            card.Remover    += (_, _) => RemoverMaquina(card);
            card.Visualizar += (_, _) => VisualizarMaquina(card);
            card.MouseLeftButtonUp += (_, _) => MarcarAlterado();

            Canvas.SetLeft(card, m.PosX);
            Canvas.SetTop(card, m.PosY);
            _mapaCanvas.Children.Add(card);
            _cards.Add(card);
            MarcarAlterado();
        }

        private void EditarMaquina(CardMaquina card)
        {
            if (_empresaAtual == null || card.Maquina == null) return;
            var dlg = new JanelaEdicaoMaquina(this, _empresaAtual, card.Maquina);
            if (dlg.ShowDialog() != true) return;

            var setor = _empresaAtual.BuscarSetor(card.Maquina.SetorId);
            card.AtualizarSetor(setor!);
            card.Maquina = card.Maquina; // força re-render
            MarcarAlterado();
        }

        private void RemoverMaquina(CardMaquina card)
        {
            if (card.Maquina == null) return;
            if (MessageBox.Show($"Remover a máquina \"{card.Maquina.Hostname}\"?", "Confirmar",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;

            _pingQueue?.RemoverCard(card);
            _empresaAtual?.Maquinas.Remove(card.Maquina);
            _mapaCanvas.Children.Remove(card);
            _cards.Remove(card);
            MarcarAlterado();
        }

        private void VisualizarMaquina(CardMaquina card)
        {
            if (card.Maquina == null) return;
            var setor = _empresaAtual?.BuscarSetor(card.Maquina.SetorId);
            new JanelaVisualizacao(this, card.Maquina, setor).ShowDialog();
        }

        // ── CRUD Portas ───────────────────────────────────────────────────────
        private void NovaPorta()
        {
            if (_empresaAtual == null) { MessageBox.Show("Abra um arquivo primeiro."); return; }

            var dlg = new JanelaEdicaoPorta(this, _empresaAtual);
            if (dlg.ShowDialog() != true || dlg.Porta == null) return;

            var p = dlg.Porta;
            p.PosX = 20; p.PosY = 20;
            _empresaAtual.Portas.Add(p);

            var cardP = new CardPorta(p);
            cardP.Editar  += (_, _) => EditarPorta(cardP);
            cardP.Remover += (_, _) => RemoverPorta(cardP);
            cardP.MouseLeftButtonUp += (_, _) => MarcarAlterado();

            Canvas.SetLeft(cardP, p.PosX);
            Canvas.SetTop(cardP, p.PosY);
            _mapaCanvas.Children.Add(cardP);
            _cardsPorta.Add(cardP);
            MarcarAlterado();
        }

        private void EditarPorta(CardPorta card)
        {
            if (_empresaAtual == null || card.Porta == null) return;
            var dlg = new JanelaEdicaoPorta(this, _empresaAtual, card.Porta);
            if (dlg.ShowDialog() != true) return;
            card.Porta = card.Porta; // força re-render
            MarcarAlterado();
        }

        private void RemoverPorta(CardPorta card)
        {
            if (card.Porta == null) return;
            if (MessageBox.Show($"Remover a porta \"{card.Porta.Numero}\"?", "Confirmar",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;

            _empresaAtual?.Portas.Remove(card.Porta);
            _mapaCanvas.Children.Remove(card);
            _cardsPorta.Remove(card);
            MarcarAlterado();
        }

        // ── Setores ───────────────────────────────────────────────────────────
        private void GerenciarSetores()
        {
            if (_empresaAtual == null) { MessageBox.Show("Abra um arquivo primeiro."); return; }
            var dlg = new JanelaSetores(this, _empresaAtual);
            dlg.ShowDialog();
            if (!dlg.Alterado) return;

            // Atualiza cores nos cards
            foreach (var card in _cards)
            {
                if (card.Maquina == null) continue;
                var setor = _empresaAtual.BuscarSetor(card.Maquina.SetorId);
                card.AtualizarSetor(setor!);
            }
            MarcarAlterado();
        }

        // ── Busca ─────────────────────────────────────────────────────────────
        private void BuscarMaquina()
        {
            LimparBuscaSemClear();
            var termo = _edBusca.Text.Trim();
            if (string.IsNullOrEmpty(termo)) return;

            foreach (var card in _cards)
            {
                if (card.Maquina == null) continue;
                var m = card.Maquina;
                var hit =
                    m.Hostname.Contains(termo, StringComparison.OrdinalIgnoreCase) ||
                    m.Ip.Contains(termo, StringComparison.OrdinalIgnoreCase) ||
                    m.Ramal.Contains(termo, StringComparison.OrdinalIgnoreCase) ||
                    m.PortaSwitch.Contains(termo, StringComparison.OrdinalIgnoreCase);

                if (hit)
                {
                    card.SetHighlight(true);
                    _highlight.Add(card);
                    _mapaScroll.ScrollToHorizontalOffset(Canvas.GetLeft(card) - 100);
                    _mapaScroll.ScrollToVerticalOffset(Canvas.GetTop(card) - 100);
                }
            }

            AtualizarStatus($"Busca \"{termo}\": {_highlight.Count} resultado(s)");
        }

        private void LimparBusca()
        {
            _edBusca.Clear();
            LimparBuscaSemClear();
            if (_empresaAtual != null)
                AtualizarStatus($"{_empresaAtual.Nome}  |  {_empresaAtual.Maquinas.Count} máquina(s)");
        }

        private void LimparBuscaSemClear()
        {
            foreach (var c in _highlight) c.SetHighlight(false);
            _highlight.Clear();
        }

        // ── Salvar / Exportar ─────────────────────────────────────────────────
        private void Salvar()
        {
            if (string.IsNullOrEmpty(_jsonManager.CaminhoArquivo)) { MessageBox.Show("Nenhum arquivo carregado."); return; }

            // Salva posições atuais de todos os cards
            foreach (var c in _cards)      c.SalvarPosicao();
            foreach (var c in _cardsPorta) c.SalvarPosicao();

            try
            {
                _jsonManager.SalvarNoArquivo();
                _alterado = false;
                AtualizarStatus($"Salvo em {_jsonManager.CaminhoArquivo}  —  {DateTime.Now:HH:mm:ss}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erro ao salvar:\n{ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExportarPng()
        {
            var dlg = new SaveFileDialog { Filter = "PNG|*.png", FileName = $"mapa_{_empresaAtual?.Nome ?? "export"}.png" };
            if (dlg.ShowDialog() != true) return;

            try
            {
                var rtb = new RenderTargetBitmap(
                    (int)_mapaCanvas.ActualWidth, (int)_mapaCanvas.ActualHeight, 96, 96, PixelFormats.Pbgra32);
                rtb.Render(_mapaCanvas);

                using var stream = File.Create(dlg.FileName);
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(rtb));
                encoder.Save(stream);

                AtualizarStatus($"PNG exportado: {dlg.FileName}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erro ao exportar:\n{ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ── Config ────────────────────────────────────────────────────────────
        private void ConfigurarCaminho()
        {
            // Janela simples com campo de texto para digitar o caminho
            var win = new Window
            {
                Title  = "Configurar caminho dos dados",
                Width  = 500, Height = 160,
                ResizeMode = ResizeMode.NoResize,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this
            };

            var stack = new StackPanel { Margin = new Thickness(16) };

            stack.Children.Add(new TextBlock
            {
                Text = "Caminho da pasta com o arquivo mapa_maquinas.json\n(pode ser caminho de rede, ex: \\\\servidor\\Interno\\TI\\)",
                Margin = new Thickness(0, 0, 0, 8), TextWrapping = TextWrapping.Wrap
            });

            var edCaminho = new TextBox
            {
                Text = _config.CaminhoDados,
                Margin = new Thickness(0, 0, 0, 12)
            };
            stack.Children.Add(edCaminho);

            var btnRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var btnOk     = new Button { Content = "Salvar", Width = 90, Margin = new Thickness(0, 0, 8, 0), IsDefault = true };
            var btnCancel = new Button { Content = "Cancelar", Width = 90, IsCancel = true };

            btnOk.Click += (_, _) =>
            {
                var caminho = edCaminho.Text.Trim();
                if (string.IsNullOrEmpty(caminho)) { MessageBox.Show("Informe um caminho."); return; }
                _config.SetCaminhoDados(caminho);

                // Tenta carregar automaticamente após confirmar
                var arquivo = _config.Arquivo("mapa_maquinas.json");
                win.Close();

                if (File.Exists(arquivo))
                    CarregarArquivo(arquivo);
                else
                    AtualizarStatus($"Caminho salvo. Arquivo mapa_maquinas.json não encontrado em: {caminho}");
            };
            btnCancel.Click += (_, _) => win.Close();

            btnRow.Children.Add(btnOk);
            btnRow.Children.Add(btnCancel);
            stack.Children.Add(btnRow);

            win.Content = stack;
            win.ShowDialog();
        }

        // ── Helpers ───────────────────────────────────────────────────────────
        // ── Zoom ──────────────────────────────────────────────────────────────
        private void OnMapaMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if ((Keyboard.Modifiers & ModifierKeys.Control) == 0) return;
            e.Handled = true;   // impede o ScrollViewer de rolar

            // Ponto do mouse relativo ao canvas antes do zoom
            var pontoAntes = e.GetPosition(_mapaCanvas);

            AjustarZoom(e.Delta > 0 ? EscalaStep : -EscalaStep);

            // Ajusta o scroll para manter o ponto sob o cursor estável
            var pontoDepois = e.GetPosition(_mapaCanvas);
            var delta = pontoDepois - pontoAntes;
            _mapaScroll.ScrollToHorizontalOffset(_mapaScroll.HorizontalOffset - delta.X * _escala);
            _mapaScroll.ScrollToVerticalOffset  (_mapaScroll.VerticalOffset   - delta.Y * _escala);
        }

        private void AjustarZoom(double delta)
        {
            _escala = Math.Round(Math.Max(EscalaMin, Math.Min(EscalaMax, _escala + delta)), 2);
            _scaleTransform.ScaleX = _escala;
            _scaleTransform.ScaleY = _escala;
            _lblZoom.Text = $"{(int)(_escala * 100)}%";
        }

        private void ResetarZoom()
        {
            _escala = 1.0;
            _scaleTransform.ScaleX = 1;
            _scaleTransform.ScaleY = 1;
            _lblZoom.Text = "100%";
        }

        // ── Callbacks do PingQueue ────────────────────────────────────────────
        private void OnPingProgresso(int atual, int total)
        {
            if (atual == 0)
                AtualizarStatus(_lblStatus.Text.Split('|')[0].TrimEnd() +
                    $"  |  Ping: aguardando próximo ciclo (2 min)");
            else
                AtualizarStatus(_lblStatus.Text.Split('|')[0].TrimEnd() +
                    $"  |  Ping: verificando {atual}/{total}...");
        }

        private void OnPingCicloCompleto()
        {
            // Nada especial — o progresso já foi resetado para "aguardando"
        }

        private void MarcarAlterado()
        {
            _alterado = true;
            if (!Title.EndsWith("*")) Title = Title.TrimEnd() + " *";
        }

        private void AtualizarStatus(string msg) => _lblStatus.Text = msg;

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            _pingQueue?.Parar();
            if (_alterado)
            {
                var resp = MessageBox.Show("Há alterações não salvas. Salvar antes de fechar?",
                    "Alterações pendentes", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
                if (resp == MessageBoxResult.Cancel) { e.Cancel = true; return; }
                if (resp == MessageBoxResult.Yes)    Salvar();
            }
            base.OnClosing(e);
        }
    }
}
