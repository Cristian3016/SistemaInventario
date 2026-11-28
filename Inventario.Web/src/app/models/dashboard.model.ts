export interface ResumenProductosDto {
  total: number;
  activos: number;
  inactivos: number;
  stockBajo: number;
  sinStock: number;
}

export interface ResumenClientesDto {
  activos: number;
}

export interface ResumenProveedoresDto {
  activos: number;
}

export interface ResumenVentasDto {
  cantidad: number;
  total: number;
  totalCobrado: number;
  saldoPendiente: number;
}

export interface ResumenComprasDto {
  cantidad: number;
  total: number;
}

export interface ResumenCajaDto {
  abierta: boolean;
  datos: unknown;
}

export interface DashboardResumenDto {
  productos: ResumenProductosDto;
  clientes: ResumenClientesDto;
  proveedores: ResumenProveedoresDto;
  ventas: ResumenVentasDto;
  compras: ResumenComprasDto;
  caja: ResumenCajaDto;
  fechaConsultaUtc: string;
}
export interface ProductoStockBajoDto {
  id: number;
  nombre: string;
  codigoSku: string | null;
  stock: number;
  stockMinimo: number;
}

export interface AlertasInventarioDto {
  mensaje: string;
  cantidadProductos?: number;
  productos: ProductoStockBajoDto[];
}
export interface MovimientoComercialPorDiaDto {
  fecha: string;
  cantidad: number;
  total: number;
}

export interface ReporteVentasDto {
  desde: string;
  hasta: string;
  cantidadVentas: number;
  totalVentas: number;
  promedioVenta: number;
  ventasPorDia: MovimientoComercialPorDiaDto[];
}

export interface ReporteComprasDto {
  desde: string;
  hasta: string;
  cantidadCompras: number;
  totalCompras: number;
  promedioCompra: number;
  comprasPorDia: MovimientoComercialPorDiaDto[];
}

export interface EvolucionComercialDto {
  fecha: string;
  ventas: number;
  compras: number;
}