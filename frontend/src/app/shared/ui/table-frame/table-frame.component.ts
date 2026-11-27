import { Component, input } from '@angular/core';

@Component({
  selector: 'sur-table-frame',
  templateUrl: './table-frame.component.html',
  styleUrl: './table-frame.component.scss',
})
export class TableFrameComponent {
  readonly label = input.required<string>();
}
