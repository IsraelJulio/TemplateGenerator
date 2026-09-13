import { describeGenerationFailure } from './generation-failure';

describe('falha na geração', () => {
  it('endereça a mensagem do 400 ao campo que a API citou', () => {
    const failure = describeGenerationFailure(400, {
      title: 'Configuração inválida',
      status: 400,
      errors: { authentication: ['O Identity nativo precisa de um banco.'] },
    });

    expect(failure.title).toBe('Configuração inválida');
    expect(failure.fieldErrors['authentication']).toEqual([
      'O Identity nativo precisa de um banco.',
    ]);
    expect(failure.notImplemented).toBe(false);
  });

  it('endereça o 501 ao campo indisponível, como faz com o 400', () => {
    // ADR-0012: o `501` passou a carregar `errors`, e sem `detail`. A tela já
    // sabe posicionar `errors` inline — é o mesmo caminho de código do `400`.
    const failure = describeGenerationFailure(501, {
      title: 'Esta combinação ainda não gera projeto',
      status: 501,
      errors: { database: ['O template desta opção ainda não foi escrito.'] },
    });

    expect(failure.notImplemented).toBe(true);
    expect(failure.title).toBe('Esta combinação ainda não gera projeto');
    expect(failure.fieldErrors['database']).toEqual([
      'O template desta opção ainda não foi escrito.',
    ]);
    expect(failure.detail).toBeNull();
  });

  it('não inventa prosa no 501 quando a API não mandou nenhuma', () => {
    // O escopo do título mudou em T04: não é mais "o motor de geração ainda não
    // existe", é esta combinação. O texto de reserva precisa dizer a segunda
    // coisa, porque é ela que continua verdadeira.
    const failure = describeGenerationFailure(501, null);

    expect(failure.notImplemented).toBe(true);
    expect(failure.title).toBe('Esta combinação ainda não gera projeto');
    expect(failure.detail).toBeNull();
  });

  it('lê o Retry-After do 429 e o transforma em instrução', () => {
    expect(describeGenerationFailure(429, { title: 'Muitas requisições' }, '30').detail).toBe(
      'Tente de novo em 30 segundos.',
    );
    expect(describeGenerationFailure(429, null, '1').detail).toBe('Tente de novo em 1 segundo.');
    expect(describeGenerationFailure(429, null, 'quarta-feira').retryAfterSeconds).toBeNull();
  });

  it('diz que nada respondeu quando a chamada nem chegou', () => {
    const failure = describeGenerationFailure(0, null);

    expect(failure.title).toContain('não respondeu');
    expect(failure.notImplemented).toBe(false);
  });

  it('não inventa título quando a API não mandou ProblemDetails', () => {
    expect(describeGenerationFailure(502, null).title).toContain('502');
  });
});
