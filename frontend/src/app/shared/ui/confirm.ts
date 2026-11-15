import { ChangeDetectionStrategy, Component, ElementRef, input, output, viewChild } from '@angular/core';
import { text } from '../text';

@Component({
  selector: 'sur-confirm',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <dialog #dialog aria-labelledby="confirmation-title" aria-describedby="confirmation-detail" (close)="restore()">
      <form method="dialog" class="dialog-content">
        <h2 id="confirmation-title">{{ heading() }}</h2>
        <p id="confirmation-detail">{{ question() }}</p>
        <div class="dialog-actions">
          <button value="cancel" autofocus>{{ t.cancel }}</button>
          <button class="primary" value="confirm">{{ action() }}</button>
        </div>
      </form>
    </dialog>
  `,
})
export class Confirm {
  readonly heading = input.required<string>();
  readonly question = input.required<string>();
  readonly action = input.required<string>();
  readonly confirm = output<void>();
  protected readonly t = text;
  private readonly dialog = viewChild.required<ElementRef<HTMLDialogElement>>('dialog');
  private opener?: HTMLElement;

  show(opener: HTMLElement): void {
    const dialog = this.dialog().nativeElement;
    this.opener = opener;
    dialog.returnValue = '';
    dialog.showModal();
  }

  protected restore(): void {
    this.opener?.focus();
    if (this.dialog().nativeElement.returnValue === 'confirm') {
      this.confirm.emit();
    }
  }
}
