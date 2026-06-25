# MapaMaquinas — C# WPF

Ferramenta de mapeamento visual de ativos de rede (máquinas, switches, setores) para equipes de TI.

Permite posicionar cards de máquinas sobre a planta do ambiente, com suporte a drag-and-drop, ping em tempo real, zoom e gerenciamento de setores.

---

## Funcionalidades

- **Mapa interativo** — cards arrastáveis sobre a planta do ambiente
- **Ping em tempo real** — indicador visual de status por máquina (online/offline/aguardando)
- **Zoom** — Ctrl + scroll, botões na toolbar ou Ctrl+0 para resetar
- **Busca** — localiza máquinas por hostname, IP, ramal ou porta de switch com highlight piscante
- **Múltiplas empresas/unidades** — abas separadas por empresa
- **Gerenciamento de setores** — CRUD com color picker
- **Exportação PNG** — captura o mapa atual como imagem
- **Persistência JSON** — dados salvos em arquivo local ou de rede

---

## Requisitos

- .NET 8 SDK (Windows)
- Visual Studio 2022+ **ou** `dotnet CLI`

---

## Build & execução

```powershell
# Compilar
dotnet build MapaMaquinas.csproj

# Executar
dotnet run --project MapaMaquinas.csproj

# Publicar como executável standalone
dotnet publish MapaMaquinas.csproj -c Release -r win-x86 --self-contained -p:PublishSingleFile=true
```

Executável gerado em:
```
bin\Release\net8.0-windows\win-x86\publish\MapaMaquinas.exe
```

---

## Estrutura de arquivos

```
MapaMaquinas/
├── MapaMaquinas.csproj
├── App.cs                         ← Ponto de entrada
├── MainWindow.cs                  ← Janela principal (mapa, toolbar, menu)
│
├── Models/
│   └── Models.cs                  ← Setor, Maquina, PortaSwitch, Empresa, Repositorio
│
├── Services/
│   ├── JsonManager.cs             ← Carrega/salva o arquivo de dados
│   ├── Config.cs                  ← Lê/grava config.ini
│   └── PingService.cs             ← Ping em background com CancellationToken
│
├── Controls/
│   ├── CardMaquina.cs             ← Card drag-and-drop com indicador de ping
│   └── CardPorta.cs               ← Card de porta de switch
│
└── Views/
    ├── JanelaEdicaoMaquina.cs     ← Formulário de criação/edição de máquina
    ├── JanelaEdicaoPorta.cs       ← Formulário de criação/edição de porta
    ├── JanelaVisualizacao.cs      ← Tela de detalhes (read-only)
    └── JanelaSetores.cs           ← Gerenciador de setores
```

---

## Configuração

Na primeira execução, use **Arquivo → Configurar caminho...** e informe a pasta onde está o arquivo `mapa_maquinas.json`. Pode ser um caminho local ou de rede. O caminho é salvo em `config.ini` ao lado do executável.

---

## Formato do arquivo de dados

O sistema lê e grava um arquivo `mapa_maquinas.json` com a seguinte estrutura:

```json
{
  "versao": "1.0",
  "ultima_atualizacao": "2026-01-01T00:00:00",
  "atualizado_por": "TI",
  "empresas": [
    {
      "id": "unidade-a",
      "nome": "Unidade A",
      "mapa_arquivo": "planta.png",
      "setores": [
        { "id": "setor-1", "nome": "Administrativo", "cor": "#4A90D9" },
        { "id": "setor-2", "nome": "TI",             "cor": "#27AE60" }
      ],
      "maquinas": [
        {
          "id": "PC-EXEMPLO",
          "hostname": "PC-EXEMPLO",
          "processador": "i5-12400",
          "ram": "16GB",
          "storage": "512GB SSD",
          "ip": "192.168.0.10",
          "porta_switch": "1",
          "ramal": "100",
          "setor_id": "setor-1",
          "tipo": "desktop",
          "observacoes": "",
          "cor": "",
          "pos_x": 100,
          "pos_y": 80
        }
      ],
      "portas": []
    }
  ]
}
```

Tipos de máquina aceitos: `desktop`, `notebook`, `mac`, `servidor`, `impressora`.

---

## Atalhos de teclado

| Atalho | Ação |
|---|---|
| `Ctrl + S` | Salvar |
| `Ctrl + O` | Abrir arquivo |
| `Insert` | Nova máquina |
| `Ctrl + Scroll` | Zoom |
| `Ctrl + +` / `Ctrl + −` | Zoom +/− |
| `Ctrl + 0` | Resetar zoom (100%) |

---

## Indicador de ping

Cada card exibe uma bolinha no canto superior direito:

| Cor | Significado |
|---|---|
| 🟢 Verde | Online (exibe latência em ms) |
| 🔴 Vermelho | Sem resposta (ICMP bloqueado ou host offline) |
| 🟡 Amarelo | Aguardando primeiro resultado |
| ⚫ Cinza | Sem IP cadastrado |

O ping é disparado automaticamente ao carregar o mapa e repetido a cada 30 segundos. É possível forçar um ping imediato pelo menu de contexto do card.

> **Obs:** máquinas com firewall bloqueando ICMP aparecerão como offline mesmo que estejam acessíveis na rede. Isso é uma limitação do protocolo, não do sistema.

---

## Licença

MIT
