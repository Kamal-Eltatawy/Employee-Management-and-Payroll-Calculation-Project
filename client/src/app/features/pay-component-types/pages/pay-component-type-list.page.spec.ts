import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { problemDetailsInterceptor } from '../../../core/http/problem-details.interceptor';
import { PayComponentType } from '../data/pay-component-type.models';
import { PayComponentTypeListPage } from './pay-component-type-list.page';

describe('PayComponentTypeListPage', () => {
  let backend: HttpTestingController;

  const types: PayComponentType[] = [
    { id: 1, name: 'Housing', kind: 'Allowance', isTaxable: true, nameEn: 'Housing', nameAr: 'بدل سكن' },
    { id: 2, name: 'Loan', kind: 'Deduction', isTaxable: false, nameEn: 'Loan', nameAr: null },
  ];

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideRouter([]), provideHttpClient(withInterceptors([problemDetailsInterceptor])), provideHttpClientTesting()],
    });
    backend = TestBed.inject(HttpTestingController);
  });

  afterEach(() => backend.verify());

  async function open(): Promise<HTMLElement> {
    const fixture = TestBed.createComponent(PayComponentTypeListPage);
    fixture.detectChanges();
    TestBed.tick();
    backend.expectOne('/api/pay-component-types').flush(types);
    await fixture.whenStable();
    return fixture.nativeElement as HTMLElement;
  }

  it('lists each type with its kind and tax badges', async () => {
    const element = await open();
    const rows = element.querySelectorAll('tbody tr');

    expect(rows.length).toBe(2);
    expect(rows[0].textContent).toContain('Housing');
    expect(rows[0].textContent).toContain('بدل سكن');
    expect(rows[0].querySelector('.pill-info')?.textContent).toContain('Allowance');
    expect(rows[0].querySelector('.pill-success')?.textContent).toContain('Taxable');
    expect(rows[1].querySelector('.pill-warning')?.textContent).toContain('Deduction');
    expect(rows[1].querySelector('.pill-muted')?.textContent).toContain('Not taxable');
    expect(rows[1].querySelector('a[href="/pay-items/2/edit"]')).not.toBeNull();
  });

  it('says when there are no types', async () => {
    const fixture = TestBed.createComponent(PayComponentTypeListPage);
    fixture.detectChanges();
    TestBed.tick();
    backend.expectOne('/api/pay-component-types').flush([]);
    await fixture.whenStable();

    expect((fixture.nativeElement as HTMLElement).textContent).toContain('No pay items yet.');
  });
});
