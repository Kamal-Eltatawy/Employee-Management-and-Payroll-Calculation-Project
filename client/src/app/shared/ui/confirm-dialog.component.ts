import { Component, ElementRef, input, signal, viewChild } from '@angular/core';

/** A native <dialog>; open() resolves to true when the user confirms. */
@Component({
  selector: 'app-confirm-dialog',
  template: `
    <dialog #dialog class="confirm-dialog" (cancel)="close(false)">
      <h2 class="h5 mb-3">{{ title() }}</h2>
      <p class="mb-4">{{ message() }}</p>
      <div class="d-flex justify-content-end gap-2">
        <button type="button" class="btn btn-outline-secondary" (click)="close(false)">Cancel</button>
        <button type="button" class="btn btn-danger" (click)="close(true)">{{ confirmLabel() }}</button>
      </div>
    </dialog>
  `,
  styles: `
    .confirm-dialog { border: 0; border-radius: .5rem; padding: 1.5rem; width: min(28rem, calc(100vw - 2rem));
      box-shadow: 0 .5rem 2rem rgba(0, 0, 0, .2); }
    .confirm-dialog::backdrop { background: rgba(15, 23, 42, .45); }
  `,
})
export class ConfirmDialogComponent {
  readonly title = input('Are you sure?');
  readonly confirmLabel = input('Confirm');

  private readonly dialog = viewChild.required<ElementRef<HTMLDialogElement>>('dialog');
  private resolve?: (confirmed: boolean) => void;
  protected readonly message = signal('');

  open(message: string): Promise<boolean> {
    this.message.set(message);
    this.dialog().nativeElement.showModal();
    return new Promise((resolve) => (this.resolve = resolve));
  }

  protected close(confirmed: boolean): void {
    this.dialog().nativeElement.close();
    this.resolve?.(confirmed);
    this.resolve = undefined;
  }
}
