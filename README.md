# MapaMaquinas — C# WPF

Ferramenta de mapeamento visual de ativos de rede (máquinas, switches, setores) para equipes de TI.

Permite posicionar cards de máquinas sobre a planta do ambiente, com suporte a drag-and-drop, ping em tempo real, zoom e gerenciamento de setores. Acesso protegido por tela de login autenticada contra banco de dados.

---

## Funcionalidades

- **Login obrigatório** — autenticação contra banco de dados antes de abrir o sistema
- **Mapa interativo** — cards arrastáveis sobre a planta do ambiente
- **Ping em tempo real** — indicador visual de status por máquina (online/offline/aguardando), com ciclo global e timer visível na barra de status
- **Zoom** — Ctrl + scroll, botões na toolbar ou Ctrl+0 para resetar
- **Busca em tempo real** — localiza máquinas por hostname, IP, ramal ou porta de switch conforme você digita, com highlight piscante
- **Múltiplas empresas/unidades** — abas separadas por empresa
- **Gerenciamento de setores** — CRUD com color picker
- **Exportação PNG** — captura o mapa atual como imagem
- **Persistência JSON** — dados salvos em arquivo local ou de rede
- **Bandeja do sistema** — minimiza com o ícone do próprio app

---

## Requisitos

- .NET 8 SDK (Windows)
- Visual Studio 2022+ **ou** `dotnet CLI`
- Um banco de dados relacional acessível para autenticação (ver [Login e autenticação](#login-e-autenticação))

---

## Build & execução

```powershell
# Restaurar pacotes
dotnet restore

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

> Diferente do .NET Framework, o .NET 8 não inclui o driver de banco de dados nem acesso ao Registro do Windows por padrão — ambos são restaurados via NuGet automaticamente no build.

---

## Estrutura de arquivos

```
MapaMaquinas/
├── MapaMaquinas.csproj
├── App.cs                         ← Ponto de entrada — abre login antes do MainWindow
├── MainWindow.cs                  ← Janela principal (mapa, toolbar, menu, status bar)
│
├── Models/
│   └── Models.cs                  ← Setor, Maquina, PortaSwitch, Empresa, Repositorio
│
├── Services/
│   ├── JsonManager.cs             ← Carrega/salva o arquivo de dados
│   ├── Config.cs                  ← Lê/grava config.ini (caminho dos dados)
│   ├── PingService.cs             ← Ping individual com CancellationToken
│   ├── PingQueue.cs               ← Ciclo de ping global com timer visual
│   ├── AuthService.cs             ← Autenticação contra o banco de dados
│   ├── ConexaoConfig.cs           ← Connection string criptografada localmente
│   ├── CifraNumerica.cs           ← Cifra reversível usada no formato de senha legado
│   └── UndoManager.cs             ← Pilha de desfazer para movimentação de cards
│
├── Controls/
│   ├── CardMaquina.cs             ← Card drag-and-drop com indicador de ping
│   └── CardPorta.cs               ← Card de porta de switch
│
└── Views/
    ├── JanelaLogin.cs             ← Tela de login
    ├── JanelaConfigConexao.cs     ← Utilitário para configurar a conexão com o banco
    ├── JanelaEdicaoMaquina.cs     ← Formulário de criação/edição de máquina
    ├── JanelaEdicaoPorta.cs       ← Formulário de criação/edição de porta
    ├── JanelaVisualizacao.cs      ← Tela de detalhes (read-only)
    ├── JanelaListaMaquinas.cs     ← Lista tabular de todas as máquinas
    └── JanelaSetores.cs           ← Gerenciador de setores
```

---

## Login e autenticação

O sistema exige login antes de abrir o mapa.

| Campo | Formato |
|---|---|
| Usuário | Código numérico de tamanho fixo |
| Senha | Numérica, armazenada cifrada no banco |

A senha não é comparada em texto puro nem com hash criptográfico — usa uma cifra numérica reversível, compatível com o formato de um sistema legado pré-existente. O algoritmo está isolado em `Services/CifraNumerica.cs` e não deve ser tratado como mecanismo de segurança por si só: a proteção real do login vem do controle de acesso ao banco de dados e da forma como a conexão é armazenada (ver abaixo), não da cifra em si.

A consulta de login usa hint de leitura sem bloqueio, para não disputar locks com outras operações no banco.

### Conexão protegida — sem credenciais em arquivo

A connection string do banco **nunca é gravada em arquivo dentro do projeto**. Ela é criptografada localmente (vinculada ao usuário do Windows atual) e armazenada fora da árvore de arquivos do repositório, em um local protegido pelo próprio sistema operacional.

Por quê:
- Nada sensível pode ser commitado por acidente no Git — não existe arquivo de credenciais para versionar.
- A criptografia usada garante que só o mesmo usuário do Windows, na mesma máquina, consegue decifrar o valor salvo.
- Reinstalar o Windows, trocar de usuário ou copiar o projeto para outra máquina invalida a conexão salva — comportamento esperado, força reconfiguração local.

### Configurando a conexão pela primeira vez

Na tela de login, clique em **"Configurar conexão..."**. A janela permite:

1. Informar servidor e nome do banco de dados
2. Escolher autenticação integrada do Windows ou usuário/senha do banco
3. **Testar a conexão** antes de salvar
4. Salvar apenas após o teste ter sucesso

Nenhum dado de servidor, usuário ou senha de produção fica documentado neste README — a configuração é feita inteiramente pela interface, por quem for implantar o sistema em cada ambiente.

---

## Configuração de dados (mapa, JSON)

Na primeira execução, use **Arquivo → Configurar caminho...** e informe a pasta onde está o arquivo `mapa_maquinas.json`. Pode ser um caminho local ou de rede. O caminho é salvo em `config.ini` ao lado do executável.

> Esta configuração é independente da conexão de login — uma aponta para a pasta de dados do mapa (JSON + plantas), a outra para o banco de autenticação.

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
          "ip": "0.0.0.0",
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

## Indicador de status (barra lateral)

Cada card exibe uma barra colorida na lateral esquerda indicando o estado da máquina na rede. O IP é salvo no JSON junto com o hostname — a verificação usa os dois, de forma **independente e paralela**.

A barra lateral é **dividida em duas metades**:
- Metade **superior** → resultado do ping pelo **hostname**
- Metade **inferior** → resultado do ping pelo **IP**

| Cor | Significado |
|---|---|
| 🟢 Verde | Ping respondeu |
| 🔴 Vermelho | Sem resposta |
| 🟡 Amarelo | Aguardando verificação |
| ⬜ Cinza | Sem dado cadastrado |

**Combinações:**

| Barra | Diagnóstico |
|---|---|
| 🟢 cima + 🟢 baixo | Tudo OK — hostname e IP respondem |
| 🟢 cima + 🔴 baixo | IP errado no cadastro |
| 🔴 cima + 🟢 baixo | Hostname com problema (DNS/nome errado) |
| 🔴 cima + 🔴 baixo | Máquina offline ou ambos com problema |

> **Obs:** máquinas com firewall bloqueando ICMP aparecerão como offline mesmo acessíveis. Limitação do protocolo ICMP, não do sistema.

---

## Ciclo de ping global e timer visual

O ping roda em um **ciclo único e global**, independente de qual empresa/aba está aberta no mapa:

- Ao carregar o arquivo de dados, o sistema cria internamente um card de ping para **cada máquina de todas as empresas**, não apenas da aba visível.
- O ciclo pinga várias máquinas em paralelo (limite configurável) e, ao concluir todas, aguarda um intervalo fixo antes de reiniciar.
- **Trocar de aba, editar uma máquina, salvar ou qualquer outra ação não afeta o ciclo** — ele continua contando e pingando em segundo plano sem reiniciar.
- A contagem regressiva até o próximo ciclo aparece em tempo real na barra de status, no canto direito, mudando de cor conforme o tempo restante diminui.

O canto esquerdo da barra de status mostra a empresa atual e sua contagem de máquinas/portas — esses dois campos são completamente independentes e nunca se sobrescrevem, mesmo trocando de aba durante a contagem.

É possível forçar uma verificação imediata de uma máquina específica pelo menu de contexto do card ("Verificar agora"), sem interferir no ciclo global.

---

## Licença

MIT
