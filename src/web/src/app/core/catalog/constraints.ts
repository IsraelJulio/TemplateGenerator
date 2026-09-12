import { OptionValue, TemplateConstraint, TemplateOptionsCatalog } from './template-options.model';

/** The current value of every field, keyed exactly as the catalog keys it. */
export type Selection = Readonly<Record<string, OptionValue>>;

/** One candidate value of a field and whether the catalog rules it out. */
export interface OptionAvailability {
  readonly value: OptionValue;
  readonly disabled: boolean;
  /** Why it is unavailable, in the catalog's own words. `null` when available. */
  readonly reason: string | null;
}

/** Availability of every candidate value, keyed by field. */
export type CatalogAvailability = Readonly<Record<string, readonly OptionAvailability[]>>;

/** A rule the current selection breaks, and the fields that triggered it. */
export interface ConstraintViolation {
  readonly constraint: TemplateConstraint;
  /** Fields named by `when` — where the message belongs inline. */
  readonly fields: readonly string[];
}

function asList(value: OptionValue | readonly OptionValue[]): readonly OptionValue[] {
  return Array.isArray(value) ? value : [value as OptionValue];
}

/**
 * Whether every entry of `when` matches the selection. A field the selection
 * does not carry never matches, so the rule simply does not apply.
 */
function applies(constraint: TemplateConstraint, selection: Selection): boolean {
  return Object.entries(constraint.when).every(([field, accepted]) => {
    if (!(field in selection)) {
      return false;
    }
    return asList(accepted).includes(selection[field]);
  });
}

/**
 * Whether every entry of `requires` is satisfied. A field the selection does
 * not carry is skipped: the frontend has nothing to offer the user about a
 * field it does not render, and the backend revalidates everything anyway.
 */
function satisfied(constraint: TemplateConstraint, selection: Selection): boolean {
  return Object.entries(constraint.requires).every(([field, accepted]) => {
    if (!(field in selection)) {
      return true;
    }
    return asList(accepted).includes(selection[field]);
  });
}

/** Every catalog rule the selection breaks, in catalog order. */
export function findViolations(
  catalog: TemplateOptionsCatalog,
  selection: Selection,
): readonly ConstraintViolation[] {
  return catalog.constraints
    .filter((constraint) => applies(constraint, selection) && !satisfied(constraint, selection))
    .map((constraint) => ({ constraint, fields: Object.keys(constraint.when) }));
}

/**
 * Violation messages grouped by field, in the same shape the API uses for the
 * `errors` member of its `ProblemDetails`, so both sources render the same way.
 */
export function violationsByField(
  catalog: TemplateOptionsCatalog,
  selection: Selection,
): Readonly<Record<string, readonly string[]>> {
  const grouped: Record<string, string[]> = {};
  for (const violation of findViolations(catalog, selection)) {
    for (const field of violation.fields) {
      (grouped[field] ??= []).push(violation.constraint.message);
    }
  }
  return grouped;
}

/**
 * The values a field can take: both toggle positions for a boolean, the
 * catalog values for a choice. `type` decides; a missing `values` on a
 * malformed payload falls back to the toggle positions.
 */
function candidatesOf(catalog: TemplateOptionsCatalog, field: string): readonly OptionValue[] {
  const definition = catalog.fields[field];
  if (definition?.type === 'boolean' || definition?.values === undefined) {
    return [true, false];
  }
  return definition.values.map((option) => option.value);
}

/**
 * Resolves, for every field, which values stay available given the rest of the
 * selection — by trying each value and asking the catalog rules, never by
 * knowing anything about a particular field or value.
 */
export function evaluateAvailability(
  catalog: TemplateOptionsCatalog,
  selection: Selection,
): CatalogAvailability {
  const availability: Record<string, OptionAvailability[]> = {};

  for (const field of Object.keys(catalog.fields)) {
    availability[field] = candidatesOf(catalog, field).map((value) => {
      const [violation] = findViolations(catalog, { ...selection, [field]: value });
      return {
        value,
        disabled: violation !== undefined,
        reason: violation?.constraint.message ?? null,
      };
    });
  }

  return availability;
}
