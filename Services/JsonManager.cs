using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using MapaMaquinas.Models;

namespace MapaMaquinas.Services
{
    public class JsonManager
    {
        private readonly Repositorio _repositorio;
        private string _caminhoArquivo = "";

        public string CaminhoArquivo => _caminhoArquivo;

        public JsonManager(Repositorio repositorio)
        {
            _repositorio = repositorio;
        }

        public void CarregarDoArquivo(string caminho)
        {
            if (!File.Exists(caminho))
                throw new FileNotFoundException($"Arquivo não encontrado: {caminho}");

            _caminhoArquivo = caminho;
            var conteudo = File.ReadAllText(caminho, Encoding.UTF8);

            var root = JsonNode.Parse(conteudo) as JsonObject
                ?? throw new InvalidDataException("JSON inválido ou corrompido.");

            _repositorio.LimparTudo();
            _repositorio.Versao            = root["versao"]?.GetValue<string>() ?? "1.0";
            _repositorio.UltimaAtualizacao = root["ultima_atualizacao"]?.GetValue<string>() ?? "";
            _repositorio.AtualizadoPor     = root["atualizado_por"]?.GetValue<string>() ?? "";
            _repositorio.EscalaCards       = root["escala_cards"]  ?.GetValue<double>() ?? 1.0;

            if (root["empresas"] is JsonArray arr)
                CarregarEmpresas(arr);
        }

        private void CarregarEmpresas(JsonArray array)
        {
            foreach (var item in array)
            {
                if (item is not JsonObject obj) continue;
                var empresa = new Empresa
                {
                    Id          = Str(obj, "id"),
                    Nome        = Str(obj, "nome"),
                    MapaArquivo = Str(obj, "mapa_arquivo")
                };

                if (obj["setores"] is JsonArray setores)
                    CarregarSetores(empresa, setores);

                if (obj["maquinas"] is JsonArray maquinas)
                    CarregarMaquinas(empresa, maquinas);

                if (obj["portas"] is JsonArray portas)
                    CarregarPortas(empresa, portas);

                _repositorio.Empresas.Add(empresa);
            }
        }

        private void CarregarSetores(Empresa empresa, JsonArray array)
        {
            foreach (var item in array)
            {
                if (item is not JsonObject obj) continue;
                empresa.Setores.Add(new Setor
                {
                    Id   = Str(obj, "id"),
                    Nome = Str(obj, "nome"),
                    Cor  = Str(obj, "cor")
                });
            }
        }

        private void CarregarMaquinas(Empresa empresa, JsonArray array)
        {
            foreach (var item in array)
            {
                if (item is not JsonObject obj) continue;
                empresa.Maquinas.Add(new Maquina
                {
                    Id          = Str(obj, "id"),
                    Hostname    = Str(obj, "hostname"),
                    Ip          = Str(obj, "ip"),
                    Processador = Str(obj, "processador"),
                    Ram         = Str(obj, "ram"),
                    Storage     = Str(obj, "storage"),
                    PortaSwitch = Str(obj, "porta_switch"),
                    Ramal       = Str(obj, "ramal"),
                    SetorId     = Str(obj, "setor_id"),
                    Tipo        = Str(obj, "tipo"),
                    Observacoes = Str(obj, "observacoes"),
                    Cor         = Str(obj, "cor"),
                    PosX        = Int(obj, "pos_x"),
                    PosY        = Int(obj, "pos_y")
                });
            }
        }

        private void CarregarPortas(Empresa empresa, JsonArray array)
        {
            foreach (var item in array)
            {
                if (item is not JsonObject obj) continue;
                empresa.Portas.Add(new PortaSwitch
                {
                    Id          = Str(obj, "id"),
                    Numero      = Str(obj, "numero"),
                    Descricao   = Str(obj, "descricao"),
                    Localizacao = Str(obj, "localizacao"),
                    Observacoes = Str(obj, "observacoes"),
                    PosX        = Int(obj, "pos_x"),
                    PosY        = Int(obj, "pos_y")
                });
            }
        }

        public void SalvarNoArquivo(string? caminho = null)
        {
            var destino = caminho ?? _caminhoArquivo;
            if (string.IsNullOrEmpty(destino))
                throw new InvalidOperationException("Caminho de destino não definido.");

            _repositorio.UltimaAtualizacao = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss");

            var root = new JsonObject
            {
                ["versao"]             = _repositorio.Versao,
                ["ultima_atualizacao"] = _repositorio.UltimaAtualizacao,
                ["atualizado_por"]     = _repositorio.AtualizadoPor,
                ["escala_cards"]       = _repositorio.EscalaCards,
                ["empresas"]           = SerializarEmpresas()
            };

            var opts = new JsonSerializerOptions { WriteIndented = true };
            File.WriteAllText(destino, root.ToJsonString(opts), Encoding.UTF8);
        }

        private JsonArray SerializarEmpresas()
        {
            var arr = new JsonArray();
            foreach (var e in _repositorio.Empresas)
            {
                arr.Add(new JsonObject
                {
                    ["id"]           = e.Id,
                    ["nome"]         = e.Nome,
                    ["mapa_arquivo"] = e.MapaArquivo,
                    ["setores"]      = SerializarSetores(e),
                    ["maquinas"]     = SerializarMaquinas(e),
                    ["portas"]       = SerializarPortas(e)
                });
            }
            return arr;
        }

        private JsonArray SerializarSetores(Empresa e)
        {
            var arr = new JsonArray();
            foreach (var s in e.Setores)
                arr.Add(new JsonObject { ["id"] = s.Id, ["nome"] = s.Nome, ["cor"] = s.Cor });
            return arr;
        }

        private JsonArray SerializarMaquinas(Empresa e)
        {
            var arr = new JsonArray();
            foreach (var m in e.Maquinas)
            {
                arr.Add(new JsonObject
                {
                    ["id"]           = m.Id,
                    ["hostname"]     = m.Hostname,
                    ["ip"]           = m.Ip,
                    ["processador"]  = m.Processador,
                    ["ram"]          = m.Ram,
                    ["storage"]      = m.Storage,
                    ["porta_switch"] = m.PortaSwitch,
                    ["ramal"]        = m.Ramal,
                    ["setor_id"]     = m.SetorId,
                    ["tipo"]         = m.Tipo,
                    ["observacoes"]  = m.Observacoes,
                    ["cor"]          = m.Cor,
                    ["pos_x"]        = m.PosX,
                    ["pos_y"]        = m.PosY
                });
            }
            return arr;
        }

        private JsonArray SerializarPortas(Empresa e)
        {
            var arr = new JsonArray();
            foreach (var p in e.Portas)
            {
                arr.Add(new JsonObject
                {
                    ["id"]          = p.Id,
                    ["numero"]      = p.Numero,
                    ["descricao"]   = p.Descricao,
                    ["localizacao"] = p.Localizacao,
                    ["observacoes"] = p.Observacoes,
                    ["pos_x"]       = p.PosX,
                    ["pos_y"]       = p.PosY
                });
            }
            return arr;
        }

        private static string Str(JsonObject obj, string key) =>
            obj[key]?.GetValue<string>() ?? "";

        private static int Int(JsonObject obj, string key)
        {
            try { return obj[key]?.GetValue<int>() ?? 0; }
            catch { return 0; }
        }
    }
}
