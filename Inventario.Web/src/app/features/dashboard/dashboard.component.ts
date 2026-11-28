import {
  Component,
  inject,
  signal,
  computed,
  OnInit,
  DestroyRef
} from '@angular/core';
import { Router } from '@angular/router';
import { finalize, forkJoin } from 'rxjs';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';

import { AuthService } from '../../core/services/auth.service';
import { DashboardService } from '../../core/services/dashboard.service';

import {
  DashboardResumenDto,
  AlertasInventarioDto,
  EvolucionComercialDto
} from '../../models/dashboard.model';

import {
  PeriodoDashboard,
  calcularPeriodoDashboard
} from './dashboard-period';

@Component({
  selector: 'app-dashboard',
  imports: [],
  templateUrl: './dashboard.component.html',
  styleUrl: './dashboard.component.scss'
})
export class DashboardComponent implements OnInit {
  private readonly authService = inject(AuthService);
  private readonly dashboardService = inject(DashboardService);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);

  readonly currentUser = this.authService.currentUser;
  readonly isLoggingOut = signal(false);
  readonly errorMessage = signal('');
  readonly sidebarOpen = signal(true);

  readonly resumen = signal<DashboardResumenDto | null>(null);
  readonly isLoadingResumen = signal(false);
  readonly resumenError = signal('');

  readonly alertas = signal<AlertasInventarioDto | null>(null);
  readonly isLoadingAlertas = signal(false);
  readonly alertasError = signal('');

  readonly periodo = signal<PeriodoDashboard>(7);
  readonly evolucion = signal<EvolucionComercialDto[]>([]);
  readonly isLoadingEvolucion = signal(false);
  readonly evolucionError = signal('');
  readonly isRefreshing = computed(() =>
  this.isLoadingResumen() ||
  this.isLoadingAlertas() ||
  this.isLoadingEvolucion()
);
  readonly maximoEvolucion = computed(() =>
  Math.max(
    0,
    ...this.evolucion().map(item =>
      Math.max(item.ventas, item.compras)
    )
  )
);

readonly totalVentasPeriodo = computed(() =>
  this.evolucion().reduce(
    (total, item) => total + item.ventas,
    0
  )
);

readonly totalComprasPeriodo = computed(() =>
  this.evolucion().reduce(
    (total, item) => total + item.compras,
    0
  )
);

  readonly currencyFormatter = new Intl.NumberFormat('es-AR', {
    style: 'currency',
    currency: 'ARS',
    minimumFractionDigits: 2,
    maximumFractionDigits: 2
  });

  readonly currentDate = new Intl.DateTimeFormat('es-AR', {
    timeZone: 'America/Argentina/Cordoba',
    weekday: 'long',
    day: 'numeric',
    month: 'long',
    year: 'numeric'
  }).format(new Date());

  ngOnInit(): void {
    this.loadResumen();
    this.loadAlertas();
    this.loadEvolucion();
  }

  toggleSidebar(): void {
    this.sidebarOpen.update(value => !value);
  }
  
  refreshDashboard(): void {
  if (this.isRefreshing()) {
    return;
  }

  this.loadResumen();
  this.loadAlertas();
  this.loadEvolucion();
}

  loadResumen(): void {
    if (this.isLoadingResumen()) {
      return;
    }

    this.isLoadingResumen.set(true);
    this.resumenError.set('');

    this.dashboardService.getResumen().pipe(
      takeUntilDestroyed(this.destroyRef),
      finalize(() => this.isLoadingResumen.set(false))
    ).subscribe({
      next: respuesta => {
        this.resumen.set(respuesta);
      },
      error: error => {
        this.resumenError.set(
          error.status === 403
            ? 'No tenés permisos para consultar el resumen.'
            : 'No se pudo cargar el resumen del Dashboard.'
        );
      }
    });
  }

  loadAlertas(): void {
    if (this.isLoadingAlertas()) {
      return;
    }

    this.isLoadingAlertas.set(true);
    this.alertasError.set('');

    this.dashboardService.getAlertasInventario().pipe(
      takeUntilDestroyed(this.destroyRef),
      finalize(() => this.isLoadingAlertas.set(false))
    ).subscribe({
      next: respuesta => {
        this.alertas.set(respuesta);
      },
      error: error => {
        this.alertasError.set(
          error.status === 403
            ? 'No tenés permisos para consultar las alertas.'
            : 'No se pudieron cargar las alertas de inventario.'
        );
      }
    });
  }

  cambiarPeriodo(periodo: PeriodoDashboard): void {
    if (this.isLoadingEvolucion() || this.periodo() === periodo) {
      return;
    }

    this.periodo.set(periodo);
    this.loadEvolucion();
  }

  loadEvolucion(): void {
    if (this.isLoadingEvolucion()) {
      return;
    }

    this.isLoadingEvolucion.set(true);
    this.evolucionError.set('');
    this.evolucion.set([]);

    const rango = calcularPeriodoDashboard(this.periodo());

    forkJoin({
      ventas: this.dashboardService.getReporteVentas(
        rango.desde,
        rango.hasta
      ),
      compras: this.dashboardService.getReporteCompras(
        rango.desde,
        rango.hasta
      )
    }).pipe(
      takeUntilDestroyed(this.destroyRef),
      finalize(() => this.isLoadingEvolucion.set(false))
    ).subscribe({
      next: ({ ventas, compras }) => {
        const ventasPorFecha = new Map(
          ventas.ventasPorDia.map(item => [
            item.fecha,
            item.total
          ])
        );

        const comprasPorFecha = new Map(
          compras.comprasPorDia.map(item => [
            item.fecha,
            item.total
          ])
        );

        const datos: EvolucionComercialDto[] =
          rango.fechas.map(fecha => ({
            fecha,
            ventas: ventasPorFecha.get(fecha) ?? 0,
            compras: comprasPorFecha.get(fecha) ?? 0
          }));

        this.evolucion.set(datos);
      },
      error: error => {
        this.evolucionError.set(
          error.status === 403
            ? 'No tenés permisos para consultar los reportes.'
            : 'No se pudo cargar la evolución comercial.'
        );
      }
    });
  }

  logout(): void {
    if (this.isLoggingOut()) {
      return;
    }

    this.errorMessage.set('');
    this.isLoggingOut.set(true);

    this.authService.logout().pipe(
      finalize(() => this.isLoggingOut.set(false))
    ).subscribe({
      next: () => {
        void this.router.navigate(['/login'], {
          replaceUrl: true
        });
      },
      error: () => {
        this.errorMessage.set(
          'No se pudo cerrar la sesión. Intentá nuevamente.'
        );
      }
    });
  }
}