import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  OnInit,
  computed,
  inject,
  signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ReactiveFormsModule } from '@angular/forms';
import {
  CatalogAvailability,
  OptionAvailability,
  Selection,
  evaluateAvailability,
  violationsByField,
} from '../../core/catalog/constraints';
import {
  TemplateCatalogService,
  readProblemDetailsBlob,
} from '../../core/catalog/template-catalog.service';
import { OptionValue, TemplateOptionsCatalog } from '../../core/catalog/template-options.model';
import { describeProjectNameProblem } from '../../core/validation/project-name.validator';
import {
  PROJECT_NAME,
  RenderableField,
  TemplateForm,
  buildTemplateForm,
  readSelection,
  renderableFields,
  toTemplateRequest,
} from '../../core/form/template-form.builder';

type CatalogStatus = 'loading' | 'ready' | 'unavailable';

/**
 * The configuration screen.
 *
 * Everything it renders — fields, labels, values, defaults and which option is
 * unavailable — comes from the catalog. The finished visual design and the
 * eight screen states are T02; this is the working skeleton.
 */
@Component({
  selector: 'tg-configurator',
  imports: [ReactiveFormsModule],
  templateUrl: './configurator.html',
  styleUrl: './configurator.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class Configurator implements OnInit {
  private readonly catalogService = inject(TemplateCatalogService);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly projectNameKey = PROJECT_NAME;

  protected readonly status = signal<CatalogStatus>('loading');
  protected readonly catalog = signal<TemplateOptionsCatalog | null>(null);
  protected readonly form = signal<TemplateForm | null>(null);
  protected readonly selection = signal<Selection>({});

  /** Mirrors of the project-name control, so the view reacts without zone.js. */
  protected readonly projectName = signal('');
  protected readonly projectNameTouched = signal(false);

  protected readonly generating = signal(false);
  protected readonly generationError = signal<string | null>(null);
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
    this.catalogService
      .loadCatalog()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (catalog) => this.applyCatalog(catalog),
        error: () => {
          this.catalog.set(null);
          this.form.set(null);
          this.status.set('unavailable');
        },
      });
  }

  private applyCatalog(catalog: TemplateOptionsCatalog): void {
    const form = buildTemplateForm(catalog);

    form.valueChanges.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(() => {
      this.selection.set(readSelection(form));
      this.projectName.set(String(form.controls[PROJECT_NAME].value));
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

  protected messagesFor(field: string): readonly string[] {
    return this.constraintMessages()[field] ?? [];
  }

  protected submit(): void {
    const form = this.form();
    if (!form || this.generating()) {
      return;
    }

    form.markAllAsTouched();
    this.projectNameTouched.set(true);
    if (form.invalid || this.hasConstraintViolation()) {
      return;
    }

    this.generating.set(true);
    this.generationError.set(null);
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
          void readProblemDetailsBlob(error).then((problem) => {
            const firstFieldError = Object.values(problem?.errors ?? {})[0]?.[0];
            this.generationError.set(
              firstFieldError ?? problem?.title ?? 'Não foi possível gerar o projeto.',
            );
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
