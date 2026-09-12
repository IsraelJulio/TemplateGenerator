import { ChangeDetectionStrategy, Component } from '@angular/core';
import { Configurator } from './features/configurator/configurator';

@Component({
  selector: 'tg-root',
  imports: [Configurator],
  template: '<tg-configurator />',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class App {}
