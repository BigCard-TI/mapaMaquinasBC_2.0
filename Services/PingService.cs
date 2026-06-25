using System;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace MapaMaquinas.Services
{
    public enum StatusPing { Aguardando, Online, Offline, SemIp }

    public class ResultadoPing
    {
        public StatusPing Status  { get; init; }
        public long       Latencia { get; init; }   // ms; 0 se offline
    }

    /// <summary>
    /// Pinga um IP em loop com intervalo configurável.
    /// Chama o callback no Dispatcher da UI ao obter resultado.
    /// </summary>
    public class PingService : IDisposable
    {
        private readonly string     _ip;
        private readonly Dispatcher _dispatcher;
        private readonly Action<ResultadoPing> _callback;
        private CancellationTokenSource _cts = new();

        public static readonly TimeSpan IntervaloDefault = TimeSpan.FromSeconds(30);
        public static TimeSpan Intervalo { get; set; } = IntervaloDefault;

        public PingService(string ip, Dispatcher dispatcher, Action<ResultadoPing> callback)
        {
            _ip         = ip;
            _dispatcher = dispatcher;
            _callback   = callback;
        }

        /// <summary>Inicia o loop de ping em uma Task separada.</summary>
        public void Iniciar()
        {
            if (string.IsNullOrWhiteSpace(_ip))
            {
                _dispatcher.Invoke(() => _callback(new ResultadoPing { Status = StatusPing.SemIp }));
                return;
            }

            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            Task.Run(async () =>
            {
                // Primeiro ping imediato
                await PingarENotificar(token);

                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        await Task.Delay(Intervalo, token);
                        await PingarENotificar(token);
                    }
                    catch (TaskCanceledException) { break; }
                }
            }, token);
        }

        private async Task PingarENotificar(CancellationToken token)
        {
            if (token.IsCancellationRequested) return;

            ResultadoPing resultado;
            try
            {
                using var ping = new Ping();
                var reply = await ping.SendPingAsync(_ip, 1500);
                resultado = reply.Status == IPStatus.Success
                    ? new ResultadoPing { Status = StatusPing.Online,  Latencia = reply.RoundtripTime }
                    : new ResultadoPing { Status = StatusPing.Offline, Latencia = 0 };
            }
            catch
            {
                resultado = new ResultadoPing { Status = StatusPing.Offline, Latencia = 0 };
            }

            if (!token.IsCancellationRequested)
                _dispatcher.Invoke(() => _callback(resultado));
        }

        /// <summary>Para o loop e libera o CancellationToken.</summary>
        public void Parar() => _cts.Cancel();

        public void Dispose() => Parar();
    }
}
