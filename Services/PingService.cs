using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;

namespace MapaMaquinas.Services
{
    public enum StatusPing { Aguardando, Online, Offline, SemAlvo }

    /// <summary>
    /// Resultado com dois status independentes:
    ///   StatusHostname — resultado do ping pelo nome
    ///   StatusIp       — resultado do ping pelo IP do JSON
    /// Cada um alimenta uma metade da barra lateral do card.
    /// </summary>
    public class ResultadoPing
    {
        public StatusPing StatusHostname  { get; init; } = StatusPing.Aguardando;
        public long       LatenciaHostname { get; init; }

        public StatusPing StatusIp        { get; init; } = StatusPing.Aguardando;
        public long       LatenciaIp      { get; init; }
    }

    /// <summary>
    /// Pinga hostname e IP em paralelo — completamente independentes.
    /// Não há ordem de prioridade: os dois sempre são verificados.
    /// </summary>
    public static class PingWorker
    {
        private const int TimeoutMs = 2000;

        public static async Task<ResultadoPing> Executar(string hostname, string ip,
                                                          CancellationToken token)
        {
            if (token.IsCancellationRequested)
                return new ResultadoPing();

            var taskHostname = PingarAlvo(hostname, token);
            var taskIp       = PingarAlvo(ip,       token);

            await Task.WhenAll(taskHostname, taskIp);

            var (sHost, lHost) = taskHostname.Result;
            var (sIp,   lIp)   = taskIp.Result;

            return new ResultadoPing
            {
                StatusHostname   = sHost,
                LatenciaHostname = lHost,
                StatusIp         = sIp,
                LatenciaIp       = lIp
            };
        }

        private static async Task<(StatusPing status, long lat)> PingarAlvo(
            string alvo, CancellationToken token)
        {
            if (string.IsNullOrWhiteSpace(alvo))
                return (StatusPing.SemAlvo, 0);
            try
            {
                using var ping = new Ping();
                var reply = await ping.SendPingAsync(alvo, TimeoutMs);
                return reply.Status == IPStatus.Success
                    ? (StatusPing.Online,  reply.RoundtripTime)
                    : (StatusPing.Offline, 0);
            }
            catch { return (StatusPing.Offline, 0); }
        }
    }
}
