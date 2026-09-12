import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { CatalogFailure } from '../../core/catalog/catalog-diagnosis';

/**
 * Estado 8 de `docs/design/visual-spec.md`: catálogo indisponível.
 *
 * A tela precisa dizer o que houve, e dizer qual das causas foi. O diagnóstico
 * já chega pronto de `catalog-diagnosis.ts`; aqui só se apresenta, incluindo o
 * que cada sondagem devolveu — é isso que separa "a API está fora do ar" de "a
 * rota do catálogo não existe" para quem for investigar.
 */
@Component({
  selector: 'tg-catalog-outage',
  templateUrl: './catalog-outage.html',
  styleUrl: './catalog-outage.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CatalogOutage {
  readonly failure = input.required<CatalogFailure>();
  readonly retry = output<void>();
}
