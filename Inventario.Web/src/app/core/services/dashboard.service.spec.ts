import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import {
  HttpTestingController,
  provideHttpClientTesting
} from '@angular/common/http/testing';
import {
  describe,
  it,
  expect,
  beforeEach,
  afterEach
} from 'vitest';

import { DashboardService } from './dashboard.service';
import { environment } from '../../../environments/environment';

import {
  DashboardResumenDto,
  AlertasInventarioDto,
  ReporteVentasDto,
  ReporteComprasDto
} from '../../models/dashboard.model';

describe('DashboardService', () => {
  let service: DashboardService;
  let httpMock: HttpTestingController;

  const apiUrl = environment.apiUrl;
  const desde = '2026-11-21T00:00:00-03:00';
  const hasta = '2026-11-27T23:59:59.9999999-03:00';

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        DashboardService,
        provideHttpClient(),
        provideHttpClientTesting()
      ]
    });

    service = TestBed.inject(DashboardService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('debe crear el servicio', () => {
    expect(service).toBeTruthy();
  });

  it('debe obtener el resumen general', () => {
    const respuesta: DashboardResumenDto = {
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

    service.getResumen().subscribe(datos => {
      expect(datos).toEqual(respuesta);
    });

    const request = httpMock.expectOne(
      `${apiUrl}/reportes/resumen`
    );

    expect(request.request.method).toBe('GET');

    request.flush(respuesta);
  });

  it('debe obtener las alertas de inventario', () => {
    const respuesta: AlertasInventarioDto = {
      mensaje: 'No hay productos con stock bajo.',
      productos: []
    };

    service.getAlertasInventario().subscribe(datos => {
      expect(datos).toEqual(respuesta);
    });

    const request = httpMock.expectOne(
      `${apiUrl}/Productos/stock-bajo`
    );

    expect(request.request.method).toBe('GET');

    request.flush(respuesta);
  });

  it('debe obtener el reporte de ventas con fechas', () => {
    const respuesta: ReporteVentasDto = {
      desde,
      hasta,
      cantidadVentas: 3,
      totalVentas: 343000,
      promedioVenta: 114333.33,
      ventasPorDia: [
        {
          fecha: '2026-11-23',
          cantidad: 2,
          total: 193000
        },
        {
          fecha: '2026-11-26',
          cantidad: 1,
          total: 150000
        }
      ]
    };

    service.getReporteVentas(desde, hasta).subscribe(datos => {
      expect(datos).toEqual(respuesta);
    });

    const request = httpMock.expectOne(
      req => req.url === `${apiUrl}/reportes/ventas`
    );

    expect(request.request.method).toBe('GET');
    expect(request.request.params.get('desde')).toBe(desde);
    expect(request.request.params.get('hasta')).toBe(hasta);

    request.flush(respuesta);
  });

  it('debe obtener el reporte de compras con fechas', () => {
    const respuesta: ReporteComprasDto = {
      desde,
      hasta,
      cantidadCompras: 2,
      totalCompras: 660000,
      promedioCompra: 330000,
      comprasPorDia: [
        {
          fecha: '2026-11-22',
          cantidad: 1,
          total: 160000
        },
        {
          fecha: '2026-11-26',
          cantidad: 1,
          total: 500000
        }
      ]
    };

    service.getReporteCompras(desde, hasta).subscribe(datos => {
      expect(datos).toEqual(respuesta);
    });

    const request = httpMock.expectOne(
      req => req.url === `${apiUrl}/reportes/compras`
    );

    expect(request.request.method).toBe('GET');
    expect(request.request.params.get('desde')).toBe(desde);
    expect(request.request.params.get('hasta')).toBe(hasta);

    request.flush(respuesta);
  });

  it('debe propagar los errores del backend', () => {
    let estado: number | undefined;

    service.getResumen().subscribe({
      next: () => {
        throw new Error('Se esperaba un error HTTP.');
      },
      error: error => {
        estado = error.status;
      }
    });

    const request = httpMock.expectOne(
      `${apiUrl}/reportes/resumen`
    );

    request.flush('Error interno del servidor', {
      status: 500,
      statusText: 'Internal Server Error'
    });

    expect(estado).toBe(500);
  });

  it('debe propagar los errores de permisos', () => {
    let estado: number | undefined;

    service.getAlertasInventario().subscribe({
      next: () => {
        throw new Error('Se esperaba un error HTTP.');
      },
      error: error => {
        estado = error.status;
      }
    });

    const request = httpMock.expectOne(
      `${apiUrl}/Productos/stock-bajo`
    );

    request.flush('Acceso denegado', {
      status: 403,
      statusText: 'Forbidden'
    });

    expect(estado).toBe(403);
  });
});