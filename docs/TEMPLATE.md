# MapaMaquinas — Template para Recriação

Este documento descreve o sistema com nível de detalhe suficiente para que uma IA recrie o projeto do zero, em qualquer linguagem ou framework.

---

## Propósito do sistema

Ferramenta desktop para equipes de TI visualizarem e monitorarem os ativos de rede (computadores, switches) de um ambiente, posicionados sobre a planta física do local. O sistema verifica conectividade via ping e exibe o resultado visualmente em cada card de máquina.

---

## Requisitos funcionais

### RF-01 — Mapa visual interativo
- Exibir uma imagem de fundo (planta do ambiente) em uma área rolável e com zoom
- Posicionar cards de máquinas sobre essa imagem com drag-and-drop livre
- Salvar e restaurar a posição de cada card (coordenadas X, Y)
- Suportar múltiplas empresas/unidades em abas separadas

### RF-02 — Card de máquina
- Exibir hostname, IP, porta de switch e ramal (opcional)
- Indicador visual de status de ping por duas verificações independentes:
  - Ping pelo hostname
  - Ping pelo IP cadastrado
- Menu de contexto: ver detalhes, editar, remover, verificar agora
- Duplo clique abre detalhes
- Highlight visual (pisca) quando encontrado pela busca

### RF-03 — Ping em tempo real
- Verificar hostname e IP de forma paralela e independente (resultado separado para cada)
- Ciclo automático: verificar todas as máquinas, aguardar 2 minutos, repetir
- Limitar paralelismo para não sobrecarregar a rede (máximo 5 simultâneos)
- Permitir verificação manual imediata de uma máquina específica
- Não bloquear a interface durante o ping (background thread)

### RF-04 — Gerenciamento de dados
- CRUD de máquinas (campos: hostname, IP, processador, RAM, storage, porta switch, ramal, setor, tipo, observações)
- CRUD de setores (nome + cor hex) — cor define o fundo dos cards
- CRUD de portas de switch (número, descrição, localização)
- Persistência em arquivo JSON local ou de rede
- Validação: hostname obrigatório, setor obrigatório, número de porta único

### RF-05 — Busca
- Buscar por hostname, IP, ramal ou porta de switch
- Destacar visualmente os cards encontrados
- Rolar o mapa até o primeiro resultado

### RF-06 — Zoom
- Zoom via scroll do mouse (com tecla modificadora) centrado no cursor
- Botões de zoom na toolbar e por atalho de teclado
- Range: 25% a 300%, incremento de 10%
- Reset para 100% com um clique

### RF-07 — Exportação
- Exportar o mapa atual como imagem PNG

---

## Requisitos não funcionais

- **RNF-01** — Aplicação desktop Windows nativa
- **RNF-02** — Sem dependências externas além do SDK padrão da plataforma
- **RNF-03** — UI responsiva durante operações de ping (nunca travar)
- **RNF-04** — Persistência em JSON legível e editável manualmente
- **RNF-05** — Configuração de caminho de dados sem edição de código (suporte a UNC)
- **RNF-06** — Não salvar automaticamente — exigir ação explícita do usuário

---

## Modelo de dados

### Hierarquia

```
Repositorio
  versao: string
  ultima_atualizacao: string (ISO 8601)
  atualizado_por: string
  empresas: Empresa[]

Empresa
  id: string
  nome: string
  mapa_arquivo: string (caminho da imagem de fundo)
  setores: Setor[]
  maquinas: Maquina[]
  portas: PortaSwitch[]

Setor
  id: string
  nome: string
  cor: string (#RRGGBB)

Maquina
  id: string (igual ao hostname)
  hostname: string
  ip: string
  processador: string
  ram: string
  storage: string
  porta_switch: string
  ramal: string
  setor_id: string (FK → Setor.id)
  tipo: enum (desktop | notebook | mac | servidor | impressora)
  observacoes: string
  cor: string (reservado, sempre vazio — cor vem do setor)
  pos_x: int
  pos_y: int

PortaSwitch
  id: string
  numero: string
  descricao: string
  localizacao: string
  observacoes: string
  pos_x: int
  pos_y: int
```

### Formato do arquivo JSON

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
      "portas": []
    }
  ]
}
```

---

## Comportamento do ping

### Lógica de verificação

Os dois pings (hostname e IP) são executados em **paralelo e de forma completamente independente**. Não há cascata — o resultado de um não interfere no outro.

```
Para cada máquina no ciclo:
  Pinga hostname  (ICMP, timeout 2s)  →  StatusHostname
  Pinga IP        (ICMP, timeout 2s)  →  StatusIp
  (ambos em paralelo)
  Atualiza o card com os dois resultados
```

### Estados possíveis por verificação

```
Online    → ping respondeu dentro do timeout
Offline   → ping não respondeu ou erro
Aguardando → ainda não verificado neste ciclo
SemAlvo   → campo vazio no cadastro (não tenta pingar)
```

### Ciclo da fila

```
INÍCIO DO CICLO
  snapshot da lista de cards
  para cada grupo de até N cards (N = MaxConcorrencia):
    executa PingWorker.Executar(hostname, ip)
    atualiza card no thread de UI
  aguarda todos finalizarem (WaitAll)
FIM DO CICLO
  pausa 2 minutos
  repete
```

### Verificação manual

Uma máquina pode ser verificada fora do ciclo sem interrompê-lo. A verificação manual usa o mesmo `PingWorker` mas não passa pelo semáforo da fila.

---

## Indicador visual de status (barra lateral do card)

```
┌──┬────────────────┐
│▓▓│ PC-FIN01       │
│░░│ 192.168.0.10   │
└──┴────────────────┘
 ↑
 Barra de 4px dividida em duas metades verticais:
   Metade superior (▓) → resultado do ping pelo hostname
   Metade inferior (░) → resultado do ping pelo IP
```

Cores:
- Verde  → Online
- Vermelho → Offline
- Amarelo → Aguardando
- Cinza  → SemAlvo

Uma linha divisória sutil (semitransparente) separa as duas metades.

### Tabela de diagnósticos

| Superior | Inferior | Diagnóstico |
|---|---|---|
| Verde | Verde | Tudo OK |
| Verde | Vermelho | IP cadastrado errado |
| Vermelho | Verde | Hostname com problema (DNS/registro) |
| Vermelho | Vermelho | Máquina offline |

---

## Componentes da interface

### Janela principal

```
Menu
  Arquivo: Abrir, Salvar, Configurar caminho, Exportar PNG, Sair
  Máquinas: Nova máquina, Gerenciar setores
  Portas: Nova porta de switch

Toolbar
  [Abrir] [Salvar] | [+ Máquina] [+ Porta] [⚙ Setores] | [Busca] [🔍] [✕] | [−] [100%] [+]

Área central (splitter redimensionável)
  Lateral esquerda (160px):
    TabControl com uma aba por empresa
    Legenda de cores (fixa na parte inferior)
  Área do mapa:
    ScrollViewer com zoom via ScaleTransform
    Canvas com imagem de fundo + cards

Barra de status
  Texto com: nome da empresa | contagem | progresso do ping
```

### Legenda

Painel fixo no rodapé do painel lateral. Exibe:
- As 4 cores possíveis com nome e descrição
- 4 exemplos de combinação com barra dupla (mini-card visual)

### Formulário de máquina

- Campos em ScrollBox vertical
- ComboBox para setor (com preview de cor ao lado)
- ComboBox para tipo
- Botões Salvar / Cancelar

### Gerenciador de setores

- ListView à esquerda com lista de setores
- Painel de edição à direita (nome + cor hex + preview + seletor)
- Rodapé com botões Novo, Excluir, Fechar
- Ao fechar com alterações, o mapa reaplica as cores

---

## Configuração

Arquivo `config.ini` ao lado do executável:

```ini
[Config]
CaminhoDados=\\servidor\Interno\TI\
```

- Na ausência do arquivo, usa pasta `data\` ao lado do executável
- Interface de configuração: janela simples com campo de texto (sem browser de pasta)
- Ao salvar, tenta carregar o JSON automaticamente

---

## Arquivo de configuração de projeto (referência)

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net8.0-windows</TargetFramework>
    <UseWPF>true</UseWPF>
    <UseWindowsForms>true</UseWindowsForms>  <!-- para ColorDialog -->
    <Nullable>enable</Nullable>
    <ImplicitUsings>disable</ImplicitUsings>
    <StartupObject>MapaMaquinas.App</StartupObject>
    <PlatformTarget>x86</PlatformTarget>
  </PropertyGroup>
</Project>
```

---

## Regras de negócio

1. **Hostname é obrigatório** para cadastrar uma máquina — é usado como ID
2. **Setor é obrigatório** para cadastrar uma máquina — define a cor do card
3. **Cor do card vem exclusivamente do setor** — o campo `cor` da máquina existe mas não é usado
4. **Setor com máquinas vinculadas não pode ser excluído** — deve reatribuir as máquinas primeiro
5. **Número da porta de switch deve ser único** por empresa
6. **Posição é salva no modelo ao soltar o drag** — persiste no JSON apenas ao salvar
7. **Salvar é explícito** — o sistema não salva automaticamente
8. **Ao fechar com alterações não salvas**, exibe confirmação (Sim / Não / Cancelar)
9. **Ping não bloqueia a UI** — todo processamento ocorre em background tasks
10. **Ao trocar de empresa**, cancela o ciclo de ping em andamento e inicia um novo

---

## Atalhos de teclado obrigatórios

| Atalho | Ação |
|---|---|
| `Ctrl+S` | Salvar |
| `Ctrl+O` | Abrir arquivo |
| `Insert` | Nova máquina |
| `Ctrl+Scroll` | Zoom no ponto do cursor |
| `Ctrl++` / `Ctrl+-` | Zoom in/out |
| `Ctrl+0` | Resetar zoom para 100% |
| Duplo clique no card | Ver detalhes |

---

## Comportamentos de UX importantes

- **Tooltip do card** deve exibir: hostname, resultado do ping por hostname, IP, resultado do ping por IP, tipo, porta switch, CPU, RAM, storage, ramal. Deve permanecer visível por pelo menos 60 segundos.
- **Busca** aplica highlight piscante (borda colorida alternando) nos cards encontrados e rola o viewport até o primeiro resultado.
- **Drag and drop** usa captura do mouse (`SetCapture` / `CaptureMouse`) e clamp nos limites do canvas.
- **Zoom** deve ser centrado no ponto do cursor, não no centro do canvas. Após o zoom, ajustar o scroll para manter o ponto visualmente estável.
- **Nova máquina** sempre aparece em uma posição visível (ex: 20, 20), não em 0, 0.
- **PopularAbas** deve desconectar o evento de seleção durante a montagem para evitar disparo prematuro, e carregar o mapa da primeira empresa manualmente após reconectar.
- **Exportar PNG** deve renderizar o Canvas inteiro, incluindo partes fora da viewport visível.

---

## O que NÃO implementar

- Autenticação / login
- Banco de dados (tudo em JSON)
- Sincronização em tempo real entre múltiplos usuários
- Notificações / alertas sonoros
- Histórico de alterações
- Relatórios em PDF ou Excel
- Integração com AD / LDAP
- Descoberta automática de máquinas na rede
