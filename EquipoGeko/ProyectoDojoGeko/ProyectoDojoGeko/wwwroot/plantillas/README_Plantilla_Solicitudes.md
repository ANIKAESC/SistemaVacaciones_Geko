# Plantilla de Carga Masiva de Solicitudes

## Descripción
Esta plantilla de Excel está diseñada para facilitar la carga masiva de solicitudes de vacaciones con sus respectivos detalles en el sistema Dojo Geko.

## Estructura de la Plantilla

### Hoja "Instrucciones"
Contiene las instrucciones detalladas para el uso correcto de la plantilla, incluyendo:
- Instrucciones generales de uso
- Estructura de datos de las tablas
- Campos obligatorios y opcionales
- Formatos requeridos

### Hoja "SolicitudEncabezado"
Contiene los datos principales de las solicitudes:

| Campo | Tipo | Obligatorio | Descripción |
|-------|------|-------------|-------------|
| IdSolicitud | Número | No | Se genera automáticamente |
| IdEmpleado | Número | Sí | ID del empleado solicitante |
| NombreEmpleado | Texto | Sí | Nombre completo del empleado |
| DiasSolicitadosTotal | Número | Sí | Suma total de días solicitados |
| FechaIngresoSolicitud | Fecha | Sí | Fecha de creación de la solicitud |
| SolicitudLider | Texto | Sí | Nombre del líder que autoriza |
| Observaciones | Texto | No | Comentarios adicionales |
| Estado | Número | Sí | 1=Pendiente, 2=Aprobada, 3=Rechazada |
| IdAutorizador | Número | No | ID del usuario autorizador |
| FechaAutorizacion | Fecha | No | Fecha de autorización |
| MotivoRechazo | Texto | No | Razón del rechazo |

### Hoja "SolicitudDetalle"
Contiene los períodos de vacaciones para cada solicitud:

| Campo | Tipo | Obligatorio | Descripción |
|-------|------|-------------|-------------|
| IdSolicitudDetalle | Número | No | Se genera automáticamente |
| IdSolicitud | Número | Sí | Debe coincidir con el encabezado |
| FechaInicio | Fecha | Sí | Fecha de inicio del período |
| FechaFin | Fecha | Sí | Fecha de fin del período |
| DiasHabilesTomados | Número | Sí | Días hábiles en este período |

## Instrucciones de Uso

### 1. Preparación de Datos
- Complete los datos en las hojas correspondientes
- Asegúrese de que los campos obligatorios estén llenos
- Use el formato DD/MM/YYYY para las fechas
- No modifique los nombres de las columnas

### 2. Relación Encabezado-Detalle
- Cada fila en "SolicitudEncabezado" representa una solicitud
- Las filas en "SolicitudDetalle" deben tener el mismo IdSolicitud que el encabezado
- Una solicitud puede tener múltiples períodos de vacaciones

### 3. Ejemplo de Datos
La plantilla incluye ejemplos de datos para facilitar la comprensión del formato requerido.

### 4. Validación
- Los días totales en el encabezado deben coincidir con la suma de días en los detalles
- Las fechas deben ser válidas
- Los IDs de empleados deben existir en el sistema

## Proceso de Carga

1. Complete la plantilla con sus datos
2. Guarde el archivo como .xlsx
3. Suba el archivo a través de la funcionalidad de carga masiva en el sistema
4. El sistema procesará automáticamente los datos y creará las solicitudes

## Consideraciones Importantes

- Los campos marcados con * son obligatorios
- El sistema validará la consistencia de los datos antes de la importación
- Se generará un reporte de errores si hay problemas con los datos
- Las solicitudes se crearán con estado "Pendiente" por defecto

## Soporte
Para cualquier duda o problema con la plantilla, contacte al equipo de desarrollo del proyecto Dojo Geko.
