# Requisitos

Identificadores `RF-n` (funcional) e `RNF-n` (não funcional) são citados pelos critérios de
aceite em `docs/backlog.json`. Não renumere.

## Funcionais — plataforma geradora

| ID | Requisito |
|---|---|
| RF-01 | A tela apresenta os seis campos da matriz de opções com os padrões aplicados. |
| RF-02 | O catálogo de opções vem do backend; o frontend não codifica valores nem regras. |
| RF-03 | Combinações inválidas são impedidas na tela, com explicação do motivo. |
| RF-04 | O resumo lateral mostra arquitetura, funcionalidades e estrutura prevista do projeto. |
| RF-05 | O envio gera e baixa um ZIP, com estado visível de "gerando". |
| RF-06 | Erro de validação preserva as escolhas e exibe mensagem compreensível em português. |
| RF-07 | Envio duplicado não gera dois downloads. |
| RF-08 | Não há cadastro, login nem histórico. |

## Funcionais — projeto gerado

| ID | Requisito |
|---|---|
| RF-10 | O ZIP contém solução, fontes, testes, configurações, exemplos HTTP e README. |
| RF-11 | O projeto compila com `dotnet build` sem editar código-fonte. |
| RF-12 | Existe um endpoint de saúde **público** em qualquer combinação. |
| RF-13 | Existe CRUD de `Item` (identificador + `title` obrigatório) completo. |
| RF-14 | Sem autenticação, o CRUD é público. Com autenticação, exige token; sem token responde `401`. |
| RF-15 | Com autenticação, os dados são compartilhados entre usuários — sem regra de proprietário. Token válido acessa qualquer `Item`. |
| RF-16 | `database = none`: armazenamento em memória, volátil, perdido no reinício. |
| RF-17 | `database ≠ none`: EF Core com migração inicial; dados sobrevivem ao reinício. |
| RF-18 | `authentication = identity`: endpoints nativos de cadastro, login bearer e renovação funcionam. Não exige segredo JWT. |
| RF-19 | `authentication = jwt`: valida `Authority` e `Audience` configurados; rejeita assinatura, emissor, audiência e validade incorretos. |
| RF-20 | `swagger = true`: documento OpenAPI e UI disponíveis, habilitados por padrão em Development. `swagger = false`: ausentes, e sem a dependência no projeto. |
| RF-21 | O README descreve os comandos exatos daquela combinação: instalação, configuração, migração, execução, autenticação e teste do CRUD. |
| RF-22 | O ZIP contém um manifesto com versão do template e opções escolhidas. |

## Não funcionais

| ID | Requisito |
|---|---|
| RNF-01 | A geração **não** executa comandos, não restaura pacotes e não compila durante a requisição. |
| RNF-02 | A saída é determinística: mesma configuração + mesma versão de template ⇒ mesmo ZIP byte a byte. Ver [ADR-0003](../decisions/adr-0003-zip-deterministico.md). |
| RNF-03 | Caminhos e nomes são validados; *path traversal*, colisão de arquivos e substituição insegura são impossíveis. |
| RNF-04 | Downloads concorrentes são isolados; há limite de requisições e de gerações simultâneas. |
| RNF-05 | Nenhum segredo real em arquivo versionado ou gerado. |
| RNF-06 | Dependências fixadas por versão exata e verificadas. |
| RNF-07 | Nenhuma chamada a modelo de IA no caminho de geração. |
| RNF-08 | Nenhuma dependência de container. Ver [ADR-0005](../decisions/adr-0005-sem-containers.md). |
| RNF-09 | Todas as dependências gratuitas e com licença permissiva; licenças de terceiros registradas em [`../THIRD-PARTY.md`](../THIRD-PARTY.md). |
| RNF-10 | A tela funciona com teclado, tem contraste adequado e se adapta a celular e desktop. |
