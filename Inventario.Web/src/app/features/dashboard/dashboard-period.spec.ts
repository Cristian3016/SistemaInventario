import { describe, it, expect } from 'vitest';
import { calcularPeriodoDashboard } from './dashboard-period';

describe('calcularPeriodoDashboard', () => {
  const fechaReferencia = new Date('2026-11-27T15:00:00Z');

  it('debe calcular un período de 7 días', () => {
    const resultado = calcularPeriodoDashboard(7, fechaReferencia);

    expect(resultado.fechas).toHaveLength(7);
    expect(resultado.fechas[0]).toBe('2026-11-21');
    expect(resultado.fechas[6]).toBe('2026-11-27');
  });

  it('debe calcular un período de 15 días', () => {
    const resultado = calcularPeriodoDashboard(15, fechaReferencia);

    expect(resultado.fechas).toHaveLength(15);
    expect(resultado.fechas[0]).toBe('2026-11-13');
    expect(resultado.fechas[14]).toBe('2026-11-27');
  });

  it('debe calcular un período de 30 días', () => {
    const resultado = calcularPeriodoDashboard(30, fechaReferencia);

    expect(resultado.fechas).toHaveLength(30);
    expect(resultado.fechas[0]).toBe('2026-10-29');
    expect(resultado.fechas[29]).toBe('2026-11-27');
  });

  it('debe generar correctamente las fechas desde y hasta', () => {
    const resultado = calcularPeriodoDashboard(7, fechaReferencia);

    expect(resultado.desde).toBe(
      '2026-11-21T00:00:00-03:00'
    );

    expect(resultado.hasta).toBe(
      '2026-11-27T23:59:59.9999999-03:00'
    );
  });

  it('debe contemplar el cambio de año', () => {
    const fecha = new Date('2026-01-03T15:00:00Z');
    const resultado = calcularPeriodoDashboard(7, fecha);

    expect(resultado.fechas[0]).toBe('2025-12-28');
    expect(resultado.fechas[6]).toBe('2026-01-03');
  });

  it('debe contemplar los años bisiestos', () => {
    const fecha = new Date('2028-03-02T15:00:00Z');
    const resultado = calcularPeriodoDashboard(7, fecha);

    expect(resultado.fechas).toContain('2028-02-29');
    expect(resultado.fechas).toHaveLength(7);
  });

  it('debe respetar la zona horaria de Argentina', () => {
    const fecha = new Date('2026-11-28T01:00:00Z');
    const resultado = calcularPeriodoDashboard(7, fecha);

    expect(resultado.fechas[6]).toBe('2026-11-27');
  });

  it('no debe generar fechas duplicadas', () => {
    const resultado = calcularPeriodoDashboard(30, fechaReferencia);

    expect(new Set(resultado.fechas).size).toBe(30);
  });
});