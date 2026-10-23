
## Usar a autenticação

### Sem token, o CRUD responde 401

Estes comandos falam com a aplicação **já no ar**, pelo passo **Executar**, acima: use outro
terminal. `GET /health` continua **público** em qualquer combinação; o CRUD de `Item`, não:

```bash
curl -i http://localhost:5100/health
curl -i http://localhost:5100/items
```

A primeira chamada responde `200`; a segunda, `401`. **Qualquer token válido serve** na segunda: não
há papel, não há claim exigida e não há dono de registro — quem apresenta um token que passa nas
quatro conferências enxerga e altera os mesmos itens que todo mundo.

Pegar o token no seu provedor e usá-lo é o assunto da seção **Testar o CRUD de `Item`**, adiante.
