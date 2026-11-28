export interface LoginDto {
  nombreUsuario: string;
  password: string;
}

export interface LoginRespuestaDto {
  usuarioId: number | string;
  nombre: string;
  apellido: string;
  nombreUsuario: string;
  sesionId: string;
  accessToken: string;
  tokenType: string;
  fechaExpiracionAccessToken: string;
  fechaExpiracionSesion: string;
  fechaExpiracionInactividad: string;
  advertenciaInactividadSegundos: number | string;
}