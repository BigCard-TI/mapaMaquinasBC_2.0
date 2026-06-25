using System;
using System.Collections.Generic;
using System.Windows.Media;

namespace MapaMaquinas.Models
{
    public class Setor
    {
        public string Id   { get; set; } = "";
        public string Nome { get; set; } = "";
        public string Cor  { get; set; } = "#808080";

        public Color CorAsColor()
        {
            try
            {
                var hex = Cor.TrimStart('#');
                if (hex.Length == 6)
                {
                    var r = Convert.ToByte(hex.Substring(0, 2), 16);
                    var g = Convert.ToByte(hex.Substring(2, 2), 16);
                    var b = Convert.ToByte(hex.Substring(4, 2), 16);
                    return Color.FromRgb(r, g, b);
                }
            }
            catch { }
            return Color.FromRgb(128, 128, 128);
        }

        public SolidColorBrush CorAsBrush() => new SolidColorBrush(CorAsColor());
    }

    public class Maquina
    {
        public string Id          { get; set; } = "";
        public string Hostname    { get; set; } = "";
        public string Processador { get; set; } = "";
        public string Ip          { get; set; } = "";
        public string Ram         { get; set; } = "";
        public string Storage     { get; set; } = "";
        public string PortaSwitch { get; set; } = "";
        public string Ramal       { get; set; } = "";
        public string SetorId     { get; set; } = "";
        public string Tipo        { get; set; } = "desktop";
        public string Observacoes { get; set; } = "";
        public string Cor         { get; set; } = "";
        public int    PosX        { get; set; } = 0;
        public int    PosY        { get; set; } = 0;

        public bool IsMac => string.Equals(Tipo, "mac", StringComparison.OrdinalIgnoreCase);
    }

    public class PortaSwitch
    {
        public string Id          { get; set; } = "";
        public string Numero      { get; set; } = "";
        public string Descricao   { get; set; } = "";
        public string Localizacao { get; set; } = "";
        public string Observacoes { get; set; } = "";
        public int    PosX        { get; set; } = 0;
        public int    PosY        { get; set; } = 0;
    }

    public class Empresa
    {
        public string Id          { get; set; } = "";
        public string Nome        { get; set; } = "";
        public string MapaArquivo { get; set; } = "";
        public List<Setor>       Setores  { get; set; } = new();
        public List<Maquina>     Maquinas { get; set; } = new();
        public List<PortaSwitch> Portas   { get; set; } = new();

        public Setor?   BuscarSetor(string id)   =>
            Setores.Find(s  => string.Equals(s.Id,  id, StringComparison.OrdinalIgnoreCase));
        public Maquina? BuscarMaquina(string id) =>
            Maquinas.Find(m => string.Equals(m.Id,  id, StringComparison.OrdinalIgnoreCase));
    }

    public class Repositorio
    {
        public List<Empresa> Empresas          { get; set; } = new();
        public string        Versao             { get; set; } = "1.0";
        public string        UltimaAtualizacao  { get; set; } = "";
        public string        AtualizadoPor      { get; set; } = "";

        public Empresa? BuscarEmpresa(string id) =>
            Empresas.Find(e => string.Equals(e.Id, id, StringComparison.OrdinalIgnoreCase));

        public void LimparTudo() => Empresas.Clear();
    }
}
