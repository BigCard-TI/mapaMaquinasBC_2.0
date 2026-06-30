# Guia de Uso — MapaMaquinas

Manual para quem usa o sistema no dia a dia.

---

## Abrindo o sistema

Ao executar o programa, a primeira tela exibida é a de **login**.

```
┌─────────────────────────────┐
│      Mapa de Máquinas        │
│                               │
│  Usuário (4 dígitos)         │
│  [____]                      │
│                               │
│  Senha                       │
│  [______]                    │
│                               │
│         [ Entrar ]           │
│                               │
│   Configurar conexão...      │
└─────────────────────────────┘
```

Digite seu código de usuário (4 dígitos) e sua senha, e clique em **Entrar** ou pressione `Enter`.

### "Conexão não configurada"

Se esta mensagem aparecer, é a primeira vez que o sistema roda nesta máquina, ou a configuração foi perdida. Clique em **"Configurar conexão..."** e siga as instruções na tela — normalmente o suporte de TI já vai ter as informações necessárias (servidor e banco de dados).

### Senha ou usuário incorretos

O sistema avisa especificamente qual campo está errado. Tente novamente; se persistir, contate o suporte de TI para confirmar seu cadastro.

---

## Configurando a pasta de dados

Na primeira abertura (após o login), se aparecer um aviso sobre "caminho de dados não configurado", vá em:

```
Arquivo → Configurar caminho...
```

Informe a pasta onde estão os arquivos do mapa (pode ser uma pasta de rede compartilhada). O sistema lembra esse caminho nas próximas vezes.

---

## Navegando pelo mapa

- **Abas no topo** — cada empresa/unidade tem sua própria aba com seu próprio mapa.
- **Arrastar um card** — clique e segure sobre o card de uma máquina para movê-la pela planta.
- **Zoom** — use `Ctrl + scroll do mouse`, os botões `+`/`−` na barra de ferramentas, ou `Ctrl + 0` para voltar a 100%.
- **Duplo clique em um card** — abre a tela de detalhes da máquina.
- **Clique direito em um card** — menu de contexto com opções de editar, remover e verificar ping imediatamente.

---

## Buscando uma máquina

Digite no campo de busca da barra de ferramentas. A busca acontece **em tempo real**, conforme você digita — não é necessário pressionar Enter. Apagar o texto remove o destaque automaticamente.

A busca encontra máquinas por hostname, IP, ramal ou porta de switch.

---

## Indicador de status na lateral do card

Cada card tem uma pequena barra colorida do lado esquerdo, dividida em duas metades:

```
┌──┬──────────────────────┐
│🟢│ PC-FIN01              │  ← metade de cima: ping pelo nome
│🟢│ 192.168.x.x    P.5   │  ← metade de baixo: ping pelo IP
└──┴──────────────────────┘
```

| Cor | Significado |
|---|---|
| 🟢 Verde | Respondeu ao ping |
| 🔴 Vermelho | Não respondeu |
| 🟡 Amarelo | Aguardando a próxima verificação |
| ⬜ Cinza | Sem IP ou hostname cadastrado |

Se as duas metades estiverem verdes, a máquina está plenamente acessível. Se uma estiver vermelha e a outra verde, geralmente indica um problema de cadastro (IP errado, ou nome não resolvendo via DNS) — não necessariamente que a máquina está offline.

> Firewalls que bloqueiam ping (ICMP) fazem a máquina aparecer como offline mesmo estando ligada e acessível por outros meios. Isso é uma limitação do protocolo, não um erro do sistema.

---

## Timer de verificação (barra inferior)

No rodapé da tela, no canto direito, aparece a contagem regressiva até a próxima rodada de verificação de todas as máquinas:

```
⏱ Próximo ping em 01:47
```

Essa contagem **não para** quando você troca de empresa, edita uma máquina ou salva — ela continua rodando em segundo plano o tempo todo. Durante a verificação ativa, o texto muda para `🔄 Verificando X/Y...`.

Para verificar uma máquina específica imediatamente, sem esperar o ciclo, clique com o botão direito sobre o card e escolha **"Verificar agora"**.

---

## Cadastrando uma nova máquina

```
Máquinas → Nova máquina...   (ou tecla Insert)
```

Preencha os campos disponíveis. Hostname é obrigatório e não pode se repetir dentro da mesma empresa. Selecione o setor ao qual a máquina pertence — isso define a cor padrão do card.

---

## Gerenciando setores

```
Máquinas → Gerenciar setores...
```

Permite criar, editar (nome e cor) e excluir setores. Um setor não pode ser excluído enquanto houver máquinas vinculadas a ele — reatribua as máquinas a outro setor primeiro.

---

## Exportando o mapa como imagem

```
Arquivo → Exportar PNG...
```

Gera uma imagem PNG do mapa exatamente como está sendo exibido na tela no momento, incluindo a posição de todos os cards.

---

## Salvando alterações

```
Arquivo → Salvar     (ou Ctrl + S)
```

Salva a posição de todos os cards e qualquer alteração de cadastro no arquivo de dados. O sistema indica visualmente quando há alterações não salvas.

---

## Minimizando para a bandeja

Ao minimizar a janela, o sistema continua rodando na bandeja do sistema (perto do relógio do Windows), mantendo o ciclo de verificação ativo. Clique no ícone para restaurar a janela.

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
| `Ctrl + Z` | Desfazer último movimento de card |
