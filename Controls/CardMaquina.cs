using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using MapaMaquinas.Models;
using MapaMaquinas.Services;

namespace MapaMaquinas.Controls
{
    /// <summary>
    /// Card visual de uma máquina no mapa.
    /// Drag-and-drop, menu de contexto, highlight piscante para busca,
    /// indicador de ping em tempo real no canto superior direito.
    /// </summary>
    public class CardMaquina : Canvas
    {
        // ── Tamanho ───────────────────────────────────────────────────────────
        private const double CardWidth   = 58;
        private const double AlturaBase  = 26;
        private const double AlturaRamal = 9;

        // ── Highlight ─────────────────────────────────────────────────────────
        private static readonly Color HighlightVivo  = Color.FromRgb(255, 0,   0);
        private static readonly Color HighlightMeio  = Color.FromRgb(255, 96,  0);
        private static readonly Color HighlightHalo  = Color.FromRgb(255, 192, 0);
        private static readonly Color HighlightFraco = Color.FromRgb(192, 128, 0);
        private const int HighlightIntervalo = 300;

        // ── Cores de ping ─────────────────────────────────────────────────────
        private static readonly Color PingOnline    = Color.FromRgb(50,  205, 50);   // verde
        private static readonly Color PingOffline   = Color.FromRgb(220, 50,  50);   // vermelho
        private static readonly Color PingAguardando = Color.FromRgb(255, 200, 0);   // amarelo
        private static readonly Color PingSemIp     = Color.FromRgb(120, 120, 120);  // cinza

        private const double PingDot = 5;   // diâmetro da bolinha

        // ── Estado ────────────────────────────────────────────────────────────
        private Maquina? _maquina;
        private Setor?   _setor;

        private bool   _dragging;
        private Point  _dragOrigin;
        private bool   _highlight;
        private bool   _blinkState;
        private DispatcherTimer? _blinkTimer;

        // ── Ping ──────────────────────────────────────────────────────────────
        private PingService?   _pingService;
        private ResultadoPing  _pingResult = new() { Status = StatusPing.Aguardando };

        // ── Eventos ───────────────────────────────────────────────────────────
        public event EventHandler? Editar;
        public event EventHandler? Remover;
        public event EventHandler? Visualizar;

        // ── Propriedades ──────────────────────────────────────────────────────
        public Maquina? Maquina
        {
            get => _maquina;
            set { _maquina = value; AtualizarVisual(); }
        }

        public Setor? Setor
        {
            get => _setor;
            set { _setor = value; AtualizarVisual(); }
        }

        public bool Highlight => _highlight;

        // ── Construtor ────────────────────────────────────────────────────────
        public CardMaquina(Maquina maquina, Setor? setor)
        {
            _maquina = maquina;
            _setor   = setor;
            Cursor   = Cursors.SizeAll;

            _blinkTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(HighlightIntervalo) };
            _blinkTimer.Tick += (_, _) => { _blinkState = !_blinkState; InvalidateVisual(); };

            CriarMenuContexto();
            AtualizarVisual();
            IniciarPing();
        }

        // ── Menu de contexto ──────────────────────────────────────────────────
        private void CriarMenuContexto()
        {
            var menu = new ContextMenu();

            var itemVer = new MenuItem { Header = "Ver detalhes" };
            itemVer.Click += (_, _) => { _dragging = false; Visualizar?.Invoke(this, EventArgs.Empty); };
            menu.Items.Add(itemVer);
            menu.Items.Add(new Separator());

            var itemEditar = new MenuItem { Header = "Editar máquina" };
            itemEditar.Click += (_, _) => { _dragging = false; Editar?.Invoke(this, EventArgs.Empty); };
            menu.Items.Add(itemEditar);

            var itemRemover = new MenuItem { Header = "Remover máquina" };
            itemRemover.Click += (_, _) => { _dragging = false; Remover?.Invoke(this, EventArgs.Empty); };
            menu.Items.Add(itemRemover);

            menu.Items.Add(new Separator());

            var itemPing = new MenuItem { Header = "Pingar agora" };
            itemPing.Click += (_, _) => ReiniciarPing();
            menu.Items.Add(itemPing);

            ContextMenu = menu;
        }

        // ── Visual ────────────────────────────────────────────────────────────
        private void AtualizarVisual()
        {
            if (_maquina == null) return;

            Width  = CardWidth;
            Height = !string.IsNullOrEmpty(_maquina.Ramal) ? AlturaBase + AlturaRamal : AlturaBase;

            AtualizarTooltip();
            InvalidateVisual();
        }

        private void AtualizarTooltip()
        {
            if (_maquina == null) return;

            var pingInfo = _pingResult.Status switch
            {
                StatusPing.Online     => $"🟢 Online ({_pingResult.Latencia} ms)",
                StatusPing.Offline    => "🔴 Sem resposta",
                StatusPing.Aguardando => "🟡 Verificando...",
                _                     => "⚫ Sem IP"
            };

            ToolTip = new ToolTip
            {
                Content = $"{_maquina.Hostname}\n" +
                          $"IP: {_maquina.Ip}  |  Porta: {_maquina.PortaSwitch}\n" +
                          $"-----------------\n" +
                          $"Tipo: {_maquina.Tipo}\n" +
                          $"CPU: {_maquina.Processador}\n" +
                          $"RAM: {_maquina.Ram}  |  HD: {_maquina.Storage}" +
                          (_maquina.Ramal != "" ? $"\nRamal: {_maquina.Ramal}" : "") +
                          $"\n-----------------\n{pingInfo}"
            };
            ToolTipService.SetShowDuration(this, 60000);
            ToolTipService.SetInitialShowDelay(this, 0);
        }

        public void AtualizarSetor(Setor setor)
        {
            _setor = setor;
            AtualizarVisual();
        }

        public void SetHighlight(bool value)
        {
            if (_highlight == value) return;
            _highlight  = value;
            _blinkState = value;
            if (value) _blinkTimer!.Start();
            else       _blinkTimer!.Stop();
            InvalidateVisual();
        }

        // ── Ping ──────────────────────────────────────────────────────────────
        public void IniciarPing()
        {
            _pingService?.Parar();
            _pingResult = new ResultadoPing { Status = StatusPing.Aguardando };

            _pingService = new PingService(
                _maquina?.Ip ?? "",
                Dispatcher,
                resultado =>
                {
                    _pingResult = resultado;
                    AtualizarTooltip();
                    InvalidateVisual();
                });

            _pingService.Iniciar();
        }

        public void ReiniciarPing()
        {
            _pingResult = new ResultadoPing { Status = StatusPing.Aguardando };
            InvalidateVisual();
            IniciarPing();
        }

        public void PararPing() => _pingService?.Parar();

        // ── Renderização ──────────────────────────────────────────────────────
        protected override void OnRender(DrawingContext dc)
        {
            if (_maquina == null) return;

            var corFundo = _setor?.CorAsColor() ?? Color.FromRgb(136, 136, 136);
            var rect     = new Rect(0, 0, Width, Height);

            // Fundo
            dc.DrawRectangle(new SolidColorBrush(corFundo), null, rect);

            // Bordas / highlight
            if (_highlight && _blinkState)
            {
                dc.DrawRectangle(null, new Pen(new SolidColorBrush(HighlightHalo), 1), Inflated(rect, 4));
                dc.DrawRectangle(null, new Pen(new SolidColorBrush(HighlightMeio), 1), Inflated(rect, 2));
                dc.DrawRectangle(null, new Pen(new SolidColorBrush(HighlightVivo), 4), Inflated(rect, 1));
            }
            else if (_highlight)
            {
                dc.DrawRectangle(null, new Pen(new SolidColorBrush(HighlightFraco), 1), Inflated(rect, 1));
            }
            else
            {
                dc.DrawRectangle(null, new Pen(new SolidColorBrush(DarkenColor(corFundo, 40)), 1), rect);
            }

            // ── Texto ─────────────────────────────────────────────────────────
            double y  = 3;
            double cx = Width / 2;

            var ft = MakeText(_maquina.Hostname, 6, bold: true);
            dc.DrawText(ft, new Point(cx - ft.Width / 2, y));
            y += 9;

            var ipTxt = UltimoOcteto(_maquina.Ip);
            if (!string.IsNullOrEmpty(_maquina.PortaSwitch)) ipTxt += "-" + _maquina.PortaSwitch;
            var ft2 = MakeText(ipTxt, 6, bold: false);
            dc.DrawText(ft2, new Point(cx - ft2.Width / 2, y));
            y += 9;

            if (!string.IsNullOrEmpty(_maquina.Ramal))
            {
                var ft3 = MakeText(_maquina.Ramal, 6, bold: true);
                dc.DrawText(ft3, new Point(cx - ft3.Width / 2, y));
            }

            // ── Bolinha de ping ───────────────────────────────────────────────
            DesenharIndicadorPing(dc);
        }

        private void DesenharIndicadorPing(DrawingContext dc)
        {
            var cor = _pingResult.Status switch
            {
                StatusPing.Online     => PingOnline,
                StatusPing.Offline    => PingOffline,
                StatusPing.Aguardando => PingAguardando,
                _                     => PingSemIp
            };

            // Posição: canto superior direito, com 2px de margem
            double cx = Width  - PingDot / 2 - 2;
            double cy = PingDot / 2 + 2;

            // Halo escuro para contraste com qualquer cor de fundo
            dc.DrawEllipse(
                new SolidColorBrush(Color.FromArgb(120, 0, 0, 0)),
                null,
                new Point(cx, cy),
                PingDot / 2 + 1, PingDot / 2 + 1);

            // Bolinha colorida
            dc.DrawEllipse(
                new SolidColorBrush(cor),
                null,
                new Point(cx, cy),
                PingDot / 2, PingDot / 2);

            // Brilho interno (ponto branco pequeno) — só quando online
            if (_pingResult.Status == StatusPing.Online)
            {
                dc.DrawEllipse(
                    new SolidColorBrush(Color.FromArgb(180, 255, 255, 255)),
                    null,
                    new Point(cx - 1, cy - 1),
                    1.2, 1.2);
            }
        }

        // ── Helpers ───────────────────────────────────────────────────────────
        private static FormattedText MakeText(string text, double size, bool bold) =>
            new(text,
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface(new FontFamily("Segoe UI"),
                    FontStyles.Normal,
                    bold ? FontWeights.Bold : FontWeights.Normal,
                    FontStretches.Normal),
                size, Brushes.White,
                VisualTreeHelper.GetDpi(Application.Current.MainWindow).PixelsPerDip);

        private static Rect Inflated(Rect r, double d) =>
            new(r.X - d, r.Y - d, r.Width + 2 * d, r.Height + 2 * d);

        private static Color DarkenColor(Color c, byte amount) =>
            Color.FromRgb(
                (byte)Math.Max(0, c.R - amount),
                (byte)Math.Max(0, c.G - amount),
                (byte)Math.Max(0, c.B - amount));

        private static string UltimoOcteto(string ip)
        {
            if (string.IsNullOrEmpty(ip)) return "";
            var idx = ip.LastIndexOf('.');
            return idx >= 0 ? ip[(idx + 1)..] : ip;
        }

        // ── Drag and drop ─────────────────────────────────────────────────────
        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            if (e.ClickCount == 2) { Visualizar?.Invoke(this, EventArgs.Empty); return; }
            _dragging   = true;
            _dragOrigin = e.GetPosition(this);
            CaptureMouse();
            Panel.SetZIndex(this, 1000);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (!_dragging) return;

            var pos  = e.GetPosition(Parent as IInputElement);
            var newX = pos.X - _dragOrigin.X;
            var newY = pos.Y - _dragOrigin.Y;

            if (Parent is FrameworkElement parent)
            {
                newX = Math.Max(0, Math.Min(newX, parent.ActualWidth  - Width));
                newY = Math.Max(0, Math.Min(newY, parent.ActualHeight - Height));
            }

            Canvas.SetLeft(this, newX);
            Canvas.SetTop(this, newY);
        }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonUp(e);
            if (!_dragging) return;
            _dragging = false;
            ReleaseMouseCapture();
            Panel.SetZIndex(this, 1);
            SalvarPosicao();
        }

        public void SalvarPosicao()
        {
            if (_maquina == null) return;
            _maquina.PosX = (int)Canvas.GetLeft(this);
            _maquina.PosY = (int)Canvas.GetTop(this);
        }
    }
}
