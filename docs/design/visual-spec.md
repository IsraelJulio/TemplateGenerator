# Especificação visual

Esta é a fonte da direção visual. O papel `frontend` usa a skill `frontend-design` **junto com**
este documento — a skill dá método, este documento dá as decisões deste produto.

## Direção

Fundo off-white, texto grafite, um acento verde profundo, divisórias discretas e espaçamento
generoso. A tela deve parecer uma ferramenta calma e decidida, não um formulário corporativo nem
uma landing page.

Os valores abaixo estão fixados. São eles que vivem como tokens CSS em `src/web/src/styles.css`;
esta tabela é a decisão, o CSS é a cópia executável dela.

| Papel | Token | Valor | Uso |
|---|---|---|---|
| Fundo | `--paper` | `#F2F4F1` | superfície da página — off-white de leve viés verde |
| Texto | `--ink` | `#1A211E` | corpo e títulos |
| Acento | `--accent` | `#0E4429` | ação primária, seleção, foco |
| Divisória | `--rule` | `#D7DED8` | separação entre blocos |

Oito tokens de apoio, derivados dos quatro papéis e não papéis novos — os doze juntos são
exatamente os que `styles.css` define: `--panel` `#FFFFFF` (superfície dos dois blocos sobre a
página), `--ink-soft` `#4C5852` (texto secundário), `--accent-soft` `#E4EEE7` (realce da opção
escolhida), `--accent-strong` `#0A3320` (acento sob o ponteiro), `--on-accent` `#FFFFFF` (texto
sobre o acento, no botão primário), `--rule-strong` `#788880` (borda de controle e guia da árvore,
onde a divisória decorativa não basta) e o par de erro `--danger` `#8C1D18` / `--danger-soft`
`#FBEDEC`.

**Contraste mínimo AA para texto e para o indicador de foco.** Isso não se afirma de olho: cada
par efetivamente usado é recalculado a partir deste `styles.css` em
`src/web/src/app/core/design/contrast.spec.ts`. O pior par de texto fica em **6,26:1** (mínimo
4,5:1) e o pior elemento de interface em **3,14:1** (mínimo 3:1). `--rule` é a única cor abaixo do
limiar de 3:1, e deliberadamente: é a linha de baixo relevo que desenha a régua do cabeçalho, o
contorno dos painéis, a separação entre campos, a caixa das opções, a linha entre uma opção e a
seguinte e o topo da faixa de ações. Em nenhum desses lugares ela é a única pista do agrupamento —
o agrupamento vem do `<fieldset>` com sua `<legend>`, do título de cada bloco e da ordem da tela;
se a linha sumisse, nenhuma informação se perderia. São três os pares decorativos isentos, e são
todos os que existem: `--rule` sobre `--panel` **1,37:1**, sobre `--paper` **1,24:1** e sobre
`--accent-soft` **1,15:1** (a separação entre opções quando a linha está selecionada). Onde a
borda **é** informação — controle de texto, guia da árvore — quem entra é `--rule-strong`, que
passa os 3:1 nas três superfícies. Os três pares isentos estão declarados também em
`contrast.spec.ts`, que falha se algum deles deixar de precisar da isenção.

O indicador de foco é um só na tela inteira: contorno de 2 px em `--accent`, com 2 px de respiro.

## Tipografia

- **Newsreader** — títulos, rótulos de campo e o nome do projeto no resumo.
- **Public Sans** — controles, corpo, resumo e árvore de estrutura.

Ambas fixadas em **variável, peso 400–700, subset latino, `.woff2`**, com `font-display: swap` e
pré-carga das duas (as duas aparecem acima da dobra). **Hospedadas localmente** (sem CDN), com as
licenças acompanhando os arquivos e registradas em [`../THIRD-PARTY.md`](../THIRD-PARTY.md).

Toda declaração de fonte carrega uma pilha de fallback real:

- `--font-display`: `'Newsreader', 'Iowan Old Style', 'Palatino Linotype', Palatino, Georgia,
  'Times New Roman', serif`
- `--font-text`: `'Public Sans', 'Segoe UI', system-ui, -apple-system, 'Helvetica Neue', Arial,
  sans-serif`

Corpo em 16 px com entrelinha 1,55. A escala vive em tokens (`--size-hint` 13 px a `--size-title`
40 px); o título da página encolhe por `clamp` até 32 px no celular.

## Layout

- **Desktop:** duas colunas — formulário à esquerda, resumo da configuração à direita.
- **Celular:** seções empilhadas, formulário primeiro, resumo depois.
- Gutter lateral mínimo preservado em qualquer largura; a página nunca rola na horizontal.

## Resumo lateral

Mostra, sempre atualizado: arquitetura escolhida, funcionalidades ativas e **estrutura prevista do
projeto** (a árvore de pastas que o ZIP vai conter). É o que transforma a escolha abstrata em algo
conferível antes do download.

## Estados a implementar

Todos os oito, não só o caminho feliz:

1. Inicial, com padrões aplicados.
2. Validação inline, no campo que falhou, em português.
3. Opção incompatível — desabilitada **com explicação visível do motivo**, nunca só apagada.
4. Explicação de cada opção, acessível sem sair do fluxo.
5. Geração em andamento, com o botão bloqueado contra duplo envio (RF-07).
6. Falha na geração, preservando todas as escolhas (RF-06).
7. Download concluído.
8. Catálogo indisponível — a tela precisa dizer isso, não ficar vazia.

## Implementação

Componentes standalone, Reactive Forms, CSS próprio. Sem biblioteca de componentes de terceiros.

## Verificação

Em navegador, **com conteúdo real**, em desktop e celular, com navegação por teclado. Captura de
tela no relatório da tarefa. Inspeção visual não é opcional nem substituível por teste unitário.
