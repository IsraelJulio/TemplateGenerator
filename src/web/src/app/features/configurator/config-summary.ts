import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { Selection } from '../../core/catalog/constraints';
import { TemplateOptionsCatalog } from '../../core/catalog/template-options.model';
import {
  PLACEHOLDER_PROJECT_NAME,
  StructureLine,
  flattenStructure,
  projectStructure,
} from '../../core/summary/project-structure';

/** Uma escolha, pronta para exibir: tudo vem do catálogo. */
export interface SummaryChoice {
  readonly key: string;
  readonly label: string;
  readonly valueLabel: string;
  readonly description: string | null;
}

/**
 * O resumo da configuração (RF-04).
 *
 * Os rótulos das escolhas vêm do catálogo — este componente não sabe que campos
 * existem nem que valores eles têm. A árvore prevista vem de
 * `core/summary/project-structure.ts`, que é uma **projeção** enquanto o motor
 * de geração não existir; o aviso disso aparece na própria tela.
 */
@Component({
  selector: 'tg-config-summary',
  templateUrl: './config-summary.html',
  styleUrl: './config-summary.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ConfigSummary {
  readonly catalog = input.required<TemplateOptionsCatalog>();
  readonly selection = input.required<Selection>();
  readonly projectName = input<string>('');

  protected readonly placeholderName = PLACEHOLDER_PROJECT_NAME;

  /** O nome digitado, aparado, ou `null` enquanto não houver um. */
  protected readonly displayName = computed<string | null>(() => {
    const name = this.projectName().trim();
    return name === '' ? null : name;
  });

  /** Uma linha por campo do catálogo, com o rótulo do valor escolhido. */
  protected readonly choices = computed<readonly SummaryChoice[]>(() => {
    const catalog = this.catalog();
    const selection = this.selection();

    return Object.entries(catalog.fields).map(([key, field]) => {
      const value = selection[key];

      if (field.type === 'boolean' || field.values === undefined) {
        return {
          key,
          label: field.label,
          valueLabel: value === true ? 'Sim' : 'Não',
          description: field.description ?? null,
        };
      }

      const chosen = field.values.find((option) => option.value === value);
      return {
        key,
        label: field.label,
        valueLabel: chosen?.label ?? String(value),
        description: chosen?.description ?? null,
      };
    });
  });

  protected readonly structure = computed<readonly StructureLine[]>(() =>
    flattenStructure(projectStructure(this.selection(), this.projectName())),
  );
}
