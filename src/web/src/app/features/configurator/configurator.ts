import { HttpErrorResponse } from '@angular/common/http';
import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  ElementRef,
  OnInit,
  computed,
  inject,
  signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ReactiveFormsModule } from '@angular/forms';
import { CatalogFailure, diagnoseCatalogFailure } from '../../core/catalog/catalog-diagnosis';
import {
  CatalogAvailability,
  OptionAvailability,
  Selection,
  evaluateAvailability,
  violationsByField,
} from '../../core/catalog/constraints';
import {
  GenerationFailure,
  describeGenerationFailure,
} from '../../core/catalog/generation-failure';
import {
  TemplateCatalogService,
  readProblemDetailsBlob,
} from '../../core/catalog/template-catalog.service';
import { OptionValue, TemplateOptionsCatalog } from '../../core/catalog/template-options.model';
import {
  PROJECT_NAME,
  RenderableField,
  TemplateForm,
  buildTemplateForm,
  readSelection,
  renderableFields,
  toTemplateRequest,
} from '../../core/form/template-form.builder';
import { describeProjectNameProblem } from '../../core/validation/project-name.validator';
import { CatalogOutage } from './catalog-outage';
import { ConfigSummary } from './config-summary';

type CatalogStatus = 'loading' | 'ready' | 'unavailable';

/**
 * A tela de configuração.
 *
 * Tudo que ela renderiza — campos, rótulos, valores, padrões e qual opção está
 * indisponível — vem do catálogo. Os oito estados de
 * `docs/design/visual-spec.md` estão aqui: carregando, inicial com padrões,
 * validação inline, opção incompatível com motivo, explicação de cada opção,
 * geração em andamento, falha preservando as escolhas, download concluído e
 * catálogo indisponível (este último desdobrado pelo diagnóstico de
 * `catalog-diagnosis.ts`).
 */
@Component({
  selector: 'tg-configurator',
  imports: [ReactiveFormsModule, CatalogOutage, ConfigSummary],
  templateUrl: './configurator.html',
  styleUrl: './configurator.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class Configurator implements OnInit {
  private readonly catalogService = inject(TemplateCatalogService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);

  protected readonly projectNameKey = PROJECT_NAME;

  protected readonly status = signal<CatalogStatus>('loading');
  protected readonly catalogFailure = signal<CatalogFailure | null>(null);
  protected readonly catalog = signal<TemplateOptionsCatalog | null>(null);
  protected readonly form = signal<TemplateForm | null>(null);
  protected readonly selection = signal<Selection>({});

  /** Mirrors of the project-name control, so the view reacts without zone.js. */
  protected readonly projectName = signal('');
  protected readonly projectNameTouched = signal(false);

  protected readonly generating = signal(false);
  protected readonly generationFailure = signal<GenerationFailure | null>(null);
  protected readonly generatedFileName = signal<string | null>(null);

  /** The catalog fields, in catalog order. */
  protected readonly fields = computed<readonly RenderableField[]>(() => {
    const catalog = this.catalog();
    return catalog ? renderableFields(catalog) : [];
  });

  /** Which values the catalog rules out, given the rest of the selection. */
  protected readonly availability = computed<CatalogAvailability>(() => {
    const catalog = this.catalog();
    return catalog ? evaluateAvailability(catalog, this.selection()) : {};
  });

  /** Constraint messages grouped by the field that triggered them. */
  protected readonly constraintMessages = computed<Readonly<Record<string, readonly string[]>>>(
    () => {
      const catalog = this.catalog();
      return catalog ? violationsByField(catalog, this.selection()) : {};
    },
  );

  protected readonly hasConstraintViolation = computed(
    () => Object.keys(this.constraintMessages()).length > 0,
  );

  /** The project-name problem to show inline, once the field has been visited. */
  protected readonly projectNameError = computed(() =>
    this.projectNameTouched() ? describeProjectNameProblem(this.projectName().trim()) : null,
  );

  ngOnInit(): void {
    this.loadCatalog();
  }

  protected loadCatalog(): void {
    this.status.set('loading');
    this.catalogFailure.set(null);

    this.catalogService
      .loadCatalog()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (catalog) => this.applyCatalog(catalog),
        error: (error: unknown) => this.diagnose(error),
      });
  }

  /**
   * The catalog call alone cannot tell "the API is down" from "that route is
   * not there" — both arrive as a failed request. `GET /api/health` breaks the
   * tie, so a failure is only reported after probing it.
   */
  private diagnose(error: unknown): void {
    const optionsStatus = error instanceof HttpErrorResponse ? error.status : 0;

    this.catalogService
      .probeHealth()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((health) => {
        this.catalog.set(null);
        this.form.set(null);
        this.catalogFailure.set(diagnoseCatalogFailure(optionsStatus, health));
        this.status.set('unavailable');
      });
  }

  private applyCatalog(catalog: TemplateOptionsCatalog): void {
    const form = buildTemplateForm(catalog);

    form.valueChanges.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(() => {
      this.selection.set(readSelection(form));
      this.projectName.set(String(form.controls[PROJECT_NAME].value));
      // Uma mensagem do servidor sobre o valor anterior deixou de valer.
      this.generationFailure.set(null);
      this.generatedFileName.set(null);
    });

    this.catalog.set(catalog);
    this.form.set(form);
    this.selection.set(readSelection(form));
    this.projectName.set(String(form.controls[PROJECT_NAME].value));
    this.projectNameTouched.set(false);
    this.status.set('ready');
  }

  /** Availability of one value of one field. */
  protected availabilityOf(field: string, value: OptionValue): OptionAvailability | undefined {
    return this.availability()[field]?.find((option) => option.value === value);
  }

  /**
   * What to show inline under a field: the catalog constraint it breaks now,
   * plus whatever the API addressed to that same field on the last attempt.
   */
  protected messagesFor(field: string): readonly string[] {
    const local = this.constraintMessages()[field] ?? [];
    const fromApi = this.generationFailure()?.fieldErrors[field] ?? [];
    return [...local, ...fromApi.filter((message) => !local.includes(message))];
  }

  protected submit(): void {
    const form = this.form();
    if (!form || this.generating()) {
      return;
    }

    form.markAllAsTouched();
    this.projectNameTouched.set(true);

    if (form.invalid || this.hasConstraintViolation()) {
      if (form.controls[PROJECT_NAME].invalid) {
        // Atributo em vez de `#id`: o seletor não depende de o id ser um
        // identificador CSS válido, e não precisa de `CSS.escape`.
        this.host.nativeElement
          .querySelector<HTMLInputElement>(`input[id="${PROJECT_NAME}"]`)
          ?.focus();
      }
      return;
    }

    this.generating.set(true);
    this.generationFailure.set(null);
    this.generatedFileName.set(null);

    this.catalogService
      .generate(toTemplateRequest(form))
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (template) => {
          this.generating.set(false);
          this.generatedFileName.set(template.fileName);
          this.offerDownload(template.fileName, template.content);
        },
        error: (error: unknown) => {
          this.generating.set(false);
          const status = error instanceof HttpErrorResponse ? error.status : 0;
          const retryAfter =
            error instanceof HttpErrorResponse ? error.headers.get('Retry-After') : null;

          void readProblemDetailsBlob(error).then((problem) => {
            this.generationFailure.set(describeGenerationFailure(status, problem, retryAfter));
          });
        },
      });
  }

  private offerDownload(fileName: string, content: Blob): void {
    const url = URL.createObjectURL(content);
    const link = document.createElement('a');
    link.href = url;
    link.download = fileName;
    link.click();
    URL.revokeObjectURL(url);
  }
}
