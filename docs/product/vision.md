# Visão do produto

## Problema

Começar um projeto .NET novo com arquitetura, banco e autenticação decididos custa horas de
montagem repetitiva. `dotnet new webapi` entrega pouco; os scaffolds completos entregam demais e
com opinião difícil de remover.

## Proposta

Uma tela única onde a pessoa escolhe seis coisas e baixa um ZIP com um projeto .NET que
**compila e roda**, com um CRUD de exemplo funcionando e um README que explica exatamente os
comandos daquela combinação.

## Critério de sucesso do MVP

> Uma pessoa que nunca viu o projeto consegue abrir o gerador seguindo a documentação, escolher
> uma combinação, baixar o ZIP e executar o projeto realizando **somente** as configurações e
> comandos indicados no README — sem editar código-fonte.

## Escopo do MVP

**Está dentro:**

- Tela de configuração com seis campos e resumo ao lado.
- Geração determinística do ZIP para as 32 combinações válidas.
- Backend como fonte de verdade do catálogo de opções.
- Execução local reproduzível da plataforma, documentada.

**Está fora (etapa posterior):**

- Hospedagem pública da plataforma.
- Cadastro, login ou histórico de projetos gerados.
- Persistência de qualquer dado da plataforma (o gerador é *stateless*).
- Qualquer chamada a modelo de IA durante a geração.

## Premissas

- Interface e documentação em **português**; identificadores de código em **inglês**.
- O ZIP contém **apenas** o backend .NET. Angular pertence à plataforma geradora.
- Todas as ferramentas, bibliotecas e recursos usados são **gratuitos**.
- Nenhuma dependência de container. Ver
  [`../decisions/adr-0005-sem-containers.md`](../decisions/adr-0005-sem-containers.md).

## Público

Pessoa desenvolvedora .NET que quer um ponto de partida limpo e sabe o que quer, não alguém
aprendendo .NET do zero. O README explica os comandos, não os conceitos.
