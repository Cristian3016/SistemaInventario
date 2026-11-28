export type PeriodoDashboard = 7 | 15 | 30;

export interface RangoDashboard {
  desde: string;
  hasta: string;
  fechas: string[];
}

export function calcularPeriodoDashboard(
  dias: PeriodoDashboard,
  ahora: Date = new Date()
): RangoDashboard {
  const partes = new Intl.DateTimeFormat('es-AR', {
    timeZone: 'America/Argentina/Cordoba',
    year: 'numeric',
    month: '2-digit',
    day: '2-digit'
  }).formatToParts(ahora);

  const anio = Number(partes.find(p => p.type === 'year')?.value);
  const mes = Number(partes.find(p => p.type === 'month')?.value);
  const dia = Number(partes.find(p => p.type === 'day')?.value);

  const fin = new Date(Date.UTC(anio, mes - 1, dia));
  const inicio = new Date(fin);

  inicio.setUTCDate(inicio.getUTCDate() - (dias - 1));

  const fechas = Array.from({ length: dias }, (_, indice) => {
    const fecha = new Date(inicio);

    fecha.setUTCDate(fecha.getUTCDate() + indice);

    return fecha.toISOString().slice(0, 10);
  });

  return {
    desde: `${inicio.toISOString().slice(0, 10)}T00:00:00-03:00`,
    hasta: `${fin.toISOString().slice(0, 10)}T23:59:59.9999999-03:00`,
    fechas
  };
}