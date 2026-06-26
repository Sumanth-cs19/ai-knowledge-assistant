import { Component, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';

@Component({
  selector: 'app-error-state',
  imports: [RouterLink, MatButtonModule, MatIconModule],
  templateUrl: './error-state.component.html',
  styleUrl: './error-state.component.scss'
})
export class ErrorStateComponent {
  readonly statusCode = input('500');
  readonly title = input('Something went wrong');
  readonly message = input('Please try again.');
  readonly actionLink = input('/dashboard');
  readonly actionText = input('Go to Dashboard');
}
