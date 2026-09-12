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

  it('marca o 501 como estado transitório do projeto, não erro da pessoa', () => {
    const failure = describeGenerationFailure(501, {
      title: 'Geração ainda não implementada',
      status: 501,
      detail: 'A configuração é válida, mas o motor de geração ainda não existe.',
    });

    expect(failure.notImplemented).toBe(true);
    expect(failure.detail).toContain('motor de geração');
    expect(failure.fieldErrors).toEqual({});
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
