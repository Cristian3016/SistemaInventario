import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';

import { environment } from '../../../environments/environment';
import {
  DashboardResumenDto,
  AlertasInventarioDto,
  ReporteVentasDto,
  ReporteComprasDto
} from '../../models/dashboard.model';

@Injectable({
  providedIn: 'root'
})
export class DashboardService {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = environment.apiUrl;

  getResumen(): Observable<DashboardResumenDto> {
    return this.http.get<DashboardResumenDto>(
      `${this.apiUrl}/reportes/resumen`
    );
  }

  getAlertasInventario(): Observable<AlertasInventarioDto> {
    return this.http.get<AlertasInventarioDto>(
      `${this.apiUrl}/Productos/stock-bajo`
    );
  }

  getReporteVentas(
    desde: string,
    hasta: string
  ): Observable<ReporteVentasDto> {
    const params = new HttpParams()
      .set('desde', desde)
      .set('hasta', hasta);

    return this.http.get<ReporteVentasDto>(
      `${this.apiUrl}/reportes/ventas`,
      { params }
    );
  }

  getReporteCompras(
    desde: string,
    hasta: string
  ): Observable<ReporteComprasDto> {
    const params = new HttpParams()
      .set('desde', desde)
      .set('hasta', hasta);

    return this.http.get<ReporteComprasDto>(
      `${this.apiUrl}/reportes/compras`,
      { params }
    );
  }
}