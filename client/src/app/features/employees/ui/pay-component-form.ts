import { FormControl, FormGroup } from '@angular/forms';

export type PayComponentForm = FormGroup<{
  payComponentTypeId: FormControl<number>;
  amount: FormControl<number>;
}>;
