using System;
using System.Text;

namespace MapaMaquinas.Services
{
    /// <summary>
    /// Cifra numérica simétrica usada para a senha de 6 dígitos do usuário.
    /// Port direto do algoritmo Python original:
    ///
    ///   def encode(numero: str) -> str:
    ///       d = numero.zfill(6)
    ///       return ''.join(chr(50 + 2*i + int(d[5-i])) for i in range(6))
    ///
    ///   def decode(cifrado: str) -> str:
    ///       digits = [ord(cifrado[i]) - 50 - 2*i for i in range(6)]
    ///       return ''.join(str(d) for d in reversed(digits))
    ///
    /// IMPORTANTE: esta cifra NÃO é segura para proteger a senha — é apenas
    /// o formato de armazenamento usado pelo banco legado (coluna SENHA).
    /// A segurança real da aplicação vem da conexão (DPAPI) e do controle
    /// de acesso ao próprio banco, não desta cifra.
    /// </summary>
    public static class CifraNumerica
    {
        /// <summary>Codifica um número de até 6 dígitos no formato armazenado no banco.</summary>
        public static string Encode(string numero)
        {
            var d = numero.PadLeft(6, '0');
            if (d.Length != 6)
                throw new ArgumentException("Número deve ter no máximo 6 dígitos.", nameof(numero));

            var sb = new StringBuilder(6);
            for (int i = 0; i < 6; i++)
            {
                int digito = d[5 - i] - '0';
                if (digito < 0 || digito > 9)
                    throw new ArgumentException("Número contém caractere não numérico.", nameof(numero));
                sb.Append((char)(50 + 2 * i + digito));
            }
            return sb.ToString();
        }

        /// <summary>Decodifica o valor armazenado no banco de volta para o número original.</summary>
        public static string Decode(string cifrado)
        {
            if (cifrado.Length != 6)
                throw new ArgumentException("Texto cifrado deve ter exatamente 6 caracteres.", nameof(cifrado));

            var digits = new int[6];
            for (int i = 0; i < 6; i++)
                digits[i] = cifrado[i] - 50 - 2 * i;

            var sb = new StringBuilder(6);
            for (int i = 5; i >= 0; i--)
                sb.Append(digits[i]);

            return sb.ToString();
        }
    }
}
