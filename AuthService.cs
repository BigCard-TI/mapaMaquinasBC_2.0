using System;
using System.Data;
using Microsoft.Data.SqlClient;
using System.Threading.Tasks;

namespace MapaMaquinas.Services
{
    public enum ResultadoLogin
    {
        Sucesso,
        UsuarioNaoEncontrado,
        SenhaIncorreta,
        ErroConexao,
        ConexaoNaoConfigurada
    }

    /// <summary>
    /// Autentica contra a tabela USUARIOS (CODIGO, SENHA) do SQL Server.
    /// A senha é armazenada cifrada com CifraNumerica — comparamos o valor
    /// já cifrado, sem decifrar o que vem do banco (evita expor a senha
    /// em memória além do necessário).
    /// </summary>
    public static class AuthService
    {
        public static async Task<ResultadoLogin> Autenticar(string codigoUsuario, string senha)
        {
            string connectionString;
            try
            {
                connectionString = ConexaoConfig.Carregar();
            }
            catch (InvalidOperationException)
            {
                return ResultadoLogin.ConexaoNaoConfigurada;
            }

            string senhaCifrada;
            try
            {
                senhaCifrada = CifraNumerica.Encode(senha);
            }
            catch
            {
                // Senha em formato inválido (não numérico ou tamanho errado)
                return ResultadoLogin.SenhaIncorreta;
            }

            try
            {
                using var conn = new SqlConnection(connectionString);
                await conn.OpenAsync();

                using var cmd = new SqlCommand(
                    "SELECT SENHA FROM USUARIOS WHERE CODIGO = @codigo", conn);
                cmd.Parameters.Add("@codigo", SqlDbType.NVarChar, 4).Value = codigoUsuario;

                var resultado = await cmd.ExecuteScalarAsync();

                if (resultado == null || resultado == DBNull.Value)
                    return ResultadoLogin.UsuarioNaoEncontrado;

                var senhaArmazenada = resultado.ToString() ?? "";

                return string.Equals(senhaArmazenada, senhaCifrada, StringComparison.Ordinal)
                    ? ResultadoLogin.Sucesso
                    : ResultadoLogin.SenhaIncorreta;
            }
            catch (SqlException)
            {
                return ResultadoLogin.ErroConexao;
            }
        }

        /// <summary>Testa a conexão sem autenticar — usado na tela "Configurar conexão".</summary>
        public static async Task<(bool ok, string mensagem)> TestarConexao(string connectionString)
        {
            try
            {
                using var conn = new SqlConnection(connectionString);
                await conn.OpenAsync();

                // Confirma que a tabela esperada existe
                using var cmd = new SqlCommand(
                    "SELECT COUNT(*) FROM USUARIOS", conn);
                await cmd.ExecuteScalarAsync();

                return (true, "Conexão estabelecida e tabela USUARIOS acessível.");
            }
            catch (SqlException ex)
            {
                return (false, $"Falha na conexão: {ex.Message}");
            }
            catch (Exception ex)
            {
                return (false, $"Erro: {ex.Message}");
            }
        }
    }
}
