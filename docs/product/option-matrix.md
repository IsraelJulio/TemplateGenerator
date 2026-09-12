# Matriz de opções

## Campos

| Campo | Chave | Valores | Padrão |
|---|---|---|---|
| Nome do projeto | `projectName` | texto válido como projeto e namespace C# | — (obrigatório) |
| Arquitetura | `architecture` | `simple`, `clean` | `simple` |
| Banco | `database` | `none`, `sqlite`, `postgresql` | `none` |
| Autenticação | `authentication` | `none`, `identity`, `jwt` | `none` |
| Swagger | `swagger` | `true`, `false` | `true` |
| Versão .NET | `dotnetVersion` | `net10.0` | `net10.0` |

`dotnetVersion` tem um valor só no MVP, mas o catálogo é modelado como lista para permitir
expansão sem quebrar o contrato. A tela mostra o campo habilitado, com um item.

## Regra de compatibilidade

**`authentication = identity` exige `database ∈ {sqlite, postgresql}`.**

Motivo: o ASP.NET Core Identity precisa de um `UserStore` persistente. Um Identity sobre
armazenamento volátil perderia os usuários a cada reinício, contrariando o critério de
"comportamento após reinício".

Não há outras restrições. `jwt` funciona com qualquer banco, inclusive `none`, porque a validação
do token não depende de persistência.

## Contagem

```
2 (arquitetura) × 3 (banco) × 3 (autenticação) × 2 (swagger) = 36 combinações brutas
− 2 (arquitetura) × 2 (swagger) × 1 (database=none ∧ authentication=identity) = 4 inválidas
= 32 combinações válidas
```

O número 32 aparece na definição de pronto e na estratégia de testes. **Se esta fórmula mudar,
atualize os dois.**

## Validação do nome do projeto

`projectName` precisa satisfazer, simultaneamente:

- Ser um identificador C# válido por segmento, segmentos separados por `.`
  (ex.: `Acme.Billing.Api`).
- Cada segmento: começa com letra **ASCII** (`A-Z`, `a-z`) ou `_`, seguido de letras ASCII,
  dígitos ou `_`.
- Não colidir com palavra reservada do C# em nenhum segmento.
- Entre 1 e 100 caracteres no total.
- Não conter `/`, `\`, `..`, caractere de controle, nem nome reservado do Windows
  (`CON`, `PRN`, `AUX`, `NUL`, `COM1`–`COM9`, `LPT1`–`LPT9`).

### Por que ASCII e não Unicode

O C# aceita `Cotação.Api` como identificador, e a interface do produto é em português — a
tentação de permitir acentos é real. Foi **decidido restringir a ASCII** mesmo assim.

O nome vira duas coisas fora do C#: **diretório dentro do ZIP** e **nome de arquivo no
`Content-Disposition`**. Unicode nos dois exige a flag UTF-8 nas entradas do ZIP e codificação
RFC 5987 no header, e cada um é uma chance a mais de extração errada em ferramenta antiga, além
de um detalhe a mais no determinismo do [ADR-0003](../decisions/adr-0003-zip-deterministico.md).

A mensagem de erro precisa dizer o que fazer, não só recusar: *"Use apenas letras sem acento,
dígitos e `_`."*

### Espaço em branco

O servidor **apara espaços nas pontas** antes de validar, igual ao frontend. Duas validações com
comportamentos diferentes para a mesma entrada produzem divergência entre quem usa a tela e quem
chama a API direto — e essa divergência é mais cara que a rigidez.

A mesma regra vale no frontend (feedback imediato) e no backend (fonte de verdade). O backend
rejeita com `ProblemDetails` mesmo que o frontend tenha deixado passar.

## Representação no catálogo

`GET /api/template-options` devolve esta matriz — valores, rótulos em português, padrões e
restrições — para que o frontend nunca precise codificar a regra de compatibilidade. Ver
[`../architecture/http-contract.md`](../architecture/http-contract.md).
