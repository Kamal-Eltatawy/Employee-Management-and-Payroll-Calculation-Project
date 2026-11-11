import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { problemDetailsInterceptor } from '../../../core/http/problem-details.interceptor';
import { PayComponentTypeFormPage } from './pay-component-type-form.page';

describe('PayComponentTypeFormPage', () => {
  let backend: HttpTestingController;
  let fixture: ComponentFixture<PayComponentTypeFormPage>;
  let element: HTMLElement;

  beforeEach(async () => {
    TestBed.configureTestingModule({
      providers: [provideRouter([{ path: 'pay-items', children: [] }]), provideHttpClient(withInterceptors([problemDetailsInterceptor])), provideHttpClientTesting()],
    });
    backend = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(PayComponentTypeFormPage);
    await fixture.whenStable();
    element = fixture.nativeElement as HTMLElement;
  });

  afterEach(() => backend.verify());

  function type(selector: string, value: string): void {
    const input = element.querySelector(selector) as HTMLInputElement;
    input.value = value;
    input.dispatchEvent(new Event('input'));
  }

  function chooseKind(kind: 'Allowance' | 'Deduction'): void {
    const select = element.querySelector('#kind') as HTMLSelectElement;
    select.value = kind;
    select.dispatchEvent(new Event('change'));
  }

  async function submit(): Promise<void> {
    (element.querySelector('form') as HTMLFormElement).dispatchEvent(new Event('submit'));
    await fixture.whenStable();
  }

  it('clears and locks "taxable" for a deduction, and sends it as false', async () => {
    const taxable = element.querySelector('#isTaxable') as HTMLInputElement;
    expect(taxable.checked).toBe(true);

    chooseKind('Deduction');
    await fixture.whenStable();
    expect(taxable.checked).toBe(false);
    expect(taxable.disabled).toBe(true);

    type('#name', ' Loan ');
    await submit();

    const request = backend.expectOne({ method: 'POST', url: '/api/pay-component-types' });
    expect(request.request.body).toEqual({ name: 'Loan', nameAr: null, kind: 'Deduction', isTaxable: false });
    request.flush({ id: 9, name: 'Loan', kind: 'Deduction', isTaxable: false, nameEn: 'Loan', nameAr: null });
  });

  it('unlocks "taxable" again for an allowance', async () => {
    chooseKind('Deduction');
    chooseKind('Allowance');
    await fixture.whenStable();

    expect((element.querySelector('#isTaxable') as HTMLInputElement).disabled).toBe(false);
  });

  it('shows a duplicate name (409) on the name field', async () => {
    type('#name', 'Housing');
    await submit();

    backend
      .expectOne({ method: 'POST', url: '/api/pay-component-types' })
      .flush(
        { title: 'Conflict.', detail: 'A pay component type named \'Housing\' already exists.', errors: { name: ['A pay component type named \'Housing\' already exists.'] } },
        { status: 409, statusText: 'Conflict' },
      );
    await fixture.whenStable();

    expect(element.querySelector('#name')?.classList).toContain('is-invalid');
    expect(element.textContent).toContain('already exists');
    expect(element.querySelector('.alert-danger')).toBeNull();
  });

  async function openForEdit(isInUse: boolean): Promise<void> {
    fixture = TestBed.createComponent(PayComponentTypeFormPage);
    fixture.componentRef.setInput('id', '5');
    await fixture.whenStable();
    element = fixture.nativeElement as HTMLElement;
    backend
      .expectOne({ method: 'GET', url: '/api/pay-component-types/5' })
      .flush({ id: 5, name: 'Uniform', kind: 'Allowance', isTaxable: true, nameEn: 'Uniform', nameAr: null, isInUse });
    await fixture.whenStable();
  }

  it('lets the kind change while no employee uses the item, and sends it (D71)', async () => {
    await openForEdit(false);
    const select = element.querySelector('#kind') as HTMLSelectElement;
    expect(select.disabled).toBe(false);
    expect(element.textContent).toContain('its kind can still change');

    chooseKind('Deduction');
    await submit();

    const request = backend.expectOne({ method: 'PUT', url: '/api/pay-component-types/5' });
    expect(request.request.body).toEqual({ name: 'Uniform', nameAr: null, isTaxable: false, kind: 'Deduction' });
    request.flush({ id: 5, name: 'Uniform', kind: 'Deduction', isTaxable: false, nameEn: 'Uniform', nameAr: null, isInUse: false });
  });

  it('locks the kind of an item in use and explains why (D71)', async () => {
    await openForEdit(true);

    expect((element.querySelector('#kind') as HTMLSelectElement).disabled).toBe(true);
    expect(element.textContent).toContain('Payslips are not stored');
  });

  it('requires a name', async () => {
    await submit();

    backend.expectNone('/api/pay-component-types');
    expect(element.textContent).toContain('Name is required.');
  });
});
