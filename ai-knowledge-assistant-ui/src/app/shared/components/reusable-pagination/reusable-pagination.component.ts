import { Component, EventEmitter, input, Output } from '@angular/core';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';

@Component({
  selector: 'app-reusable-pagination',
  imports: [MatPaginatorModule],
  templateUrl: './reusable-pagination.component.html'
})
export class ReusablePaginationComponent {
  readonly length = input(0);
  readonly pageIndex = input(0);
  readonly pageSize = input(10);
  readonly pageSizeOptions = input<number[]>([5, 10, 20]);
  @Output() pageChanged = new EventEmitter<PageEvent>();
}
