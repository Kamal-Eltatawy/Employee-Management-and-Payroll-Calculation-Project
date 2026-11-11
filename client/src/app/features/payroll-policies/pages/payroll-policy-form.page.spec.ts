import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { problemDetailsInterceptor } from '../../../core/http/problem-details.interceptor';
import { TaxScheme } from '../data/payroll-policy.models';
import { PayrollPolicyFormPage } from './payroll-policy-form.page';

const schemes: TaxScheme[] = [
  { id: 1, name: 'Standard', method: 'Progressive' },
  { id: 2, name: 'Flat Rate', method: 'Flat' },
  { id: 3, name: 'Tax Exempt', method: 'Exempt' },
];

describe('PayrollPolicyFormPage', () => {
  let backend: HttpTestingController;
  let fixture: ComponentFixture<PayrollPolicyFormPage>;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideRouter([]), provideHttpClient(withInterceptors([problemDetailsInterceptor])), provideHttpClientTesting()],
    });
    backend = TestBed.inject(HttpTestingController);
  });

  afterEach(() => backend.verify());

  async function openNew(scheme: string): Promise<HTMLElement> {
    fixture = TestBed.createComponent(PayrollPolicyFormPage);
    fixture.componentRef.setInput('scheme', scheme);
    fixture.detectChanges();
    TestBed.tick();
    backend.expectOne('/api/tax-schemes').flush(schemes);
    await fixture.whenStable();
    return fixture.nativeElement as HTMLElement;
  }

  function type(element: HTMLElement, selector: string, value: string): void {
    const input = element.querySelector(selector) as HTMLInputElement;
    input.value = value;
    input.dispatchEvent(new Event('input'));
  }

  async function submit(element: HTMLElement): Promise<void> {
    (element.querySelector('form') as HTMLFormElement).dispatchEvent(new Event('submit'));
    await fixture.whenStable();
  }

  it('starts a flat scheme with one open row', async () => {
    const element = await openNew('2');
    expect(element.querySelectorAll('app-tax-brackets-editor .item').length).toBe(1);
    expect(element.querySelector('#upperBound0')).toBeNull();
  });

  it('hides the bracket editor for an exempt scheme', async () => {
    const element = await openNew('3');
    expect(element.querySelector('app-tax-brackets-editor input')).toBeNull();
    expect(element.textContent).toContain('tax exempt');
  });

  it('sends the first day of the month and maps server errors onto bracket rows', async () => {
    const element = await openNew('1');
    type(element, '#insuranceRatePercent', '11');
    type(element, '#ratePercent0', '10');
    await submit(element);

    const request = backend.expectOne({ method: 'POST', url: '/api/payroll-policies' });
    const body = request.request.body as { effectiveFrom: string; insuranceCap: unknown; taxBrackets: unknown };
    expect(body.effectiveFrom).toMatch(/^\d{4}-\d{2}-01$/);
    expect(body.insuranceCap).toBeNull();
    expect(body.taxBrackets).toEqual([{ upperBound: null, ratePercent: 10 }]);

    request.flush(
      { title: 'Validation failed.', errors: { 'taxBrackets[0].ratePercent': ['Rate must be at most 100.'], effectiveFrom: ['Date taken.'] } },
      { status: 400, statusText: 'Bad Request' },
    );
    await fixture.whenStable();

    expect(element.querySelector('app-tax-brackets-editor .item')?.textContent).toContain('Rate must be at most 100.');
    expect(element.textContent).toContain('Date taken.');
    expect(element.querySelector('.alert-danger')).toBeNull();
  });

  it('does not send a form whose brackets break the method rules', async () => {
    const element = await openNew('1');
    type(element, '#insuranceRatePercent', '11');
    type(element, '#upperBound0', '5000');
    type(element, '#ratePercent0', '10');
    await submit(element);

    backend.expectNone('/api/payroll-policies');
    expect(element.textContent).toContain('Leave the upper bound of the last bracket empty.');
  });

  it('shows a closed policy read-only', async () => {
    fixture = TestBed.createComponent(PayrollPolicyFormPage);
    fixture.componentRef.setInput('id', '7');
    fixture.detectChanges();
    TestBed.tick();
    backend.expectOne('/api/tax-schemes').flush(schemes);
    backend.expectOne('/api/payroll-policies/7').flush({
      id: 7, taxSchemeId: 1, effectiveFrom: '2020-01-01', insuranceRatePercent: 11, insuranceCap: 2000, isModifiable: false,
      taxBrackets: [{ upperBound: 5000, ratePercent: 0 }, { upperBound: null, ratePercent: 20 }],
    });
    await fixture.whenStable();
    const element = fixture.nativeElement as HTMLElement;

    expect(element.textContent).toContain('read-only');
    expect(element.querySelector('button[type=submit]')).toBeNull();
    expect(element.querySelector('app-tax-brackets-editor .remove')).toBeNull();
    expect((element.querySelector('#insuranceRatePercent') as HTMLInputElement).disabled).toBe(true);
    expect(element.querySelectorAll('app-tax-brackets-editor .item').length).toBe(2);
  });
});
