import { Component, computed, inject, viewChild } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { ApiError, inlineMessage, resourceApiError } from '../../../core/http/api-error';
import { NotificationService } from '../../../core/notifications/notification.service';
import { ConfirmDialogComponent } from '../../../shared/ui/confirm-dialog.component';
import { IconComponent } from '../../../shared/ui/icon.component';
import { PayComponentTypesApiService } from '../data/pay-component-types-api.service';
import { PayComponentType } from '../data/pay-component-type.models';

/** The allowance and deduction types HR can assign to employees. */
@Component({
  selector: 'app-pay-component-type-list-page',
  imports: [RouterLink, ConfirmDialogComponent, IconComponent],
  templateUrl: './pay-component-type-list.page.html',
  styleUrl: './pay-component-type-list.page.scss',
})
export class PayComponentTypeListPage {
  private readonly api = inject(PayComponentTypesApiService);
  private readonly notifications = inject(NotificationService);
  private readonly confirmDialog = viewChild.required(ConfirmDialogComponent);

  private readonly types = rxResource({ stream: () => this.api.list() });

  protected readonly items = computed(() => (this.types.hasValue() ? this.types.value() : []));
  protected readonly loading = this.types.isLoading;
  /** 4xx only: network failures and 5xx are already shown as a toast by problemDetailsInterceptor. */
  protected readonly error = computed(() => inlineMessage(resourceApiError(this.types.error())));

  protected async confirmDelete(type: PayComponentType): Promise<void> {
    const confirmed = await this.confirmDialog().open(
      $localize`:@@payItems.deleteConfirm:Delete ${type.name}:name:? This is only possible while no employee (including deleted ones) uses it.`,
    );
    if (!confirmed) {
      return;
    }

    this.api.delete(type.id).subscribe({
      next: () => {
        this.notifications.success($localize`:@@payItems.deleted:${type.name}:name: was deleted.`);
        this.types.reload();
      },
      error: (error: ApiError) => this.notifications.error(error.detail ?? error.title),
    });
  }
}
