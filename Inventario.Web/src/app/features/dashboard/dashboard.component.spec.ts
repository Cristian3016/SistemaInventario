import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { Observable, of, Subject, throwError } from 'rxjs';
import { describe, it, expect, beforeEach, vi } from 'vitest';

import { DashboardComponent } from './dashboard.component';
import { AuthService } from '../../core/services/auth.service';
import { DashboardService } from '../../core/services/dashboard.service';

import {
  DashboardResumenDto,
  AlertasInventarioDto,
  ReporteVentasDto,
  ReporteComprasDto
} from '../../models/dashboard.model';

describe('DashboardComponent', () => {
  let component: DashboardComponent;
  let fixture: ComponentFixture<DashboardComponent>;

  const resumenMock: DashboardResumenDto = {
    productos: {
      total: 2,
      activos: 2,
      inactivos: 0,
      stockBajo: 0,
      sinStock: 0
    },
    clientes: {
      activos: 1
    },
    proveedores: {
      activos: 2
    },
    ventas: {
      cantidad: 3,
      total: 343000,
      totalCobrado: 245000,
      saldoPendiente: 98000
    },
    compras: {
      cantidad: 2,
      total: 660000
    },
    caja: {
      abierta: false,
      datos: null
    },
    fechaConsultaUtc: '2026-11-28T01:25:13Z'
  };

  const alertasMock: AlertasInventarioDto = {
    mensaje: 'No hay productos con stock bajo.',
    productos: []
  };

  const authServiceMock = {
    currentUser: signal({
      usuarioId: 1,
      nombre: 'Usuario',
      apellido: 'Prueba',
      nombreUsuario: 'usuarioPrueba'
    }),
    logout: vi.fn((): Observable<void> => of(undefined))
  };

  const routerMock = {
    navigate: vi.fn().mockResolvedValue(true)
  };

  const dashboardServiceMock = {
    getResumen: vi.fn(
      (): Observable<DashboardResumenDto> => of(resumenMock)
    ),

    getAlertasInventario: vi.fn(
      (): Observable<AlertasInventarioDto> => of(alertasMock)
    ),

    getReporteVentas: vi.fn(
      (desde: string, hasta: string): Observable<ReporteVentasDto> =>
        of({
          desde,
          hasta,
          cantidadVentas: 3,
          totalVentas: 343000,
          promedioVenta: 114333.33,
          ventasPorDia: [
            {
              fecha: desde.slice(0, 10),
              cantidad: 3,
              total: 343000
            }
          ]
        })
    ),

    getReporteCompras: vi.fn(
      (desde: string, hasta: string): Observable<ReporteComprasDto> =>
        of({
          desde,
          hasta,
          cantidadCompras: 2,
          totalCompras: 660000,
          promedioCompra: 330000,
          comprasPorDia: [
            {
              fecha: desde.slice(0, 10),
              cantidad: 2,
              total: 660000
            }
          ]
        })
    )
  };

  beforeEach(async () => {
    vi.clearAllMocks();

    await TestBed.configureTestingModule({
      imports: [DashboardComponent],
      providers: [
        {
          provide: AuthService,
          useValue: authServiceMock
        },
        {
          provide: DashboardService,
          useValue: dashboardServiceMock
        },
        {
          provide: Router,
          useValue: routerMock
        }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(DashboardComponent);
    component = fixture.componentInstance;
  });

  it('debe crear el componente', () => {
    fixture.detectChanges();

    expect(component).toBeTruthy();
  });

  it('debe mostrar el nombre del usuario autenticado', () => {
    fixture.detectChanges();

    const elemento = fixture.nativeElement as HTMLElement;

    expect(elemento.textContent).toContain('Usuario Prueba');
    expect(elemento.textContent).toContain('usuarioPrueba');
  });

  it('debe mostrar una única sección de bienvenida', () => {
    fixture.detectChanges();

    const elemento = fixture.nativeElement as HTMLElement;

    expect(
      elemento.querySelectorAll('.welcome-section').length
    ).toBe(1);
  });

  it('debe cargar toda la información al inicializarse', () => {
    fixture.detectChanges();

    expect(
      dashboardServiceMock.getResumen
    ).toHaveBeenCalledTimes(1);

    expect(
      dashboardServiceMock.getAlertasInventario
    ).toHaveBeenCalledTimes(1);

    expect(
      dashboardServiceMock.getReporteVentas
    ).toHaveBeenCalledTimes(1);

    expect(
      dashboardServiceMock.getReporteCompras
    ).toHaveBeenCalledTimes(1);
  });

  it('debe almacenar el resumen general', () => {
    fixture.detectChanges();

    expect(component.resumen()).toEqual(resumenMock);
    expect(component.isLoadingResumen()).toBe(false);
    expect(component.resumenError()).toBe('');
  });

  it('debe mostrar las cuatro tarjetas y los paneles del Dashboard', () => {
    fixture.detectChanges();

    const elemento = fixture.nativeElement as HTMLElement;
    const tarjetas = elemento.querySelectorAll('.summary-card');

    expect(tarjetas.length).toBe(4);
    expect(tarjetas[0].textContent).toContain('2');

    expect(tarjetas[1].textContent).toContain(
      component.currencyFormatter.format(343000)
    );

    expect(tarjetas[2].textContent).toContain('0');
    expect(tarjetas[3].textContent).toContain('Cerrada');

    expect(
      elemento.querySelectorAll('.dashboard-panel').length
    ).toBe(2);

    expect(elemento.textContent).toContain('Resumen comercial');
    expect(elemento.textContent).toContain('Alertas de inventario');
  });

  it('debe mostrar el inventario sin alertas', () => {
    fixture.detectChanges();

    const elemento = fixture.nativeElement as HTMLElement;

    expect(elemento.textContent).toContain(
      'Inventario sin alertas'
    );

    expect(component.alertas()?.productos).toHaveLength(0);
  });

  it('debe mostrar los productos con stock bajo', () => {
    dashboardServiceMock.getAlertasInventario.mockReturnValueOnce(
      of({
        mensaje: 'Hay productos con stock bajo.',
        cantidadProductos: 1,
        productos: [
          {
            id: 1,
            nombre: 'Taladro eléctrico',
            codigoSku: 'TAL-001',
            stock: 2,
            stockMinimo: 5
          }
        ]
      })
    );

    fixture.detectChanges();

    const elemento = fixture.nativeElement as HTMLElement;

    expect(
      elemento.querySelectorAll('.inventory-alert-item').length
    ).toBe(1);

    expect(elemento.textContent).toContain('Taladro eléctrico');
    expect(elemento.textContent).toContain('TAL-001');
    expect(elemento.textContent).toContain('Stock: 2');
    expect(elemento.textContent).toContain('Mínimo: 5');
  });

  it('debe manejar errores al consultar el resumen', () => {
    dashboardServiceMock.getResumen.mockReturnValueOnce(
      throwError(() => ({ status: 500 }))
    );

    fixture.detectChanges();

    expect(component.resumenError()).toBe(
      'No se pudo cargar el resumen del Dashboard.'
    );

    expect(component.isLoadingResumen()).toBe(false);
    expect(component.resumen()).toBeNull();
  });

  it('debe manejar errores de permisos', () => {
    dashboardServiceMock.getResumen.mockReturnValueOnce(
      throwError(() => ({ status: 403 }))
    );

    fixture.detectChanges();

    expect(component.resumenError()).toBe(
      'No tenés permisos para consultar el resumen.'
    );
  });

  it('debe recuperar el resumen después de reintentar', () => {
    dashboardServiceMock.getResumen.mockReturnValueOnce(
      throwError(() => ({ status: 500 }))
    );

    fixture.detectChanges();

    component.loadResumen();

    expect(component.resumen()).toEqual(resumenMock);
    expect(component.resumenError()).toBe('');

    expect(
      dashboardServiceMock.getResumen
    ).toHaveBeenCalledTimes(2);
  });

  it('debe manejar los errores de alertas de forma independiente', () => {
    dashboardServiceMock.getAlertasInventario.mockReturnValueOnce(
      throwError(() => ({ status: 500 }))
    );

    fixture.detectChanges();

    expect(component.alertasError()).toBe(
      'No se pudieron cargar las alertas de inventario.'
    );

    expect(component.resumen()).toEqual(resumenMock);
    expect(component.resumenError()).toBe('');
  });

  it('debe generar inicialmente una evolución de 7 días', () => {
    fixture.detectChanges();

    expect(component.periodo()).toBe(7);
    expect(component.evolucion()).toHaveLength(7);
    expect(component.totalVentasPeriodo()).toBe(343000);
    expect(component.totalComprasPeriodo()).toBe(660000);
    expect(component.maximoEvolucion()).toBe(660000);
  });

  it('debe completar con cero los días sin movimientos', () => {
    fixture.detectChanges();

    const datos = component.evolucion();

    expect(datos[0].ventas).toBe(343000);
    expect(datos[0].compras).toBe(660000);

    expect(
      datos.slice(1).every(
        item => item.ventas === 0 && item.compras === 0
      )
    ).toBe(true);
  });

  it('debe cambiar el período estadístico a 15 días', () => {
    fixture.detectChanges();

    component.cambiarPeriodo(15);

    const fechas = component.evolucion();

    expect(component.periodo()).toBe(15);
    expect(fechas).toHaveLength(15);

    expect(
      dashboardServiceMock.getReporteVentas
    ).toHaveBeenLastCalledWith(
      `${fechas[0].fecha}T00:00:00-03:00`,
      `${fechas[14].fecha}T23:59:59.9999999-03:00`
    );
  });

  it('debe cambiar el período estadístico a 30 días', () => {
    fixture.detectChanges();

    component.cambiarPeriodo(30);

    expect(component.periodo()).toBe(30);
    expect(component.evolucion()).toHaveLength(30);
  });

  it('no debe consultar nuevamente el mismo período', () => {
    fixture.detectChanges();

    component.cambiarPeriodo(7);

    expect(
      dashboardServiceMock.getReporteVentas
    ).toHaveBeenCalledTimes(1);

    expect(
      dashboardServiceMock.getReporteCompras
    ).toHaveBeenCalledTimes(1);
  });

  it('debe manejar errores de los reportes estadísticos', () => {
    dashboardServiceMock.getReporteVentas.mockReturnValueOnce(
      throwError(() => ({ status: 500 }))
    );

    fixture.detectChanges();

    expect(component.evolucionError()).toBe(
      'No se pudo cargar la evolución comercial.'
    );

    expect(component.evolucion()).toHaveLength(0);
    expect(component.isLoadingEvolucion()).toBe(false);
  });

  it('debe actualizar todos los datos manualmente', () => {
    fixture.detectChanges();

    component.refreshDashboard();

    expect(
      dashboardServiceMock.getResumen
    ).toHaveBeenCalledTimes(2);

    expect(
      dashboardServiceMock.getAlertasInventario
    ).toHaveBeenCalledTimes(2);

    expect(
      dashboardServiceMock.getReporteVentas
    ).toHaveBeenCalledTimes(2);

    expect(
      dashboardServiceMock.getReporteCompras
    ).toHaveBeenCalledTimes(2);
  });

  it('debe conservar el período al actualizar los datos', () => {
    fixture.detectChanges();

    component.cambiarPeriodo(30);
    component.refreshDashboard();

    expect(component.periodo()).toBe(30);
    expect(component.evolucion()).toHaveLength(30);
  });

  it('debe impedir actualizaciones simultáneas', () => {
    const respuestaPendiente =
      new Subject<DashboardResumenDto>();

    dashboardServiceMock.getResumen.mockReturnValueOnce(
      respuestaPendiente.asObservable()
    );

    fixture.detectChanges();

    expect(component.isRefreshing()).toBe(true);

    component.refreshDashboard();

    expect(
      dashboardServiceMock.getResumen
    ).toHaveBeenCalledTimes(1);

    respuestaPendiente.next(resumenMock);
    respuestaPendiente.complete();

    expect(component.isRefreshing()).toBe(false);
  });

  it('debe ocultar y mostrar el menú lateral', () => {
    fixture.detectChanges();

    expect(component.sidebarOpen()).toBe(true);

    component.toggleSidebar();

    expect(component.sidebarOpen()).toBe(false);

    component.toggleSidebar();

    expect(component.sidebarOpen()).toBe(true);
  });

  it('debe cerrar sesión y navegar al login', () => {
    fixture.detectChanges();

    component.logout();

    expect(authServiceMock.logout).toHaveBeenCalledTimes(1);

    expect(routerMock.navigate).toHaveBeenCalledWith(
      ['/login'],
      { replaceUrl: true }
    );

    expect(component.isLoggingOut()).toBe(false);
    expect(component.errorMessage()).toBe('');
  });

  it('debe manejar errores al cerrar sesión', () => {
    authServiceMock.logout.mockReturnValueOnce(
      throwError(() => ({ status: 500 }))
    );

    fixture.detectChanges();

    component.logout();

    expect(component.errorMessage()).toBe(
      'No se pudo cerrar la sesión. Intentá nuevamente.'
    );

    expect(component.isLoggingOut()).toBe(false);
    expect(routerMock.navigate).not.toHaveBeenCalled();
  });

  it('debe impedir solicitudes duplicadas de cierre de sesión', () => {
    const respuestaPendiente = new Subject<void>();

    authServiceMock.logout.mockReturnValueOnce(
      respuestaPendiente.asObservable()
    );

    fixture.detectChanges();

    component.logout();
    component.logout();

    expect(authServiceMock.logout).toHaveBeenCalledTimes(1);
    expect(component.isLoggingOut()).toBe(true);

    respuestaPendiente.next(undefined);
    respuestaPendiente.complete();

    expect(component.isLoggingOut()).toBe(false);
  });
});