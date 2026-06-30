# Template de Recriação — MapaMaquinas

Especificação funcional completa para recriar este sistema do zero usando um assistente de IA, sem depender do código-fonte original. Nenhum dado de produção, nome de empresa, servidor ou credencial está incluído — tudo aqui é estrutural.

---

## Objetivo do sistema

Aplicação desktop Windows para mapeamento visual de ativos de rede (computadores, switches) sobre a planta física de um ou mais ambientes/unidades, com monitoramento de conectividade em tempo real.

---

## Stack obrigatória

- C# / .NET 8, WPF (`net8.0-windows`)
- UI construída inteiramente por código (sem XAML/`.xaml`)
- Plataforma alvo `x86`
- Persistência principal: arquivo JSON local ou de rede
- Autenticação: banco de dados relacional externo (agnóstico ao motor específico na especificação — implementação de referência usa SQL Server)

---

## Tela 1 — Login

Primeira tela exibida, antes de qualquer outra janela. Bloqueia o restante do app até autenticação bem-sucedida ou cancelamento.

### Campos
- **Usuário**: campo numérico, tamanho fixo (4 dígitos no projeto de referência — ajustável)
- **Senha**: campo numérico, mascarado (`PasswordBox`), tamanho máximo definido (6 dígitos no projeto de referência)

### Comportamento
- `Enter` no campo de usuário avança o foco para senha
- `Enter` no campo de senha tenta autenticar
- Botão "Entrar" como `IsDefault`
- Mensagens de erro específicas por causa: usuário não encontrado, senha incorreta, conexão não configurada, erro de conexão
- Inputs **alinhados à esquerda** (não centralizados) — preenchimento natural de texto
- Link/botão secundário "Configurar conexão..." abre a Tela 2

### Armadilha crítica de inicialização (WPF)

Se a tela de login for a primeira janela do `Application` e for fechada via `DialogResult = true` (sucesso), o WPF por padrão entende isso como "última janela fechada" e encerra todo o processo **antes** da janela principal abrir — mesmo que o código tente abrir a próxima janela em seguida.

Solução: definir `Application.ShutdownMode = ShutdownMode.OnExplicitShutdown` antes de exibir o login, e só trocar para `ShutdownMode.OnMainWindowClose` (associando `Application.MainWindow`) depois que a janela principal for de fato criada.

---

## Tela 2 — Configurar conexão

Utilitário para configurar a conexão com o banco de autenticação, acessível a partir da tela de login.

### Campos
- Servidor
- Nome do banco de dados
- Tipo de autenticação: integrada do sistema operacional OU usuário/senha do banco (radio buttons mutuamente exclusivos)
- Usuário do banco (habilitado apenas se autenticação por usuário/senha)
- Senha do banco (idem, mascarada)

### Comportamento
- Botão "Testar conexão" — só prossegue se a conexão **e** o acesso à tabela/estrutura de autenticação esperada forem confirmados
- Botão "Salvar" **desabilitado até o teste ter sucesso**
- Ao salvar, a connection string resultante é criptografada e persistida de forma vinculada ao usuário do sistema operacional atual — nunca em arquivo de texto, nunca versionável
- Não pré-preencher campos com uma conexão já salva (evita reexibir segredos em tela)

### Mecanismo de armazenamento seguro recomendado (Windows)

DPAPI (`System.Security.Cryptography.ProtectedData`) com `DataProtectionScope.CurrentUser`, persistindo o blob resultante em um local fora da árvore de arquivos do projeto — por exemplo, uma chave própria no Registro do Windows (`HKEY_CURRENT_USER`). Vantagens: nenhuma chave de criptografia para gerenciar/distribuir, nenhum arquivo sensível para acidentalmente versionar, e a decifragem só funciona na mesma máquina/usuário que gravou.

---

## Lógica de autenticação

1. Carregar a connection string protegida; se ausente, falhar com mensagem específica direcionando para a Tela 2.
2. Transformar a senha digitada no mesmo formato armazenado no banco (ver "Cifra de senha legada" abaixo, se aplicável ao seu caso de migração).
3. Consultar o banco pelo código do usuário, comparando o valor já transformado — evitar decifrar o valor vindo do banco quando possível.
4. Usar hint de leitura sem bloqueio na consulta (ex: `WITH (NOLOCK)` em SQL Server), por ser uma única leitura pontual de baixa criticidade transacional.

### Cifra de senha legada (opcional, apenas se precisar compatibilidade com sistema anterior)

Algoritmo de referência, reversível, não criptograficamente seguro — usado apenas para compatibilidade de formato com uma base de dados pré-existente, não como mecanismo de segurança:

```python
def encode(numero: str) -> str:
    d = numero.zfill(6)
    return ''.join(chr(50 + 2*i + int(d[5-i])) for i in range(6))

def decode(cifrado: str) -> str:
    digits = [ord(cifrado[i]) - 50 - 2*i for i in range(6)]
    return ''.join(str(d) for d in reversed(digits))
```

Se você está construindo do zero sem precisar dessa compatibilidade, **prefira hash com salt** (ex: PBKDF2, bcrypt, Argon2) em vez deste esquema.

---

## Tela 3 — Janela principal (mapa)

### Estrutura geral
- Menu superior (Arquivo, Máquinas, Portas)
- Barra de ferramentas (abrir, salvar, nova máquina, nova porta, setores, busca, lista, desfazer, filtro, zoom, tamanho de card)
- Abas — uma por empresa/unidade cadastrada
- Dentro de cada aba: imagem de fundo (planta do ambiente) com cards posicionáveis sobre ela, dentro de um `ScrollViewer`
- Painel lateral de legenda (opcional, mas recomendado) com contadores por status
- Barra de status inferior com **dois campos independentes**: nome da empresa atual à esquerda, status do ciclo de verificação à direita — nunca compartilhando a mesma string

### Cards de máquina
- Retangulares, arrastáveis (drag and drop livre dentro do canvas)
- Cor de fundo conforme o setor ao qual pertencem
- Barra lateral esquerda dividida em duas metades indicando dois resultados de verificação independentes (ex: por nome e por endereço IP)
- Duplo clique abre detalhes; clique direito abre menu de contexto (editar, remover, verificar agora)

### Cards de porta (opcional)
- Representação visual simplificada de portas de switch, também arrastáveis, com tooltip de detalhes

---

## Sistema de verificação de conectividade (ping)

Este é o componente mais sensível a bugs de estado. Especificação detalhada:

### Requisito funcional
- Verificar todas as máquinas cadastradas, em **todas as empresas/abas**, em um ciclo contínuo.
- Limitar concorrência (ex: 5 verificações simultâneas) para não sobrecarregar a rede.
- Ao concluir uma rodada completa, aguardar um intervalo fixo (ex: 2 minutos) antes de reiniciar.
- **O ciclo e seu temporizador devem ser completamente independentes da navegação do usuário** — trocar de aba, editar um cadastro, salvar, ou qualquer outra interação não deve reiniciar, pausar ou de qualquer forma afetar o ciclo em andamento.
- Exibir contagem regressiva visível e atualizada a cada segundo até a próxima rodada.
- Permitir verificação avulsa e imediata de uma única máquina, sem interferir no ciclo geral.

### Padrão de implementação recomendado

Separar completamente o **estado de verificação** (que deve ser global e persistente) da **representação visual no canvas** (que é recriada toda vez que o usuário troca de aba):

```
Estado de verificação:
  - Um objeto de card "lógico" por máquina, criado UMA ÚNICA VEZ ao carregar os dados,
    para TODAS as empresas de uma vez (não apenas a aba visível no momento).
  - Indexado por identificador único da máquina (ex: dicionário chave=ID).
  - O ciclo de verificação opera exclusivamente sobre esta coleção.

Representação visual:
  - Cards recriados a cada troca de aba, apenas para exibição.
  - Ao serem criados, "espelham" o estado atual do card lógico correspondente
    (consultando o resultado já calculado).
  - Registram um callback no card lógico para receber atualizações futuras
    em tempo real, repassando-as ao card visual.
  - Ao trocar de aba novamente, esse callback é explicitamente removido
    (não apenas descartando a referência) para evitar acúmulo de
    delegates pendurados em objetos visuais já descartados.
```

Esse desenho evita três classes de bug observadas na implementação de referência:
1. Reinício do timer ao trocar de aba (porque o ciclo nunca é recriado, só populado uma vez)
2. Zeragem de contadores de status ao trocar de aba (porque os contadores leem do estado lógico permanente, não dos cards visuais voláteis)
3. Sumiço do texto do timer ao trocar de aba (porque o label do timer é fisicamente separado do label de nome da empresa — nunca a mesma `string` sendo dividida/recomposta)

### Contagem regressiva — implementação

Em vez de um único delay de duração total, decompor em um loop de 1 segundo por iteração, disparando um evento a cada tick com o tempo restante. Isso é o que viabiliza a exibição visual da contagem regressiva (`MM:SS`) — um delay único não permite atualização incremental da UI.

```
para s de total_segundos até 1, decrescendo:
    se cancelado: encerrar
    disparar evento(tempo_restante = s)
    aguardar 1 segundo
```

O cancelamento deste loop deve ocorrer **apenas** ao encerrar a aplicação ou carregar um novo conjunto de dados — nunca por uma ação de navegação do usuário.

---

## Indicador visual de status — duas fontes independentes

Cada máquina deve ser verificada por **dois métodos independentes e em paralelo** (ex: nome de rede e endereço IP), sem que um dependa do resultado do outro. O indicador visual reflete os dois resultados lado a lado ou empilhados, permitindo diagnosticar rapidamente se o problema é de cadastro (um responde, outro não) ou de fato a máquina está inacessível (nenhum responde).

Estados possíveis por método: respondeu / não respondeu / aguardando verificação / sem dado cadastrado para verificar.

---

## Funcionalidades de suporte

- **Busca em tempo real**: filtra e destaca visualmente os cards correspondentes conforme o usuário digita, sem exigir tecla de confirmação. Campo vazio remove o destaque imediatamente.
- **Desfazer (undo)**: pilha limitada (ex: 50 entradas) de últimas posições de cards movidos, restaurável.
- **Exportação de imagem**: captura o canvas atual (incluindo posições dos cards) como arquivo de imagem.
- **Bandeja do sistema**: minimizar para a bandeja mantém o processo e o ciclo de verificação ativos; o ícone usado deve ser o ícone da própria aplicação, não um ícone genérico do sistema operacional.
- **Persistência de configuração de caminho de dados**: arquivo de configuração local (ex: `.ini`) separado e independente da configuração de conexão de autenticação.

---

## Modelo de dados (arquivo de persistência principal)

Estrutura JSON hierárquica: repositório → lista de empresas/unidades → cada uma com lista de setores, lista de máquinas e lista de portas. Cada máquina referencia um setor por ID e armazena sua posição (coordenadas X/Y) dentro do canvas daquela empresa.

```
Repositorio
  └─ Empresa[]
       ├─ Setor[]        (id, nome, cor)
       ├─ Maquina[]      (id, hostname, ip, setor_id, posição x/y, atributos técnicos livres)
       └─ Porta[]        (id, identificador, descrição, posição x/y)
```

---

## Princípios de segurança a preservar em qualquer recriação

1. Nenhuma credencial de banco de dados em arquivo versionável — sempre criptografada e armazenada fora da árvore do repositório.
2. A cifra de senha (se herdada de sistema legado) nunca deve ser tratada como controle de segurança suficiente por si só.
3. Consultas de autenticação devem usar hints de leitura sem bloqueio quando o motor de banco suportar, por serem operações de leitura simples e frequentes.
4. Nenhum dado real de ambiente (servidor, nome de empresa, IPs de produção) deve constar em documentação, comentários de código ou exemplos — toda configuração específica de ambiente é feita em tempo de execução pela interface.
