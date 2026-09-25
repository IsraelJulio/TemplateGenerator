
        // Todo o CRUD exige um token válido (RF-14): sem o cabeçalho `Authorization`, a resposta é
        // 401. Com um token que passe nas conferências de assinatura, emissor, audiência e
        // validade, o acesso é o mesmo para qualquer usuário, sem regra de proprietário e sem
        // exigência de papel ou claim (RF-15). `GET /health` não está neste grupo e continua
        // público em qualquer combinação (RF-12).
        items.RequireAuthorization();
