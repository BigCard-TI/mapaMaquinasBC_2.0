using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using MapaMaquinas.Controls;

namespace MapaMaquinas.Services
{
    /// <summary>
    /// Fila de ping com concorrência controlada.
    ///
    /// Fluxo por ciclo:
    ///   - Todas as máquinas são pingadas em paralelo, limitadas a
    ///     <see cref="MaxConcorrencia"/> simultâneas (padrão: 5).
    ///   - Ao terminar o ciclo completo, aguarda <see cref="PausaEntreCiclos"/> (2 min).
    ///   - Recomeça.
    ///
    /// "Verificar agora" pinga um card fora de ciclo, sem interferir na fila.
    /// </summary>
    public class PingQueue : IDisposable
    {
        // ── Configuração ──────────────────────────────────────────────────────
        /// <summary>Máximo de máquinas pingadas ao mesmo tempo.</summary>
        public static int MaxConcorrencia { get; set; } = 5;

        /// <summary>Pausa após o ciclo completo antes de reiniciar.</summary>
        public static TimeSpan PausaEntreCiclos { get; set; } = TimeSpan.FromMinutes(2);

        // ── Estado ────────────────────────────────────────────────────────────
        private readonly Dispatcher _dispatcher;
        private List<CardMaquina>   _cards = new();
        private CancellationTokenSource _cts = new();

        public event Action<int, int>? ProgressoAtualizado;   // (concluídos, total)
        public event Action?           CicloCompleto;

        public PingQueue(Dispatcher dispatcher) => _dispatcher = dispatcher;

        // ── API pública ───────────────────────────────────────────────────────

        public void Iniciar(List<CardMaquina> cards)
        {
            Parar();
            _cards = new List<CardMaquina>(cards);
            _cts   = new CancellationTokenSource();
            Task.Run(() => Loop(_cts.Token));
        }

        public void Parar() => _cts.Cancel();

        public void PingarAgora(CardMaquina card)
        {
            Task.Run(async () => await PingarCard(card, CancellationToken.None));
        }

        public void AdicionarCard(CardMaquina card)
        {
            lock (_cards) { if (!_cards.Contains(card)) _cards.Add(card); }
        }

        public void RemoverCard(CardMaquina card)
        {
            lock (_cards) { _cards.Remove(card); }
        }

        // ── Loop principal ────────────────────────────────────────────────────
        private async Task Loop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                List<CardMaquina> snapshot;
                lock (_cards) { snapshot = new List<CardMaquina>(_cards); }

                int total     = snapshot.Count;
                int concluidos = 0;

                // Semáforo para limitar concorrência
                var sem = new SemaphoreSlim(MaxConcorrencia, MaxConcorrencia);

                var tasks = snapshot.Select(async card =>
                {
                    await sem.WaitAsync(token);
                    try
                    {
                        if (token.IsCancellationRequested) return;
                        await PingarCard(card, token);
                        var atual = Interlocked.Increment(ref concluidos);
                        _dispatcher.Invoke(() => ProgressoAtualizado?.Invoke(atual, total));
                    }
                    finally { sem.Release(); }
                }).ToList();

                try { await Task.WhenAll(tasks); }
                catch (TaskCanceledException) { return; }

                if (token.IsCancellationRequested) return;

                _dispatcher.Invoke(() =>
                {
                    ProgressoAtualizado?.Invoke(0, total);
                    CicloCompleto?.Invoke();
                });

                // Pausa de 2 min antes do próximo ciclo
                try { await Task.Delay(PausaEntreCiclos, token); }
                catch (TaskCanceledException) { return; }
            }
        }

        private async Task PingarCard(CardMaquina card, CancellationToken token)
        {
            var hostname = card.Maquina?.Hostname ?? "";
            var ip       = card.Maquina?.Ip       ?? "";
            var resultado = await PingWorker.Executar(hostname, ip, token);

            if (!token.IsCancellationRequested)
                _dispatcher.Invoke(() => card.AtualizarResultadoPing(resultado));
        }

        public void Dispose() => Parar();
    }
}
