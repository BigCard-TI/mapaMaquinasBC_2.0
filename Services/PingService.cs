using System;
using System.Net;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;

namespace MapaMaquinas.Services
{
    public enum StatusPing { Aguardando, Online, Offline, SemAlvo }

    public class ResultadoDualPing
    {
        public StatusPing StatusNome   { get; init; } = StatusPing.Aguardando;
        public long       LatenciaNome { get; init; }
        public StatusPing StatusIp     { get; init; } = StatusPing.Aguardando;
        public long       LatenciaIp   { get; init; }
        public string     IpResolvido  { get; init; } = "";
        public bool       IpBatem      { get; init; }
    }

    /// <summary>
    /// Executa o dual ping (hostname + IP) de uma única máquina.
    /// Não tem loop — é chamado pelo PingQueue na hora certa.
    /// </summary>
    public static class PingWorker
    {
        private const int TimeoutMs = 2000;

        public static async Task<ResultadoDualPing> Executar(string hostname, string ip,
                                                              CancellationToken token)
        {
            if (token.IsCancellationRequested)
                return new ResultadoDualPing();

            var taskNome = PingarAlvo(string.IsNullOrWhiteSpace(hostname) ? null : hostname, token);
            var taskIp   = PingarAlvo(string.IsNullOrWhiteSpace(ip)       ? null : ip,       token);
            var taskDns  = ResolverDns(hostname, token);

            await Task.WhenAll(taskNome, taskIp, taskDns);

            var (sNome, lNome) = taskNome.Result;
            var (sIp,   lIp)   = taskIp.Result;
            var ipResolvido     = taskDns.Result;

            bool batem = !string.IsNullOrEmpty(ipResolvido)
                         && !string.IsNullOrEmpty(ip)
                         && string.Equals(ipResolvido.Trim(), ip.Trim(),
                                          StringComparison.OrdinalIgnoreCase);

            return new ResultadoDualPing
            {
                StatusNome   = sNome,
                LatenciaNome = lNome,
                StatusIp     = sIp,
                LatenciaIp   = lIp,
                IpResolvido  = ipResolvido,
                IpBatem      = batem
            };
        }

        private static async Task<(StatusPing, long)> PingarAlvo(string? alvo,
                                                                   CancellationToken token)
        {
            if (string.IsNullOrWhiteSpace(alvo)) return (StatusPing.SemAlvo, 0);
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

        private static async Task<string> ResolverDns(string hostname, CancellationToken token)
        {
            if (string.IsNullOrWhiteSpace(hostname)) return "";
            try
            {
                var entry = await Dns.GetHostEntryAsync(hostname);
                foreach (var addr in entry.AddressList)
                    if (addr.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                        return addr.ToString();
                return entry.AddressList.Length > 0 ? entry.AddressList[0].ToString() : "";
            }
            catch { return ""; }
        }
    }
}
