using System;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32;

namespace MapaMaquinas.Services
{
    /// <summary>
    /// Armazena a connection string do SQL Server criptografada via DPAPI
    /// (CurrentUser scope) diretamente no Registro do Windows — HKCU.
    ///
    /// Por que Registro e não arquivo .dat:
    ///   - Não cria nenhum arquivo na pasta do projeto/instalação.
    ///   - Nada sensível pode ser versionado por acidente no Git.
    ///   - DPAPI/CurrentUser já garante que só o mesmo usuário Windows na
    ///     mesma máquina consegue decifrar — o Registro só guarda o "onde".
    ///
    /// Chave usada:  HKEY_CURRENT_USER\Software\BigCard\MapaMaquinas
    /// Valor:        ConexaoSql  (REG_BINARY — blob DPAPI)
    /// </summary>
    public static class ConexaoConfig
    {
        private const string CaminhoChaveRegistro = @"Software\BigCard\MapaMaquinas";
        private const string NomeValor             = "ConexaoSql";

        // Entropia adicional fixa no binário — não é segredo, apenas evita que
        // qualquer outro processo que também use DPAPI/CurrentUser no Windows
        // do mesmo usuário acidentalmente decifre este blob específico.
        private static readonly byte[] Entropia =
            Encoding.UTF8.GetBytes("MapaMaquinas.BigCard.ConexaoSql.v1");

        /// <summary>True se já existe uma connection string configurada.</summary>
        public static bool Existe()
        {
            using var chave = Registry.CurrentUser.OpenSubKey(CaminhoChaveRegistro);
            return chave?.GetValue(NomeValor) is byte[];
        }

        /// <summary>
        /// Criptografa e grava a connection string no Registro (HKCU).
        /// Só pode ser decifrada pelo mesmo usuário Windows na mesma máquina.
        /// </summary>
        public static void Salvar(string connectionString)
        {
            var dadosClaros = Encoding.UTF8.GetBytes(connectionString);
            var dadosCifrados = ProtectedData.Protect(
                dadosClaros, Entropia, DataProtectionScope.CurrentUser);

            using var chave = Registry.CurrentUser.CreateSubKey(CaminhoChaveRegistro);
            chave.SetValue(NomeValor, dadosCifrados, RegistryValueKind.Binary);
        }

        /// <summary>
        /// Lê e decifra a connection string do Registro.
        /// Lança exceção se não houver nada configurado ou se a descriptografia
        /// falhar (ex: perfil de usuário diferente, máquina diferente).
        /// </summary>
        public static string Carregar()
        {
            using var chave = Registry.CurrentUser.OpenSubKey(CaminhoChaveRegistro);
            if (chave?.GetValue(NomeValor) is not byte[] dadosCifrados)
                throw new InvalidOperationException(
                    "Nenhuma conexão configurada. Use 'Configurar conexão...' no menu.");

            byte[] dadosClaros;
            try
            {
                dadosClaros = ProtectedData.Unprotect(
                    dadosCifrados, Entropia, DataProtectionScope.CurrentUser);
            }
            catch (CryptographicException ex)
            {
                throw new InvalidOperationException(
                    "Não foi possível descriptografar a conexão salva. " +
                    "Isso ocorre se o arquivo foi copiado de outra máquina/usuário. " +
                    "Reconfigure a conexão.", ex);
            }

            return Encoding.UTF8.GetString(dadosClaros);
        }

        /// <summary>Remove a connection string salva (ex: botão "Esquecer conexão").</summary>
        public static void Remover()
        {
            Registry.CurrentUser.DeleteSubKeyTree(CaminhoChaveRegistro, throwOnMissingSubKey: false);
        }
    }
}
