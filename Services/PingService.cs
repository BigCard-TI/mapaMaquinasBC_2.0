using System;
using System.Net;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;

namespace MapaMaquinas.Services
{
    public enum StatusPing { Aguardando, Online, Offline, SemAlvo }

    /// <summary>
    /// Três estados possíveis para o card:
    ///
    ///   Online    — ping por nome respondeu → máquina ligada e DNS ok   (barra verde)
    ///   DnsAlerta — nome falhou, IP respondeu → ligada mas DNS com problema (barra amarela)
    ///   Offline   — nenhum respondeu → máquina desligada ou inacessível (barra vermelha)
    ///   Aguardando — ainda não foi verificada neste ciclo                (barra amarela clara)
    /// </summary>
    public enum StatusMaquina { Aguardando, Online, DnsAlerta, Offline }

    public class ResultadoPing
    {
        public StatusMaquina Status      { get; init; } = StatusMaquina.Aguardando;
        public string        IpResolvido { get; init; } = "";
        public long          Latencia    { get; init; }   // ms do ping que respondeu (0 se nenhum)
    }

    /// <summary>
    /// Lógica de verificação de uma máquina:
    ///   1. Tenta ping pelo hostname
    ///      → respondeu: Online ✔ (mais nada precisa ser feito)
    ///   2. Se falhou: resolve DNS e tenta ping pelo IP
    ///      → respondeu: DnsAlerta ⚠ (máquina viva, DNS com problema)
    ///   3. Se falhou também: Offline ✗
    /// </summary>
    public static class PingWorker
    {
        private const int TimeoutMs = 2000;

        public static async Task<ResultadoPing> Executar(string hostname,
                                                          CancellationToken token)
        {
            if (string.IsNullOrWhiteSpace(hostname))
                return new ResultadoPing { Status = StatusMaquina.Offline };

            if (token.IsCancellationRequested)
                return new ResultadoPing { Status = StatusMaquina.Aguardando };

            // ── Passo 1: ping pelo nome ───────────────────────────────────────
            var (pingNomeOk, latNome) = await PingarAlvo(hostname, token);

            if (pingNomeOk)
            {
                // Resolve DNS em paralelo para exibir o IP no card (não bloqueia o status)
                var ip = await ResolverDns(hostname, token);
                return new ResultadoPing
                {
                    Status      = StatusMaquina.Online,
                    IpResolvido = ip,
                    Latencia    = latNome
                };
            }

            if (token.IsCancellationRequested)
                return new ResultadoPing { Status = StatusMaquina.Aguardando };

            // ── Passo 2: resolve DNS e pinga pelo IP ──────────────────────────
            var ipResolvido = await ResolverDns(hostname, token);

            if (string.IsNullOrEmpty(ipResolvido))
                return new ResultadoPing { Status = StatusMaquina.Offline };

            var (pingIpOk, latIp) = await PingarAlvo(ipResolvido, token);

            return new ResultadoPing
            {
                Status      = pingIpOk ? StatusMaquina.DnsAlerta : StatusMaquina.Offline,
                IpResolvido = ipResolvido,
                Latencia    = latIp
            };
        }

        private static async Task<(bool ok, long lat)> PingarAlvo(string alvo,
                                                                    CancellationToken token)
        {
            try
            {
                using var ping = new Ping();
                var reply = await ping.SendPingAsync(alvo, TimeoutMs);
                return reply.Status == IPStatus.Success
                    ? (true,  reply.RoundtripTime)
                    : (false, 0);
            }
            catch { return (false, 0); }
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
