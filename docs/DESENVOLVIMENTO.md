# MapaMaquinas — Documentação de Desenvolvimento

## Visão geral

Aplicação desktop Windows em **C# .NET 8 WPF**. Não usa XAML — toda a UI é construída programaticamente em C#. Não tem dependências NuGet externas além do SDK padrão do .NET 8.

---

## Requisitos

- .NET 8 SDK (Windows)
- Visual Studio 2022+ ou `dotnet CLI`
- Windows 10 ou superior (WPF + WindowsForms para ColorDialog)

---

## Estrutura do projeto

```
MapaMaquinas/
├── App.cs                         ← Ponto de entrada (STAThread)
├── MainWindow.cs                  ← Janela principal
├── MapaMaquinas.csproj
│
├── Models/
│   └── Models.cs                  ← Entidades de domínio
│
├── Services/
│   ├── Config.cs                  ← Leitura/gravação de config.ini
│   ├── JsonManager.cs             ← Serialização do arquivo de dados
│   ├── PingService.cs             ← PingWorker e ResultadoPing
│   └── PingQueue.cs               ← Orquestrador da fila de ping
│
├── Controls/
│   ├── CardMaquina.cs             ← Card visual de máquina (Canvas customizado)
│   └── CardPorta.cs               ← Card visual de porta de switch
│
├── Views/
│   ├── JanelaEdicaoMaquina.cs     ← Formulário de criação/edição de máquina
│   ├── JanelaEdicaoPorta.cs       ← Formulário de criação/edição de porta
│   ├── JanelaVisualizacao.cs      ← Tela de detalhes (read-only)
│   └── JanelaSetores.cs           ← Gerenciador de setores
│
└── docs/
    ├── USO.md
    ├── DESENVOLVIMENTO.md
    └── TEMPLATE.md
```

---

## Camada de Modelos — `Models/Models.cs`

Entidades puras (POCOs), sem lógica de UI.

```
Repositorio
  └── List<Empresa>
        ├── List<Setor>       (id, nome, cor hex)
        ├── List<Maquina>     (id, hostname, ip, processador, ram, storage,
        │                      porta_switch, ramal, setor_id, tipo, observacoes,
        │                      cor, pos_x, pos_y)
        └── List<PortaSwitch> (id, numero, descricao, localizacao, observacoes,
                               pos_x, pos_y)
```

`Setor.CorAsColor()` converte hex `#RRGGBB` para `System.Windows.Media.Color`.

---

## Camada de Serviços

### `Config.cs`

Lê e grava `config.ini` ao lado do executável. Formato simples:
```ini
[Config]
CaminhoDados=\\servidor\Interno\TI\
```

API:
```csharp
config.CaminhoDados          // string — pasta dos dados
config.Arquivo("nome.json")  // Path.Combine(CaminhoDados, nome)
config.CaminhoValido()       // Directory.Exists(CaminhoDados)
config.SetCaminhoDados(path) // salva no ini
```

### `JsonManager.cs`

Lê e grava `mapa_maquinas.json` usando `System.Text.Json.Nodes` (sem geração de código).

```csharp
manager.CarregarDoArquivo(caminho);  // popula Repositorio
manager.SalvarNoArquivo();           // sobrescreve o mesmo arquivo
manager.SalvarNoArquivo(outroCaminho);
```

O JSON atualiza `ultima_atualizacao` automaticamente ao salvar.

### `PingService.cs`

Define `StatusPing` (enum) e `ResultadoPing` (POCO), e o worker estático `PingWorker`.

```csharp
// Os dois pings rodam em paralelo — completamente independentes
var resultado = await PingWorker.Executar(hostname, ip, token);
// resultado.StatusHostname  → StatusPing.Online / Offline / Aguardando / SemAlvo
// resultado.LatenciaHostname → long (ms)
// resultado.StatusIp        → StatusPing.*
// resultado.LatenciaIp      → long (ms)
```

Timeout por ping: **2000ms**. Usa `System.Net.NetworkInformation.Ping`.

### `PingQueue.cs`

Orquestrador centralizado. Um único `PingQueue` por janela principal.

```
Fluxo por ciclo:
  snapshot da lista de cards
    → lança tasks com SemaphoreSlim(MaxConcorrencia=5)
    → cada task: PingWorker.Executar → card.AtualizarResultadoPing()
    → aguarda Task.WhenAll
    → dispara evento CicloCompleto
    → Task.Delay(PausaEntreCiclos = 2 min)
    → repete
```

API pública:
```csharp
queue.Iniciar(cards);          // inicia/reinicia o ciclo
queue.Parar();                 // cancela (CancellationToken)
queue.PingarAgora(card);       // ping fora de ciclo, não interrompe a fila
queue.AdicionarCard(card);     // thread-safe (lock)
queue.RemoverCard(card);       // thread-safe (lock)

// Eventos
queue.ProgressoAtualizado += (concluidos, total) => ...;
queue.CicloCompleto       += () => ...;
```

---

## Controles customizados

### `CardMaquina.cs`

Herda de `Canvas` (WPF). Toda a renderização é feita em `OnRender(DrawingContext)` — sem filhos visuais.

**Dimensões:**
```csharp
CardWidth  = 74
LinhaH     = 11   // altura de cada linha de texto
PadTop     = 4
PadBase    = 3
BarraW     = 4    // largura da barra lateral
PadLeft    = BarraW + 4
```

**Barra lateral dividida:**
```csharp
double meioY = Height / 2;
// metade superior → corHostname (baseada em _ping.StatusHostname)
// metade inferior → corIp       (baseada em _ping.StatusIp)
// linha divisória: Pen 0.5px com alpha 80
```

**Highlight de busca** — `DispatcherTimer` a 300ms alterna `_blinkState`, redesenhando bordas concêntricas.

**Drag and drop** — `MouseCapture` + `Canvas.SetLeft/Top` com clamp nos limites do parent.

**API do card:**
```csharp
card.AtualizarResultadoPing(resultado);  // chamado pelo PingQueue (UI thread)
card.ResetarPing();                       // volta para Aguardando
card.AtualizarSetor(setor);              // atualiza cor de fundo
card.SetHighlight(bool);                 // ativa/desativa pisca-pisca
card.SalvarPosicao();                    // escreve PosX/PosY no modelo
card.OnPingarAgora = () => ...;          // callback wired pelo MainWindow
```

### `CardPorta.cs`

Estrutura similar ao `CardMaquina`, mas mais simples. Fundo cinza escuro fixo (`#505050`), sem ping. Exibe número da porta e localização.

---

## `MainWindow.cs`

Toda a UI é construída programaticamente. Não existe arquivo `.xaml`.

**Layout principal (Grid 4 linhas):**
```
Linha 0 → Menu
Linha 1 → ToolBar
Linha 2 → Splitter (lateral + mapa)
Linha 3 → StatusBar
```

**Splitter (Grid 3 colunas):**
```
Coluna 0 → PainelLateral (Grid 2 linhas: TabControl + Legenda)
Coluna 1 → GridSplitter (4px)
Coluna 2 → ScrollViewer → Canvas (_mapaCanvas)
```

**Zoom:**
```csharp
_scaleTransform = new ScaleTransform(1, 1);
_mapaCanvas.RenderTransform = _scaleTransform;
// PreviewMouseWheel → AjustarZoom(±EscalaStep)
// EscalaMin=0.25, EscalaMax=3.0, EscalaStep=0.1
```

**Ciclo de vida do mapa:**
```
CarregarArquivo(caminho)
  → JsonManager.CarregarDoArquivo
  → PopularAbas (desconecta evento, limpa, reconecta, CarregarMapa manual)

CarregarMapa(empresa)
  → PingQueue.Parar + LimparCards
  → cria CardMaquina para cada Maquina
  → Canvas.SetLeft/Top com PosX/PosY do modelo
  → PingQueue.Iniciar(_cards)

LimparCards
  → PingQueue.Parar
  → remove children do Canvas
  → limpa listas _cards e _cardsPorta
```

---

## Janelas de diálogo (Views/)

Todas herdam de `Window` e são construídas 100% por código. Usam `ShowDialog()` e retornam via `DialogResult = true/false`.

| Janela | Propósito |
|---|---|
| `JanelaEdicaoMaquina` | Criar/editar máquina. Valida hostname obrigatório e setor. Preview de cor do setor. |
| `JanelaEdicaoPorta` | Criar/editar porta de switch. Valida número obrigatório e duplicidade. |
| `JanelaVisualizacao` | Read-only. Topo colorido com cor do setor. Scroll de linhas label/valor. |
| `JanelaSetores` | Lista + painel de edição lado a lado. ColorDialog para seleção de cor. Bloqueia exclusão de setor com máquinas. |

---

## Arquivo de dados — `mapa_maquinas.json`

```json
{
  "versao": "1.0",
  "ultima_atualizacao": "2026-01-15T14:32:00",
  "atualizado_por": "TI",
  "empresas": [
    {
      "id": "unidade-a",
      "nome": "Unidade A",
      "mapa_arquivo": "planta.png",
      "setores": [
        { "id": "financeiro", "nome": "Financeiro", "cor": "#4A90D9" }
      ],
      "maquinas": [
        {
          "id": "PC-FIN01",
          "hostname": "PC-FIN01",
          "ip": "192.168.0.10",
          "processador": "i5-12400",
          "ram": "16GB",
          "storage": "512GB SSD",
          "porta_switch": "5",
          "ramal": "201",
          "setor_id": "financeiro",
          "tipo": "desktop",
          "observacoes": "",
          "cor": "",
          "pos_x": 120,
          "pos_y": 80
        }
      ],
      "portas": [
        {
          "id": "porta_1",
          "numero": "1",
          "descricao": "Switch principal",
          "localizacao": "Rack sala servidores",
          "observacoes": "",
          "pos_x": 300,
          "pos_y": 50
        }
      ]
    }
  ]
}
```

**Tipos válidos para `maquinas.tipo`:** `desktop`, `notebook`, `mac`, `servidor`, `impressora`

**Imagem de fundo:** `mapa_arquivo` pode ser caminho absoluto ou relativo à pasta de dados. Formatos suportados pelo WPF: PNG, JPG, BMP, GIF.

---

## Build e publicação

```powershell
# Debug
dotnet build MapaMaquinas.csproj

# Executável standalone para distribuição (sem .NET instalado)
dotnet publish MapaMaquinas.csproj -c Release -r win-x86 --self-contained -p:PublishSingleFile=true

# Saída
bin\Release\net8.0-windows\win-x86\publish\MapaMaquinas.exe
```

---

## .gitignore relevante

```
bin/
obj/
*.user
.vs/
config.ini      ← não versionar (caminho local de cada máquina)
data/           ← não versionar (dados da empresa)
*.png           ← não versionar (plantas do ambiente)
```

---

## Decisões de design

| Decisão | Motivo |
|---|---|
| Sem XAML | Facilita manutenção pontual em arquivo único, sem sincronizar .cs e .xaml |
| Sem NuGet externos | Reduz dependências e simplifica build/distribuição |
| Canvas customizado para cards | Permite renderização precisa com OnRender, necessária para a barra dividida e highlight |
| PingQueue centralizado | Evita N loops independentes; permite controle de concorrência e pausa global |
| SemaphoreSlim(5) | Limita carga na rede sem serializar completamente as verificações |
| IP fixo no JSON | Permite detectar divergência entre o cadastro e a realidade da rede |
| `config.ini` simples | Evita dependência de registro do Windows; portátil para ambientes de rede |

---

## Pontos de extensão comuns

**Adicionar novo campo à máquina:**
1. `Models/Models.cs` → adicionar propriedade em `Maquina`
2. `Services/JsonManager.cs` → `CarregarMaquinas` e `SerializarMaquinas`
3. `Views/JanelaEdicaoMaquina.cs` → adicionar campo no layout e salvar
4. `Views/JanelaVisualizacao.cs` → adicionar linha no `AdicionarLinha`

**Alterar intervalo de ping:**
```csharp
// Services/PingQueue.cs
PingQueue.PausaEntreCiclos = TimeSpan.FromMinutes(5);

// Services/PingService.cs
private const int TimeoutMs = 3000;
```

**Alterar concorrência do ping:**
```csharp
PingQueue.MaxConcorrencia = 10;
```

**Alterar cores da barra lateral:**
```csharp
// Controls/CardMaquina.cs
private static readonly Color CorOnline  = Color.FromRgb(50, 205, 50);
private static readonly Color CorOffline = Color.FromRgb(210, 50, 50);
private static readonly Color CorAguard  = Color.FromRgb(255, 190, 0);
```
