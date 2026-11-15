import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { text } from '../text';

export type DataState<T> = { kind: 'loading' } | { kind: 'ready'; value: T } | { kind: 'error'; message: string };

@Component({
  selector: 'sur-data-state',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @switch (kind()) {
      @case ('loading') {
        <div class="state" role="status"><span class="spinner" aria-hidden="true"></span><strong>{{ t.loading }}</strong></div>
      }
      @case ('empty') {
        <div class="state" role="status"><strong>{{ t.empty }}</strong><span>{{ t.emptyHint }}</span></div>
      }
      @case ('error') {
        <div class="state" role="alert"><strong>{{ t.error }}</strong><span>{{ message() }}</span>
          <button type="button" (click)="retry.emit()">{{ t.retry }}</button></div>
      }
    }
  `,
})
export class DataStateView {
  readonly kind = input.required<'loading' | 'error' | 'empty'>();
  readonly message = input('');
  readonly retry = output<void>();
  protected readonly t = text;
}
