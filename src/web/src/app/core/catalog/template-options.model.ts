/**
 * Types that mirror `docs/architecture/http-contract.md`.
 *
 * Nothing here enumerates option values or compatibility rules: field keys,
 * option values, defaults and constraints are all data returned by
 * `GET /api/template-options`. Adding an option or a constraint on the backend
 * must not require a change in this file (RF-02).
 */

/** A value a field can hold. Text fields carry `string`, toggles carry `boolean`. */
export type OptionValue = string | boolean;

/** One selectable value of a field, with its label in Portuguese. */
export interface TemplateOptionValue {
  readonly value: string;
  readonly label: string;
  readonly description?: string;
}

/**
 * What kind of control a field is. The contract admits these two and no others:
 * `choice` carries `values`, `boolean` is a toggle and carries none.
 */
export type FieldType = 'choice' | 'boolean';

/**
 * One configurable field of the catalog.
 *
 * `type` is mandatory on the wire (serialization rule 3), so it is mandatory
 * here. The rendering code still falls back to the absence of `values` when a
 * malformed payload arrives — tolerance on read is deliberate, but it is not a
 * shape this project models as legal.
 */
export interface TemplateField {
  readonly label: string;
  readonly type: FieldType;
  readonly default: OptionValue;
  readonly values?: readonly TemplateOptionValue[];
  readonly description?: string;
}

/**
 * A compatibility rule expressed as data.
 *
 * `when` is a conjunction of field/value matches. When every entry matches the
 * current selection, every entry of `requires` must also be satisfied — the
 * selected value has to be among the accepted ones.
 *
 * The backend always emits arrays on both sides, even for a single value
 * (serialization rule 4). A bare value is accepted on read for robustness, not
 * because the contract allows it.
 */
export interface TemplateConstraint {
  readonly id: string;
  readonly when: Readonly<Record<string, OptionValue | readonly OptionValue[]>>;
  readonly requires: Readonly<Record<string, OptionValue | readonly OptionValue[]>>;
  readonly message: string;
}

/** Body of `GET /api/template-options`. */
export interface TemplateOptionsCatalog {
  readonly templateVersion: string;
  readonly fields: Readonly<Record<string, TemplateField>>;
  readonly constraints: readonly TemplateConstraint[];
}

/**
 * Body of `POST /api/templates`: the project name plus one entry per catalog
 * field, keyed exactly as the catalog keys them.
 */
export interface TemplateRequest {
  readonly projectName: string;
  readonly [field: string]: OptionValue;
}

/** `application/problem+json` payload (RFC 9457) returned on 400 and 429. */
export interface ProblemDetails {
  readonly type?: string;
  readonly title?: string;
  readonly status?: number;
  readonly detail?: string;
  readonly instance?: string;
  /** Field-keyed messages, so the screen can place them inline. */
  readonly errors?: Readonly<Record<string, readonly string[]>>;
}

/** A generated template, as delivered by `POST /api/templates`. */
export interface GeneratedTemplate {
  readonly fileName: string;
  readonly content: Blob;
}
