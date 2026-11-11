import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { problemDetailsInterceptor } from '../../../core/http/problem-details.interceptor';
import { toIsoMonth } from '../../../shared/util/dates';
import { PayrollPolicy, TaxScheme } from '../data/payroll-policy.models';
import { PayrollPolicyListPage, policyInEffect } from './payroll-policy-list.page';

const currentMonth = toIsoMonth(new Date());
const nextYear = `${new Date().getFullYear() + 1}-01-01`;

const schemes: TaxScheme[] = [
  { id: 1, name: 'Standard', method: 'Progressive' },
  { id: 2, name: 'Flat Rate', method: 'Flat' },
];

const policy = (id: number, taxSchemeId: number, effectiveFrom: string, isModifiable: boolean): PayrollPolicy => ({
  id, taxSchemeId, effectiveFrom, isModifiable, insuranceRatePercent: 11, insuranceCap: null,
  taxBrackets: taxSchemeId === 1 ? [{ upperBound: 5000, ratePercent: 0 }, { upperBound: null, ratePercent: 20 }] : [{ upperBound: null, ratePercent: 15 }],
});

const policies = [
  policy(1, 1, '2020-01-01', false),
  policy(2, 1, '2021-06-01', false),
  policy(3, 1, nextYear, true),
  policy(4, 2, '2020-01-01', false),
];

describe('policyInEffect', () => {
  it('picks the latest policy starting on or before the month', () => {
    expect(policyInEffect(policies.filter((p) => p.taxSchemeId === 1), currentMonth)?.id).toBe(2);
    expect(policyInEffect(policies, '2019-12')).toBeNull();
  });
});

describe('PayrollPolicyListPage', () => {
  let backend: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideRouter([]), provideHttpClient(withInterceptors([problemDetailsInterceptor])), provideHttpClientTesting()],
    });
    backend = TestBed.inject(HttpTestingController);
  });

  afterEach(() => backend.verify());

  async function open(scheme?: string) {
    const fixture = TestBed.createComponent(PayrollPolicyListPage);
    if (scheme) {
      fixture.componentRef.setInput('scheme', scheme);
    }
    fixture.detectChanges();
    TestBed.tick();
    backend.expectOne('/api/tax-schemes').flush(schemes);
    backend.expectOne('/api/payroll-policies').flush(policies);
    await fixture.whenStable();
    return fixture.nativeElement as HTMLElement;
  }

  it('shows one tab per scheme and the first scheme newest first', async () => {
    const element = await open();

    const tabs = Array.from(element.querySelectorAll('.scheme-tabs button')).map((tab) => tab.textContent?.trim());
    expect(tabs).toEqual(['Standard', 'Flat Rate']);
    expect(element.querySelector('.scheme-tabs .active')?.textContent).toContain('Standard');

    const rows = element.querySelectorAll('tbody tr');
    expect(rows.length).toBe(3);
    expect(rows[0].textContent).toContain('Upcoming');
    expect(rows[1].textContent).toContain('June 2021');
    expect(rows[1].textContent).toContain('In effect this month');
  });

  it('offers edit and delete only on modifiable policies', async () => {
    const element = await open();
    const rows = element.querySelectorAll('tbody tr');

    expect(rows[0].querySelector('button.text-danger')).not.toBeNull();
    expect(rows[1].querySelector('button.text-danger')).toBeNull();
    expect(rows[1].textContent).toContain('Read-only');
    expect(rows[1].querySelector('a[title="View"]')).not.toBeNull();
  });

  it('opens the scheme from the URL', async () => {
    const element = await open('2');

    expect(element.querySelector('.scheme-tabs .active')?.textContent).toContain('Flat Rate');
    expect(element.querySelectorAll('tbody tr').length).toBe(1);
    expect(element.textContent).toContain('15%');
  });
});
