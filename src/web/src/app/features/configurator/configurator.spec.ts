import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { vi } from 'vitest';
import { TemplateConstraint } from '../../core/catalog/template-options.model';
import {
  CATALOG_FIXTURE,
  IMPLEMENTED_CATALOG_FIXTURE,
  SNAPSHOT_CATALOG,
  UNAVAILABLE_REASON,
} from '../../testing/catalog.fixture';
import { Configurator } from './configurator';

/*
 * Rótulos, descrições e mensagens são lidos do snapshot do catálogo, nunca
 * escritos aqui: um spec que afirmasse uma frase copiada continuaria verde com
 * a tela mostrando outra coisa, se a cópia e o payload divergissem.
 */

/** O primeiro par `(campo, valor)` de um lado da restrição. */
function firstMatch(side: TemplateConstraint['when']): { field: string; value: string } {
  const [field, accepted] = Object.entries(side)[0];
  const [value] = accepted as readonly string[];
  return { field, value };
}

/** A restrição do catálogo, o valor que a dispara e um valor que a satisfaz. */
const [CONSTRAINT] = SNAPSHOT_CATALOG.constraints;
const TRIGGER = firstMatch(CONSTRAINT.when);
const SATISFIER = firstMatch(CONSTRAINT.requires);

/** Todo texto de explicação que o catálogo manda: de campo e de opção. */
const DESCRIPTIONS = Object.values(SNAPSHOT_CATALOG.fields).flatMap((field) => [
  ...(field.description ? [field.description] : []),
  ...(field.values ?? []).flatMap((option) => (option.description ? [option.description] : [])),
]);

describe('Configurator', () => {
  let fixture: ComponentFixture<Configurator>;
  let http: HttpTestingController;

  function element(): HTMLElement {
    return fixture.nativeElement as HTMLElement;
  }

  function radio(field: string, value: string): HTMLInputElement {
    const found = element().querySelector<HTMLInputElement>(
      `input[type="radio"][data-field="${field}"][data-value="${value}"]`,
    );

    if (!found) {
      throw new Error(`Opção ${field}=${value} não foi renderizada.`);
    }
    return found;
  }

  async function settle(): Promise<void> {
    await fixture.whenStable();
    fixture.detectChanges();
  }

  beforeEach(async () => {
    TestBed.configureTestingModule({
      imports: [Configurator],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });

    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(Configurator);
    fixture.detectChanges();
  });

  afterEach(() => http.verify());

  /**
   * Entrega o catálogo. O padrão é o que a API manda hoje, com `unavailable`
   * cheio; `IMPLEMENTED_CATALOG_FIXTURE` é o mesmo catálogo com tudo
   * implementado, e serve aos testes que precisam mexer numa opção que hoje não
   * tem template — sem ele, o clique cairia num controle desabilitado e o teste
   * passaria a afirmar outra coisa.
   */
  async function serveCatalog(catalog = CATALOG_FIXTURE): Promise<void> {
    http.expectOne('/api/template-options').flush(catalog);
    await settle();
  }

  /** Preenche um nome válido e entrega a requisição de geração pendente. */
  async function submitWith(name = 'Acme.Billing.Api') {
    const input = element().querySelector<HTMLInputElement>('input[type="text"]')!;
    input.value = name;
    input.dispatchEvent(new Event('input'));
    await settle();

    element().querySelector('form')?.dispatchEvent(new Event('submit'));
    await settle();

    return http.expectOne('/api/templates');
  }

  it('mostra que está carregando antes de o catálogo chegar', () => {
    expect(element().textContent).toContain('Carregando as opções');
    http.expectOne('/api/template-options').flush(CATALOG_FIXTURE);
  });

  it('monta um bloco por campo do catálogo, com os rótulos em português', async () => {
    await serveCatalog();

    const legends = Array.from(element().querySelectorAll('legend')).map((node) =>
      node.textContent?.trim(),
    );

    expect(legends).toEqual(Object.values(SNAPSHOT_CATALOG.fields).map((field) => field.label));
    expect(element().querySelector('input[type="text"]')).not.toBeNull();
  });

  it('aplica os padrões do catálogo aos controles', async () => {
    await serveCatalog();

    for (const [key, field] of Object.entries(SNAPSHOT_CATALOG.fields)) {
      if (field.type === 'boolean') {
        const toggle = element().querySelector<HTMLInputElement>(`input[data-field="${key}"]`);
        expect(toggle?.checked, key).toBe(field.default);
      } else {
        expect(radio(key, String(field.default)).checked, key).toBe(true);
      }
    }
  });

  it('desabilita a opção que a restrição do catálogo impede, com o motivo visível', async () => {
    await serveCatalog(IMPLEMENTED_CATALOG_FIXTURE);

    const recusado = radio(TRIGGER.field, TRIGGER.value);
    expect(recusado.disabled).toBe(true);
    expect(element().textContent).toContain(CONSTRAINT.message);
  });

  it('liga o motivo ao controle desabilitado por aria-describedby', async () => {
    await serveCatalog(IMPLEMENTED_CATALOG_FIXTURE);

    const id = radio(TRIGGER.field, TRIGGER.value).getAttribute('aria-describedby');

    expect(id).not.toBeNull();
    expect(element().querySelector(`[id="${id}"]`)?.textContent).toContain(CONSTRAINT.message);
  });

  it('libera a opção quando a seleção passa a satisfazer a restrição', async () => {
    await serveCatalog(IMPLEMENTED_CATALOG_FIXTURE);

    radio(SATISFIER.field, SATISFIER.value).click();
    await settle();

    expect(radio(TRIGGER.field, TRIGGER.value).disabled).toBe(false);
  });

  it('mostra a explicação de cada opção sem tirar a pessoa do fluxo', async () => {
    await serveCatalog();

    const form = element().querySelector('form')!.textContent ?? '';
    expect(DESCRIPTIONS.length).toBeGreaterThan(0);
    for (const description of DESCRIPTIONS) {
      expect(form).toContain(description);
    }
  });

  it('não renderiza explicação vazia na opção que o catálogo manda sem description', async () => {
    // Desde T10 o contrato exige `description` e o snapshot cumpre; a tolerância
    // do cliente (regra 8) é provada tirando-a de um valor de cada campo de escolha.
    const catalog = JSON.parse(JSON.stringify(CATALOG_FIXTURE)) as typeof CATALOG_FIXTURE;
    const semDescricao = Object.entries(catalog.fields).flatMap(([key, field]) => {
      const option = field.values?.[0];
      if (!option) return [];
      delete (option as { description?: string }).description;
      return [{ key, option }];
    });
    expect(semDescricao.length, 'nenhum campo de escolha no catálogo').toBeGreaterThan(0);

    await serveCatalog(catalog);

    for (const { key, option } of semDescricao) {
      const label = radio(key, option.value).closest('label')!;
      expect(label.querySelector('.option__label')?.textContent?.trim()).toBe(option.label);
      expect(label.querySelector('.option__description')).toBeNull();
    }
    expect(element().textContent).not.toContain('undefined');

    // O resumo também: a linha do campo mostra o rótulo do valor e nenhuma
    // explicação inventada.
    const resumo = element().querySelector('tg-config-summary')!;
    expect(resumo.textContent).not.toContain('undefined');
  });

  it('diz que o catálogo está indisponível em vez de mostrar tela vazia', async () => {
    http
      .expectOne('/api/template-options')
      .flush('', { status: 503, statusText: 'Service Unavailable' });
    http.expectOne('/api/health').flush('{"status":"ok"}');
    await settle();

    // A frase inteira: "A API está no ar" sozinha casaria também com o título
    // de `route-missing`, que é um diagnóstico diferente deste 503.
    expect(element().querySelector('[role="alert"]')?.textContent).toContain(
      'A API está no ar, mas falhou ao montar o catálogo.',
    );

    element().querySelector('button')?.click();
    http.expectOne('/api/template-options').flush(CATALOG_FIXTURE);
    await settle();
  });

  it('distingue API fora do ar de rota ausente consultando /api/health', async () => {
    // Catálogo em 404 e health sem resposta: a API não está lá.
    http.expectOne('/api/template-options').error(new ProgressEvent('error'), { status: 404 });
    http.expectOne('/api/health').error(new ProgressEvent('error'), { status: 0 });
    await settle();

    expect(element().textContent).toContain('A API do gerador não respondeu.');

    // Agora o mesmo 404, com o health de pé: a rota é que não existe.
    element().querySelector('button')?.click();
    http.expectOne('/api/template-options').error(new ProgressEvent('error'), { status: 404 });
    http.expectOne('/api/health').flush('{"status":"ok"}');
    await settle();

    expect(element().textContent).toContain('a rota do catálogo não existe');
    expect(element().textContent).toContain('GET /api/health');
  });

  it('mostra o erro do nome do projeto ao sair do campo', async () => {
    await serveCatalog();

    const input = element().querySelector<HTMLInputElement>('input[type="text"]')!;
    input.value = 'Acme Billing';
    input.dispatchEvent(new Event('input'));
    input.dispatchEvent(new Event('blur'));
    await settle();

    expect(element().querySelector('.field__error')?.textContent).toContain('Use letras');
  });

  it('não envia a requisição enquanto o nome do projeto for inválido', async () => {
    await serveCatalog();

    element().querySelector('form')?.dispatchEvent(new Event('submit'));
    await fixture.whenStable();

    http.expectNone('/api/templates');
  });

  it('bloqueia o botão durante a geração, contra duplo envio (RF-07)', async () => {
    await serveCatalog();
    const request = await submitWith();

    const button = element().querySelector<HTMLButtonElement>('button[type="submit"]')!;
    expect(button.disabled).toBe(true);
    expect(button.textContent).toContain('Gerando');

    // Um segundo envio enquanto o primeiro corre não gera outra chamada.
    element().querySelector('form')?.dispatchEvent(new Event('submit'));
    await fixture.whenStable();
    http.expectNone('/api/templates');

    request.flush(new Blob(['zip']), {
      headers: { 'Content-Disposition': 'attachment; filename="Acme.Billing.Api.zip"' },
    });
    await settle();
  });

  it('dois envios no mesmo instante geram uma chamada e um download só (RF-07)', async () => {
    // O caso que o botão desabilitado sozinho não cobre: o segundo clique chega
    // antes de a tela redesenhar o botão. Quem barra é a guarda do `submit()`.
    await serveCatalog();
    const downloads = vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => {});
    try {
      const input = element().querySelector<HTMLInputElement>('input[type="text"]')!;
      input.value = 'Acme.Billing.Api';
      input.dispatchEvent(new Event('input'));
      await settle();

      const button = element().querySelector<HTMLButtonElement>('button[type="submit"]')!;
      button.click();
      button.click();
      element().querySelector('form')?.dispatchEvent(new Event('submit'));
      await settle();

      const requests = http.match('/api/templates');
      expect(requests).toHaveLength(1);

      requests[0].flush(new Blob(['zip']), {
        headers: { 'Content-Disposition': 'attachment; filename="Acme.Billing.Api.zip"' },
      });
      await settle();

      expect(downloads).toHaveBeenCalledTimes(1);
      http.expectNone('/api/templates');
    } finally {
      downloads.mockRestore();
    }
  });

  it('põe no campo do nome a recusa que o servidor endereçou a ele (RF-06)', async () => {
    // A tela aceitou o nome; o servidor, que é soberano, recusou. A mensagem
    // dele tem de cair inline no campo, e não só no aviso geral.
    await serveCatalog();
    const request = await submitWith('Acme.Billing.Api');

    const mensagem = 'Mensagem do servidor para o nome.';
    request.flush(
      new Blob([
        JSON.stringify({
          title: 'Configuração inválida',
          status: 400,
          errors: { projectName: [mensagem] },
        }),
      ]),
      { status: 400, statusText: 'Bad Request' },
    );
    await settle();

    const input = element().querySelector<HTMLInputElement>('input[type="text"]')!;
    const bloco = input.closest('.field')!;
    expect(bloco.querySelector('.field__error')?.textContent).toContain(mensagem);
    expect(input.getAttribute('aria-invalid')).toBe('true');
    expect(input.getAttribute('aria-describedby')).toContain(`${input.id}-error`);
    expect(input.value).toBe('Acme.Billing.Api');
  });

  it('não descarta a recusa endereçada a uma chave que a tela não renderiza', async () => {
    await serveCatalog();
    const request = await submitWith();

    const mensagem = 'Mensagem para uma chave desconhecida.';
    request.flush(
      new Blob([
        JSON.stringify({
          title: 'Configuração inválida',
          status: 400,
          errors: { inexistente: [mensagem] },
        }),
      ]),
      { status: 400, statusText: 'Bad Request' },
    );
    await settle();

    expect(element().querySelector('.notice--error')?.textContent).toContain(mensagem);
  });

  it('anuncia o download concluído com o nome do arquivo', async () => {
    await serveCatalog();
    const request = await submitWith();

    request.flush(new Blob(['zip']), {
      headers: { 'Content-Disposition': 'attachment; filename="Acme.Billing.Api.zip"' },
    });
    await settle();

    expect(element().querySelector('[role="status"]')?.textContent).toContain('Download concluído');
    expect(element().textContent).toContain('Acme.Billing.Api.zip');
  });

  it('preserva todas as escolhas quando a geração falha (RF-06)', async () => {
    // Com o catálogo de hoje estas duas opções chegam desabilitadas, e o 400
    // que o teste exercita não seria alcançável por elas. O que se afirma aqui é
    // que nenhuma escolha se perde na falha — para isso as opções precisam ser
    // clicáveis.
    await serveCatalog(IMPLEMENTED_CATALOG_FIXTURE);

    radio('database', 'postgresql').click();
    await settle();
    radio(TRIGGER.field, TRIGGER.value).click();
    await settle();

    const request = await submitWith('Acme.Billing.Api');
    request.flush(
      new Blob([
        JSON.stringify({
          title: 'Configuração inválida',
          status: 400,
          errors: { authentication: ['Mensagem do servidor para o campo.'] },
        }),
      ]),
      { status: 400, statusText: 'Bad Request' },
    );
    await settle();

    expect(radio('database', 'postgresql').checked).toBe(true);
    expect(radio(TRIGGER.field, TRIGGER.value).checked).toBe(true);
    expect(element().querySelector<HTMLInputElement>('input[type="text"]')?.value).toBe(
      'Acme.Billing.Api',
    );
    expect(element().textContent).toContain('Mensagem do servidor para o campo.');
    expect(element().textContent).toContain('Nenhuma escolha foi perdida');
  });

  it('trata o 501 como estado transitório do projeto, não como erro da pessoa', async () => {
    // **Este caso não é alcançável clicando**, e é de propósito: a tela
    // desabilita todo valor sem template. O caso real é o catálogo envelhecido
    // numa aba aberta antes de uma implantação — o servidor responde a verdade
    // de hoje a uma seleção montada com a de ontem. Por isso o catálogo aqui é o
    // de "tudo implementado" e a recusa vem pelo dublê de rede: é a única forma
    // honesta de encenar a aba velha.
    await serveCatalog(IMPLEMENTED_CATALOG_FIXTURE);
    radio('database', 'sqlite').click();
    await settle();

    const request = await submitWith();

    request.flush(
      new Blob([
        JSON.stringify({
          title: 'Esta combinação ainda não gera projeto',
          status: 501,
          errors: { database: [UNAVAILABLE_REASON] },
        }),
      ]),
      { status: 501, statusText: 'Not Implemented' },
    );
    await settle();

    expect(element().textContent).toContain('Esta combinação ainda não gera projeto');
    expect(element().querySelector('.notice--pending')).not.toBeNull();
    expect(element().querySelector('.notice--error')).toBeNull();

    // O `errors` do 501 é endereçado ao campo, como o do 400: a mensagem sai
    // inline, no bloco do campo recusado, e não só no aviso geral.
    const campo = element()
      .querySelector('input[data-field="database"]')
      ?.closest('fieldset')?.textContent;
    expect(campo).toContain(UNAVAILABLE_REASON);

    // E a escolha continua onde estava (RF-06).
    expect(radio('database', 'sqlite').checked).toBe(true);
  });

  it('desabilita a opção sem template e diz por quê', async () => {
    await serveCatalog();

    const sqlite = radio('database', 'sqlite');
    expect(sqlite.disabled).toBe(true);
    expect(element().textContent).toContain(UNAVAILABLE_REASON);

    const id = sqlite.getAttribute('aria-describedby');
    expect(element().querySelector(`[id="${id}"]`)?.textContent).toContain(UNAVAILABLE_REASON);
  });

  it('deixa clicável o que tem template, nas duas arquiteturas', async () => {
    // O outro lado do teste acima: desabilitar demais esconderia metade do
    // produto. A Clean entrou no motor em T04 e precisa continuar alcançável.
    await serveCatalog();

    expect(radio('architecture', 'clean').disabled).toBe(false);
    expect(radio('database', 'none').disabled).toBe(false);
    expect(radio('authentication', 'none').disabled).toBe(false);
    expect(element().querySelector<HTMLInputElement>('input[type="checkbox"]')?.disabled).toBe(
      false,
    );
  });

  it('descarta a mensagem do servidor assim que a pessoa muda a escolha', async () => {
    await serveCatalog();
    const request = await submitWith();

    request.flush(new Blob([JSON.stringify({ title: 'Configuração inválida', status: 400 })]), {
      status: 400,
      statusText: 'Bad Request',
    });
    await settle();
    expect(element().textContent).toContain('Configuração inválida');

    radio('architecture', 'clean').click();
    await settle();

    expect(element().textContent).not.toContain('Configuração inválida');
  });

  it('mostra o resumo com as escolhas e a estrutura prevista', async () => {
    await serveCatalog();

    const input = element().querySelector<HTMLInputElement>('input[type="text"]')!;
    input.value = 'Acme.Billing.Api';
    input.dispatchEvent(new Event('input'));
    await settle();

    const resumo = element().querySelector('tg-config-summary')!;
    const texto = resumo.textContent ?? '';

    for (const field of Object.values(SNAPSHOT_CATALOG.fields)) {
      expect(texto).toContain(field.label);
      const chosen = field.values?.find((option) => option.value === field.default);
      if (chosen) {
        expect(texto).toContain(chosen.label);
      }
    }
    expect(texto).not.toContain('undefined');
    expect(texto).toContain('Acme.Billing.Api.sln');
    expect(texto).toContain('README.md');
  });

  it('refaz a estrutura prevista quando a arquitetura muda', async () => {
    await serveCatalog();

    const antes = element().querySelector('tg-config-summary')!.textContent ?? '';
    expect(antes).not.toContain('.Domain');

    radio('architecture', 'clean').click();
    await settle();

    const depois = element().querySelector('tg-config-summary')!.textContent ?? '';
    expect(depois).toContain('.Domain');
    expect(depois).toContain('.Infrastructure');
  });
});
