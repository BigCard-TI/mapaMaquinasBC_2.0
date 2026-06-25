using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using MapaMaquinas.Controls;

namespace MapaMaquinas.Services
{
    /// <summary>
    /// Fila centralizada de ping.
    ///
    /// Fluxo:
    ///   1. Pinga a primeira máquina da lista.
    ///   2. Notifica o card via Dispatcher (UI thread).
    ///   3. Aguarda um pequeno intervalo entre máquinas (PausaEntreMaquinas).
    ///   4. Repete até o fim da lista.
    ///   5. Ao terminar o ciclo completo, aguarda PausaEntreCiclos (2 min).
    ///   6. Recomeça do início.
    ///
    /// Só uma máquina é pingada por vez — sem paralelismo — para não
    /// sobrecarregar a rede.
    /// </summary>
    public class PingQueue : IDisposable
    {
        // ── Configuração ──────────────────────────────────────────────────────
        /// <summary>Pausa entre o ping de cada máquina (evita rajada).</summary>
        public static TimeSpan PausaEntreMaquinas { get; set; } = TimeSpan.FromSeconds(1);

        /// <summary>Pausa após o ciclo completo antes de reiniciar.</summary>
        public static TimeSpan PausaEntreCiclos { get; set; } = TimeSpan.FromMinutes(2);

        // ── Estado ────────────────────────────────────────────────────────────
        private readonly Dispatcher _dispatcher;
        private List<CardMaquina>   _cards = new();
        private CancellationTokenSource _cts = new();
        private bool _rodando;

        // Índice da máquina sendo pingada no momento (para o status bar)
        public event Action<int, int>? ProgressoAtualizado;  // (atual, total)
        public event Action?           CicloCompleto;

        public PingQueue(Dispatcher dispatcher)
        {
            _dispatcher = dispatcher;
        }

        // ── API pública ───────────────────────────────────────────────────────

        /// <summary>
        /// Define a lista de cards e (re)inicia o loop de ping.
        /// Cancela qualquer ciclo em andamento antes de começar.
        /// </summary>
        public void Iniciar(List<CardMaquina> cards)
        {
            Parar();
            _cards  = new List<CardMaquina>(cards);   // cópia local
            _cts    = new CancellationTokenSource();
            _rodando = true;
            Task.Run(() => Loop(_cts.Token));
        }

        /// <summary>Cancela imediatamente e aguarda o loop encerrar.</summary>
        public void Parar()
        {
            _cts.Cancel();
            _rodando = false;
        }

        /// <summary>
        /// Força o ping imediato de um card específico fora de ciclo
        /// (chamado pelo menu "Verificar agora" do card).
        /// Não interrompe o ciclo em andamento.
        /// </summary>
        public void PingarAgora(CardMaquina card)
        {
            Task.Run(async () => await PingarCard(card, CancellationToken.None));
        }

        // ── Loop principal ────────────────────────────────────────────────────
        private async Task Loop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                // Captura snapshot da lista para o ciclo atual
                // (cards podem ser adicionados/removidos entre ciclos)
                List<CardMaquina> snapshot;
                lock (_cards) { snapshot = new List<CardMaquina>(_cards); }

                int total = snapshot.Count;

                for (int i = 0; i < total; i++)
                {
                    if (token.IsCancellationRequested) return;

                    var card = snapshot[i];

                    // Notifica progresso na UI
                    int atual = i + 1;
                    _dispatcher.Invoke(() => ProgressoAtualizado?.Invoke(atual, total));

                    await PingarCard(card, token);

                    // Pausa entre máquinas (exceto após a última)
                    if (i < total - 1 && !token.IsCancellationRequested)
                    {
                        try { await Task.Delay(PausaEntreMaquinas, token); }
                        catch (TaskCanceledException) { return; }
                    }
                }

                // Ciclo completo
                _dispatcher.Invoke(() =>
                {
                    ProgressoAtualizado?.Invoke(0, total);
                    CicloCompleto?.Invoke();
                });

                // Aguarda antes do próximo ciclo
                try { await Task.Delay(PausaEntreCiclos, token); }
                catch (TaskCanceledException) { return; }
            }
        }

        private async Task PingarCard(CardMaquina card, CancellationToken token)
        {
            if (token.IsCancellationRequested) return;

            string hostname, ip;

            // Lê as propriedades no UI thread para evitar race condition
            _dispatcher.Invoke(() =>
            {
                hostname = card.Maquina?.Hostname ?? "";
                ip       = card.Maquina?.Ip       ?? "";
            });

            // Relê fora do lock (valores já capturados)
            hostname = card.Maquina?.Hostname ?? "";
            ip       = card.Maquina?.Ip       ?? "";

            var resultado = await PingWorker.Executar(hostname, ip, token);

            if (!token.IsCancellationRequested)
                _dispatcher.Invoke(() => card.AtualizarResultadoPing(resultado));
        }

        // ── Gerência da lista em tempo real ───────────────────────────────────

        public void AdicionarCard(CardMaquina card)
        {
            lock (_cards) { if (!_cards.Contains(card)) _cards.Add(card); }
        }

        public void RemoverCard(CardMaquina card)
        {
            lock (_cards) { _cards.Remove(card); }
        }

        public void Dispose() => Parar();
    }
}
