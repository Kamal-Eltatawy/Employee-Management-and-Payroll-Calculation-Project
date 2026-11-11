import { Component, inject } from '@angular/core';
import { NotificationService } from './notification.service';

@Component({
  selector: 'app-toasts',
  template: `
    <div class="toast-stack">
      @for (toast of notifications.toasts(); track toast.id) {
        <!-- Errors interrupt the screen reader (assertive); success and info wait their turn (polite). -->
        <div class="toast-item" [class.error]="toast.kind === 'error'" [class.info]="toast.kind === 'info'"
             [attr.role]="toast.kind === 'error' ? 'alert' : 'status'"
             [attr.aria-live]="toast.kind === 'error' ? 'assertive' : 'polite'" aria-atomic="true">
          <span class="flex-grow-1">{{ toast.message }}</span>
          <button type="button" class="close" (click)="notifications.dismiss(toast.id)"
                  aria-label="Dismiss" i18n-aria-label="@@toast.dismiss">×</button>
        </div>
      }
    </div>
  `,
  styles: `
    .toast-stack { position: fixed; inset-inline-end: 1.25rem; inset-block-end: 1.25rem; z-index: 1080;
      width: min(24rem, calc(100vw - 2rem)); display: flex; flex-direction: column; gap: .5rem; }
    .toast-item { display: flex; align-items: flex-start; gap: .75rem; padding: .85rem 1rem; border-radius: .6rem;
      background: #ecfdf3; color: #054f31; border: 1px solid #abefc6; box-shadow: var(--app-card-shadow); }
    .toast-item.error { background: #fef3f2; color: #7a271a; border-color: #fecdca; }
    .toast-item.info { background: #eff8ff; color: #194185; border-color: #b2ddff; }
    .close { border: 0; background: none; font-size: 1.25rem; line-height: 1; opacity: .6; color: inherit; }
    .close:hover { opacity: 1; }
    @media print { .toast-stack { display: none; } }
  `,
})
export class ToastComponent {
  protected readonly notifications = inject(NotificationService);
}
