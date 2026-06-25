using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;

namespace MapaMaquinas.Services
{
    /// <summary>
    /// Estados do card:
    ///   Online    — hostname respondeu ao ping                  (barra verde)
    ///   IpAlerta  — hostname falhou, IP respondeu               (barra amarela)
    ///   Offline   — nenhum respondeu                            (barra vermelha)
    ///   Aguardando — ainda não verificada neste ciclo           (barra cinza)
    /// </summary>
    public enum StatusMaquina { Aguardando, Online, IpAlerta, Offline }

    public class ResultadoPing
    {
        public StatusMaquina Status   { get; init; } = StatusMaquina.Aguardando;
        public long          Latencia { get; init; }
    }

    /// <summary>
    /// Verificação em dois passos usando hostname e IP fixos do JSON:
    ///   1. Pinga pelo hostname → OK: Online
    ///   2. Hostname falhou → pinga pelo IP → OK: IpAlerta (ligada mas nome sem resposta)
    ///   3. Ambos falharam → Offline
    /// </summary>
    public static class PingWorker
    {
        private const int TimeoutMs = 2000;

        public static async Task<ResultadoPing> Executar(string hostname, string ip,
                                                          CancellationToken token)
        {
            if (token.IsCancellationRequested)
                return new ResultadoPing { Status = StatusMaquina.Aguardando };

            // Passo 1 — ping pelo hostname
            if (!string.IsNullOrWhiteSpace(hostname))
            {
                var (ok, lat) = await Pingar(hostname, token);
                if (ok) return new ResultadoPing { Status = StatusMaquina.Online, Latencia = lat };
            }

            if (token.IsCancellationRequested)
                return new ResultadoPing { Status = StatusMaquina.Aguardando };

            // Passo 2 — ping pelo IP fixo
            if (!string.IsNullOrWhiteSpace(ip))
            {
                var (ok, lat) = await Pingar(ip, token);
                if (ok) return new ResultadoPing { Status = StatusMaquina.IpAlerta, Latencia = lat };
            }

            return new ResultadoPing { Status = StatusMaquina.Offline };
        }

        private static async Task<(bool ok, long lat)> Pingar(string alvo,
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
    }
}
