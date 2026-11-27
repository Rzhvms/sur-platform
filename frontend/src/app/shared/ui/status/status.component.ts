import { Component, input } from '@angular/core';
import { RiskStatus, statuses, text } from '../../text';

@Component({
  selector: 'sur-status',
  templateUrl: './status.component.html',
})
export class StatusComponent {
  readonly value = input.required<RiskStatus>();
  protected readonly labels = statuses;
  protected readonly t = text;
}
