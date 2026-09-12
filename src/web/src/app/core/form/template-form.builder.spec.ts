import { CATALOG_FIXTURE, MALFORMED_CATALOG_FIXTURE } from '../../testing/catalog.fixture';
import { TemplateOptionsCatalog } from '../catalog/template-options.model';
import {
  PROJECT_NAME,
  buildTemplateForm,
  readSelection,
  renderableFields,
  toTemplateRequest,
} from './template-form.builder';

describe('buildTemplateForm', () => {
  it('cria um controle por campo do catálogo, mais o nome do projeto', () => {
    const form = buildTemplateForm(CATALOG_FIXTURE);

    expect(Object.keys(form.controls)).toEqual([
      PROJECT_NAME,
      'architecture',
      'database',
      'authentication',
      'swagger',
      'dotnetVersion',
    ]);
  });

  it('aplica os padrões que vieram do catálogo', () => {
    const form = buildTemplateForm(CATALOG_FIXTURE);

    expect(readSelection(form)).toEqual({
      architecture: 'simple',
      database: 'none',
      authentication: 'none',
      swagger: true,
      dotnetVersion: 'net10.0',
    });
  });

  it('acompanha um catálogo com campo novo, sem alteração de código', () => {
    const catalogoAmpliado: TemplateOptionsCatalog = {
      ...CATALOG_FIXTURE,
      fields: {
        ...CATALOG_FIXTURE.fields,
        telemetry: {
          label: 'Telemetria',
          default: false,
          type: 'boolean',
        },
      },
    };

    const form = buildTemplateForm(catalogoAmpliado);

    expect(form.controls['telemetry'].value).toBe(false);
    expect(renderableFields(catalogoAmpliado).at(-1)).toMatchObject({
      key: 'telemetry',
      isToggle: true,
    });
  });

  it('decide o tipo de controle pelo type que o catálogo declara', () => {
    const fields = renderableFields(CATALOG_FIXTURE);

    expect(fields.find((item) => item.key === 'architecture')?.isToggle).toBe(false);
    expect(fields.find((item) => item.key === 'swagger')?.isToggle).toBe(true);
  });

  it('tolera na leitura um campo sem type, que o backend não emite', () => {
    const fields = renderableFields(MALFORMED_CATALOG_FIXTURE);

    expect(fields.find((item) => item.key === 'architecture')?.isToggle).toBe(false);
    expect(fields.find((item) => item.key === 'swagger')?.isToggle).toBe(true);
  });

  it('exige nome de projeto válido', () => {
    const form = buildTemplateForm(CATALOG_FIXTURE);
    const projectName = form.controls[PROJECT_NAME];

    expect(form.invalid).toBe(true);

    projectName.setValue('nome inválido');
    expect(projectName.invalid).toBe(true);

    projectName.setValue('Acme.Billing.Api');
    expect(form.valid).toBe(true);
  });

  it('monta o corpo de POST /api/templates a partir do formulário', () => {
    const form = buildTemplateForm(CATALOG_FIXTURE);
    form.controls[PROJECT_NAME].setValue('  Acme.Billing.Api  ');
    form.controls['database'].setValue('postgresql');
    form.controls['authentication'].setValue('identity');
    form.controls['swagger'].setValue(false);

    expect(toTemplateRequest(form)).toEqual({
      projectName: 'Acme.Billing.Api',
      architecture: 'simple',
      database: 'postgresql',
      authentication: 'identity',
      swagger: false,
      dotnetVersion: 'net10.0',
    });
  });
});
