import { AbstractControl, ValidationErrors, ValidatorFn } from '@angular/forms';

/**
 * Name rule from `docs/product/option-matrix.md`.
 *
 * This is convenience only — immediate feedback while typing. The backend
 * revalidates the name and is the one that decides.
 */

const SEGMENT = /^[A-Za-z_][A-Za-z0-9_]*$/;
const WINDOWS_RESERVED = /^(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])$/i;
const MAX_LENGTH = 100;

/** True when the text carries a C0/C1 control character. */
function hasControlCharacter(text: string): boolean {
  for (const character of text) {
    const code = character.codePointAt(0) ?? 0;
    if (code < 0x20 || (code >= 0x7f && code <= 0x9f)) {
      return true;
    }
  }
  return false;
}

/** Reserved words of C#. Contextual keywords are valid identifiers and stay out. */
const CSHARP_KEYWORDS = new Set([
  'abstract',
  'as',
  'base',
  'bool',
  'break',
  'byte',
  'case',
  'catch',
  'char',
  'checked',
  'class',
  'const',
  'continue',
  'decimal',
  'default',
  'delegate',
  'do',
  'double',
  'else',
  'enum',
  'event',
  'explicit',
  'extern',
  'false',
  'finally',
  'fixed',
  'float',
  'for',
  'foreach',
  'goto',
  'if',
  'implicit',
  'in',
  'int',
  'interface',
  'internal',
  'is',
  'lock',
  'long',
  'namespace',
  'new',
  'null',
  'object',
  'operator',
  'out',
  'override',
  'params',
  'private',
  'protected',
  'public',
  'readonly',
  'ref',
  'return',
  'sbyte',
  'sealed',
  'short',
  'sizeof',
  'stackalloc',
  'static',
  'string',
  'struct',
  'switch',
  'this',
  'throw',
  'true',
  'try',
  'typeof',
  'uint',
  'ulong',
  'unchecked',
  'unsafe',
  'ushort',
  'using',
  'virtual',
  'void',
  'volatile',
  'while',
]);

/** The reason the name is unusable, in Portuguese, or `null` when it is fine. */
export function describeProjectNameProblem(name: string): string | null {
  if (name.length === 0) {
    return 'Informe o nome do projeto.';
  }

  if (name.length > MAX_LENGTH) {
    return `O nome pode ter no máximo ${MAX_LENGTH} caracteres.`;
  }

  if (hasControlCharacter(name)) {
    return 'O nome não pode conter caracteres de controle.';
  }

  if (name.includes('/') || name.includes('\\')) {
    return 'O nome não pode conter barras.';
  }

  if (name.includes('..')) {
    return 'O nome não pode conter dois pontos seguidos.';
  }

  const segments = name.split('.');

  for (const segment of segments) {
    if (!SEGMENT.test(segment)) {
      return 'Use letras, dígitos e "_", separando os segmentos por ponto. Cada segmento começa com letra ou "_".';
    }

    if (CSHARP_KEYWORDS.has(segment)) {
      return `"${segment}" é uma palavra reservada do C# e não pode nomear um segmento.`;
    }

    if (WINDOWS_RESERVED.test(segment)) {
      return `"${segment}" é um nome reservado do Windows e não pode nomear um segmento.`;
    }
  }

  return null;
}

/** Reactive Forms wrapper around {@link describeProjectNameProblem}. */
export const projectNameValidator: ValidatorFn = (
  control: AbstractControl,
): ValidationErrors | null => {
  const value = typeof control.value === 'string' ? control.value.trim() : '';
  const problem = describeProjectNameProblem(value);
  return problem ? { projectName: { message: problem } } : null;
};
