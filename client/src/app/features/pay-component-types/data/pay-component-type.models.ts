import { PayComponentKind, PayComponentType } from '../../employees/data/employee.models';

export type { PayComponentKind, PayComponentType };

/** The kind is chosen once, on create (S5). */
export interface CreatePayComponentTypeRequest {
  name: string;
  kind: PayComponentKind;
  isTaxable: boolean;
  nameAr: string | null;
}

/** The kind can change only while no employee uses the item (D71); the server answers 409 otherwise. */
export interface UpdatePayComponentTypeRequest {
  name: string;
  isTaxable: boolean;
  nameAr: string | null;
  kind: PayComponentKind;
}
