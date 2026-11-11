import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, TestRequest, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { AuthService } from '../../../core/auth/auth.service';
import { problemDetailsInterceptor } from '../../../core/http/problem-details.interceptor';
import { NotificationService } from '../../../core/notifications/notification.service';
import { Payslip } from '../data/payslip.models';
import { PayslipPage } from './payslip.page';

describe('PayslipPage', () => {
  let backend: HttpTestingController;
  let fixture: ComponentFixture<PayslipPage>;

  const payslip: Payslip = {
    employeeId: 5, employeeName: 'Ali Hassan', month: '2026-11', policyId: 2, policyEffectiveFrom: '2026-01-01',
    taxSchemeName: 'Standard', taxMethod: 'Progressive', prorationFactor: 1, daysPaid: 30, daysInMonth: 30,
    baseSalary: 17000, allowances: [], grossPay: 17000, taxableIncome: 17000, incomeTax: 1000, socialInsurance: 500,
    deductions: [], otherDeductionsTotal: 0, netPay: 15500, isNetNegative: false,
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideRouter([]), provideHttpClient(withInterceptors([problemDetailsInterceptor])), provideHttpClientTesting()],
    });
    backend = TestBed.inject(HttpTestingController);
  });

  afterEach(() => backend.verify());

  async function open(id?: string): Promise<HTMLElement> {
    fixture = TestBed.createComponent(PayslipPage);
    if (id) {
      fixture.componentRef.setInput('id', id);
    }
    fixture.componentRef.setInput('month', '2026-11');
    // Not whenStable(): a loading resource is a pending task until its request is answered.
    fixture.detectChanges();
    TestBed.tick();
    return fixture.nativeElement as HTMLElement;
  }

  const request = (): TestRequest => backend.expectOne((r) => r.url === '/api/employees/5/payslip');

  const answer = async (status: number, body: object) => {
    request().flush(body, { status, statusText: 'Error' });
    await fixture.whenStable();
  };

  it('shows the payslip', async () => {
    const element = await open('5');
    request().flush(payslip);
    await fixture.whenStable();

    expect(element.textContent).toContain('Ali Hassan');
    expect(element.querySelector('.alert')).toBeNull();
  });

  it('shows a 4xx problem inline', async () => {
    const element = await open('5');
    await answer(404, { title: 'Not found.', detail: 'Employee 5 was not found.' });

    expect(element.querySelector('.alert-danger')?.textContent).toContain('Employee 5 was not found.');
  });

  it('shows a 422 as a warning', async () => {
    const element = await open('5');
    await answer(422, { title: 'Not payable.', detail: 'The employee is inactive.' });

    expect(element.querySelector('.alert-warning')?.textContent).toContain('The employee is inactive.');
    expect(element.querySelector('.alert-danger')).toBeNull();
  });

  it('leaves a 5xx to the toast: no second message on the page', async () => {
    const element = await open('5');
    await answer(500, { title: 'Server error.' });

    expect(element.querySelector('.alert')).toBeNull();
    expect(TestBed.inject(NotificationService).toasts().map((toast) => toast.message)).toEqual(['Server error.']);
  });

  it('explains a missing employee record instead of requesting /api/employees/0/payslip', async () => {
    TestBed.inject(AuthService).startSession({
      accessToken: 't', expiresAtUtc: '2999-01-01T00:00:00Z', roles: ['Employee'], employeeId: null, email: 'new@demo.local',
    });
    const element = await open();

    backend.expectNone(() => true);
    expect(element.textContent).toContain('not linked to an employee record');
    expect(element.textContent).not.toContain('Loading');
  });
});
