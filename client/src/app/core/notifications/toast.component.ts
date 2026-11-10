import { Component, inject } from '@angular/core';
import { NotificationService } from './notification.service';

@Component({
  selector: 'app-toasts',
  template: `
    <div class="toast-stack" aria-live="polite">
      @for (toast of notifications.toasts(); track toast.id) {
        <div class="alert d-flex align-items-start gap-2 shadow-sm mb-2"
             [class.alert-success]="toast.kind === 'success'"
             [class.alert-danger]="toast.kind === 'error'"
             role="status">
          <span class="flex-grow-1">{{ toast.message }}</span>
          <button type="button" class="btn-close-text" (click)="notifications.dismiss(toast.id)" aria-label="Dismiss">×</button>
        </div>
      }
    </div>
  `,
  styles: `
    .toast-stack { position: fixed; right: 1rem; bottom: 1rem; z-index: 1080; width: min(24rem, calc(100vw - 2rem)); }
    .btn-close-text { border: 0; background: none; font-size: 1.25rem; line-height: 1; opacity: .6; }
    .btn-close-text:hover { opacity: 1; }
  `,
})
export class ToastComponent {
  protected readonly notifications = inject(NotificationService);
}
