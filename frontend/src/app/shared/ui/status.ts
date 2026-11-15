import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { RiskStatus, statuses, text } from '../text';

@Component({
  selector: 'sur-status',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<span class="pill" [attr.data-status]="value()"><i aria-hidden="true"></i>{{ labels[value()] ?? t.unknown }}</span>`,
})
export class Status {
  readonly value = input.required<RiskStatus>();
  protected readonly labels = statuses;
  protected readonly t = text;
}

@Component({
  selector: 'sur-rating',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @let points = score();
    @if (points === null) { <span class="muted">{{ t.unrated }}</span> }
    @else { <span class="rating" [attr.data-band]="band(points)" [attr.aria-label]="t.rating + ': ' + points">{{ points }}</span> }
  `,
})
export class Rating {
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
