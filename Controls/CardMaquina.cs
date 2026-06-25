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
    ///
    /// Layout:
    ///   ┌─┬──────────────────────┐
    ///   │█│ PC-NOME              │   ← hostname
    ///   │█│ 192.168.0.10  P.5   │   ← IP do JSON + porta switch
    ///   │█│ 201                  │   ← ramal (opcional)
    ///   └─┴──────────────────────┘
    ///    ↑ barra lateral colorida (verde/vermelho/amarelo = status geral do ping)
    ///
    /// O IP nunca vem do JSON — sempre resolvido em tempo real via DNS.
    /// </summary>
    public class CardMaquina : Canvas
    {
        // ── Dimensões ─────────────────────────────────────────────────────────
        private const double BarraW      = 4;    // largura da barra lateral
        private const double CardWidth   = 74;
        private const double LinhaH      = 11;
        private const double PadTop      = 4;
        private const double PadBase     = 3;
        private const double PadLeft     = BarraW + 4;  // texto começa após a barra

        // ── Cores de status ───────────────────────────────────────────────────
        private static readonly Color CorOnline   = Color.FromRgb(50,  205, 50);
        private static readonly Color CorOffline  = Color.FromRgb(210, 50,  50);
        private static readonly Color CorAguard   = Color.FromRgb(255, 190, 0);
        private static readonly Color CorSemAlvo  = Color.FromRgb(110, 110, 110);
        private static readonly Color CorDiverg   = Color.FromRgb(255, 140, 0);  // laranja

        // ── Highlight de busca ────────────────────────────────────────────────
        private static readonly Color HlVivo  = Color.FromRgb(255, 0,   0);
        private static readonly Color HlMeio  = Color.FromRgb(255, 96,  0);
        private static readonly Color HlHalo  = Color.FromRgb(255, 192, 0);
        private static readonly Color HlFraco = Color.FromRgb(192, 128, 0);
        private const int HlIntervalo = 300;

        // ── Estado ────────────────────────────────────────────────────────────
        private Maquina? _maquina;
        private Setor?   _setor;
        private bool     _dragging;
        private Point    _dragOrigin;
        private bool     _highlight;
        private bool     _blinkState;
        private DispatcherTimer? _blinkTimer;

        // Resultado do ping — atualizado pelo PingQueue via AtualizarResultadoPing()
        private ResultadoPing _ping = new();

        // Callback para "Verificar agora" — wired pelo MainWindow
        public Action? OnPingarAgora { get; set; }

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

            _blinkTimer = new DispatcherTimer
                { Interval = TimeSpan.FromMilliseconds(HlIntervalo) };
            _blinkTimer.Tick += (_, _) => { _blinkState = !_blinkState; InvalidateVisual(); };

            CriarMenuContexto();
            AtualizarVisual();
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

            var itemPing = new MenuItem { Header = "Verificar agora" };
            itemPing.Click += (_, _) => OnPingarAgora?.Invoke();
            menu.Items.Add(itemPing);

            ContextMenu = menu;
        }

        // ── API de ping ───────────────────────────────────────────────────────

        public void AtualizarResultadoPing(ResultadoPing resultado)
        {
            _ping = resultado;
            AtualizarTooltip();
            InvalidateVisual();
        }

        public void ResetarPing()
        {
            _ping = new ResultadoPing();
            AtualizarTooltip();
            InvalidateVisual();
        }

        // ── Visual ────────────────────────────────────────────────────────────
        private void AtualizarVisual()
        {
            if (_maquina == null) return;
            Width  = CardWidth;
            int linhas = string.IsNullOrEmpty(_maquina.Ramal) ? 2 : 3;
            Height = PadTop + linhas * LinhaH + PadBase;
            AtualizarTooltip();
            InvalidateVisual();
        }

        private void AtualizarTooltip()
        {
            if (_maquina == null) return;

            string StrStatus(StatusPing s, long lat) => s switch
            {
                StatusPing.Online     => $"OK ({lat} ms)",
                StatusPing.Offline    => "Sem resposta",
                StatusPing.Aguardando => "Aguardando...",
                _                     => "—"
            };

            ToolTip = new ToolTip
            {
                Content =
                    $"{_maquina.Hostname}\n" +
                    $"──────────────────────────\n" +
                    $"Hostname : {StrStatus(_ping.StatusHostname, _ping.LatenciaHostname)}\n" +
                    $"IP       : {StrStatus(_ping.StatusIp,       _ping.LatenciaIp)}\n" +
                    $"──────────────────────────\n" +
                    $"IP cadastrado : {_maquina.Ip}\n" +
                    $"Tipo: {_maquina.Tipo}   Porta SW: {_maquina.PortaSwitch}\n" +
                    $"CPU: {_maquina.Processador}\n" +
                    $"RAM: {_maquina.Ram}   HD: {_maquina.Storage}" +
                    (string.IsNullOrEmpty(_maquina.Ramal) ? "" : $"\nRamal: {_maquina.Ramal}")
            };
            ToolTipService.SetShowDuration(this, 60000);
            ToolTipService.SetInitialShowDelay(this, 0);
        }

        public void AtualizarSetor(Setor setor) { _setor = setor; AtualizarVisual(); }

        public void SetHighlight(bool value)
        {
            if (_highlight == value) return;
            _highlight  = value;
            _blinkState = value;
            if (value) _blinkTimer!.Start();
            else       _blinkTimer!.Stop();
            InvalidateVisual();
        }

        // ── Renderização ──────────────────────────────────────────────────────
        protected override void OnRender(DrawingContext dc)
        {
            if (_maquina == null) return;

            var corFundo = _setor?.CorAsColor() ?? Color.FromRgb(136, 136, 136);
            var rect     = new Rect(0, 0, Width, Height);

            // ── Fundo ─────────────────────────────────────────────────────────
            dc.DrawRectangle(new SolidColorBrush(corFundo), null, rect);

            // ── Barra lateral colorida (status geral do ping) ─────────────────
            // Barra dividida: metade de cima = hostname, metade de baixo = IP
            double meioY = Height / 2;

            var corHostname = _ping.StatusHostname switch
            {
                StatusPing.Online     => CorOnline,
                StatusPing.Offline    => CorOffline,
                StatusPing.SemAlvo    => CorSemAlvo,
                _                     => CorAguard
            };
            var corIp = _ping.StatusIp switch
            {
                StatusPing.Online     => CorOnline,
                StatusPing.Offline    => CorOffline,
                StatusPing.SemAlvo    => CorSemAlvo,
                _                     => CorAguard
            };

            // Metade superior — hostname
            dc.DrawRectangle(new SolidColorBrush(corHostname), null,
                new Rect(0, 0, BarraW, meioY));

            // Metade inferior — IP
            dc.DrawRectangle(new SolidColorBrush(corIp), null,
                new Rect(0, meioY, BarraW, meioY));

            // Linha divisória sutil entre as duas metades
            dc.DrawLine(new Pen(new SolidColorBrush(Color.FromArgb(80, 0, 0, 0)), 0.5),
                new Point(0, meioY), new Point(BarraW, meioY));

            // ── Borda externa / highlight ─────────────────────────────────────
            if (_highlight && _blinkState)
            {
                dc.DrawRectangle(null, new Pen(new SolidColorBrush(HlHalo),  1), Inflated(rect, 4));
                dc.DrawRectangle(null, new Pen(new SolidColorBrush(HlMeio),  1), Inflated(rect, 2));
                dc.DrawRectangle(null, new Pen(new SolidColorBrush(HlVivo),  4), Inflated(rect, 1));
            }
            else if (_highlight)
            {
                dc.DrawRectangle(null, new Pen(new SolidColorBrush(HlFraco), 1), Inflated(rect, 1));
            }
            else
            {
                dc.DrawRectangle(null,
                    new Pen(new SolidColorBrush(DarkenColor(corFundo, 40)), 1), rect);
            }

            // ── Textos ────────────────────────────────────────────────────────
            double y = PadTop;

            // Linha 1 — Hostname (bold)
            DesenharTexto(dc, _maquina.Hostname, bold: true, y: y);
            y += LinhaH;

            // Linha 2 — IP resolvido via DNS + porta switch
            var ipTxt = string.IsNullOrEmpty(_maquina.Ip) ? "—" : _maquina.Ip;
            if (!string.IsNullOrEmpty(_maquina.PortaSwitch)) ipTxt += "  P." + _maquina.PortaSwitch;
            DesenharTexto(dc, ipTxt, bold: false, y: y);
            y += LinhaH;

            // Linha 3 — Ramal (opcional)
            if (!string.IsNullOrEmpty(_maquina.Ramal))
                DesenharTexto(dc, _maquina.Ramal, bold: true, y: y);
        }

        private void DesenharTexto(DrawingContext dc, string texto, bool bold, double y)
        {
            double areaW = CardWidth - PadLeft - 3;
            var ft = MakeText(texto, 6.5, bold);
            double tx = PadLeft + Math.Max(0, areaW / 2 - ft.Width / 2);
            dc.DrawText(ft, new Point(tx, y + (LinhaH - ft.Height) / 2));
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
