# MapaMaquinas — Guia de Uso

## O que é

MapaMaquinas é uma ferramenta de monitoramento visual de ativos de rede. Ela exibe as máquinas do ambiente sobre uma planta do local, com status de conectividade em tempo real via ping. É usada pela equipe de TI para identificar rapidamente quais máquinas estão online, offline ou com problema de cadastro.

---

## Primeira execução

Na primeira vez que abrir o sistema, configure o caminho dos dados:

1. Vá em **Arquivo → Configurar caminho...**
2. Digite o caminho da pasta onde está o arquivo `mapa_maquinas.json`
   - Caminho local: `C:\TI\dados\`
   - Caminho de rede: `\\servidor\Interno\TI\`
3. Clique em **Salvar** — o sistema carregará o arquivo automaticamente

O caminho é salvo em `config.ini` ao lado do executável e lembrado nas próximas execuções.

---

## Interface geral

```
┌─ Menu ──────────────────────────────────────────────────────────┐
├─ Toolbar ───────────────────────────────────────────────────────┤
│ [Lateral]    │  [Mapa — área principal]                         │
│              │                                                   │
│  Empresas    │   ┌──┬──────────────┐  ┌──┬──────────────┐      │
│  ─────────   │   │██│ PC-FIN01     │  │██│ PC-RH01      │      │
│  BigCard     │   │██│ 192.168.0.10 │  │██│ 192.168.0.11 │      │
│              │   └──┴──────────────┘  └──┴──────────────┘      │
│  STATUS      │                                                   │
│  Legenda     │                                                   │
├─ Status bar ────────────────────────────────────────────────────┤
```

---

## Cards de máquina

```
┌──┬──────────────────────┐
│▓▓│ PC-FIN01             │  ← hostname
│▓▓│ 192.168.0.10   P.5  │  ← IP + porta do switch
│▓▓│ 201                  │  ← ramal (opcional)
└──┴──────────────────────┘
 ↑
 Barra lateral dividida:
   metade superior = ping pelo hostname
   metade inferior = ping pelo IP
```

### Cores da barra lateral

| Cor | Significado |
|---|---|
| Verde | Ping respondeu |
| Vermelho | Sem resposta |
| Amarelo | Aguardando verificação |
| Cinza | Sem dado cadastrado |

### Diagnósticos pela combinação das metades

| Superior (hostname) | Inferior (IP) | Diagnóstico |
|---|---|---|
| Verde | Verde | Tudo OK |
| Verde | Vermelho | IP errado no cadastro |
| Vermelho | Verde | Problema no hostname / DNS |
| Vermelho | Vermelho | Máquina offline ou inacessível |

### Interações com o card

| Ação | Resultado |
|---|---|
| Arrastar | Reposiciona no mapa |
| Duplo clique | Abre tela de detalhes |
| Clique direito | Menu de contexto |

**Menu de contexto:**
- **Ver detalhes** — todas as informações cadastradas
- **Editar máquina** — abre o formulário de edição
- **Remover máquina** — remove com confirmação
- **Verificar agora** — ping imediato fora do ciclo

---

## Ping automático

- Até **5 máquinas** são pingadas simultaneamente
- Após verificar todas, aguarda **2 minutos** antes de repetir
- Progresso na barra de status: `Ping: verificando 12/30...`
- Para forçar verificação individual: menu de contexto → **Verificar agora**

> Máquinas com firewall bloqueando ICMP aparecerão como offline mesmo acessíveis. Limitação do protocolo, não do sistema.

---

## Máquinas

### Adicionar
Toolbar **+ Máquina** ou `Insert`

Campos:
- **Hostname** *(obrigatório)* — nome da máquina na rede
- **IP** *(recomendado)* — endereço IP para ping independente
- **Processador / RAM / Storage** — hardware
- **Porta Switch** — porta de switch onde está conectada
- **Ramal** — telefone (opcional)
- **Setor** *(obrigatório)* — define a cor do card
- **Tipo** — Desktop / Notebook / Mac / Servidor / Impressora
- **Observações** — campo livre

### Editar
Clique direito no card → **Editar máquina**

### Remover
Clique direito no card → **Remover máquina** → confirmar

---

## Setores

Os setores definem a cor de fundo dos cards.

1. Toolbar **⚙ Setores** ou **Máquinas → Gerenciar setores**
2. Selecione um setor para editar ou clique em **Novo setor**
3. Defina nome e cor (hex ou seletor `...`)
4. **Salvar setor** → **Fechar** para aplicar no mapa

> Não é possível excluir setor com máquinas vinculadas. Reatribua as máquinas antes.

---

## Portas de switch

Cards cinzas escuros que documentam a localização física dos switches.

Toolbar **+ Porta** → preencha número, descrição, localização → arraste para posição.

---

## Zoom

| Ação | Resultado |
|---|---|
| `Ctrl + Scroll` | Zoom no ponto do cursor |
| `Ctrl + +` / `Ctrl + −` | +/− 10% |
| `Ctrl + 0` | Reseta para 100% |
| Botões na toolbar | Mesmas funções |

Range: 25% a 300%.

---

## Busca

Digite na caixa da toolbar → `Enter` ou 🔍

Busca por: hostname, IP, ramal ou porta de switch. Cards encontrados piscam com borda vermelha. Clique `✕` para limpar.

---

## Salvar

O sistema **não salva automaticamente**. Use:
- `Ctrl + S`
- Botão 💾 na toolbar

Ao fechar com alterações não salvas, o sistema pergunta se deseja salvar.

---

## Exportar PNG

**Arquivo → Exportar PNG...** — gera imagem do mapa atual.

---

## Atalhos

| Atalho | Ação |
|---|---|
| `Ctrl + S` | Salvar |
| `Ctrl + O` | Abrir arquivo |
| `Insert` | Nova máquina |
| `Ctrl + Scroll` | Zoom |
| `Ctrl + +` / `−` | Zoom in/out |
| `Ctrl + 0` | Resetar zoom |

---

## Dicas rápidas

- Salve após reposicionar vários cards — o posicionamento só é persistido ao salvar
- Verde/Vermelho: hostname ok, IP errado → verifique o IP cadastrado
- Vermelho/Verde: IP ok, hostname falhou → verifique o DNS interno
- A legenda no painel lateral é referência rápida sempre visível
