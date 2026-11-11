import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { problemDetailsInterceptor } from '../../../core/http/problem-details.interceptor';
import { Employee } from '../data/employee.models';
import { EmployeeFormPage } from './employee-form.page';

describe('EmployeeFormPage', () => {
  let backend: HttpTestingController;
  let fixture: ComponentFixture<EmployeeFormPage>;
  let element: HTMLElement;

  const employee: Employee = {
    id: 7, fullName: 'Sara Ahmed', email: 'sara@demo.local', jobTitle: 'Accountant', departmentId: 2, departmentName: 'Finance',
    taxSchemeId: 1, taxSchemeName: 'Standard', hireDate: '2023-02-15', baseMonthlySalary: 12000, status: 'Active',
    payComponents: [], rowVersion: 'v1',
  };

  const flushReferenceData = () => {
    backend.expectOne('/api/departments').flush([{ id: 2, name: 'Finance' }]);
    backend.expectOne('/api/tax-schemes').flush([{ id: 1, name: 'Standard', method: 'Progressive' }]);
    backend.expectOne('/api/pay-component-types').flush([]);
  };

  const submit = async () => {
    (element.querySelector('button[type=submit]') as HTMLButtonElement).click();
    await fixture.whenStable();
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([{ path: 'employees', children: [] }]),
        provideHttpClient(withInterceptors([problemDetailsInterceptor])),
        provideHttpClientTesting(),
      ],
    });
    backend = TestBed.inject(HttpTestingController);
  });

  afterEach(() => backend.verify());

  it('requires a department and a tax scheme on a new employee (no default scheme, D68)', async () => {
    fixture = TestBed.createComponent(EmployeeFormPage);
    element = fixture.nativeElement as HTMLElement;
    await fixture.whenStable();
    flushReferenceData();
    await fixture.whenStable();

    await submit();

    const fieldText = (id: string) => element.querySelector(`#${id}`)?.parentElement?.textContent ?? '';
    expect(fieldText('departmentId')).toContain('Please select a department.');
    expect(fieldText('taxSchemeId')).toContain('Please select a tax scheme.');
    expect(fieldText('departmentId')).not.toContain('greater than zero');
    expect((element.querySelector('#taxSchemeId') as HTMLSelectElement).selectedOptions[0].textContent).toContain('Select a tax scheme');
  });

  it('after a concurrency conflict reloads the latest version, rowVersion included', async () => {
    fixture = TestBed.createComponent(EmployeeFormPage);
    fixture.componentRef.setInput('id', '7');
    element = fixture.nativeElement as HTMLElement;
    await fixture.whenStable();
    flushReferenceData();
    backend.expectOne('/api/employees/7').flush(employee);
    await fixture.whenStable();

    await submit();
    backend
      .expectOne({ method: 'PUT', url: '/api/employees/7' })
      .flush({ title: 'Conflict.', detail: 'Someone else changed this employee.' }, { status: 409, statusText: 'Conflict' });
    await fixture.whenStable();

    const reload = Array.from(element.querySelectorAll('button')).find((b) => b.textContent?.includes('Reload latest'));
    expect(reload).toBeDefined();
    reload!.click();
    backend.expectOne('/api/employees/7').flush({ ...employee, jobTitle: 'Senior Accountant', rowVersion: 'v2' });
    await fixture.whenStable();

    expect((element.querySelector('#jobTitle') as HTMLInputElement).value).toBe('Senior Accountant');
    expect(element.textContent).not.toContain('Reload latest');

    await submit();
    const retry = backend.expectOne({ method: 'PUT', url: '/api/employees/7' });
    expect(retry.request.body.rowVersion).toBe('v2');
    retry.flush({ ...employee, rowVersion: 'v3' });
  });

  it('does not offer a reload when the 409 is about a field', async () => {
    fixture = TestBed.createComponent(EmployeeFormPage);
    fixture.componentRef.setInput('id', '7');
    element = fixture.nativeElement as HTMLElement;
    await fixture.whenStable();
    flushReferenceData();
    backend.expectOne('/api/employees/7').flush(employee);
    await fixture.whenStable();

    await submit();
    backend
      .expectOne({ method: 'PUT', url: '/api/employees/7' })
      .flush({ title: 'Conflict.', errors: { email: ['Email is already in use.'] } }, { status: 409, statusText: 'Conflict' });
    await fixture.whenStable();

    expect(element.textContent).toContain('Email is already in use.');
    expect(element.textContent).not.toContain('Reload latest');
  });
});
