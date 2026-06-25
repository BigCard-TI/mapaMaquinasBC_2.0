using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using MapaMaquinas.Models;

namespace MapaMaquinas.Controls
{
    public class CardPorta : Canvas
    {
        private const double CardW = 58;
        private const double CardH = 32;

        private static readonly Color CorFundo = Color.FromRgb(80, 80, 80);
        private static readonly Color CorBorda = Color.FromRgb(48, 48, 48);

        private PortaSwitch? _porta;
        private bool  _dragging;
        private Point _dragOrigin;

        public event EventHandler? Editar;
        public event EventHandler? Remover;

        public PortaSwitch? Porta
        {
            get => _porta;
            set { _porta = value; AtualizarVisual(); }
        }

        public CardPorta(PortaSwitch porta)
        {
            _porta = porta;
            Width  = CardW;
            Height = CardH;
            Cursor = Cursors.SizeAll;

            CriarMenuContexto();
            AtualizarVisual();
        }

        private void CriarMenuContexto()
        {
            var menu = new ContextMenu();
            var itemEditar = new MenuItem { Header = "Editar porta" };
            itemEditar.Click += (_, _) => { _dragging = false; Editar?.Invoke(this, EventArgs.Empty); };
            menu.Items.Add(itemEditar);

            var itemRemover = new MenuItem { Header = "Remover porta" };
            itemRemover.Click += (_, _) => { _dragging = false; Remover?.Invoke(this, EventArgs.Empty); };
            menu.Items.Add(itemRemover);

            ContextMenu = menu;
        }

        private void AtualizarVisual()
        {
            if (_porta == null) return;
            var extra = "";
            if (!string.IsNullOrEmpty(_porta.Localizacao)) extra += $"\nLocal: {_porta.Localizacao}";
            if (!string.IsNullOrEmpty(_porta.Descricao))   extra += $"\n{_porta.Descricao}";
            if (!string.IsNullOrEmpty(_porta.Observacoes)) extra += $"\n{_porta.Observacoes}";

            ToolTip = new ToolTip { Content = $"Porta {_porta.Numero}{extra}" };
            ToolTipService.SetShowDuration(this, 60000);
            ToolTipService.SetInitialShowDelay(this, 0);

            InvalidateVisual();
        }

        protected override void OnRender(DrawingContext dc)
        {
            if (_porta == null) return;

            var rect = new Rect(0, 0, Width, Height);

            dc.DrawRectangle(new SolidColorBrush(CorFundo), new Pen(new SolidColorBrush(CorBorda), 1), rect);

            // Ícone de switch (pequeno retângulo com linhas)
            var iconeRect = new Rect(3, 4, 8, Height - 8);
            dc.DrawRectangle(null, new Pen(new SolidColorBrush(Color.FromRgb(170, 170, 170)), 1), iconeRect);
            dc.DrawLine(new Pen(Brushes.DarkGray, 1), new Point(5, 7),  new Point(5, 10));
            dc.DrawLine(new Pen(Brushes.DarkGray, 1), new Point(8, 7),  new Point(8, 10));

            double cx = Width / 2;

            // Linha 1 — Número
            var t1 = MakeText("P." + _porta.Numero, 7, bold: true);
            dc.DrawText(t1, new Point(cx - t1.Width / 2 + 4, 5));

            // Linha 2 — Localização ou descrição
            var linha2 = !string.IsNullOrEmpty(_porta.Localizacao) ? _porta.Localizacao : _porta.Descricao;
            if (!string.IsNullOrEmpty(linha2))
            {
                var t2 = MakeText(linha2, 6, bold: false);
                dc.DrawText(t2, new Point(cx - t2.Width / 2 + 4, 16));
            }
        }

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

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
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
            if (_porta == null) return;
            _porta.PosX = (int)Canvas.GetLeft(this);
            _porta.PosY = (int)Canvas.GetTop(this);
        }
    }
}
