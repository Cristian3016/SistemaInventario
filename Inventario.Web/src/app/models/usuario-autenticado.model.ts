export interface RolUsuarioDto {
  id: number;
  nombre: string;
}

export interface PermisoUsuarioDto {
  id: number;
  nombre: string;
  codigo: string;
}

export interface SesionUsuarioDto {
  id: string;
  fechaCreacion: string;
  ultimaActividad: string;
  fechaExpiracion: string;
  fechaExpiracionInactividad: string;
  advertenciaInactividadSegundos: number;
}

export interface UsuarioAutenticadoDto {
  id: number;
  nombre: string;
  apellido: string;
  nombreUsuario: string;
  email: string | null;
  activo: boolean;
  fechaCreacion: string;
  ultimoAcceso: string | null;
  cantidadRoles: number;
  roles: RolUsuarioDto[];
  cantidadPermisos: number;
  permisos: PermisoUsuarioDto[];
  sesion: SesionUsuarioDto;
}