import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { text } from '../../text';

@Component({
  selector: 'sur-not-found',
  imports: [RouterLink],
  templateUrl: './not-found.component.html',
})
export class NotFoundComponent {
  protected readonly t = text;
}
