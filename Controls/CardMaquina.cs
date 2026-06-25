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
    /// O ping é gerenciado externamente pelo PingQueue — o card apenas exibe o resultado.
    /// </summary>
    public class CardMaquina : Canvas
    {
        // ── Dimensões ─────────────────────────────────────────────────────────
        private const double CardWidth   = 74;
        private const double LinhaAltura = 11;
        private const double PaddingTopo = 4;
        private const double PaddingBase = 3;
        private const double DotR        = 3.5;
        private const double DotX        = CardWidth - DotR - 3;

        // ── Highlight ─────────────────────────────────────────────────────────
        private static readonly Color HighlightVivo  = Color.FromRgb(255, 0,   0);
        private static readonly Color HighlightMeio  = Color.FromRgb(255, 96,  0);
        private static readonly Color HighlightHalo  = Color.FromRgb(255, 192, 0);
        private static readonly Color HighlightFraco = Color.FromRgb(192, 128, 0);
        private const int HighlightIntervalo = 300;

        // ── Cores de ping ─────────────────────────────────────────────────────
        private static readonly Color CorOk      = Color.FromRgb(50,  205, 50);
        private static readonly Color CorErro    = Color.FromRgb(220, 50,  50);
        private static readonly Color CorAguard  = Color.FromRgb(255, 200, 0);
        private static readonly Color CorSemAlvo = Color.FromRgb(110, 110, 110);

        // ── Estado ────────────────────────────────────────────────────────────
        private Maquina? _maquina;
        private Setor?   _setor;
        private bool     _dragging;
        private Point    _dragOrigin;
        private bool     _highlight;
        private bool     _blinkState;
        private DispatcherTimer? _blinkTimer;

        // ── Resultado do ping (atualizado externamente pelo PingQueue) ─────────
        private ResultadoDualPing _ping = new();

        // Callback para "verificar agora" — preenchido pelo MainWindow
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
                { Interval = TimeSpan.FromMilliseconds(HighlightIntervalo) };
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

        // ── API de ping (chamada pelo PingQueue) ──────────────────────────────

        /// <summary>Recebe o resultado do PingQueue e redesenha o card.</summary>
        public void AtualizarResultadoPing(ResultadoDualPing resultado)
        {
            _ping = resultado;
            AtualizarTooltip();
            InvalidateVisual();
        }

        /// <summary>Reseta o card para estado "Aguardando" (ex: ao trocar de empresa).</summary>
        public void ResetarPing()
        {
            _ping = new ResultadoDualPing();
            AtualizarTooltip();
            InvalidateVisual();
        }

        // ── Visual ────────────────────────────────────────────────────────────
        private void AtualizarVisual()
        {
            if (_maquina == null) return;
            Width  = CardWidth;
            int linhas = string.IsNullOrEmpty(_maquina.Ramal) ? 2 : 3;
            Height = PaddingTopo + linhas * LinhaAltura + PaddingBase;
            AtualizarTooltip();
            InvalidateVisual();
        }

        private void AtualizarTooltip()
        {
            if (_maquina == null) return;

            string Str(StatusPing s, long lat) => s switch
            {
                StatusPing.Online     => $"OK ({lat} ms)",
                StatusPing.Offline    => "Sem resposta",
                StatusPing.Aguardando => "Aguardando...",
                _                     => "—"
            };

            var dnsInfo = string.IsNullOrEmpty(_ping.IpResolvido) ? "" :
                $"\n  DNS: {_ping.IpResolvido}" +
                (_ping.IpBatem ? " ✔ bate com o cadastro" : " ✘ diverge do cadastro");

            ToolTip = new ToolTip
            {
                Content =
                    $"{_maquina.Hostname}\n" +
                    $"──────────────────────\n" +
                    $"● Nome : {Str(_ping.StatusNome, _ping.LatenciaNome)}{dnsInfo}\n" +
                    $"● IP   : {_maquina.Ip}  {Str(_ping.StatusIp, _ping.LatenciaIp)}\n" +
                    $"──────────────────────\n" +
                    $"Tipo: {_maquina.Tipo}   Porta SW: {_maquina.PortaSwitch}\n" +
                    $"CPU: {_maquina.Processador}\n" +
                    $"RAM: {_maquina.Ram}  HD: {_maquina.Storage}" +
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

            dc.DrawRectangle(new SolidColorBrush(corFundo), null, rect);

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

            // Linha 1 — Hostname + status DNS
            DesenharLinha(dc, _maquina.Hostname, bold: true, y: PaddingTopo, _ping.StatusNome);

            // Linha 2 — IP/Porta + status ICMP
            double y2 = PaddingTopo + LinhaAltura;
            var ipTxt = UltimoOcteto(_maquina.Ip);
            if (!string.IsNullOrEmpty(_maquina.PortaSwitch)) ipTxt += "-" + _maquina.PortaSwitch;
            DesenharLinha(dc, ipTxt, bold: false, y: y2, _ping.StatusIp);

            // Linha 3 — Ramal (sem bolinha)
            if (!string.IsNullOrEmpty(_maquina.Ramal))
            {
                double y3 = y2 + LinhaAltura;
                var ft = MakeText(_maquina.Ramal, 6.5, bold: true);
                double cx = (CardWidth - DotR * 2 - 4) / 2;
                dc.DrawText(ft, new Point(cx - ft.Width / 2, y3 + (LinhaAltura - ft.Height) / 2));
            }

            // Borda laranja: ambos online mas IPs divergem
            if (_ping.StatusNome == StatusPing.Online &&
                _ping.StatusIp   == StatusPing.Online &&
                !_ping.IpBatem   &&
                !string.IsNullOrEmpty(_ping.IpResolvido))
            {
                dc.DrawRectangle(null,
                    new Pen(new SolidColorBrush(Color.FromRgb(255, 160, 0)), 1.5),
                    Inflated(rect, 1));
            }
        }

        private void DesenharLinha(DrawingContext dc, string texto, bool bold,
                                   double y, StatusPing status)
        {
            double areaTexto = CardWidth - DotR * 2 - 8;
            var ft = MakeText(texto, 6.5, bold);
            double tx = Math.Max(2, areaTexto / 2 - ft.Width / 2);
            dc.DrawText(ft, new Point(tx, y + (LinhaAltura - ft.Height) / 2));
            DesenharDot(dc, status, DotX, y + LinhaAltura / 2);
        }

        private static void DesenharDot(DrawingContext dc, StatusPing status, double cx, double cy)
        {
            var cor = status switch
            {
                StatusPing.Online     => CorOk,
                StatusPing.Offline    => CorErro,
                StatusPing.Aguardando => CorAguard,
                _                     => CorSemAlvo
            };
            dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(100, 0, 0, 0)),
                null, new Point(cx, cy), DotR + 1, DotR + 1);
            dc.DrawEllipse(new SolidColorBrush(cor),
                null, new Point(cx, cy), DotR, DotR);
            if (status == StatusPing.Online)
                dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(170, 255, 255, 255)),
                    null, new Point(cx - 1.2, cy - 1.2), 1.5, 1.5);
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
