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
    /// Fila de ping com concorrência controlada e ciclo GLOBAL.
    ///
    /// Fluxo por ciclo:
    ///   - Todas as máquinas de TODAS as empresas são pingadas em paralelo,
    ///     limitadas a <see cref="MaxConcorrencia"/> simultâneas (padrão: 5).
    ///   - Ao terminar o ciclo completo, aguarda <see cref="PausaEntreCiclos"/> (2 min).
    ///   - Recomeça — sem reiniciar o timer ao trocar de aba.
    ///
    /// "Verificar agora" pinga um card fora de ciclo, sem interferir na fila.
    ///
    /// IMPORTANTE: a lista global é mantida aqui. Ao trocar de empresa, o
    /// MainWindow chama AdicionarCard/RemoverCard — nunca Iniciar() novamente.
    /// Iniciar() é chamado UMA única vez após o carregamento inicial.
    /// </summary>
    public class PingQueue : IDisposable
    {
        // ── Configuração ──────────────────────────────────────────────────────
        public static int      MaxConcorrencia  { get; set; } = 5;
        public static TimeSpan PausaEntreCiclos { get; set; } = TimeSpan.FromMinutes(2);

        // ── Estado ────────────────────────────────────────────────────────────
        private readonly Dispatcher         _dispatcher;
        private readonly List<CardMaquina>  _cards = new();
        private CancellationTokenSource     _cts   = new();
        private bool                        _rodando = false;

        public event Action<int, int>? ProgressoAtualizado;
        public event Action?           CicloCompleto;

        public PingQueue(Dispatcher dispatcher) => _dispatcher = dispatcher;

        // ── API pública ───────────────────────────────────────────────────────

        /// <summary>
        /// Inicia o loop de ping com os cards fornecidos.
        /// Deve ser chamado UMA única vez — ao carregar o arquivo JSON.
        /// Para adicionar cards de outras empresas use AdicionarCard().
        /// </summary>
        public void Iniciar(IEnumerable<CardMaquina> cardsIniciais)
        {
            lock (_cards)
            {
                _cards.Clear();
                _cards.AddRange(cardsIniciais);
            }

            if (!_rodando)
            {
                _cts     = new CancellationTokenSource();
                _rodando = true;
                Task.Run(() => Loop(_cts.Token));
            }
        }

        /// <summary>
        /// Adiciona cards de uma nova empresa ao ciclo em andamento.
        /// NÃO reinicia o timer — o ciclo continua de onde estava.
        /// </summary>
        public void AdicionarCards(IEnumerable<CardMaquina> novosCards)
        {
            lock (_cards)
            {
                foreach (var c in novosCards)
                    if (!_cards.Contains(c))
                        _cards.Add(c);
            }
        }

        public void Parar()
        {
            _cts.Cancel();
            _rodando = false;
        }

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

        /// <summary>Retorna snapshot thread-safe de todos os cards registrados.</summary>
        public List<CardMaquina> TodosCards
        {
            get { lock (_cards) { return new List<CardMaquina>(_cards); } }
        }

        /// <summary>Remove todos os cards de uma empresa específica do ciclo.</summary>
        public void RemoverCards(IEnumerable<CardMaquina> cardsRemover)
        {
            lock (_cards)
            {
                foreach (var c in cardsRemover)
                    _cards.Remove(c);
            }
        }

        // ── Loop principal ────────────────────────────────────────────────────
        private async Task Loop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                // Snapshot thread-safe de todos os cards (todas as empresas)
                List<CardMaquina> snapshot;
                lock (_cards) { snapshot = new List<CardMaquina>(_cards); }

                int total      = snapshot.Count;
                int concluidos = 0;

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

                try   { await Task.WhenAll(tasks); }
                catch (TaskCanceledException) { return; }

                if (token.IsCancellationRequested) return;

                _dispatcher.Invoke(() =>
                {
                    ProgressoAtualizado?.Invoke(0, total);
                    CicloCompleto?.Invoke();
                });

                // ── Aguarda 2 minutos antes do próximo ciclo ──────────────────
                // Este delay NUNCA é interrompido pela troca de aba.
                // Só é cancelado quando Parar() é chamado (fechar o app).
                try   { await Task.Delay(PausaEntreCiclos, token); }
                catch (TaskCanceledException) { return; }
            }
        }

        private async Task PingarCard(CardMaquina card, CancellationToken token)
        {
            var hostname  = card.Maquina?.Hostname ?? "";
            var ip        = card.Maquina?.Ip       ?? "";
            var resultado = await PingWorker.Executar(hostname, ip, token);

            if (!token.IsCancellationRequested)
                _dispatcher.Invoke(() => card.AtualizarResultadoPing(resultado));
        }

        public void Dispose() => Parar();
    }
}
