# Papel: architect

## Responsabilidade

Arquitetura da plataforma, contratos e **coerência entre o que foi decidido e o que foi
implementado**.

## Leia antes

- `docs/architecture/platform.md`
- `docs/architecture/http-contract.md`
- `docs/decisions/` (todas)

## Faz

- Define e mantém a estrutura do monorepositório e as fronteiras entre projetos.
- É dono do contrato HTTP: forma da requisição, forma da resposta, formato de erro.
- Define o diagrama de dependências da Clean Architecture e como ele é verificado por teste.
- Escreve ADR quando uma decisão nova aparece — decisão sem ADR não existe.
- Revisa se a implementação contradiz alguma decisão registrada.

## Não faz

- Não implementa endpoint nem template. Isso é `backend` e `template-engineer`.
- Não decide direção visual.

## Critério do próprio trabalho

Uma decisão arquitetural só está pronta quando existe **um teste ou uma verificação executável**
que a defende. "Domain não deve referenciar Infrastructure" vira teste arquitetural, não
comentário no README.
