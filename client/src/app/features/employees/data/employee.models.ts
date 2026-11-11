export type EmployeeStatus = 'Active' | 'Inactive';
export type PayComponentKind = 'Allowance' | 'Deduction';
export type TaxMethod = 'Progressive' | 'Flat' | 'Exempt';
export type EmployeeSortBy = 'name' | 'hireDate' | 'salary';
export type SortDirection = 'asc' | 'desc';

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
}

export interface EmployeeListItem {
  id: number;
  fullName: string;
  email: string;
  jobTitle: string;
  departmentName: string;
  hireDate: string;
  baseMonthlySalary: number;
  status: EmployeeStatus;
}

export interface EmployeePayComponent {
  payComponentTypeId: number;
  name: string;
  kind: PayComponentKind;
  isTaxable: boolean;
  amount: number;
}

export interface Employee {
  id: number;
  fullName: string;
  email: string;
  jobTitle: string;
  departmentId: number;
  departmentName: string;
  taxSchemeId: number;
  taxSchemeName: string;
  hireDate: string;
  baseMonthlySalary: number;
  status: EmployeeStatus;
  payComponents: EmployeePayComponent[];
  rowVersion: string;
}

export interface PayComponentAssignment {
  payComponentTypeId: number;
  amount: number;
}

export interface EmployeeRequest {
  fullName: string;
  email: string;
  jobTitle: string;
  departmentId: number;
  taxSchemeId: number;
  hireDate: string;
  baseMonthlySalary: number;
  status: EmployeeStatus;
  payComponents: PayComponentAssignment[];
}

export interface UpdateEmployeeRequest extends EmployeeRequest {
  rowVersion: string;
}

export interface EmployeeQuery {
  search?: string;
  departmentId?: number;
  status?: EmployeeStatus;
  sortBy: EmployeeSortBy;
  sortDir: SortDirection;
  page: number;
  pageSize: number;
}

export interface Department {
  id: number;
  name: string;
}

export interface PayComponentType {
  id: number;
  /** In the request language (D58). */
  name: string;
  kind: PayComponentKind;
  isTaxable: boolean;
  nameEn: string;
  nameAr: string | null;
  /** Assigned to at least one employee (deleted ones too): its kind cannot change and it cannot be deleted (D71). */
  isInUse?: boolean;
}

export interface TaxScheme {
  id: number;
  name: string;
  method: TaxMethod;
}
