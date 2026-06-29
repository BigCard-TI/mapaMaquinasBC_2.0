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
    /// Fila de ping com ciclo GLOBAL — completamente independente do canvas.
    ///
    /// Regras:
    ///   - IniciarGlobal() é chamado UMA vez ao carregar o JSON, com os cards
    ///     de TODAS as empresas já criados. Nunca é chamado novamente.
    ///   - Trocar de aba não afeta esta classe em nada.
    ///   - O timer de 2 minutos só é cancelado por Parar() (fechar o app).
    ///   - AdicionarCard / RemoverCard servem para nova máquina criada/excluída em runtime.
    /// </summary>
    public class PingQueue : IDisposable
    {
        public static int      MaxConcorrencia  { get; set; } = 5;
        public static TimeSpan PausaEntreCiclos { get; set; } = TimeSpan.FromMinutes(2);

        private readonly Dispatcher        _dispatcher;
        private readonly List<CardMaquina> _cards   = new();
        private CancellationTokenSource    _cts     = new();
        private bool                       _rodando = false;

        public event Action<int, int>? ProgressoAtualizado;
        public event Action?           CicloCompleto;

        public PingQueue(Dispatcher dispatcher) => _dispatcher = dispatcher;

        /// <summary>
        /// Inicia o ciclo global. Deve ser chamado UMA única vez por carregamento de JSON.
        /// cards = todos os CardMaquina de todas as empresas, já criados.
        /// </summary>
        public void IniciarGlobal(List<CardMaquina> cards)
        {
            Parar();   // cancela ciclo anterior se houver (novo JSON aberto)

            lock (_cards)
            {
                _cards.Clear();
                _cards.AddRange(cards);
            }

            _cts     = new CancellationTokenSource();
            _rodando = true;
            Task.Run(() => Loop(_cts.Token));
        }

        public void Parar()
        {
            _cts.Cancel();
            _rodando = false;
        }

        /// <summary>Pinga um card imediatamente fora do ciclo ("Verificar agora").</summary>
        public void PingarAgora(CardMaquina card)
        {
            Task.Run(async () => await PingarCard(card, CancellationToken.None));
        }

        /// <summary>Adiciona card ao ciclo em runtime (nova máquina criada).</summary>
        public void AdicionarCard(CardMaquina card)
        {
            lock (_cards) { if (!_cards.Contains(card)) _cards.Add(card); }
        }

        /// <summary>Remove card do ciclo em runtime (máquina excluída).</summary>
        public void RemoverCard(CardMaquina card)
        {
            lock (_cards) { _cards.Remove(card); }
        }

        /// <summary>Snapshot thread-safe de todos os cards do ciclo.</summary>
        public List<CardMaquina> TodosCards
        {
            get { lock (_cards) { return new List<CardMaquina>(_cards); } }
        }

        private async Task Loop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                // Snapshot — inclui cards de TODAS as empresas
                List<CardMaquina> snapshot;
                lock (_cards) { snapshot = new List<CardMaquina>(_cards); }

                int total      = snapshot.Count;
                int concluidos = 0;
                var sem        = new SemaphoreSlim(MaxConcorrencia, MaxConcorrencia);

                var tasks = snapshot.Select(async card =>
                {
                    await sem.WaitAsync(token);
                    try
                    {
                        if (token.IsCancellationRequested) return;
                        await PingarCard(card, token);
                        var c = Interlocked.Increment(ref concluidos);
                        _dispatcher.Invoke(() => ProgressoAtualizado?.Invoke(c, total));
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

                // 2 minutos de pausa — não é interrompida por nada além de Parar()
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
