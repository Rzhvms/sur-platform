import { Component, ElementRef, input, output, viewChild } from '@angular/core';
import { text } from '../../text';

@Component({
  selector: 'sur-confirm',
  templateUrl: './confirm.component.html',
  styleUrl: './confirm.component.scss',
})
export class ConfirmComponent {
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
