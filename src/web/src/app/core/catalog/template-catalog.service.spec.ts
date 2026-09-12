import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { CATALOG_FIXTURE } from '../../testing/catalog.fixture';
import { GeneratedTemplate, ProblemDetails } from './template-options.model';
import { TemplateCatalogService, readProblemDetailsBlob } from './template-catalog.service';

describe('TemplateCatalogService', () => {
  let service: TemplateCatalogService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });

    service = TestBed.inject(TemplateCatalogService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('busca o catálogo em GET /api/template-options', () => {
    let received: unknown;
    service.loadCatalog().subscribe((catalog) => (received = catalog));

    const request = http.expectOne('/api/template-options');
    expect(request.request.method).toBe('GET');
    request.flush(CATALOG_FIXTURE);

    expect(received).toEqual(CATALOG_FIXTURE);
  });

  it('envia a configuração em POST /api/templates e devolve o ZIP nomeado', async () => {
    let generated: GeneratedTemplate | undefined;
    service
      .generate({
        projectName: 'Acme.Billing.Api',
        architecture: 'simple',
        database: 'sqlite',
        authentication: 'identity',
        swagger: true,
        dotnetVersion: 'net10.0',
      })
      .subscribe((template) => (generated = template));

    const request = http.expectOne('/api/templates');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({
      projectName: 'Acme.Billing.Api',
      architecture: 'simple',
      database: 'sqlite',
      authentication: 'identity',
      swagger: true,
      dotnetVersion: 'net10.0',
    });

    request.flush(new Blob(['zip'], { type: 'application/zip' }), {
      headers: { 'Content-Disposition': 'attachment; filename="Acme.Billing.Api.zip"' },
    });

    expect(generated?.fileName).toBe('Acme.Billing.Api.zip');
    expect(await generated?.content.text()).toBe('zip');
  });

  it('cai no nome do projeto quando a resposta não traz Content-Disposition', () => {
    let generated: GeneratedTemplate | undefined;
    service
      .generate({
        projectName: 'Acme.Api',
        architecture: 'simple',
        database: 'none',
        authentication: 'none',
        swagger: true,
        dotnetVersion: 'net10.0',
      })
      .subscribe((template) => (generated = template));

    http.expectOne('/api/templates').flush(new Blob(['zip']));

    expect(generated?.fileName).toBe('Acme.Api.zip');
  });

  it('lê o ProblemDetails de uma resposta 400 entregue como blob', async () => {
    const problem: ProblemDetails = {
      type: 'https://templategenerator.local/problems/invalid-configuration',
      title: 'Configuração inválida',
      status: 400,
      errors: {
        authentication: ['O Identity nativo precisa de um banco para persistir os usuários.'],
      },
    };

    let failure: unknown;
    service
      .generate({
        projectName: 'Acme.Api',
        architecture: 'simple',
        database: 'none',
        authentication: 'identity',
        swagger: true,
        dotnetVersion: 'net10.0',
      })
      .subscribe({ error: (error: unknown) => (failure = error) });

    http
      .expectOne('/api/templates')
      .flush(new Blob([JSON.stringify(problem)], { type: 'application/problem+json' }), {
        status: 400,
        statusText: 'Bad Request',
      });

    const parsed = await readProblemDetailsBlob(failure);
    expect(parsed?.title).toBe('Configuração inválida');
    expect(parsed?.errors?.['authentication']?.[0]).toBe(
      'O Identity nativo precisa de um banco para persistir os usuários.',
    );
  });

  it('propaga a falha quando o catálogo está indisponível', () => {
    let status: number | undefined;
    service.loadCatalog().subscribe({
      error: (error: { status: number }) => (status = error.status),
    });

    http
      .expectOne('/api/template-options')
      .flush('', { status: 503, statusText: 'Service Unavailable' });

    expect(status).toBe(503);
  });
});
