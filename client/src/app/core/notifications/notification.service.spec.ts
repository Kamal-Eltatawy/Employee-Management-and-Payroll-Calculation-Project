import { TestBed } from '@angular/core/testing';
import { NotificationService, TOAST_AUTO_DISMISS_MS } from './notification.service';
import { ToastComponent } from './toast.component';

describe('NotificationService', () => {
  let notifications: NotificationService;

  beforeEach(() => {
    vi.useFakeTimers();
    notifications = TestBed.inject(NotificationService);
  });

  afterEach(() => vi.useRealTimers());

  it('closes success and info toasts after 5 seconds', () => {
    notifications.success('Saved.');
    notifications.info('Note.');

    vi.advanceTimersByTime(TOAST_AUTO_DISMISS_MS - 1);
    expect(notifications.toasts().length).toBe(2);

    vi.advanceTimersByTime(1);
    expect(notifications.toasts()).toEqual([]);
  });

  it('keeps error toasts until they are dismissed', () => {
    notifications.error('The server cannot be reached.');

    vi.advanceTimersByTime(60_000);
    const [toast] = notifications.toasts();
    expect(toast.kind).toBe('error');

    notifications.dismiss(toast.id);
    expect(notifications.toasts()).toEqual([]);
  });
});

describe('ToastComponent', () => {
  it('announces errors assertively and other toasts politely', async () => {
    const fixture = TestBed.createComponent(ToastComponent);
    const notifications = TestBed.inject(NotificationService);
    notifications.error('Failed.');
    notifications.success('Saved.');
    await fixture.whenStable();

    const items = Array.from(fixture.nativeElement.querySelectorAll('.toast-item')) as HTMLElement[];
    expect(items.map((item) => [item.getAttribute('role'), item.getAttribute('aria-live')])).toEqual([
      ['alert', 'assertive'],
      ['status', 'polite'],
    ]);
  });
});
