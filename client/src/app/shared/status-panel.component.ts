import { Component, input } from '@angular/core';

@Component({ selector: 'app-status-panel', standalone: true, template: `
  <section class="status-panel" [attr.role]="tone() === 'error' ? 'alert' : 'status'" aria-live="polite" aria-atomic="true" [class.status-panel--error]="tone() === 'error'" [class.status-panel--empty]="tone() === 'empty'">
    <h2>{{ title() }}</h2><p>{{ message() }}</p><ng-content />
  </section>
` })
export class StatusPanelComponent {
  readonly title = input.required<string>();
  readonly message = input.required<string>();
  readonly tone = input<'error' | 'empty' | 'info'>('info');
}

