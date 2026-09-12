import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { CATALOG_FIXTURE } from '../../testing/catalog.fixture';
import { Configurator } from './configurator';

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

  async function serveCatalog(): Promise<void> {
    http.expectOne('/api/template-options').flush(CATALOG_FIXTURE);
    await fixture.whenStable();
    fixture.detectChanges();
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

    expect(legends).toEqual(['Arquitetura', 'Banco', 'Autenticação', 'Swagger', 'Versão .NET']);
    expect(element().querySelector('input[type="text"]')).not.toBeNull();
  });

  it('aplica os padrões do catálogo aos controles', async () => {
    await serveCatalog();

    expect(radio('architecture', 'simple').checked).toBe(true);
    expect(radio('database', 'none').checked).toBe(true);
    expect(element().querySelector<HTMLInputElement>('input[type="checkbox"]')?.checked).toBe(true);
  });

  it('desabilita a opção que a restrição do catálogo impede, com o motivo visível', async () => {
    await serveCatalog();

    const identity = radio('authentication', 'identity');
    expect(identity.disabled).toBe(true);
    expect(element().textContent).toContain(
      'O Identity nativo precisa de um banco para persistir os usuários.',
    );
  });

  it('libera a opção quando a seleção passa a satisfazer a restrição', async () => {
    await serveCatalog();

    const sqlite = radio('database', 'sqlite');
    sqlite.click();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(radio('authentication', 'identity').disabled).toBe(false);
  });

  it('diz que o catálogo está indisponível em vez de mostrar tela vazia', async () => {
    http
      .expectOne('/api/template-options')
      .flush('', { status: 503, statusText: 'Service Unavailable' });
    await fixture.whenStable();
    fixture.detectChanges();

    expect(element().querySelector('[role="alert"]')?.textContent).toContain(
      'O catálogo de opções não respondeu',
    );

    element().querySelector('button')?.click();
    http.expectOne('/api/template-options').flush(CATALOG_FIXTURE);
  });

  it('mostra o erro do nome do projeto ao sair do campo', async () => {
    await serveCatalog();

    const input = element().querySelector<HTMLInputElement>('input[type="text"]')!;
    input.value = 'Acme Billing';
    input.dispatchEvent(new Event('input'));
    input.dispatchEvent(new Event('blur'));
    await fixture.whenStable();
    fixture.detectChanges();

    expect(element().querySelector('.error')?.textContent).toContain('Use letras');
  });

  it('não envia a requisição enquanto o nome do projeto for inválido', async () => {
    await serveCatalog();

    element().querySelector('form')?.dispatchEvent(new Event('submit'));
    await fixture.whenStable();

    http.expectNone('/api/templates');
  });
});
