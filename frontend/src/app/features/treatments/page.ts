import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { text } from '../../shared/text';
import { treatmentText } from './treatment-contract';

@Component({
  imports: [RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<h1>{{ t.treatments }}</h1><p>{{ w.manageFromRisk }}</p><a class="action-link" routerLink="/risks">{{ w.toRisks }}</a>`,
})
export class Treatments {
  protected readonly t = text;
  protected readonly w = treatmentText;
}
