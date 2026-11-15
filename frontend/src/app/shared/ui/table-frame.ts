import { ChangeDetectionStrategy, Component, input } from '@angular/core';

@Component({
  selector: 'sur-table-frame',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<div class="table-frame" role="region" [attr.aria-label]="label()" tabindex="0"><ng-content /></div>`,
})
export class TableFrame {
  readonly label = input.required<string>();
}
