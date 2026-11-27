import { Component, input } from '@angular/core';
import { text } from '../../text';

@Component({
  selector: 'sur-rating',
  templateUrl: './rating.component.html',
})
export class RatingComponent {
  readonly score = input.required<number | null>();
  protected readonly t = text;

  protected readonly band = scoreBand;
}

/** Одинаковая цветовая шкала для карточек и матрицы. */
export function scoreBand(score: number): string {
  if (score === 0) {
    return 'zero';
  }
  if (score <= 5) {
    return 'low';
  }
  if (score <= 12) {
    return 'medium';
  }
  return score <= 19 ? 'high' : 'critical';
}
