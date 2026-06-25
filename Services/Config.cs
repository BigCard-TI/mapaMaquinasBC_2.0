using System;
using System.IO;

namespace MapaMaquinas.Services
{
    public class Config
    {
        private readonly string _caminhoIni;
        private string _caminhoDados = "";

        public Config()
        {
            var exeDir = AppDomain.CurrentDomain.BaseDirectory;
            _caminhoIni = Path.Combine(exeDir, "config.ini");
            CarregarIni();
        }

        private void CarregarIni()
        {
            if (File.Exists(_caminhoIni))
            {
                foreach (var linha in File.ReadAllLines(_caminhoIni))
                {
                    var parts = linha.Split('=', 2);
                    if (parts.Length == 2 && parts[0].Trim().Equals("CaminhoDados", StringComparison.OrdinalIgnoreCase))
                    {
                        _caminhoDados = parts[1].Trim();
                        break;
                    }
                }
            }

            if (string.IsNullOrEmpty(_caminhoDados))
                _caminhoDados = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data");
        }

        private void SalvarIni()
        {
            File.WriteAllLines(_caminhoIni, new[]
            {
                "[Config]",
                $"CaminhoDados={_caminhoDados}"
            });
        }

        public string CaminhoDados => _caminhoDados;

        public string Arquivo(string nome) => Path.Combine(_caminhoDados, nome);

        public void SetCaminhoDados(string caminho)
        {
            _caminhoDados = caminho;
            SalvarIni();
        }

        public bool CaminhoValido() => Directory.Exists(_caminhoDados);
    }
}
