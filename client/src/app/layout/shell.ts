import { Component, inject } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from '../core/auth/auth.service';

@Component({ selector: 'app-shell', standalone: true, imports: [RouterLink, RouterLinkActive, RouterOutlet], templateUrl: './shell.html', styleUrl: './shell.scss' })
export class ShellComponent {
  readonly auth = inject(AuthService);
  logout(): void { this.auth.logout(); }
}


