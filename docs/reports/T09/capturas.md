# T09: capturas de tela

Tiradas em 2026-10-23 contra a API .NET real (perfil `http`, porta 5080) e o `ng serve` com proxy
(porta 4200), no Chromium do Playwright 1.63.0. Desktop: 1440×900. Celular: Pixel 7 (412×915,
DPR 2,625). O script que as gerou (`capturas.mjs`) não está no repositório: ele não afirma nada, só
produz imagens, então não é teste. O comportamento de cada estado está afirmado pelos specs de
`src/web/e2e/`.

| Arquivo | Estado | O que se vê |
|---|---|---|
| `01-inicial-desktop.png`, `01-inicial-desktop-pagina-inteira.png` | 1, inicial | Duas colunas, formulário à esquerda e resumo à direita. Padrões do catálogo marcados (Simples, Nenhum, Nenhuma, Swagger ligado, .NET 10). O resumo mostra "Sem nome ainda" e a árvore com `SeuProjeto/`. |
| `01-inicial-celular.png`, `01-inicial-celular-pagina-inteira.png` | 1, inicial | Seções empilhadas, formulário primeiro e resumo depois. Gutter lateral preservado, sem rolagem horizontal. |
| `03-opcao-incompativel-desktop.png` / `-celular.png` | 3 | Em Autenticação, "Identity" aparece desabilitado, com fundo `--paper` e rótulo em `--ink-soft`. Logo abaixo, em vermelho: "Indisponível agora: O Identity nativo precisa de um banco para persistir os usuários.", que é a mensagem da restrição do catálogo. |
| `04-gerando-desktop.png` / `-celular.png` | 5 | Botão "Gerando…" em cinza (`--ink-soft`, cursor `progress`) e a linha "Montando o pacote. O botão fica bloqueado até terminar, para não gerar dois downloads." A requisição real estava retida por `page.route` e foi solta logo depois. |
| `05-erro-servidor-no-campo-desktop.png` / `-celular.png` (e `-pagina-inteira`) | 2 e 6 | `400` real do servidor: o nome foi trocado no corpo para `Acme.class.Api`. A mensagem do servidor, "O trecho 'class' é uma palavra reservada do C#.", aparece inline sob o campo, que tem a borda em `--danger`. As escolhas não padrão continuam marcadas: Clean, PostgreSQL, JWT e Swagger desligado, e o resumo também as mostra. No fim do formulário aparece o aviso "Configuração inválida / Nenhuma escolha foi perdida." |
| `05b-erro-local-desktop.png` | 2 | Validação local ao sair do campo com "Acme Billing": a mensagem em português aparece sob o campo. |
| `06-download-concluido-desktop.png` / `-celular.png` | 7 | Aviso verde "Download concluído. Acme.Billing.Api.zip foi salvo pelo navegador." acima do botão, que volta a dizer "Gerar projeto". |
| `07-catalogo-indisponivel-desktop.png` / `-celular.png` | 8 | Capturado com a API **parada**. "A API do gerador não respondeu.", com a explicação, as duas sondas (`GET /api/health` e `GET /api/template-options`, ambas HTTP 502 do proxy) e o botão "Tentar de novo". A tela não fica vazia. |
| `08-foco-teclado-radio-*.png` | foco | Só teclado (Tab até o grupo, seta para baixo): "Clean Architecture" selecionada, com o contorno de foco de 2 px em `--accent` e respiro em volta do rádio. |
| `08-foco-teclado-botao-*.png` | foco | Tab até o botão: anel de 2 px em `--accent` com 2 px de respiro em volta de "Gerar projeto". |

## Defeitos vistos nas capturas

- **Corrigido em T09:** a linha sob o botão dizia "com os arquivos listados ao lado". No celular,
  o resumo fica embaixo. Agora diz "listados no resumo". Algumas capturas da primeira rodada
  mostravam a frase antiga e foram refeitas.
- **Não corrigido:** o placeholder `Acme.Billing.Api` do campo vazio, em `--ink-soft`, parece um
  valor preenchido (`01-inicial-*`). Só o "Sem nome ainda" do resumo mostra que o campo está vazio.
- **Não corrigido, aceitável:** o círculo do rádio desabilitado (`03-*`) é muito claro. WCAG isenta
  controle desabilitado, e o motivo em texto está visível.
- **Artefato do teste, não da tela:** em `05-*` o campo mostra `Acme.Billing.Api` e a mensagem fala
  de 'class'. É o nome que o teste trocou no corpo para provocar o `400` real.
