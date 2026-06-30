# Guia do Desenvolvedor — MapaMaquinas

Documentação técnica para quem vai manter, estender ou depurar este projeto.

---

## Stack

- **.NET 8**, WPF (`net8.0-windows`)
- Plataforma alvo: `x86` (compatível com ambientes corporativos legados)
- Sem MVVM/bindings declarativos — toda a UI é construída por código (`new Grid()`, `new Button()`, etc), sem XAML. Manter esse padrão ao adicionar telas novas.
- Dois pacotes NuGet (únicos do projeto): driver de SQL Server e acesso ao Registro do Windows — ambos necessários porque o .NET Core/5+/8 não os inclui por padrão como o .NET Framework incluía.

---

## Arquitetura geral

```
App.cs
  └─ JanelaLogin (ShowDialog, bloqueia até autenticar)
       ├─ falhou/cancelado → Application.Shutdown()
       └─ sucesso → MainWindow (janela principal)
```

### Por que `ShutdownMode` importa aqui

O WPF tem `ShutdownMode.OnLastWindowClose` como padrão. Como `JanelaLogin` é a primeira janela aberta via `ShowDialog()`, fechá-la — mesmo com sucesso — dispara esse comportamento e encerra o `Application` antes do `MainWindow` sequer existir.

A solução em `App.cs`:

```csharp
app.ShutdownMode = ShutdownMode.OnExplicitShutdown;  // enquanto login está em andamento
// ... login ...
app.ShutdownMode = ShutdownMode.OnMainWindowClose;   // só depois que MainWindow existe
app.MainWindow = main;
```

Se alguém remover esse trecho achando redundante, o app vai fechar sozinho assim que o login for bem-sucedido. É a causa mais provável caso isso volte a acontecer.

---

## Login e autenticação

### Fluxo

```
JanelaLogin.TentarLogin()
  └─ AuthService.Autenticar(codigo, senha)
       ├─ ConexaoConfig.Carregar()        → connection string (lança se não configurada)
       ├─ CifraNumerica.Encode(senha)     → senha no formato armazenado no banco
       └─ SELECT ... WITH (NOLOCK)        → compara o valor já cifrado
```

A comparação é feita sobre o valor **cifrado**, nunca decifrando o que vem do banco — minimiza a janela de exposição da senha em memória.

### `CifraNumerica` — não é criptografia

É um algoritmo determinístico e reversível, mantido apenas para compatibilidade com o formato de armazenamento de um sistema anterior. Qualquer um com acesso ao código-fonte consegue decifrar trivialmente. **Não adicione lógica de segurança que dependa desta cifra ser secreta.** A segurança do login depende inteiramente de:

1. Controle de acesso ao banco de dados (quem pode ler a tabela de usuários)
2. A connection string estar protegida (ver `ConexaoConfig` abaixo)

Se um dia for necessário aumentar a segurança real, a mudança correta é trocar esse esquema por hash com salt (ex: PBKDF2/bcrypt) — o que exigiria migração da coluna de senha no banco, fora do escopo deste código.

### `ConexaoConfig` — armazenamento da connection string

```csharp
ProtectedData.Protect(bytes, entropia, DataProtectionScope.CurrentUser)
```

- Usa DPAPI do Windows, escopo `CurrentUser` — só o mesmo usuário do Windows, na mesma máquina, decifra.
- O blob resultante é gravado em `HKEY_CURRENT_USER\Software\<chave do produto>`, **nunca em arquivo**. Isso é proposital: qualquer arquivo na árvore do projeto é candidato a ser commitado por engano; uma chave de Registro fora da pasta do projeto não.
- `Salvar()`, `Carregar()`, `Existe()` e `Remover()` são os únicos pontos de entrada — não acesse o Registro diretamente fora desta classe.

Se o app for movido para outra máquina ou outro usuário do Windows logar, `Carregar()` lança `InvalidOperationException` ao tentar decifrar — isso é esperado e tratado em `AuthService` como `ResultadoLogin.ConexaoNaoConfigurada`/`ErroConexao`. Não tente "corrigir" isso fazendo fallback silencioso; force reconfiguração explícita via `JanelaConfigConexao`.

### `JanelaConfigConexao`

Monta a connection string a partir dos campos da UI, **testa antes de permitir salvar** (`AuthService.TestarConexao`), e só grava via `ConexaoConfig.Salvar()` após o teste ter sucesso. Não pré-preenche campos com dados de uma conexão já salva — evita reexibir credenciais na tela.

---

## Ciclo de ping — arquitetura

Esta é a parte mais sutil do código e a que mais gerou bugs durante o desenvolvimento. Documentando com cuidado para não reintroduzi-los.

### O problema original

A primeira implementação criava os cards de ping junto com os cards visuais do canvas, recriando-os a cada troca de aba. Isso causava três sintomas:
1. O ciclo de ping reiniciava ao trocar de empresa, perdendo o timer de 2 minutos.
2. O contador de máquinas na legenda zerava ao trocar de aba (porque iterava sobre os cards do canvas, que tinham acabado de ser recriados com status "Aguardando").
3. O texto do timer "Próximo ping em X:XX" sumia, porque dividia a string da barra de status com `_lblStatus.Text.Split('|')` — e `CarregarMapa` sobrescrevia esse texto ao trocar de empresa.

### A solução — separação total entre canvas e ciclo de ping

```
MainWindow
  ├─ _cards          → List<CardMaquina>           (visual, recriado a cada troca de aba)
  └─ _cardsPing      → Dictionary<string, CardMaquina>  (permanente, chave = Maquina.Id)
```

`_cardsPing` é populado **uma única vez**, em `PopularAbas()`, com um `CardMaquina` para **cada máquina de todas as empresas** — não apenas da aba visível. `PingQueue.IniciarGlobal()` também é chamado uma única vez ali, recebendo todos esses cards.

Quando o usuário troca de aba, `CarregarMapa()` cria novos objetos `CardMaquina` para exibição no canvas, mas em vez de pingá-los diretamente, eles **espelham** o resultado do `cardPing` correspondente:

```csharp
if (_cardsPing.TryGetValue(m.Id, out var cardPing))
{
    card.AtualizarResultadoPing(cardPing.UltimoResultado);  // estado já calculado
    cardPing.OnResultadoAtualizado = resultado =>
        Dispatcher.Invoke(() => card.AtualizarResultadoPing(resultado));  // atualizações futuras
}
```

`LimparCards()` (chamado ao trocar de aba) desconecta esse callback (`cp.OnResultadoAtualizado = null`) para não acumular referências penduradas em cards de canvas já descartados — sem isso, há vazamento de memória sutil ao longo de muitas trocas de aba.

### Status bar — dois labels, nunca um só

```
_lblStatus       (esquerda)  → nome da empresa, contagem de máquinas/portas
_lblPingStatus   (direita)   → timer/progresso do ping
```

Cada um só é escrito pelo seu próprio conjunto de métodos (`AtualizarStatus()` vs. `OnPingProgresso`/`OnContagemRegressiva`). **Nunca volte a fazer um deles depender do texto do outro via `Split` ou concatenação** — foi exatamente isso que causou o bug original.

### `PingQueue` — contagem regressiva segundo a segundo

Em vez de um único `Task.Delay(TimeSpan.FromMinutes(2))`, o loop conta de trás para frente disparando um evento a cada segundo:

```csharp
for (int s = totalSegundos; s > 0; s--)
{
    if (token.IsCancellationRequested) return;
    _dispatcher.Invoke(() => ContagemRegressiva?.Invoke(s));
    await Task.Delay(1000, token);
}
```

Isso é o que permite o label `⏱ Próximo ping em 01:47` atualizar visualmente. O `CancellationToken` só é cancelado por `Parar()` (chamado ao fechar o app ou abrir um novo arquivo JSON) — nenhuma ação de UI do dia a dia deve cancelar este token.

---

## Pacotes NuGet e por que existem

| Pacote | Motivo |
|---|---|
| Driver de SQL Server | `System.Data.SqlClient` não vem embutido no .NET 8 como vinha no Framework — precisa ser referenciado |
| Acesso ao Registro do Windows | Idem — `Microsoft.Win32.Registry` é pacote separado no .NET Core/5+/8 |

DPAPI em si (`System.Security.Cryptography.ProtectedData`) **já vem embutido** no .NET 8 para Windows e não precisa de pacote adicional.

---

## Build e compilação via linha de comando

Se o ambiente usa Delphi/IDE community que bloqueia compilação via `dcc32` em linha de comando (não se aplica a este projeto C#, mas documentando porque já foi confundido): este projeto é .NET puro, `dotnet build`/`dotnet publish` funcionam sem restrição de edição/licença.

```powershell
dotnet restore
dotnet build MapaMaquinas.csproj -c Release
dotnet publish MapaMaquinas.csproj -c Release -r win-x86 --self-contained -p:PublishSingleFile=true
```

---

## Armadilhas conhecidas / coisas para não fazer

- **Não crie um segundo `PingQueue.cs`** em outra pasta do projeto (ex: raiz vs. `Services/`). Já aconteceu — gera erro de ambiguidade de tipo (`CS0101`/duplicação de membro) que confunde bastante porque a mensagem de erro não deixa óbvio que são dois arquivos físicos diferentes definindo a mesma classe. Sempre confirme com uma busca recursiva pelo nome do arquivo antes de adicionar uma unit nova.
- **Não centralize inputs de texto/senha por padrão** — UX do projeto é preenchimento da esquerda para a direita (`HorizontalContentAlignment = Left`, sem `TextAlignment.Center`).
- **Não faça a contagem regressiva do ping depender de qualquer estado do canvas** (empresa selecionada, cards visíveis, etc). Ela deve ser cega a isso por design.
- **Não documente credenciais reais** (servidor, usuário, senha de banco) em nenhum arquivo do repositório, incluindo comentários de código ou exemplos em Markdown. A configuração é 100% feita pela UI em tempo de execução, por ambiente.
