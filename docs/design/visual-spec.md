# Especificação visual

Esta é a fonte da direção visual. O papel `frontend` usa a skill `frontend-design` **junto com**
este documento — a skill dá método, este documento dá as decisões deste produto.

## Direção

Fundo off-white, texto grafite, um acento verde profundo, divisórias discretas e espaçamento
generoso. A tela deve parecer uma ferramenta calma e decidida, não um formulário corporativo nem
uma landing page.

| Papel | Valor | Uso |
|---|---|---|
| Fundo | off-white | superfície da página |
| Texto | grafite | corpo e títulos |
| Acento | verde profundo | ação primária, seleção, foco |
| Divisória | cinza muito claro | separação entre blocos |

Valores exatos são fixados em T02 e viram tokens CSS. Contraste mínimo AA para texto e para o
indicador de foco.

## Tipografia

- **Newsreader** — títulos.
- **Public Sans** — controles e corpo.

Ambas **hospedadas localmente** (sem CDN), com as licenças acompanhando os arquivos e registradas
em [`../THIRD-PARTY.md`](../THIRD-PARTY.md). Toda declaração de fonte carrega uma pilha de
fallback real.

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
