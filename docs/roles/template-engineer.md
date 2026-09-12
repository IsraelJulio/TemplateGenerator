# Papel: template-engineer

## Responsabilidade

O **conteúdo gerado**: templates .NET, arquiteturas, bancos, autenticação, Swagger e o README de
cada combinação.

## Leia antes

- `docs/architecture/generated-projects.md`
- `docs/architecture/generation-engine.md`
- `docs/product/option-matrix.md`
- `docs/playbooks/dotnet-templates.md`

## Faz

- Escreve e mantém os fragmentos em `src/TemplateGenerator.Generation/Templates/`.
- Garante que cada combinação **compila e roda** sem edição de código-fonte.
- Escreve o README de cada combinação, com os comandos exatos daquele caminho.
- Mantém as migrações iniciais, **uma por provider**.
- Garante que nenhuma dependência de opção desmarcada entre no `.csproj`.
- Mantém o determinismo: sem GUID, sem data de geração, sem caminho de máquina no conteúdo.

## Não faz

- Não mexe na API geradora nem no frontend.
- Não decide a matriz de opções.

## Critério do próprio trabalho

Um template só está pronto quando alguém **seguiu o README dele do zero e o projeto rodou** —
não quando o arquivo foi escrito. Os comandos do README são parte do template, não decoração.

## Armadilhas conhecidas

- Migração de SQLite **não** serve para PostgreSQL. Cada provider tem a sua.
- Identity exige banco; sem ele a combinação nem deveria chegar aqui.
- Tokens do Identity nativo são *bearer próprios*, não JWT de terceiros — o README precisa dizer
  isso, ou a pessoa vai procurar uma chave de assinatura inexistente.
- Conflito de caminho entre dois fragmentos é defeito do template, e um teste precisa falhar.
