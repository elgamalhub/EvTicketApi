import { Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { AuthService } from '../../core/auth.service';
import { Icon } from '../../shared/icon';

@Component({
  selector: 'app-not-found-page',
  imports: [RouterLink, Icon],
  templateUrl: './not-found-page.html',
  styleUrl: './not-found-page.scss'
})
export class NotFoundPage {
  private readonly auth = inject(AuthService);

  readonly isAuthenticated = this.auth.isAuthenticated;
  readonly isOrganizer = this.auth.isOrganizer;
}
