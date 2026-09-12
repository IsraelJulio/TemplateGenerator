import { FormControl, FormGroup, Validators } from '@angular/forms';
import { Selection } from '../catalog/constraints';
import {
  OptionValue,
  TemplateField,
  TemplateOptionsCatalog,
  TemplateRequest,
} from '../catalog/template-options.model';
import { projectNameValidator } from '../validation/project-name.validator';

/** The one control that does not come from the catalog. */
export const PROJECT_NAME = 'projectName';

export type TemplateForm = FormGroup<Record<string, FormControl<OptionValue>>>;

/** A catalog field paired with its key, ready to render. */
export interface RenderableField {
  readonly key: string;
  readonly field: TemplateField;
  /** True for a toggle, false for a list of values. */
  readonly isToggle: boolean;
}

/**
 * How a field is rendered.
 *
 * `type` decides it — the contract guarantees the field is there. The absence
 * of `values` is only a fallback for a malformed payload, and it errs towards a
 * toggle because rendering a choice with no values to choose from is worse.
 */
function isToggle(field: TemplateField): boolean {
  return field.type === 'boolean' || field.values === undefined;
}

/** The catalog fields in the order the catalog lists them. */
export function renderableFields(catalog: TemplateOptionsCatalog): readonly RenderableField[] {
  return Object.entries(catalog.fields).map(([key, field]) => ({
    key,
    field,
    isToggle: isToggle(field),
  }));
}

/**
 * Assembles the form from the catalog: one control per field, each starting at
 * the default the backend sent. No field key and no default is written here.
 */
export function buildTemplateForm(catalog: TemplateOptionsCatalog): TemplateForm {
  const controls: Record<string, FormControl<OptionValue>> = {
    [PROJECT_NAME]: new FormControl<OptionValue>('', {
      nonNullable: true,
      validators: [Validators.required, projectNameValidator],
    }),
  };

  for (const { key, field } of renderableFields(catalog)) {
    controls[key] = new FormControl<OptionValue>(field.default, { nonNullable: true });
  }

  return new FormGroup(controls);
}

/** The current value of every catalog field, without the project name. */
export function readSelection(form: TemplateForm): Selection {
  const selection: Record<string, OptionValue> = {};
  for (const [key, control] of Object.entries(form.controls)) {
    if (key !== PROJECT_NAME) {
      selection[key] = control.value;
    }
  }
  return selection;
}

/** The body of `POST /api/templates` for the current form state. */
export function toTemplateRequest(form: TemplateForm): TemplateRequest {
  const projectName = form.controls[PROJECT_NAME].value;
  return {
    ...readSelection(form),
    projectName: typeof projectName === 'string' ? projectName.trim() : '',
  };
}
