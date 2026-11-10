import { Injectable, signal } from '@angular/core';

export interface Toast {
  id: number;
  kind: 'success' | 'error';
  message: string;
}

@Injectable({ providedIn: 'root' })
export class NotificationService {
  private nextId = 1;
  private readonly items = signal<Toast[]>([]);

  readonly toasts = this.items.asReadonly();

  success(message: string): void {
    this.show('success', message);
  }

  error(message: string): void {
    this.show('error', message);
  }

  dismiss(id: number): void {
    this.items.update((toasts) => toasts.filter((toast) => toast.id !== id));
  }

  private show(kind: Toast['kind'], message: string): void {
    const toast: Toast = { id: this.nextId++, kind, message };
    this.items.update((toasts) => [...toasts, toast]);
    setTimeout(() => this.dismiss(toast.id), 5000);
  }
}
