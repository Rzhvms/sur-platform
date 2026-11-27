import { Component, input, output } from '@angular/core';
import { text } from '../../text';

export type DataState<T> = { kind: 'loading' } | { kind: 'ready'; value: T } | { kind: 'error'; message: string };

@Component({
  selector: 'sur-data-state',
  templateUrl: './data-state.component.html',
})
export class DataStateComponent {
  readonly kind = input.required<'loading' | 'error' | 'empty'>();
  readonly message = input('');
  readonly retry = output<void>();
  protected readonly t = text;
}
