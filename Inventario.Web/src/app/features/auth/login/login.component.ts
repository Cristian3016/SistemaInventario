import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { finalize } from 'rxjs';

import { AuthService } from '../../../core/services/auth.service';
import { LoginDto } from '../../../models/auth.model';

@Component({
  selector: 'app-login',
  imports: [ReactiveFormsModule],
  templateUrl: './login.component.html',
  styleUrl: './login.component.scss'
})
export class LoginComponent {
  private readonly fb = inject(FormBuilder);
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);

  readonly showPassword = signal(false);
  readonly isLoading = signal(false);
  readonly errorMessage = signal('');
  readonly loginSuccess = signal(false);

  readonly loginForm = this.fb.nonNullable.group({
    username: ['', Validators.required],
    password: ['', Validators.required]
  });

  togglePassword(): void {
    this.showPassword.update(value => !value);
  }

  onSubmit(): void {
    if (this.isLoading()) {
      return;
    }

    this.errorMessage.set('');
    this.loginSuccess.set(false);

    if (this.loginForm.invalid) {
      this.loginForm.markAllAsTouched();
      return;
    }

    const credenciales: LoginDto = {
      nombreUsuario: this.loginForm.controls.username.value.trim(),
      password: this.loginForm.controls.password.value
    };

    this.isLoading.set(true);

    this.authService.login(credenciales)
      .pipe(
        finalize(() => this.isLoading.set(false))
      )
      .subscribe({
        next: () => {
          this.loginSuccess.set(true);
          this.showPassword.set(false);
          this.loginForm.reset();
          void this.router.navigate(['/dashboard']);
        },
        error: (error) => {
          if (error.status === 401) {
            this.errorMessage.set('Usuario o contraseña incorrectos.');
          } else if (error.status === 0) {
            this.errorMessage.set('No se pudo conectar con el servidor.');
          } else {
            this.errorMessage.set('No se pudo iniciar sesión. Intentá nuevamente.');
          }
        }
      });
  }
}