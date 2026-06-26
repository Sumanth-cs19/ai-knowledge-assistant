import { Component, input } from '@angular/core';

@Component({
  selector: 'app-reusable-table',
  templateUrl: './reusable-table.component.html',
  styleUrl: './reusable-table.component.scss'
})
export class ReusableTableComponent<T extends Record<string, unknown>> {
  readonly columns = input<string[]>([]);
  readonly rows = input<T[]>([]);
}
