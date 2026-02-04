using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
// Usamos Mvc.Rendering para enviar tanto el Id del estado como el nombre del estado al ViewBag
using Microsoft.AspNetCore.Mvc.Rendering;
using ProyectoDojoGeko.Data;
using ProyectoDojoGeko.Filters;
using ProyectoDojoGeko.Models;
using ProyectoDojoGeko.Services;
using ProyectoDojoGeko.Services.Interfaces;
using ProyectoDojoGeko.Helper.Solicitudes;
using ClosedXML.Excel;
using Microsoft.Extensions.Configuration;
using System.Data.SqlClient;

namespace ProyectoDojoGeko.Controllers
{

    [AuthorizeSession]
    public class SolicitudesController : Controller
    {
        #region INYECCIÓN DE DEPENDENCIAS

        // Instanciamos el daoEmpleado
        private readonly daoEmpleadoWSAsync _daoEmpleado;
        private readonly daoSolicitudesAsync _daoSolicitud;
        private readonly ILoggingService _loggingService;
        private readonly IBitacoraService _bitacoraService;
        private readonly IEstadoService _estadoService;
        private readonly ISolicitudConverter _solicitudeConverter;
        private readonly daoFeriados _daoFeriados;
        private readonly daoEmpleadoEquipoWSAsync _daoEmpleadoEquipo;
        private readonly daoProyectoEquipoWSAsync _daoProyectoEquipo;
        private readonly ILogger<SolicitudesController> _logger;
        private readonly IConfiguration _configuration;
        private readonly EmailService _emailService;
        //private readonly daoEmpleadoEquipo _daoEmpleadoEquipo;

        /*=================================================   
		==   Service: PdfSolicitudService               == 
		=================================================*/
        /***Generación de PDF: Usa wkhtmltopdf con plantilla HTML que replica exactamente tu formato
		**Compresión Brotli: Reduce significativamente el tamaño de almacenamiento
		**Almacenamiento en DB: PDFs comprimidos se guardan en tabla `SolicitudPDF`
		**Control de Descarga: Permite descarga solo hasta que se apruebe la solicitud
		**Gestión Automática: Se crea el PDF al crear la solicitud y se restringe al aprobar*/
        private readonly IPdfSolicitudService _pdfService;

        public SolicitudesController(
            daoEmpleadoWSAsync daoEmpleado,
            daoSolicitudesAsync daoSolicitud,
            ILoggingService loggingService,
            IBitacoraService bitacoraService,
            IEstadoService estadoService,
            ISolicitudConverter solicitudConverter,
            daoFeriados daoFeriados,
            daoEmpleadoEquipoWSAsync daoEmpleadoEquipo,
            daoProyectoEquipoWSAsync daoProyectoEquipo,
            IPdfSolicitudService pdfService,
            ILogger<SolicitudesController> logger,
            IConfiguration configuration,
            EmailService emailService)
        {
            _daoEmpleado = daoEmpleado;
            _daoSolicitud = daoSolicitud;
            _loggingService = loggingService;
            _bitacoraService = bitacoraService;
            _estadoService = estadoService;
            _solicitudeConverter = solicitudConverter;
            _daoFeriados = daoFeriados;
            _daoEmpleadoEquipo = daoEmpleadoEquipo;
            _daoProyectoEquipo = daoProyectoEquipo;
            _pdfService = pdfService;
            _logger = logger;
            _configuration = configuration;
            _emailService = emailService;
        }

        #endregion

        #region Métodos de Validación de PDF

        /// <summary>
        /// Valida que un archivo sea un PDF válido verificando su firma mágica
        /// </summary>
        private async Task<bool> EsPdfValido(IFormFile archivo)
        {
            try
            {
                using var stream = archivo.OpenReadStream();
                var cabecera = new byte[5];
                await stream.ReadAsync(cabecera, 0, 5);
                
                // Los archivos PDF válidos comienzan con "%PDF-" (25 50 44 46 2D en hexadecimal)
                return cabecera[0] == 0x25 && // %
                       cabecera[1] == 0x50 && // P
                       cabecera[2] == 0x44 && // D
                       cabecera[3] == 0x46 && // F
                       cabecera[4] == 0x2D;   // -
            }
            catch
            {
                return false;
            }
        }

        #endregion

        // Método para obtener feriados y pasarlos como un diccionario de fecha y proporción
        private async Task<Dictionary<string, decimal>> GetFeriadosConProporcion()
        {
            var feriadosFijos = await _daoFeriados.ListarFeriadosFijos();
            var feriadosVariables = await _daoFeriados.ListarFeriadosVariables();

            var dates = new Dictionary<string, decimal>();
            var currentYear = DateTime.Now.Year;

            // Agrega feriados fijos para el año actual y el siguiente
            foreach (var feriado in feriadosFijos)
            {
                if (DateTime.DaysInMonth(currentYear, feriado.Mes) >= feriado.Dia)
                {
                    dates[new DateTime(currentYear, feriado.Mes, feriado.Dia).ToString("yyyy-MM-dd")] = feriado.ProporcionDia;
                }
                if (DateTime.DaysInMonth(currentYear + 1, feriado.Mes) >= feriado.Dia)
                {
                    dates[new DateTime(currentYear + 1, feriado.Mes, feriado.Dia).ToString("yyyy-MM-dd")] = feriado.ProporcionDia;
                }
            }

            // Agrega feriados variables
            foreach (var feriado in feriadosVariables)
            {
                dates[feriado.Fecha.ToString("yyyy-MM-dd")] = feriado.ProporcionDia;
            }

            return dates;
        }

        // Acción para rechazar una solicitud
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Rechazar(int idSolicitud, int idAutorizador, string? observaciones = null)
        {
            try
            {
                // Log para debug
                _logger.LogInformation($"🔴 Rechazar - IdSolicitud: {idSolicitud}, IdAutorizador: {idAutorizador}, Observaciones: {observaciones}");

                // Estado 6 = Rechazada
                bool resultado = await _daoSolicitud.ActualizarEstadoSolicitud(idSolicitud, 6, idAutorizador, observaciones);
                
                _logger.LogInformation($"🔴 Resultado del rechazo: {resultado}");
                
                if (resultado)
                {
                    // Actualizar días acumulados del empleado (devolver días)
                    try
                    {
                        var solicitud = await _daoSolicitud.ObtenerDetalleSolicitudAsync(idSolicitud);
                        if (solicitud != null && solicitud.Encabezado != null)
                        {
                            await _daoEmpleado.ActualizarDiasAcumuladosEmpleadoAsync(solicitud.Encabezado.IdEmpleado);
                            await _bitacoraService.RegistrarBitacoraAsync("Días Actualizados", $"Días devueltos al empleado {solicitud.Encabezado.IdEmpleado} después de rechazar solicitud {idSolicitud}");
                        }
                    }
                    catch (Exception diasEx)
                    {
                        _logger.LogWarning($"⚠️ No se pudieron actualizar días después de rechazar: {diasEx.Message}");
                    }

                    // Registrar en bitácora
                    await _bitacoraService.RegistrarBitacoraAsync("Rechazar", $"Solicitud {idSolicitud} rechazada por autorizador {idAutorizador}");

                    TempData["SuccessMessage"] = "La solicitud ha sido rechazada correctamente. El motivo ha sido registrado.";
                    return RedirectToAction("Detalle", new { id = idSolicitud });
                }
                
                TempData["MensajeError"] = "No se pudo rechazar la solicitud. Por favor, intente nuevamente.";
                return RedirectToAction("Detalle", new { id = idSolicitud });
            }
            catch (Exception ex)
            {
                _logger.LogError($"🔴 Error al rechazar: {ex.Message}");
                await RegistrarError("Rechazar solicitud", ex);
                TempData["MensajeError"] = $"Ocurrió un error al intentar rechazar la solicitud: {ex.Message}";
                return RedirectToAction("Detalle", new { id = idSolicitud });
            }
        }

        // Método para registrar errores en el log
        private async Task RegistrarError(string accion, Exception ex)
        {
            var usuario = HttpContext.Session.GetString("Usuario") ?? "Sistema";
            await _loggingService.RegistrarLogAsync(new LogViewModel
            {
                Accion = $"Error {accion}",
                Descripcion = $"Error al {accion} por {usuario}: {ex.Message}",
                Estado = false
            });
        }

        // Vista principal para ver todas las solicitudes
        // GET: SolicitudesController
        [AuthorizeRole("Empleado", "SuperAdministrador")]
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            try
            {
                // Validar IdEmpleado en sesión primero
                var idEmpleadoSesion = HttpContext.Session.GetInt32("IdEmpleado");
                if (idEmpleadoSesion == null || idEmpleadoSesion <= 0)
                {
                    await RegistrarError("acceder a la vista de solicitudes", new Exception("IdEmpleado no encontrado en sesión."));
                    return RedirectToAction("Index", "Login");
                }

                // Extraemos los datos del empleado desde la sesión
                var empleado = await _daoEmpleado.ObtenerEmpleadoPorIdAsync(idEmpleadoSesion.Value);

                if (empleado == null)
                {
                    await RegistrarError("acceder a la vista de solicitudes", new Exception("Empleado no encontrado en la sesión."));
                    return RedirectToAction("Index", "Login");
                }

                // Inicializar colecciones por si algo falla más adelante
                var solicitudes = new List<SolicitudViewModel>();
                try
                {
                    // Obtiene todas las solicitudes y sus detalles
                    solicitudes = await _daoSolicitud.ObtenerSolicitudesPorEmpleadoAsync(empleado.IdEmpleado);
                }
                catch (Exception exSol)
                {
                    await RegistrarError("obtener solicitudes del empleado", exSol);
                    TempData["Error"] = "No fue posible cargar las solicitudes. Se muestra la vista sin datos.";
                }

                // Obtiene todos los estados de solicitud para el dropdown
                try
                {
                    var estados = await _estadoService.ObtenerEstadosActivosSolicitudesAsync();
                    ViewBag.Estados = estados.Select(e => new SelectListItem
                    {
                        Value = e.IdEstadoSolicitud.ToString(),
                        Text = e.NombreEstado
                    }).ToList();
                }
                catch (Exception exEstados)
                {
                    await RegistrarError("obtener estados de solicitudes", exEstados);
                    ViewBag.Estados = new List<SelectListItem>();
                }

                // Calcular días disponibles correctamente
                ViewBag.DiasDisponibles = await CalcularDiasDisponiblesAsync(empleado.IdEmpleado);

                // Mandamos los feriados a la vista para deshabilitarlos en el calendario
                try
                {
                    ViewBag.Feriados = await GetFeriadosConProporcion();
                }
                catch (Exception exFeriados)
                {
                    await RegistrarError("obtener feriados", exFeriados);
                    ViewBag.Feriados = new Dictionary<DateTime, double>();
                }

                // Cargar autorizadores para el modal de cumpleaños
                try
                {
                    using (var connection = new SqlConnection(_configuration.GetConnectionString("DefaultConnection")))
                    {
                        await connection.OpenAsync();
                        var query = @"SELECT DISTINCT u.IdUsuario, e.NombresEmpleado, e.ApellidosEmpleado
                                     FROM Usuarios u
                                     INNER JOIN Empleados e ON u.FK_IdEmpleado = e.IdEmpleado
                                     INNER JOIN UsuariosRol ur ON u.IdUsuario = ur.FK_IdUsuario
                                     WHERE ur.FK_IdRol IN (3, 4, 5)
                                     AND e.IdEmpleado != @IdEmpleado";
                        
                        using (var command = new SqlCommand(query, connection))
                        {
                            command.Parameters.AddWithValue("@IdEmpleado", empleado.IdEmpleado);
                            using (var reader = await command.ExecuteReaderAsync())
                            {
                                var empleadosLimpio = new List<SelectListItem>();
                                while (await reader.ReadAsync())
                                {
                                    empleadosLimpio.Add(new SelectListItem
                                    {
                                        Value = reader.GetInt32(0).ToString(), // IdUsuario
                                        Text = $"{reader.GetString(1)} {reader.GetString(2)}"
                                    });
                                }
                                ViewBag.empleadosAutoriza = empleadosLimpio;
                            }
                        }
                    }
                }
                catch (Exception exAutorizadores)
                {
                    await RegistrarError("obtener autorizadores", exAutorizadores);
                    ViewBag.empleadosAutoriza = new List<SelectListItem>();
                }

                // Registramos la acción en la bitácora
                await _bitacoraService.RegistrarBitacoraAsync("Vista Solicitudes", "Acceso a la vista de solicitudes exitosamente");

                return View(solicitudes);

            }
            catch (Exception ex)
            {
                // Registra el error y muestra la vista vacía, evitando redirigir a Home para facilitar el diagnóstico
                await RegistrarError("acceder a la vista de solicitudes (general)", ex);
                TempData["Error"] = "Ocurrió un problema al cargar la vista de solicitudes.";
                ViewBag.Estados = ViewBag.Estados ?? new List<SelectListItem>();
                ViewBag.Feriados = ViewBag.Feriados ?? new Dictionary<DateTime, double>();
                return View(new List<SolicitudViewModel>());
            }

        }


        //Solicitudes RRHH
        [HttpGet]
        [AuthorizeRole("SuperAdministrador", "RRHH", "Autorizador", "TeamLider", "SubTeamLider")]
        public async Task<ActionResult> RecursosHumanos
        (
            string? nombreEmpresa = null,   // ej. "Digital Geko, S.A."
            string? estadoSolicitud = null, // ej. 'Ingresada', 'Autorizada', etc...
            string? nombresEmpleado = null, // ej. "AdminPrueba AdminPrueba"
            string? fechaInicio = null,     // ej. "2025-07-01"
            string? fechaFin = null     // ej. "2025-09-30"

        )
        {
            var solicitudes = new List<SolicitudEncabezadoViewModel>();

            try
            {
                var solicitudesResponse = await _daoSolicitud.ObtenerSolicitudEncabezadoCamposAsync(); // Todas las solicitudes (sin filtrar)

                // si el parametro es nulo no se aplica su filtro

                if (
                    !string.IsNullOrEmpty(fechaInicio) && DateTime.TryParse(fechaInicio, out var fechaDesde) &&
                    !string.IsNullOrEmpty(fechaFin) && DateTime.TryParse(fechaFin, out var fechaHasta)
                    )
                {
                    solicitudesResponse = solicitudesResponse.Where(solicitud =>
                    solicitud.FechaInicio!.Value.Date >= fechaDesde.Date &&
                    solicitud.FechaFin!.Value.Date <= fechaHasta).ToList();
                }

                if (!string.IsNullOrWhiteSpace(nombresEmpleado))
                    solicitudesResponse = solicitudesResponse.Where(solicitud => solicitud.NombreEmpleado.Equals(nombresEmpleado)).ToList();

                if (!string.IsNullOrWhiteSpace(estadoSolicitud) && int.TryParse(estadoSolicitud, out int estadoId))
                {
                    solicitudesResponse = solicitudesResponse.Where(solicitud => solicitud.Estado == estadoId).ToList();
                }

                //if (!string.IsNullOrWhiteSpace(estadoSolicitud))
                //	solicitudesResponse = solicitudesResponse.Where(solicitud => solicitud.NombreEstado.Equals(estadoSolicitud)).ToList();

                if (!string.IsNullOrWhiteSpace(nombreEmpresa))
                    solicitudesResponse = solicitudesResponse.Where(solicitud => solicitud.NombreEmpresa.Equals(nombreEmpresa)).ToList();

                // Convertimos SolicitudEncabezadoResult a SolicitudEncabezadoViewModel
                solicitudes = _solicitudeConverter.ConverListResultToViewModel(solicitudesResponse);

                //agregue

                var estados = await _estadoService.ObtenerEstadosActivosSolicitudesAsync();

                ViewBag.Estados = estados.Select(e => new SelectListItem
                {
                    Value = e.IdEstadoSolicitud.ToString(),
                    Text = e.NombreEstado
                }).ToList();

                //var estados = await _estadoService.ObtenerEstadosActivosSolicitudesAsync();


                //ViewBag.Estados = estados.Select(e => new SelectListItem
                //{
                //    Value = e.IdEstado.ToString(), // <-- Así lo espera el SelectListItem
                //    Text = e.Estado
                //}).ToList();


                await _bitacoraService.RegistrarBitacoraAsync("Vista RecursosHumanos", "Se obtubieron los encabezados de las solicitudes");
                return View(solicitudes);

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return View(solicitudes);

            }

        }

        // Vista principal para ver todas las solicitudes
        [HttpGet]
        [AuthorizeRole("SuperAdministrador", "RRHH")]
        public async Task<IActionResult> TodasLasSolicitudes()
        {
            try
            {
                // Obtener todas las solicitudes
                var solicitudes = await _daoSolicitud.ObtenerTodasLasSolicitudesAsync();
                
                // Obtener la lista de estados para mostrar los nombres
                var estados = await _estadoService.ObtenerEstadosActivosSolicitudesAsync();
                ViewBag.Estados = estados;
                
                return View(solicitudes);
            }
            catch (Exception ex)
            {
                await RegistrarError("obtener todas las solicitudes", ex);
                return BadRequest("Paso algún error :c");
            }
        }

        // Carga masiva de solicitudes desde Excel
        [HttpPost]
        [AuthorizeRole("SuperAdministrador", "RRHH")]
        public async Task<IActionResult> CargarExcel(IFormFile archivoExcel)
        {
            if (archivoExcel == null || archivoExcel.Length == 0)
                return BadRequest("Debe subir un archivo Excel válido");

            var solicitudes = new List<SolicitudViewModel>();

            using (var stream = archivoExcel.OpenReadStream())
            using (var workbook = new XLWorkbook(stream))
            {
                var wsEnc = workbook.Worksheet("SolicitudEncabezado");
                var wsDet = workbook.Worksheet("SolicitudDetalle");

                // Leer encabezados
                var encabezados = wsEnc.RangeUsed().RowsUsed().Skip(1); // omitir fila encabezado
                foreach (var row in encabezados)
                {
                    var solicitud = new SolicitudViewModel
                    {
                        Encabezado = new SolicitudEncabezadoViewModel
                        {
                            IdEmpleado = row.Cell(2).GetValue<int>(),
                            NombreEmpleado = row.Cell(3).GetValue<string>(),
                            DiasSolicitadosTotal = row.Cell(4).GetValue<decimal>(),
                            FechaIngresoSolicitud = row.Cell(5).GetDateTime(),
                            SolicitudLider = row.Cell(6).GetValue<string>(),
                            Observaciones = row.Cell(7).GetValue<string>(),
                            Estado = row.Cell(8).GetValue<int>()
                        },
                        Detalles = new List<SolicitudDetalleViewModel>()
                    };

                    // Relacionar con los detalles (buscar por IdSolicitud)
                    int idSolicitudExcel = row.Cell(1).GetValue<int>();

                    var detalles = wsDet.RangeUsed().RowsUsed().Skip(1)
                        .Where(r => r.Cell(2).GetValue<int>() == idSolicitudExcel);

                    foreach (var d in detalles)
                    {
                        solicitud.Detalles.Add(new SolicitudDetalleViewModel
                        {
                            FechaInicio = d.Cell(3).GetDateTime(),
                            FechaFin = d.Cell(4).GetDateTime(),
                            DiasHabilesTomados = d.Cell(5).GetValue<decimal>()
                        });
                    }

                    solicitudes.Add(solicitud);
                }
            }

            // Guardar en BD
            foreach (var sol in solicitudes)
            {
                await _daoSolicitud.InsertarSolicitudAsync(sol);
            }

            return Ok($"{solicitudes.Count} solicitudes procesadas y guardadas correctamente.");
        }


        // Vista principal para crear solicitudes
        // GET: SolicitudesController/Crear
        // Vista principal para crear solicitudes (formulario)
        [AuthorizeRole("SuperAdministrador", "Empleado")]
        [HttpGet]

        public async Task<IActionResult> Crear()
        {
            try
            {
                // 0. Actualizar días acumulados antes de mostrar la vista
                try
                {
                    await _daoEmpleado.ActualizarDiasAcumuladosEmpleadosAsync();
                }
                catch (Exception diasEx)
                {
                    // Si falla, logueamos pero continuamos
                    await _loggingService.RegistrarLogAsync(new LogViewModel
                    {
                        Accion = "Warning - Actualización Días",
                        Descripcion = $"No se pudieron actualizar los días acumulados al cargar vista Crear: {diasEx.Message}",
                        Estado = false
                    });
                }
                
                // 1. Obtener el objeto empleado completo, como en la vista Index.
                var idEmpleado = HttpContext.Session.GetInt32("IdEmpleado") ?? 0;
                var empleado = await _daoEmpleado.ObtenerEmpleadoPorIdAsync(idEmpleado);

                // Si no es empleado (ej: SuperAdministrador sin IdEmpleado), mostrar mensaje
                if (empleado == null)
                {
                    ViewBag.ErrorMessage = "No tienes un perfil de empleado asociado. Solo los empleados pueden crear solicitudes de vacaciones.";
                    ViewBag.DiasDisponibles = 0;
                    ViewBag.Feriados = await GetFeriadosConProporcion();
                    ViewBag.empleadosAutoriza = new List<SelectListItem>();

                    return View(new SolicitudViewModel
                    {
                        Encabezado = new SolicitudEncabezadoViewModel(),
                        Detalles = new List<SolicitudDetalleViewModel>()
                    });
                }

                // Unificar lógica: Preparar ViewBag y Sesión como en la vista Index.
                var nombreCompleto = $"{empleado.NombresEmpleado} {empleado.ApellidosEmpleado}";
                HttpContext.Session.SetString("NombreCompletoEmpleado", nombreCompleto);
                
                // Calcular días disponibles correctamente
                ViewBag.DiasDisponibles = await CalcularDiasDisponiblesAsync(empleado.IdEmpleado);

                // Mandamos los feriados a la vista para deshabilitarlos en el calendario
                ViewBag.Feriados = await GetFeriadosConProporcion();

                // Capturamos si el empleado está en algun equipo asignado
                var encuentraEquipo = await _daoProyectoEquipo.ObtenerEquipoPorEmpleadoAsync(empleado.IdEmpleado);

                // Obtenemos a los empleados completos para mostrarlos en un dropdown
                var empleados = await _daoEmpleado.ObtenerEmpleadoAsync();

                // Si no está en ningún equipo, mostramos advertencia
                if (encuentraEquipo == null || encuentraEquipo <= 0)
                {
                    ViewBag.AdvertenciaEquipo = "No estás asignado a ningún equipo. Por favor, contacta a RRHH para asignarte un equipo y que tu solicitud pueda ser revisada por un autorizador. O bien, busca tu mismo a tu lider.";

                    // Obtener todos los empleados y filtrar por rol
                    try
                    {
                        using (var connection = new SqlConnection(_configuration.GetConnectionString("DefaultConnection")))
                        {
                            await connection.OpenAsync();
                            var query = @"SELECT DISTINCT u.IdUsuario, e.NombresEmpleado, e.ApellidosEmpleado, 
                                                 ISNULL(STUFF((SELECT ', ' + r2.NombreRol
                                                        FROM UsuariosRol ur2
                                                        INNER JOIN Roles r2 ON ur2.FK_IdRol = r2.IdRol
                                                        WHERE ur2.FK_IdUsuario = u.IdUsuario
                                                        FOR XML PATH('')), 1, 2, ''), 'Sin Rol') AS Roles
                                         FROM Usuarios u
                                         INNER JOIN Empleados e ON u.FK_IdEmpleado = e.IdEmpleado
                                         LEFT JOIN UsuariosRol ur ON u.IdUsuario = ur.FK_IdUsuario
                                         WHERE (ur.FK_IdRol IN (3, 4, 5) OR ur.FK_IdRol IS NULL)
                                         AND e.IdEmpleado != @IdEmpleado
                                         AND EXISTS (SELECT 1 FROM UsuariosRol ur3 
                                                     WHERE ur3.FK_IdUsuario = u.IdUsuario 
                                                     AND ur3.FK_IdRol IN (3, 4, 5))";
                            
                            using (var command = new SqlCommand(query, connection))
                            {
                                command.Parameters.AddWithValue("@IdEmpleado", empleado.IdEmpleado);
                                using (var reader = await command.ExecuteReaderAsync())
                                {
                                    var empleadosLimpio = new List<SelectListItem>();
                                    while (await reader.ReadAsync())
                                    {
                                        empleadosLimpio.Add(new SelectListItem
                                        {
                                            Value = reader.GetInt32(0).ToString(), // IdUsuario
                                            Text = $"{reader.GetString(1)} {reader.GetString(2)}"
                                        });
                                    }
                                    ViewBag.empleadosAutoriza = empleadosLimpio;
                                }
                            }
                        }
                    }
                    catch (Exception sqlEx)
                    {
                        _logger.LogError(sqlEx, "Error al obtener empleados autorizadores");
                        // Si falla la consulta, mostrar lista vacía
                        ViewBag.empleadosAutoriza = new List<SelectListItem>();
                        ViewBag.AdvertenciaEquipo = "Error al cargar autorizadores. Por favor, contacta a RRHH.";
                    }
                }
                // Si está en un equipo, entonces buscamos a los integrantes del equipo
                else
                {

                    // Obtenemos a todos los empleados en su equipo
                    var empleadosEquipo = await _daoProyectoEquipo.ObtenerEmpleadosConRolesPorEquipoAsync(encuentraEquipo);

                    // Excluimos a los que no tengan el rol de TeamLider, SubTeamLider o Autorizador
                    var empleadosEquipoRol = empleadosEquipo.Where(e => e.Rol.IndexOf("TeamLider", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                    e.Rol.IndexOf("SubTeamLider", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                    e.Rol.IndexOf("Autorizador", StringComparison.OrdinalIgnoreCase) >= 0)
                                    .ToList();

                    // En caso de que el mismo empleado sea u tenga el rol de TeamLider o SubTeamLider, así también lo excluimos de la busqueda
                    var empleadosEquipoLimpio = empleadosEquipoRol
                        .Where(e => e.IdEmpleado != empleado.IdEmpleado)
                        .Select(e => new SelectListItem
                        {
                            Value = e.IdEmpleado.ToString(),
                            Text = $"{e.NombresEmpleado} {e.ApellidosEmpleado} - {e.Rol}"
                        })
                        .ToList();

                    ViewBag.EmpleadosAutorizaEquipo = empleadosEquipoLimpio;
                }

                await _bitacoraService.RegistrarBitacoraAsync("Vista Crear Solicitud", "Acceso a la vista de creación de solicitud.");

                var model = new SolicitudViewModel
                {
                    Encabezado = new SolicitudEncabezadoViewModel(),
                    Detalles = new List<SolicitudDetalleViewModel>()
                };


                return View(model);
            }
            catch (Exception ex)
            {
                await RegistrarError("Acceder a la vista de creación de solicitud", ex);
                TempData["ErrorMessage"] = "Ocurrió un error al cargar el formulario de solicitud. Por favor, intenta nuevamente.";
                return RedirectToAction("Index", "Solicitudes");
            }
        }


        /*=================================================   
		==   CREAR SOLICITUD CON GENERACIÓN DE PDF      == 
		=================================================*/
        /***Funcionalidad mejorada que incluye:
		**1. Creación de la solicitud en base de datos
		**2. Generación automática del PDF con wkhtmltopdf
		**3. Compresión y almacenamiento del PDF en la DB
		**4. Manejo de errores y logging detallado*/
        [AuthorizeRole("Empleado", "SuperAdministrador")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(5 * 1024 * 1024)] // 5MB limit
        public async Task<IActionResult> Crear(SolicitudViewModel solicitud)
        {

            try
            {
                // DEBUG: Log del IdAutorizador recibido
                _logger.LogInformation("🔍 IdAutorizador recibido en POST: {IdAutorizador}", solicitud.Encabezado.IdAutorizador);
                //// Handle PDF upload
                //if (DocumentoFirmado != null && DocumentoFirmado.Length > 0)
                //{
                //    // Validar tipo de contenido
                //    var allowedContentTypes = new[] { "application/pdf", "application/octet-stream" };
                //    if (!allowedContentTypes.Contains(DocumentoFirmado.ContentType.ToLower()) && 
                //        !DocumentoFirmado.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                //    {
                //        ModelState.AddModelError("DocumentoFirmado", 
                //            "Formato de archivo no válido. Solo se permiten archivos PDF.");
                //    }
                //    // Validar tamaño máximo (5MB)
                //    else if (DocumentoFirmado.Length > 5 * 1024 * 1024)
                //    {
                //        ModelState.AddModelError("DocumentoFirmado", 
                //            "El archivo es demasiado grande. El tamaño máximo permitido es de 5MB.");
                //    }
                //    // Validar contenido real del PDF
                //    else if (!await EsPdfValido(DocumentoFirmado))
                //    {
                //        ModelState.AddModelError("DocumentoFirmado", 
                //            "El archivo no es un PDF válido o está dañado.");
                //    }
                //    else
                //    {
                //        try
                //        {
                //            using var memoryStream = new MemoryStream();
                //            await DocumentoFirmado.CopyToAsync(memoryStream);
                //            // Validar que el PDF no esté vacío
                //            if (memoryStream.Length == 0)
                //            {
                //                ModelState.AddModelError("DocumentoFirmado", 
                //                    "El archivo PDF está vacío.");
                //            }
                //            else
                //            {
                //                solicitud.Encabezado.DocumentoFirmadoData = memoryStream.ToArray();
                //                solicitud.Encabezado.DocumentoContentType = "application/pdf"; // Forzamos el content type
                //            }
                //        }
                //        catch (Exception ex)
                //        {
                //            await _loggingService.RegistrarLogAsync(new LogViewModel
                //            {
                //                Accion = "Error al procesar PDF",
                //                Descripcion = $"Error al procesar el archivo PDF: {ex.Message}",
                //                Estado = false
                //            });
                //            ModelState.AddModelError("DocumentoFirmado", 
                //                "Ocurrió un error al procesar el archivo. Por favor, intente nuevamente.");
                //        }
                //    }
                //}
                //else
                //{
                //    ModelState.AddModelError("DocumentoFirmado", 
                //        "Es obligatorio adjuntar el documento firmado en formato PDF.");
                //}
                if (!ModelState.IsValid)
                {
                    // Debug: Log error de validación
                    await RegistrarError("Crear Solicitud", new Exception("Modelo no válido al crear solicitud de vacaciones."));
                    return View(solicitud);
                }

                // Recalcular los días hábiles en el backend
                var feriados = await GetFeriadosConProporcion();
                decimal totalDiasHabiles = 0;

                // Validar que el empleado tenga suficientes días disponibles
                var idEmpleado = HttpContext.Session.GetInt32("IdEmpleado");
                if (idEmpleado.HasValue)
                {
                    var empleado = await _daoEmpleado.ObtenerEmpleadoPorIdAsync(idEmpleado.Value);
                    
                    // Calcular días hábiles excluyendo el cumpleaños
                    foreach (var detalle in solicitud.Detalles)
                    {
                        totalDiasHabiles += CalcularDiasHabiles(detalle.FechaInicio, detalle.FechaFin, feriados, empleado?.FechaNacimiento);
                    }
                    if (empleado != null)
                    {
                        // 🎂 VALIDACIÓN ESPECIAL PARA CUMPLEAÑOS
                        if (totalDiasHabiles == 0)
                        {
                            _logger.LogInformation("🎂 Detectado totalDiasHabiles = 0. Verificando si es cumpleaños...");
                            
                            // Verificar si es SOLO el día de cumpleaños
                            if (EsSoloCumpleanos(solicitud.Detalles, empleado.FechaNacimiento))
                            {
                                _logger.LogInformation("✅ Confirmado: Es solicitud de cumpleaños. IdAutorizador: {IdAutorizador}", solicitud.Encabezado.IdAutorizador);
                                
                                // Permitir crear solicitud de cumpleaños con 0 días
                                solicitud.Encabezado.Observaciones = string.IsNullOrEmpty(solicitud.Encabezado.Observaciones)
                                    ? "🎂 Solicitud de día de cumpleaños - No se descuentan días de vacaciones"
                                    : solicitud.Encabezado.Observaciones + " | 🎂 Día de cumpleaños - No se descuentan días";
                                
                                TempData["InfoMessage"] = "🎂 Tu solicitud de cumpleaños ha sido creada. Este día no se descontará de tus vacaciones.";
                                
                                await _bitacoraService.RegistrarBitacoraAsync(
                                    "Solicitud Cumpleaños", 
                                    $"Solicitud de cumpleaños creada para {empleado.NombresEmpleado} {empleado.ApellidosEmpleado} - 0 días descontados. IdAutorizador: {solicitud.Encabezado.IdAutorizador}"
                                );
                                
                                // ✅ CONTINUAR con el flujo normal (asignar autorizador, crear solicitud, generar PDF)
                                // NO hacer return aquí, dejar que continúe
                            }
                            else
                            {
                                // No es cumpleaños, no permitir solicitud de 0 días
                                var mensajeError = "Debes solicitar al menos 1 día de vacaciones. Si es tu cumpleaños, asegúrate de seleccionar la fecha correcta.";
                                TempData["ErrorMessage"] = mensajeError;
                                ModelState.AddModelError("", mensajeError);
                                await RecargarViewBagCrearAsync();
                                return View(solicitud);
                            }
                        }
                        else if (totalDiasHabiles > empleado.DiasVacacionesAcumulados)
                        {
                            var mensajeError = $"No tienes suficientes días disponibles. Solicitaste {totalDiasHabiles} días pero solo tienes {empleado.DiasVacacionesAcumulados} días disponibles.";
                            TempData["ErrorMessage"] = mensajeError;
                            TempData.Keep("ErrorMessage"); // Mantener el mensaje para la próxima solicitud
                            await _bitacoraService.RegistrarBitacoraAsync("Validación de días", $"Intento de solicitud con {totalDiasHabiles} días cuando solo tiene {empleado.DiasVacacionesAcumulados} disponibles");
                            ModelState.AddModelError("", mensajeError); // Asegurar que el error se muestre en la validación del modelo
                            await RecargarViewBagCrearAsync();
                            return View(solicitud);
                        }
                    }
                    else
                    {
                        var errorMsg = "No se pudo verificar los días disponibles. Por favor, intente nuevamente.";
                        TempData["ErrorMessage"] = errorMsg;
                        TempData.Keep("ErrorMessage");
                        ModelState.AddModelError("", errorMsg);
                        await RecargarViewBagCrearAsync();
                        return View(solicitud);
                    }
                }
                else
                {
                    var errorMsg = "No se pudo identificar al empleado. Por favor, inicie sesión nuevamente.";
                    TempData["ErrorMessage"] = errorMsg;
                    TempData.Keep("ErrorMessage");
                    ModelState.AddModelError("", errorMsg);
                    await RecargarViewBagCrearAsync();
                    return View(solicitud);
                }

                //Buscamos el equipo del empleado para asignar el autorizador automáticamente
                var empleadoEquipo = await _daoEmpleadoEquipo.ObtenerEquipoAsync(idEmpleado.Value);

                int idEquipo = 0;

                if (empleadoEquipo != null)
                {
                    idEquipo = (int)empleadoEquipo.IdEquipo;
                }


                if (idEquipo != 0)
                {
                    // Si el usuario NO seleccionó autorizador, intentamos autoasignar
                    if (solicitud.Encabezado.IdAutorizador <= 0)
                    {
                        try
                        {
                            var empleadosEquipo = await _daoProyectoEquipo.ObtenerEmpleadosConRolesPorEquipoAsync(idEquipo);

                            if (empleadosEquipo == null || !empleadosEquipo.Any())
                            {
                                _logger.LogWarning("⚠️ No se encontraron empleados en el equipo. Recargando ViewBag...");
                                
                                await RecargarViewBagCrearAsync();
                                
                                var countEquipo = (ViewBag.EmpleadosAutorizaEquipo as List<SelectListItem>)?.Count ?? 0;
                                var countGeneral = (ViewBag.empleadosAutoriza as List<SelectListItem>)?.Count ?? 0;
                                
                                TempData["ErrorMessage"] = $"No se encontraron empleados en el equipo. Selecciona un autorizador manualmente. [DEBUG: Autorizadores equipo={countEquipo}, general={countGeneral}]";
                                ModelState.AddModelError("", TempData["ErrorMessage"].ToString());
                                
                                _logger.LogInformation("✅ ViewBag recargado. EmpleadosAutorizaEquipo: {Count}", countEquipo);
                                return View(solicitud);
                            }

                            var revisor = empleadosEquipo
                                .Where(e => e.IdEmpleado != idEmpleado.Value)
                                .FirstOrDefault(e => e.Rol.Contains("TeamLider", StringComparison.OrdinalIgnoreCase))
                                ?? empleadosEquipo
                                .Where(e => e.IdEmpleado != idEmpleado.Value)
                                .FirstOrDefault(e => e.Rol.Contains("SubTeamLider", StringComparison.OrdinalIgnoreCase));

                            if (revisor == null)
                            {
                                TempData["ErrorMessage"] = "No se encontró TeamLider/SubTeamLider en el equipo. Selecciona un autorizador manualmente.";
                                ModelState.AddModelError("", TempData["ErrorMessage"].ToString());
                                await RecargarViewBagCrearAsync();
                                return View(solicitud);
                            }

                            // Obtener el IdUsuario del revisor para guardarlo en FK_IdAutorizador
                            using (var connection = new SqlConnection(_configuration.GetConnectionString("DefaultConnection")))
                            {
                                await connection.OpenAsync();
                                var query = "SELECT IdUsuario FROM Usuarios WHERE FK_IdEmpleado = @IdEmpleado";
                                using (var command = new SqlCommand(query, connection))
                                {
                                    command.Parameters.AddWithValue("@IdEmpleado", revisor.IdEmpleado);
                                    var idUsuario = await command.ExecuteScalarAsync();
                                    if (idUsuario != null)
                                    {
                                        solicitud.Encabezado.IdAutorizador = Convert.ToInt32(idUsuario);
                                    }
                                    else
                                    {
                                        TempData["ErrorMessage"] = "No se pudo obtener el usuario del autorizador. Selecciona un autorizador manualmente.";
                                        ModelState.AddModelError("", TempData["ErrorMessage"].ToString());
                                        await RecargarViewBagCrearAsync();
                                        return View(solicitud);
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            await _loggingService.RegistrarLogAsync(new LogViewModel
                            {
                                Accion = "Error Asignación Revisor",
                                Descripcion = $"Error asignando revisor automáticamente: {ex.Message}",
                                Estado = false
                            });

                            ModelState.AddModelError("", ex.Message);
                            return View(solicitud);
                        }
                    }
                    // else: ya viene seleccionada en el POST, no hacemos nada
                }
                else
                {
                    // Si no tiene equipo, verificar que haya seleccionado un autorizador manualmente
                    if (solicitud.Encabezado.IdAutorizador <= 0)
                    {
                        var errorMsg = "No se pudo encontrar el equipo del empleado. Por favor, selecciona un autorizador manualmente.";
                        TempData["ErrorMessage"] = errorMsg;
                        ModelState.AddModelError("", errorMsg);
                        await RecargarViewBagCrearAsync();
                        return View(solicitud);
                    }
                    // Si seleccionó autorizador manualmente, continuar normalmente
                }


                solicitud.Encabezado.DiasSolicitadosTotal = totalDiasHabiles;
                solicitud.Encabezado.NombreEmpleado = HttpContext.Session.GetString("NombreCompletoEmpleado") ?? "Desconocido";
                solicitud.Encabezado.FechaIngresoSolicitud = DateTime.UtcNow;
                solicitud.Encabezado.Estado = 1;
                solicitud.Encabezado.IdEmpleado = HttpContext.Session.GetInt32("IdEmpleado") ?? 0;


                // Validamos que observaciones en caso de ser nulo no falle
                // Y le asignamos string.Empty en caso de que solo tenga espacios en blanco
                if (solicitud.Encabezado.Observaciones != null && solicitud.Encabezado.Observaciones.All(char.IsWhiteSpace))
                    solicitud.Encabezado.Observaciones = string.Empty;

                //solicitud.Encabezado.Observaciones = solicitud.Encabezado.Observaciones ?? string.Empty;

                // DEBUG: Log del IdAutorizador antes de guardar
                _logger.LogInformation("💾 IdAutorizador antes de guardar: {IdAutorizador}", solicitud.Encabezado.IdAutorizador);

                // 1. Crear la solicitud en la base de datos
                var idSolicitudCreada = await _daoSolicitud.InsertarSolicitudAsync(solicitud);

                // 2. Generar y guardar el PDF automáticamente
                try
                {
                    var pdfBytes = await _pdfService.GenerarPDFSolicitudAsync(idSolicitudCreada);
                    var pdfGuardado = await _pdfService.GuardarPDFEnBaseDatosAsync(idSolicitudCreada, pdfBytes);

                    if (pdfGuardado)
                    {
                        await _bitacoraService.RegistrarBitacoraAsync("PDF Generado", $"PDF generado y almacenado exitosamente para solicitud {idSolicitudCreada}");
                        TempData["SuccessMessage"] = "Solicitud creada exitosamente. El PDF ha sido generado y está disponible para descarga.";
                    }
                    else
                    {
                        await _loggingService.RegistrarLogAsync(new LogViewModel
                        {
                            Accion = "Warning - PDF no guardado",
                            Descripcion = $"La solicitud {idSolicitudCreada} se creó correctamente, pero no se pudo guardar el PDF",
                            Estado = false
                        });
                        TempData["WarningMessage"] = "Solicitud creada exitosamente, pero hubo un problema al generar el PDF.";
                    }
                }
                catch (Exception pdfEx)
                {
                    // Si falla el PDF, la solicitud ya está creada, solo logueamos el error
                    await _loggingService.RegistrarLogAsync(new LogViewModel
                    {
                        Accion = "Error PDF",
                        Descripcion = $"Error generando PDF para solicitud {idSolicitudCreada}: {pdfEx.Message}",
                        Estado = false
                    });
                    TempData["WarningMessage"] = "Solicitud creada exitosamente, pero hubo un problema al generar el PDF.";
                }
                return RedirectToAction("DetallePDF", new { id = idSolicitudCreada });


                //return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                // Debug: Log error detallado
                await _loggingService.RegistrarLogAsync(new LogViewModel
                {
                    Accion = "Debug - Error Excepción",
                    Descripcion = $"Error: {ex.Message}, StackTrace: {ex.StackTrace}",
                    Estado = false
                });

                await RegistrarError("crear solicitud de vacaciones", ex);
                
                // IMPORTANTE: Recargar datos necesarios para la vista
                await RecargarViewBagCrearAsync();
                
                // PRUEBA: Mostrar el error detallado para depuración
                ModelState.AddModelError("", $"Ocurrió un error al crear la solicitud. Detalle: {ex.Message}");
                return View(solicitud);
            }
        }

        [HttpGet]
        public async Task<IActionResult> DetallePDF(int id, bool soloVer = false)
        {
            var solicitud = await _daoSolicitud.ObtenerDetalleSolicitudAsync(id);
            if (solicitud == null) return NotFound();

            // si quieres llenar nombre desde sesión:
            solicitud.Encabezado.NombreEmpleado ??= HttpContext.Session.GetString("NombreCompletoEmpleado");

            // Cargar nombre del autorizador si existe
            if (solicitud.Encabezado.IdAutorizador.HasValue)
            {
                var autorizador = await _daoEmpleado.ObtenerEmpleadoPorIdAsync(solicitud.Encabezado.IdAutorizador.Value);
                if (autorizador != null)
                {
                    solicitud.Encabezado.NombreAutorizador = $"{autorizador.NombresEmpleado} {autorizador.ApellidosEmpleado}";
                }
            }

            // Pasar el parámetro soloVer a la vista
            ViewBag.SoloVer = soloVer;

            return View(solicitud);
        }

        /*=================================================
		==   EDITAR SOLICITUD                           == 
		=================================================*/
        [AuthorizeRole("Empleado", "SuperAdministrador")]
        [HttpGet]
        public async Task<IActionResult> Editar(int id)
        {
            try
            {
                var solicitud = await _daoSolicitud.ObtenerDetalleSolicitudAsync(id);
                if (solicitud == null)
                {
                    TempData["ErrorMessage"] = "No se encontró la solicitud.";
                    return RedirectToAction(nameof(Index));
                }

                // Verificar que el usuario sea el dueño de la solicitud o sea admin
                var idEmpleadoSesion = HttpContext.Session.GetInt32("IdEmpleado");
                var rolSesion = HttpContext.Session.GetString("Rol");

                if (solicitud.Encabezado.IdEmpleado != idEmpleadoSesion && rolSesion != "SuperAdministrador")
                {
                    TempData["ErrorMessage"] = "No tienes permiso para editar esta solicitud.";
                    return RedirectToAction(nameof(Index));
                }

                // Verificar que la solicitud esté en estado "Pendiente" (Estado = 1)
                if (solicitud.Encabezado.Estado != 1)
                {
                    TempData["ErrorMessage"] = "Solo puedes editar solicitudes en estado Pendiente.";
                    return RedirectToAction("DetallePDF", new { id });
                }

                // Cargar datos del empleado para mostrar días disponibles
                var empleado = await _daoEmpleado.ObtenerEmpleadoPorIdAsync(solicitud.Encabezado.IdEmpleado);
                if (empleado == null)
                {
                    TempData["ErrorMessage"] = "No se pudo cargar la información del empleado.";
                    return RedirectToAction(nameof(Index));
                }

                // Calcular días disponibles correctamente
                ViewBag.DiasDisponibles = await CalcularDiasDisponiblesAsync(empleado.IdEmpleado);
                
                // Cargar feriados
                ViewBag.Feriados = await GetFeriadosConProporcion();

                // Cargar información del equipo y autorizadores
                var encuentraEquipo = await _daoProyectoEquipo.ObtenerEquipoPorEmpleadoAsync(empleado.IdEmpleado);
                var empleados = await _daoEmpleado.ObtenerEmpleadoAsync();

                if (encuentraEquipo == null || encuentraEquipo <= 0)
                {
                    ViewBag.AdvertenciaEquipo = "No estás asignado a ningún equipo. Por favor, contacta a RRHH.";
                    
                    // Obtener todos los empleados y filtrar por rol usando la tabla UsuariosRol
                    using (var connection = new SqlConnection(_configuration.GetConnectionString("DefaultConnection")))
                    {
                        await connection.OpenAsync();
                        var query = @"SELECT DISTINCT u.IdUsuario, e.NombresEmpleado, e.ApellidosEmpleado, 
                                             STUFF((SELECT ', ' + r2.NombreRol
                                                    FROM UsuariosRol ur2
                                                    INNER JOIN Roles r2 ON ur2.FK_IdRol = r2.IdRol
                                                    WHERE ur2.FK_IdUsuario = u.IdUsuario
                                                    FOR XML PATH('')), 1, 2, '') AS Roles
                                     FROM Usuarios u
                                     INNER JOIN Empleados e ON u.FK_IdEmpleado = e.IdEmpleado
                                     INNER JOIN UsuariosRol ur ON u.IdUsuario = ur.FK_IdUsuario
                                     WHERE ur.FK_IdRol IN (3, 4, 5)
                                     AND e.IdEmpleado != @IdEmpleado";
                        
                        using (var command = new SqlCommand(query, connection))
                        {
                            command.Parameters.AddWithValue("@IdEmpleado", empleado.IdEmpleado);
                            using (var reader = await command.ExecuteReaderAsync())
                            {
                                var empleadosLimpio = new List<SelectListItem>();
                                while (await reader.ReadAsync())
                                {
                                    var idUsuario = reader.GetInt32(0);
                                    empleadosLimpio.Add(new SelectListItem
                                    {
                                        Value = idUsuario.ToString(), // IdUsuario
                                        Text = $"{reader.GetString(1)} {reader.GetString(2)}",
                                        Selected = idUsuario == solicitud.Encabezado.IdAutorizador
                                    });
                                }
                                ViewBag.empleadosAutoriza = empleadosLimpio;
                            }
                        }
                    }
                }
                else
                {
                    // Obtener empleados del equipo con sus roles
                    var empleadosEquipo = await _daoProyectoEquipo.ObtenerEmpleadosConRolesPorEquipoAsync(encuentraEquipo);

                    // Filtrar solo los que tengan rol de TeamLider, SubTeamLider o Autorizador
                    var empleadosEquipoRol = empleadosEquipo
                        .Where(e => e.Rol != null && (
                            e.Rol.IndexOf("TeamLider", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            e.Rol.IndexOf("SubTeamLider", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            e.Rol.IndexOf("Autorizador", StringComparison.OrdinalIgnoreCase) >= 0))
                        .ToList();

                    // Excluir al mismo empleado
                    var empleadosEquipoLimpio = empleadosEquipoRol
                        .Where(e => e.IdEmpleado != empleado.IdEmpleado)
                        .Select(e => new SelectListItem 
                        {
                            Value = e.IdEmpleado.ToString(),
                            Text = $"{e.NombresEmpleado} {e.ApellidosEmpleado} ({e.Rol})",
                            Selected = e.IdEmpleado == solicitud.Encabezado.IdAutorizador
                        })
                        .ToList();

                    ViewBag.EmpleadosAutorizaEquipo = empleadosEquipoLimpio;
                }

                // Configurar nombre completo en sesión
                var nombreCompleto = $"{empleado.NombresEmpleado} {empleado.ApellidosEmpleado}";
                HttpContext.Session.SetString("NombreCompletoEmpleado", nombreCompleto);
                
                return View("Crear", solicitud); // Reutilizamos la misma vista de Crear
            }
            catch (Exception ex)
            {
                await _loggingService.RegistrarLogAsync(new LogViewModel
                {
                    Accion = "Error - Editar Solicitud GET",
                    Descripcion = $"Error al cargar solicitud para editar: {ex.Message}",
                    Estado = false
                });

                TempData["ErrorMessage"] = "Ocurrió un error al cargar la solicitud.";
                return RedirectToAction(nameof(Index));
            }
        }

        [AuthorizeRole("Empleado", "SuperAdministrador")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Editar(SolicitudViewModel solicitud)
        {
            try
            {
                // Verificar que el usuario sea el dueño de la solicitud o sea admin
                var idEmpleadoSesion = HttpContext.Session.GetInt32("IdEmpleado");
                var rolSesion = HttpContext.Session.GetString("Rol");

                if (solicitud.Encabezado.IdEmpleado != idEmpleadoSesion && rolSesion != "SuperAdministrador")
                {
                    TempData["ErrorMessage"] = "No tienes permiso para editar esta solicitud.";
                    return RedirectToAction(nameof(Index));
                }

                if (!ModelState.IsValid)
                {
                    ViewBag.Feriados = await GetFeriadosConProporcion();
                    return View("Crear", solicitud);
                }

                // Recalcular los días hábiles
                var feriados = await GetFeriadosConProporcion();
                decimal totalDiasHabiles = 0;

                // Obtener empleado para excluir cumpleaños del cálculo
                var empleado = await _daoEmpleado.ObtenerEmpleadoPorIdAsync(solicitud.Encabezado.IdEmpleado);
                
                foreach (var detalle in solicitud.Detalles)
                {
                    totalDiasHabiles += CalcularDiasHabiles(detalle.FechaInicio, detalle.FechaFin, feriados, empleado?.FechaNacimiento);
                }

                solicitud.Encabezado.DiasSolicitadosTotal = totalDiasHabiles;

                // Actualizar la solicitud en la base de datos
                var actualizado = await _daoSolicitud.ActualizarSolicitudAsync(solicitud);

                if (actualizado)
                {
                    var idSolicitud = solicitud.Encabezado.IdSolicitud;
                    
                    // Regenerar el PDF con los nuevos datos
                    try
                    {
                        var pdfBytes = await _pdfService.GenerarPDFSolicitudAsync(idSolicitud.Value);
                        await _pdfService.GuardarPDFEnBaseDatosAsync(idSolicitud.Value, pdfBytes);

                        await _bitacoraService.RegistrarBitacoraAsync(
                            "Solicitud Actualizada",
                            $"Solicitud #{idSolicitud} actualizada por {HttpContext.Session.GetString("NombreCompletoEmpleado")}"
                        );

                        TempData["SuccessMessage"] = "Solicitud actualizada exitosamente. El PDF ha sido regenerado.";
                    }
                    catch (Exception pdfEx)
                    {
                        await _loggingService.RegistrarLogAsync(new LogViewModel
                        {
                            Accion = "Warning - PDF no regenerado",
                            Descripcion = $"Solicitud actualizada pero error al regenerar PDF: {pdfEx.Message}",
                            Estado = false
                        });

                        TempData["WarningMessage"] = "Solicitud actualizada, pero hubo un problema al regenerar el PDF.";
                    }

                    return RedirectToAction("DetallePDF", new { id = idSolicitud.Value });
                }
                else
                {
                    TempData["ErrorMessage"] = "No se pudo actualizar la solicitud.";
                    ViewBag.Feriados = await GetFeriadosConProporcion();
                    return View("Crear", solicitud);
                }
            }
            catch (Exception ex)
            {
                await _loggingService.RegistrarLogAsync(new LogViewModel
                {
                    Accion = "Error - Editar Solicitud POST",
                    Descripcion = $"Error al actualizar solicitud: {ex.Message}",
                    Estado = false
                });

                TempData["ErrorMessage"] = $"Ocurrió un error al actualizar la solicitud: {ex.Message}";
                ViewBag.Feriados = await GetFeriadosConProporcion();
                return View("Crear", solicitud);
            }
        }

        /*=================================================
		==   FIRMAR SOLICITUD (EMPLEADO)                == 
		=================================================*/
        [AuthorizeRole("Empleado", "SuperAdministrador")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EnviarSolicitud(int id)
        {
            try
            {
                var solicitud = await _daoSolicitud.ObtenerDetalleSolicitudAsync(id);
                if (solicitud == null)
                {
                    TempData["ErrorMessage"] = "No se encontró la solicitud.";
                    return RedirectToAction(nameof(Index));
                }

                // Verificar que el usuario sea el dueño de la solicitud
                var idEmpleadoSesion = HttpContext.Session.GetInt32("IdEmpleado");
                if (solicitud.Encabezado.IdEmpleado != idEmpleadoSesion)
                {
                    TempData["ErrorMessage"] = "No tienes permiso para enviar esta solicitud.";
                    return RedirectToAction("DetallePDF", new { id });
                }

                // Verificar que la solicitud esté en estado Pendiente
                if (solicitud.Encabezado.Estado != 1)
                {
                    TempData["ErrorMessage"] = "Solo puedes enviar solicitudes en estado Pendiente.";
                    return RedirectToAction("DetallePDF", new { id });
                }

                // Verificar que el empleado tenga firma registrada
                var idUsuario = HttpContext.Session.GetInt32("IdUsuario");
                if (!idUsuario.HasValue)
                {
                    TempData["ErrorMessage"] = "No se pudo obtener tu información de usuario.";
                    return RedirectToAction("DetallePDF", new { id });
                }

                // Regenerar el PDF con la firma del empleado
                var pdfBytes = await _pdfService.GenerarPDFSolicitudAsync(id);
                
                if (pdfBytes == null || pdfBytes.Length == 0)
                {
                    TempData["ErrorMessage"] = "No se pudo generar el PDF. Asegúrate de tener una firma registrada.";
                    return RedirectToAction("DetallePDF", new { id });
                }

                await _pdfService.GuardarPDFEnBaseDatosAsync(id, pdfBytes);

                // Mantener el estado como 1 (Ingresada/Pendiente) hasta que el autorizador la apruebe
                // Cuando el autorizador apruebe, cambiará a estado 4 (Autorizada) y se agregará su firma
                // No cambiamos el estado aquí, solo firmamos el PDF con la firma del empleado

                // Enviar notificación por correo al autorizador
                try
                {
                    // Obtener datos del autorizador
                    // IMPORTANTE: FK_IdAutorizador en SolicitudEncabezado es IdUsuario, NO IdEmpleado
                    var idUsuarioAutorizador = solicitud.Encabezado.IdAutorizador;
                    
                    if (idUsuarioAutorizador.HasValue && idUsuarioAutorizador.Value > 0)
                    {
                        // Primero obtener el IdEmpleado del usuario autorizador
                        int? idEmpleadoAutorizador = null;
                        using (var connection = new SqlConnection(_configuration.GetConnectionString("DefaultConnection")))
                        {
                            await connection.OpenAsync();
                            using (var cmd = new SqlCommand("SELECT FK_IdEmpleado FROM Usuarios WHERE IdUsuario = @IdUsuario", connection))
                            {
                                cmd.Parameters.AddWithValue("@IdUsuario", idUsuarioAutorizador.Value);
                                var resultado = await cmd.ExecuteScalarAsync();
                                if (resultado != null && resultado != DBNull.Value)
                                {
                                    idEmpleadoAutorizador = Convert.ToInt32(resultado);
                                }
                            }
                        }
                        
                        if (idEmpleadoAutorizador.HasValue)
                        {
                            var autorizador = await _daoEmpleado.ObtenerEmpleadoPorIdAsync(idEmpleadoAutorizador.Value);
                            
                            if (autorizador != null && !string.IsNullOrEmpty(autorizador.CorreoInstitucional))
                            {
                                // Obtener fechas de la solicitud
                                var fechaInicio = solicitud.Detalles.Min(d => d.FechaInicio);
                                var fechaFin = solicitud.Detalles.Max(d => d.FechaFin);
                                
                                // Crear URL para que el autorizador acceda directamente
                                var urlAutorizar = Url.Action(
                                    "Autorizar",
                                    "Solicitudes",
                                    null,
                                    protocol: Request.Scheme
                                );
                                
                                // Enviar correo de notificación
                                await _emailService.EnviarNotificacionNuevaSolicitudAsync(
                                    nombreEmpleado: HttpContext.Session.GetString("NombreCompletoEmpleado") ?? "Empleado",
                                    nombreAutorizador: $"{autorizador.NombresEmpleado} {autorizador.ApellidosEmpleado}",
                                    correoAutorizador: autorizador.CorreoInstitucional,
                                    numeroSolicitud: id,
                                    diasSolicitados: solicitud.Encabezado.DiasSolicitadosTotal,
                                    fechaInicio: fechaInicio,
                                    fechaFin: fechaFin,
                                    urlAutorizar: urlAutorizar
                                );
                                
                                await _bitacoraService.RegistrarBitacoraAsync(
                                    "Notificación Enviada",
                                    $"Correo de notificación enviado al autorizador {autorizador.NombresEmpleado} {autorizador.ApellidosEmpleado} ({autorizador.CorreoInstitucional}) para solicitud #{id}"
                                );
                            }
                        }
                    }
                }
                catch (Exception emailEx)
                {
                    // Si falla el envío del correo, registramos el error pero no detenemos el proceso
                    await _loggingService.RegistrarLogAsync(new LogViewModel
                    {
                        Accion = "Error Envío Correo",
                        Descripcion = $"Error al enviar correo de notificación para solicitud #{id}: {emailEx.Message}",
                        Estado = false
                    });
                    // Continuamos con el flujo normal aunque falle el correo
                }

                await _bitacoraService.RegistrarBitacoraAsync(
                    "Solicitud Enviada a Autorización",
                    $"Solicitud #{id} enviada por {HttpContext.Session.GetString("NombreCompletoEmpleado")} al autorizador"
                );

                TempData["Success"] = "¡Solicitud enviada exitosamente! El PDF ha sido firmado con tu firma digital y se ha notificado al autorizador por correo. Tu solicitud está pendiente de autorización.";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                await _loggingService.RegistrarLogAsync(new LogViewModel
                {
                    Accion = "Error - Enviar Solicitud",
                    Descripcion = $"Error al enviar solicitud: {ex.Message}",
                    Estado = false
                });

                TempData["ErrorMessage"] = $"Ocurrió un error al enviar la solicitud: {ex.Message}";
                return RedirectToAction("DetallePDF", new { id });
            }
        }


        private decimal CalcularDiasHabiles(DateTime inicio, DateTime fin, Dictionary<string, decimal> feriados, DateTime? fechaNacimiento = null)
        {
            decimal diasHabiles = 0;
            for (var date = inicio; date <= fin; date = date.AddDays(1))
            {
                // Verificar si es el día de cumpleaños del empleado
                if (fechaNacimiento.HasValue && 
                    date.Month == fechaNacimiento.Value.Month && 
                    date.Day == fechaNacimiento.Value.Day)
                {
                    // El día de cumpleaños NO cuenta como día de vacaciones
                    _logger.LogInformation("🎂 Día de cumpleaños detectado en rango: {Fecha} - NO se descuenta", date.ToString("dd/MM/yyyy"));
                    continue; // Saltar este día
                }
                
                if (date.DayOfWeek != DayOfWeek.Saturday && date.DayOfWeek != DayOfWeek.Sunday)
                {
                    string fechaActualStr = date.ToString("yyyy-MM-dd");
                    if (feriados.TryGetValue(fechaActualStr, out decimal proporcion))
                    {
                        diasHabiles += (1 - proporcion);
                    }
                    else
                    {
                        diasHabiles++;
                    }
                }
            }
            return diasHabiles;
        }

        // Método helper para verificar si el rango de fechas incluye SOLO el cumpleaños del empleado
        private bool EsSoloCumpleanos(List<SolicitudDetalleViewModel> detalles, DateTime fechaNacimiento)
        {
            // Verificar si hay un solo detalle
            if (detalles == null || detalles.Count != 1)
                return false;

            var detalle = detalles[0];
            
            // Verificar si es el mismo día (inicio = fin)
            if (detalle.FechaInicio.Date != detalle.FechaFin.Date)
                return false;

            // Verificar si coincide con el cumpleaños (mes y día)
            return detalle.FechaInicio.Month == fechaNacimiento.Month && 
                   detalle.FechaInicio.Day == fechaNacimiento.Day;
        }

        // Método helper para recargar ViewBag necesarios cuando se regresa a la vista Crear
        private async Task RecargarViewBagCrearAsync()
        {
            var debugInfo = new System.Text.StringBuilder();
            
            try
            {
                _logger.LogInformation("🔄 RecargarViewBagCrearAsync - INICIANDO");
                debugInfo.AppendLine("🔄 Recargando ViewBag...");
                
                ViewBag.Feriados = await GetFeriadosConProporcion();
                
                var idEmpleado = HttpContext.Session.GetInt32("IdEmpleado");
                _logger.LogInformation("🔄 IdEmpleado: {IdEmpleado}", idEmpleado);
                debugInfo.AppendLine($"IdEmpleado: {idEmpleado}");
                
                if (idEmpleado.HasValue)
                {
                    var empleado = await _daoEmpleado.ObtenerEmpleadoPorIdAsync(idEmpleado.Value);
                    if (empleado != null)
                    {
                        // Calcular días disponibles correctamente
                        ViewBag.DiasDisponibles = await CalcularDiasDisponiblesAsync(empleado.IdEmpleado);
                        
                        // Recargar lista de autorizadores
                        var encuentraEquipo = await _daoProyectoEquipo.ObtenerEquipoPorEmpleadoAsync(empleado.IdEmpleado);
                        _logger.LogInformation("🔄 Equipo encontrado: {Equipo}", encuentraEquipo);
                        debugInfo.AppendLine($"Equipo: {encuentraEquipo}");
                        
                        if (encuentraEquipo != null && encuentraEquipo > 0)
                        {
                            // Tiene equipo, cargar autorizadores del equipo
                            var empleadosEquipo = await _daoProyectoEquipo.ObtenerEmpleadosConRolesPorEquipoAsync(encuentraEquipo);
                            _logger.LogInformation("🔄 Empleados en equipo: {Count}", empleadosEquipo?.Count() ?? 0);
                            debugInfo.AppendLine($"Empleados en equipo: {empleadosEquipo?.Count() ?? 0}");
                            
                            // Excluir a los que no tengan el rol correcto y al empleado actual
                            var empleadosEquipoLimpio = empleadosEquipo
                                .Where(e => e.Rol != null && (
                                    e.Rol.IndexOf("TeamLider", StringComparison.OrdinalIgnoreCase) >= 0 || 
                                    e.Rol.IndexOf("SubTeamLider", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                    e.Rol.IndexOf("Autorizador", StringComparison.OrdinalIgnoreCase) >= 0))
                                .Where(e => e.IdEmpleado != empleado.IdEmpleado)
                                .Select(e => new SelectListItem
                                {
                                    Value = e.IdEmpleado.ToString(),
                                    Text = $"{e.NombresEmpleado} {e.ApellidosEmpleado} - {e.Rol}"
                                })
                                .ToList();
                            
                            _logger.LogInformation("🔄 Autorizadores después de filtrar: {Count}", empleadosEquipoLimpio.Count);
                            debugInfo.AppendLine($"Autorizadores filtrados: {empleadosEquipoLimpio.Count}");
                            ViewBag.EmpleadosAutorizaEquipo = empleadosEquipoLimpio;
                        }
                        else
                        {
                            // No tiene equipo, cargar todos los autorizadores
                            using (var connection = new SqlConnection(_configuration.GetConnectionString("DefaultConnection")))
                            {
                                await connection.OpenAsync();
                                // Usar la misma consulta que en el GET Crear
                                var query = @"SELECT DISTINCT u.IdUsuario, e.NombresEmpleado, e.ApellidosEmpleado, 
                                                     ISNULL(STUFF((SELECT ', ' + r2.NombreRol
                                                                   FROM UsuariosRol ur2
                                                                   INNER JOIN Roles r2 ON ur2.FK_IdRol = r2.IdRol
                                                                   WHERE ur2.FK_IdUsuario = u.IdUsuario
                                                                   FOR XML PATH('')), 1, 2, ''), 'Sin Rol') AS Roles
                                              FROM Usuarios u
                                              INNER JOIN Empleados e ON u.FK_IdEmpleado = e.IdEmpleado
                                              LEFT JOIN UsuariosRol ur ON u.IdUsuario = ur.FK_IdUsuario
                                              WHERE (ur.FK_IdRol IN (3, 4, 5) OR ur.FK_IdRol IS NULL)
                                              AND e.IdEmpleado != @IdEmpleado
                                              AND EXISTS (SELECT 1 FROM UsuariosRol ur3 
                                                          WHERE ur3.FK_IdUsuario = u.IdUsuario 
                                                          AND ur3.FK_IdRol IN (3, 4, 5))";
                                
                                using (var command = new SqlCommand(query, connection))
                                {
                                    command.Parameters.AddWithValue("@IdEmpleado", empleado.IdEmpleado);
                                    
                                    using (var reader = await command.ExecuteReaderAsync())
                                    {
                                        var listaAutorizadores = new List<SelectListItem>();
                                        while (await reader.ReadAsync())
                                        {
                                            listaAutorizadores.Add(new SelectListItem
                                            {
                                                Value = reader.GetInt32(0).ToString(), // IdUsuario
                                                Text = $"{reader.GetString(1)} {reader.GetString(2)}" // Nombres + Apellidos
                                            });
                                        }
                                        
                                        _logger.LogInformation("🔄 Autorizadores sin equipo: {Count}", listaAutorizadores.Count);
                                        debugInfo.AppendLine($"Autorizadores sin equipo: {listaAutorizadores.Count}");
                                        ViewBag.empleadosAutoriza = listaAutorizadores;
                                    }
                                }
                            }
                        }
                    }
                }
                
                // Guardar debug info en ViewBag para mostrarlo en la vista
                ViewBag.DebugInfo = debugInfo.ToString();
            }
            catch (Exception ex)
            {
                _logger.LogError("Error al recargar ViewBag: {Error}", ex.Message);
                debugInfo.AppendLine($"❌ ERROR: {ex.Message}");
                ViewBag.DebugInfo = debugInfo.ToString();
            }
        }

        // Vista principal para autorizar solicitudes
        // GET: SolicitudesController/Solicitudes
        /*[HttpGet]
        [AuthorizeRole("SuperAdministrador", "Autorizador", "TeamLider", "SubTeamLider")]
        public async Task<ActionResult> Autorizar()
        {
            await _bitacoraService.RegistrarBitacoraAsync("Vista Autorizar", "Acceso a la vista Autorizar exitosamente");
            var solicitudes = new List<SolicitudEncabezadoViewModel>();

            try
            {
                var rolesString = HttpContext.Session.GetString("Roles") ?? "";
                if (string.IsNullOrEmpty(rolesString)) return RedirectToAction("Index", "Login");

                var userRoles = rolesString.Split(',').ToList();

                var idEmpleado = HttpContext.Session.GetInt32("IdEmpleado");
                if (idEmpleado == null) return RedirectToAction("Index", "Login");

                if (userRoles.Contains("SuperAdministrador"))
                {
                    solicitudes = await _daoSolicitud.ObtenerSolicitudEncabezadoAutorizadorAsync();
                }
                else if (userRoles.Any(r => new[] { "TeamLider", "SubTeamLider", "Autorizador" }.Contains(r)))
                {
                    var equipos = await _daoEmpleadoEquipo.ObtenerEquiposPorEmpleadoAsync(idEmpleado.Value);
                    var solicitudesIds = new HashSet<int>();

                    foreach (var equipo in equipos)
                    {
                        var solicitudesEquipo = await _daoSolicitud.ObtenerSolicitudesPorEquipoAsync(equipo.IdEquipo);
                        foreach (var solicitud in solicitudesEquipo)
                        {
                            if (solicitudesIds.Add(solicitud.IdSolicitud))
                            {
                                solicitudes.Add(solicitud);
                            }
                        }
                    }
                }

                await _bitacoraService.RegistrarBitacoraAsync("Vista Autorizar", "Obtener lista detalles de solicitudes");
                return View(solicitudes);
            }
            catch (Exception ex)
            {
                // Log the error and redirect to the Index action (hace falta DI)***
                await RegistrarError("autorizar solicitudes", ex);
                return View(solicitudes);
            }
        }*/
        [HttpGet]
        [AuthorizeRole("SuperAdministrador", "Autorizador", "TeamLider", "SubTeamLider", "RRHH")]
        public async Task<ActionResult> Autorizar(string? estadoSolicitud = null,
           string? nombresEmpleado = null)
        {

            await _bitacoraService.RegistrarBitacoraAsync("Vista Autorizar", "Acceso a la vista Autorizar exitosamente");
            var solicitudes = new List<SolicitudEncabezadoViewModel>();

            try
            {
                if (!string.IsNullOrWhiteSpace(nombresEmpleado))
                    solicitudes = solicitudes.Where(solicitud => solicitud.NombreEmpleado.Equals(nombresEmpleado)).ToList();

                if (!string.IsNullOrWhiteSpace(estadoSolicitud) && int.TryParse(estadoSolicitud, out int estadoId))
                {
                    solicitudes = solicitudes.Where(solicitud => solicitud.Estado == estadoId).ToList();
                }
                var rolesString = HttpContext.Session.GetString("Roles");
                if (string.IsNullOrEmpty(rolesString)) return RedirectToAction("Index", "Login");

                var roles = rolesString.Split(',').Select(r => r.Trim()).ToList();
                var idUsuario = HttpContext.Session.GetInt32("IdUsuario");
                if (idUsuario == null) return RedirectToAction("Index", "Login");

                _logger.LogInformation("🔍 Vista Autorizar - Roles: {Roles}, IdUsuario: {IdUsuario}", rolesString, idUsuario);

                // Admin y RRHH ven TODAS las solicitudes PENDIENTES
                if (roles.Contains("SuperAdministrador") || roles.Contains("RRHH"))
                {
                    _logger.LogInformation("📋 Obteniendo solicitudes PENDIENTES para Admin/RRHH");
                    var todasSolicitudes = await _daoSolicitud.ObtenerSolicitudEncabezadoAutorizadorAsync(); // Sin filtro
                    // Filtrar solo las pendientes (Estado = 1 "Ingresada")
                    solicitudes = todasSolicitudes.Where(s => s.Estado == 1).ToList();
                    _logger.LogInformation("✅ Solicitudes PENDIENTES encontradas: {Count}", solicitudes.Count);
                }
                // Autorizadores solo ven las solicitudes PENDIENTES asignadas a ellos
                else if (roles.Contains("TeamLider") || roles.Contains("SubTeamLider") || roles.Contains("Autorizador"))
                {
                    _logger.LogInformation("📋 Obteniendo solicitudes PENDIENTES asignadas al autorizador con IdUsuario: {IdUsuario}", idUsuario);
                    var todasSolicitudes = await _daoSolicitud.ObtenerSolicitudEncabezadoAutorizadorAsync(idUsuario.Value);
                    // Filtrar solo las pendientes (Estado = 1 "Ingresada")
                    solicitudes = todasSolicitudes.Where(s => s.Estado == 1).ToList();
                    _logger.LogInformation("✅ Solicitudes PENDIENTES encontradas: {Count}", solicitudes.Count);
                    
                    // Log detallado de cada solicitud
                    foreach (var sol in solicitudes)
                    {
                        _logger.LogInformation("  📄 Solicitud {IdSolicitud}: {Empleado}, Estado: {Estado}, Autorizador: {Autorizador}", 
                            sol.IdSolicitud, sol.NombreEmpleado, sol.Estado, sol.IdAutorizador);
                    }
                }
                else
                {
                    _logger.LogWarning("⚠️ Roles no autorizados: {Roles}. No se cargarán solicitudes.", rolesString);
                }

                // Cargar los estados para la vista
                var estados = await _estadoService.ObtenerEstadosActivosSolicitudesAsync();
                ViewBag.Estados = estados.ToDictionary(e => e.IdEstadoSolicitud, e => e.NombreEstado);

                await _bitacoraService.RegistrarBitacoraAsync("Vista Autorizar", "Obtener lista detalles de solicitudes");
                return View(solicitudes);

            }
            catch (Exception ex)
            {
                // Log the error and redirect to the Index action (hace falta DI)***
                await RegistrarError("autorizar solicitudes", ex);
                return View(solicitudes);
            }
        }

        // GET: SolicitudesController/HistorialAutorizador
        [HttpGet]
        [AuthorizeRole("SuperAdministrador", "Autorizador", "TeamLider", "SubTeamLider", "RRHH")]
        public async Task<ActionResult> HistorialAutorizador(string? estadoSolicitud = null, string? nombresEmpleado = null)
        {
            await _bitacoraService.RegistrarBitacoraAsync("Vista Historial Autorizador", "Acceso al historial de solicitudes autorizadas");
            var solicitudes = new List<SolicitudEncabezadoViewModel>();

            try
            {
                var rolesString = HttpContext.Session.GetString("Roles");
                if (string.IsNullOrEmpty(rolesString)) return RedirectToAction("Index", "Login");

                var roles = rolesString.Split(',').Select(r => r.Trim()).ToList();
                var idUsuario = HttpContext.Session.GetInt32("IdUsuario");
                if (idUsuario == null) return RedirectToAction("Index", "Login");

                _logger.LogInformation("🔍 Vista Historial Autorizador - Roles: {Roles}, IdUsuario: {IdUsuario}", rolesString, idUsuario);

                // Admin y RRHH ven TODAS las solicitudes (sin filtro de estado)
                if (roles.Contains("SuperAdministrador") || roles.Contains("RRHH"))
                {
                    _logger.LogInformation("📋 Obteniendo TODAS las solicitudes para Admin/RRHH");
                    solicitudes = await _daoSolicitud.ObtenerSolicitudEncabezadoAutorizadorAsync(); // Sin filtro
                    _logger.LogInformation("✅ Total de solicitudes encontradas: {Count}", solicitudes.Count);
                }
                // Autorizadores ven TODAS las solicitudes asignadas a ellos (sin filtro de estado)
                else if (roles.Contains("TeamLider") || roles.Contains("SubTeamLider") || roles.Contains("Autorizador"))
                {
                    _logger.LogInformation("📋 Obteniendo TODAS las solicitudes asignadas al autorizador con IdUsuario: {IdUsuario}", idUsuario);
                    solicitudes = await _daoSolicitud.ObtenerSolicitudEncabezadoAutorizadorAsync(idUsuario.Value);
                    _logger.LogInformation("✅ Total de solicitudes encontradas: {Count}", solicitudes.Count);
                }
                else
                {
                    _logger.LogWarning("⚠️ Roles no autorizados: {Roles}. No se cargarán solicitudes.", rolesString);
                }

                // ✅ APLICAR FILTRO POR NOMBRE SI SE PROPORCIONÓ
                if (!string.IsNullOrWhiteSpace(nombresEmpleado))
                {
                    solicitudes = solicitudes.Where(s => 
                        s.NombreEmpleado.Contains(nombresEmpleado, StringComparison.OrdinalIgnoreCase)
                    ).ToList();
                    _logger.LogInformation("🔍 Filtro aplicado - Nombre: {Nombre}, Solicitudes filtradas: {Count}", nombresEmpleado, solicitudes.Count);
                }

                // ✅ APLICAR FILTRO POR ESTADO SI SE PROPORCIONÓ
                if (!string.IsNullOrWhiteSpace(estadoSolicitud) && int.TryParse(estadoSolicitud, out int estadoId))
                {
                    solicitudes = solicitudes.Where(s => s.Estado == estadoId).ToList();
                    _logger.LogInformation("🔍 Filtro aplicado - Estado: {EstadoId}, Solicitudes filtradas: {Count}", estadoId, solicitudes.Count);
                }

                // Cargar los estados para la vista
                var estados = await _estadoService.ObtenerEstadosActivosSolicitudesAsync();
                ViewBag.Estados = estados.ToDictionary(e => e.IdEstadoSolicitud, e => e.NombreEstado);

                await _bitacoraService.RegistrarBitacoraAsync("Vista Historial Autorizador", "Obtener historial completo de solicitudes");
                return View(solicitudes);

            }
            catch (Exception ex)
            {
                await RegistrarError("ver historial de solicitudes", ex);
                return View(solicitudes);
            }
        }


        /*=================================================   
		==   AUTORIZAR SOLICITUD CON RESTRICCIÓN PDF   == 
		=================================================*/
        /***Funcionalidad mejorada que incluye:
		**1. Autorización de la solicitud en base de datos
		**2. Restricción automática de descarga del PDF
		**3. Logging y manejo de errores mejorado*/
        [AuthorizeRole("SuperAdministrador", "Autorizador", "TeamLider", "SubTeamLider")]
        [HttpPost]
        public async Task<ActionResult> AutorizarSolicitud(int idSolicitud)
        {
            try
            {
                // Se valida el id
                if (idSolicitud == 0)
                {
                    TempData["ErrorMessage"] = "El campo idSolicitud no puede ser cero o estar vacio";
                }
                else
                {
                    // 1. Lógica de autorización
                    var autorizada = await _daoSolicitud.AutorizarSolicitud(idSolicitud);

                    if (autorizada)
                    {
                        // 2. Actualizar días acumulados del empleado
                        try
                        {
                            // Obtener el IdEmpleado de la solicitud
                            var solicitud = await _daoSolicitud.ObtenerDetalleSolicitudAsync(idSolicitud);
                            if (solicitud != null && solicitud.Encabezado != null)
                            {
                                // Obtener días antes del descuento
                                var empleadoAntes = await _daoEmpleado.ObtenerEmpleadoPorIdAsync(solicitud.Encabezado.IdEmpleado);
                                var diasAntes = empleadoAntes?.DiasVacacionesAcumulados ?? 0;
                                
                                _logger.LogInformation("💰 ANTES de actualizar - Empleado {IdEmpleado}: {DiasAntes} días disponibles, Solicitud #{IdSolicitud}: {DiasSolicitados} días", 
                                    solicitud.Encabezado.IdEmpleado, diasAntes, idSolicitud, solicitud.Encabezado.DiasSolicitadosTotal);
                                
                                // Actualizar solo el empleado de esta solicitud (más eficiente)
                                await _daoEmpleado.ActualizarDiasAcumuladosEmpleadoAsync(solicitud.Encabezado.IdEmpleado);
                                
                                // Obtener días después del descuento
                                
                                var empleadoDespues = await _daoEmpleado.ObtenerEmpleadoPorIdAsync(solicitud.Encabezado.IdEmpleado);
                                var diasDespues = empleadoDespues?.DiasVacacionesAcumulados ?? 0;
                                
                                _logger.LogInformation("💰 DESPUÉS de actualizar - Empleado {IdEmpleado}: {DiasDespues} días disponibles (Descuento: {Descuento} días)", 
                                    solicitud.Encabezado.IdEmpleado, diasDespues, diasAntes - diasDespues);
                                
                                await _bitacoraService.RegistrarBitacoraAsync("Días Actualizados", 
                                    $"Días acumulados actualizados para empleado {solicitud.Encabezado.IdEmpleado} después de autorizar solicitud {idSolicitud}. Antes: {diasAntes}, Después: {diasDespues}, Descontados: {diasAntes - diasDespues}");
                            }
                        }
                        catch (Exception diasEx)
                        {
                            // Si falla la actualización de días, logueamos pero no afectamos la autorización
                            _logger.LogError(diasEx, "❌ Error al actualizar días para solicitud {IdSolicitud}", idSolicitud);
                            await _loggingService.RegistrarLogAsync(new LogViewModel
                            {
                                Accion = "Warning - Actualización Días",
                                Descripcion = $"Solicitud {idSolicitud} autorizada correctamente, pero no se pudieron actualizar los días acumulados: {diasEx.Message}",
                                Estado = false
                            });
                        }
                        
                        // 3. Restringir descarga del PDF automáticamente
                        try
                        {
                            await _pdfService.RestringirDescargaPDFAsync(idSolicitud);
                            await _bitacoraService.RegistrarBitacoraAsync("PDF Restringido", $"Descarga de PDF restringida para solicitud autorizada {idSolicitud}");
                        }
                        catch (Exception pdfEx)
                        {
                            // Si falla la restricción del PDF, logueamos pero no afectamos la autorización
                            await _loggingService.RegistrarLogAsync(new LogViewModel
                            {
                                Accion = "Warning - Restricción PDF",
                                Descripcion = $"Solicitud {idSolicitud} autorizada correctamente, pero no se pudo restringir el PDF: {pdfEx.Message}",
                                Estado = false
                            });
                        }

                        TempData["SuccessMessage"] = "Solicitud autorizada con éxito. Su firma ha sido agregada al documento.";
                        // Redirigir a una vista donde se vea el PDF autorizado
                        return RedirectToAction("VerPDFAutorizado", new { idSolicitud });
                    }
                    else
                    {
                        TempData["ErrorMessage"] = "La solicitud no se pudo autorizar";
                    }
                }

            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error al autorizar solicitud no se pudo autorizar" + ex;
                await RegistrarError("Autorizar solicitud", ex);
            }

            return RedirectToAction(nameof(Autorizar));

        }

        /*=================================================   
		==   FIRMAR SOLICITUD COMO AUTORIZADOR          == 
		=================================================*/
        [HttpPost]
        [AuthorizeRole("SuperAdministrador", "Autorizador", "TeamLider", "SubTeamLider")]
        public async Task<IActionResult> FirmarSolicitudAutorizador(int idSolicitud)
        {
            try
            {
                var idUsuario = HttpContext.Session.GetInt32("IdUsuario");
                if (!idUsuario.HasValue)
                {
                    TempData["ErrorMessage"] = "No se pudo identificar al usuario.";
                    return RedirectToAction("Detalle", new { id = idSolicitud });
                }

                // Verificar que el autorizador tenga firma registrada
                // Aquí deberías verificar en UserSignatures si tiene firma
                // Por ahora, asumimos que sí y marcamos la solicitud como firmada por el autorizador
                
                TempData["SuccessMessage"] = "Solicitud firmada exitosamente. Ahora puede autorizarla.";
                await _bitacoraService.RegistrarBitacoraAsync("Firmar Solicitud", $"Autorizador firmó solicitud {idSolicitud}");
                
                return RedirectToAction("Detalle", new { id = idSolicitud });
            }
            catch (Exception ex)
            {
                await RegistrarError($"firmar solicitud {idSolicitud}", ex);
                TempData["ErrorMessage"] = "Error al firmar la solicitud.";
                return RedirectToAction("Detalle", new { id = idSolicitud });
            }
        }

        /*=================================================   
		==   VER PDF AUTORIZADO CON AMBAS FIRMAS        == 
		=================================================*/
        [HttpGet]
        [AuthorizeRole("SuperAdministrador", "Autorizador", "TeamLider", "SubTeamLider", "RRHH")]
        public async Task<IActionResult> VerPDFAutorizado(int idSolicitud)
        {
            try
            {
                var solicitud = await _daoSolicitud.ObtenerDetalleSolicitudAsync(idSolicitud);
                
                if (solicitud == null)
                {
                    TempData["ErrorMessage"] = "Solicitud no encontrada.";
                    return RedirectToAction("Autorizar");
                }

                ViewBag.IdSolicitud = idSolicitud;
                ViewBag.NombreEmpleado = solicitud.Encabezado.NombreEmpleado;
                
                return View();
            }
            catch (Exception ex)
            {
                await RegistrarError($"ver PDF autorizado de solicitud {idSolicitud}", ex);
                TempData["ErrorMessage"] = "Error al cargar el PDF autorizado.";
                return RedirectToAction("Autorizar");
            }
        }

        /*=================================================   
		==   VISUALIZAR PDF DE SOLICITUD (IFRAME)       == 
		=================================================*/
        [HttpGet]
        [AuthorizeRole("SuperAdministrador", "Empleado", "RRHH", "Autorizador", "TeamLider", "SubTeamLider")]
        public async Task<IActionResult> VisualizarPDF(int idSolicitud)
        {
            try
            {
                // Generar el PDF usando QuestPDF
                var pdfBytes = await _pdfService.GenerarPDFSolicitudAsync(idSolicitud);
                
                if (pdfBytes == null || pdfBytes.Length == 0)
                {
                    return NotFound("PDF no encontrado");
                }

                // Devolver el PDF para visualización en iframe (inline, no descarga)
                return File(pdfBytes, "application/pdf");
            }
            catch (Exception ex)
            {
                await RegistrarError($"visualizar PDF de solicitud {idSolicitud}", ex);
                return NotFound("Error al cargar el PDF");
            }
        }

        /*=================================================   
		==   DESCARGA DE PDF DE SOLICITUD               == 
		=================================================*/
        /***Funcionalidad para descargar PDFs que incluye:
		**1. Verificación de permisos de descarga
		**2. Descompresión automática del archivo Brotli
		**3. Control de acceso basado en estado de solicitud
		**4. Logging de descargas para auditoría*/
        [HttpGet]
        [AuthorizeRole("SuperAdministrador", "Empleado", "RRHH", "Autorizador", "TeamLider", "SubTeamLider")]
        public async Task<IActionResult> DescargarPDF(int idSolicitud)
        {
            try
            {
                // Verificar que el usuario tenga acceso a esta solicitud
                var idEmpleadoSesion = HttpContext.Session.GetInt32("IdEmpleado");
                var rolUsuario = HttpContext.Session.GetString("Rol");

                // Solo empleados pueden descargar sus propias solicitudes, otros roles pueden descargar cualquiera
                if (rolUsuario == "Empleado")
                {
                    var solicitudEmpleado = await _daoSolicitud.ObtenerDetalleSolicitudAsync(idSolicitud);
                    if (solicitudEmpleado?.Encabezado?.IdEmpleado != idEmpleadoSesion)
                    {
                        TempData["ErrorMessage"] = "No tienes permisos para descargar este PDF.";
                        return RedirectToAction("DetallePDF", new { id = idSolicitud, soloVer = true });
                    }
                }

                // Generar el PDF usando QuestPDF (con ambas firmas si está autorizada)
                var pdfBytes = await _pdfService.GenerarPDFSolicitudAsync(idSolicitud);

                if (pdfBytes == null || pdfBytes.Length == 0)
                {
                    TempData["ErrorMessage"] = "No se pudo generar el PDF.";
                    // Redirigir según el rol
                    if (rolUsuario == "Autorizador" || rolUsuario == "TeamLider" || rolUsuario == "SubTeamLider")
                    {
                        return RedirectToAction("Detalle", new { id = idSolicitud });
                    }
                    else if (rolUsuario == "RRHH")
                    {
                        return RedirectToAction("DetalleRH", new { id = idSolicitud });
                    }
                    return RedirectToAction("DetallePDF", new { id = idSolicitud, soloVer = true });
                }

                var nombreArchivo = $"Solicitud_Vacaciones_{idSolicitud}.pdf";

                // Log de descarga para auditoría
                var usuario = HttpContext.Session.GetString("Usuario") ?? "Sistema";
                await _bitacoraService.RegistrarBitacoraAsync("Descarga PDF", $"Usuario {usuario} descargó PDF de solicitud {idSolicitud}");

                return File(pdfBytes, "application/pdf", nombreArchivo);
            }
            catch (Exception ex)
            {
                await RegistrarError($"descargar PDF de solicitud {idSolicitud}", ex);
                TempData["ErrorMessage"] = "Error al descargar el PDF. Inténtalo de nuevo.";
                return RedirectToAction("Index");
            }
        }
        
        // Vista principal para autorizar solicitudes
        // GET: SolicitudesController/Solicitudes
        [AuthorizeRole("SuperAdministrador", "Autorizador", "TeamLider", "SubTeamLider")]
        public async Task<ActionResult> Detalle(int id)
        {
            try
            {
                var solicitud = await _daoSolicitud.ObtenerDetalleSolicitudAsync(id);

                if (solicitud == null)
                {
                    TempData["ErrorMessage"] = "La solicitud no fue encontrada.";
                    return RedirectToAction("Solicitudes");
                }

                // Obtener el empleado dueño de la solicitud
                var empleadoSolicitud = await _daoEmpleado.ObtenerEmpleadoPorIdAsync(solicitud.Encabezado.IdEmpleado);
                if (empleadoSolicitud != null)
                {
                    // Asignar el nombre completo del empleado al modelo
                    solicitud.Encabezado.NombreEmpleado = $"{empleadoSolicitud.NombresEmpleado} {empleadoSolicitud.ApellidosEmpleado}";
                }

                ViewBag.Empleado = empleadoSolicitud;

                return View(solicitud);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error al cargar la solicitud: " + ex.Message;
                return RedirectToAction("Solicitudes");//error coregido
            }
        }

        // Método para ver el documento firmado de una solicitud
        [HttpGet]
        public async Task<IActionResult> VerDocumento(int idSolicitud)
        {
            var solicitud = await _daoSolicitud.ObtenerDetalleSolicitudAsync(idSolicitud);

            if (solicitud?.Encabezado?.DocumentoFirmadoData == null)
                return NotFound("Documento no encontrado");

            return File(
                solicitud.Encabezado.DocumentoFirmadoData,
                solicitud.Encabezado.DocumentoContentType ?? "application/pdf"
            );
        }


        /*----------ErickDev-------*/
        /*Este método carga los datos de una solicitud específica */
        // GET: SolicitudesController/Solicitudes/DetalleRH 
        [AuthorizeRole("SuperAdministrador", "Autorizador", "TeamLider", "SubTeamLider", "RRHH")]
        //este solo lo agrege para poder acceder se puede remover:
        [HttpGet("Solicitudes/DetalleRH/{id}")]

        public async Task<ActionResult> DetalleRH(int id)
        {
            try
            {
                // RRHH ve las solicitudes en modo solo lectura (como el empleado)
                // Redirigir a DetallePDF con soloVer=true
                return RedirectToAction("DetallePDF", new { id = id, soloVer = true });
            }
            catch (Exception ex)
            {
                await RegistrarError("DetalleRH", ex);
                TempData["ErrorMessage"] = "Error al cargar la solicitud: " + ex.Message;
                return RedirectToAction("RecursosHumanos");
            }
        }
        /*-----End ErickDev---------*/

        //  =====================================================================
        //  = CANCELAR SOLICITUD (UNIFICADO PARA EMPLEADO, RRHH Y ADMIN)      =
        //  =====================================================================
        [HttpPost]
        [AuthorizeRole("Empleado", "SuperAdministrador", "RRHH")]
        public async Task<IActionResult> CancelarSolicitud(int idSolicitud)
        {
            try
            {
                if (idSolicitud == 0)
                {
                    TempData["ErrorMessage"] = "El ID de la solicitud no es válido.";
                    // Redirigir según el rol
                    if (User.IsInRole("SuperAdministrador") || User.IsInRole("RRHH"))
                    {
                        return RedirectToAction(nameof(RecursosHumanos));
                    }
                    return RedirectToAction(nameof(Index));
                }

                // La lógica de negocio (validar estado, devolver días, cambiar estado) 
                // está centralizada en el DAO, que llama al Stored Procedure.
                var cancelada = await _daoSolicitud.CancelarSolicitud(idSolicitud);

                if (cancelada)
                {
                    // Actualizar días acumulados del empleado (devolver días)
                    try
                    {
                        var solicitud = await _daoSolicitud.ObtenerDetalleSolicitudAsync(idSolicitud);
                        if (solicitud != null && solicitud.Encabezado != null)
                        {
                            await _daoEmpleado.ActualizarDiasAcumuladosEmpleadoAsync(solicitud.Encabezado.IdEmpleado);
                            await _bitacoraService.RegistrarBitacoraAsync("Días Actualizados", $"Días devueltos al empleado {solicitud.Encabezado.IdEmpleado} después de cancelar solicitud {idSolicitud}");
                        }
                    }
                    catch (Exception diasEx)
                    {
                        _logger.LogWarning($"⚠️ No se pudieron actualizar días después de cancelar: {diasEx.Message}");
                    }

                    TempData["SuccessMessage"] = "La solicitud ha sido cancelada exitosamente.";
                    await _bitacoraService.RegistrarBitacoraAsync("Cancelar Solicitud", $"La solicitud {idSolicitud} fue cancelada.");
                }
                else
                {
                    TempData["ErrorMessage"] = "No se pudo cancelar la solicitud. Es posible que no se encuentre en un estado que permita la cancelación (Ingresada o Autorizada).";
                }
            }
            catch (Exception ex)
            {
                await RegistrarError("cancelar solicitud", ex);
                TempData["ErrorMessage"] = "Ocurrió un error al intentar cancelar la solicitud.";
            }

            // Redirigir a la vista correcta según el rol del usuario
            if (User.IsInRole("SuperAdministrador") || User.IsInRole("RRHH"))
            {
                return RedirectToAction(nameof(RecursosHumanos));
            }

            return RedirectToAction(nameof(Index));
        }

        public async Task Firma()
        {

        }

        /*=================================================   
		==   SOLICITUD POR CUMPLEAÑOS - CREAR Y MOSTRAR   == 
		=================================================*/
        [HttpGet]
        [AuthorizeRole("Empleado", "SuperAdministrador")]
        public async Task<IActionResult> SolicitudCumpleanos(int idAutorizador, int tipoFormato)
        {
            try
            {
                _logger.LogInformation("🎂 SolicitudCumpleanos - Parámetros recibidos: idAutorizador={IdAutorizador}, tipoFormato={TipoFormato}", idAutorizador, tipoFormato);

                // Validar parámetros
                if (idAutorizador <= 0 || (tipoFormato != 1 && tipoFormato != 2))
                {
                    _logger.LogWarning("⚠️ Parámetros inválidos: idAutorizador={IdAutorizador}, tipoFormato={TipoFormato}", idAutorizador, tipoFormato);
                    TempData["ErrorMessage"] = "Parámetros inválidos. Por favor, selecciona un autorizador y formato válidos.";
                    return RedirectToAction(nameof(Index));
                }

                // Obtener empleado de sesión
                var idEmpleado = HttpContext.Session.GetInt32("IdEmpleado");
                if (idEmpleado == null)
                {
                    return RedirectToAction("Index", "Login");
                }

                var empleado = await _daoEmpleado.ObtenerEmpleadoPorIdAsync(idEmpleado.Value);
                if (empleado == null)
                {
                    _logger.LogError("❌ Empleado no encontrado con ID: {IdEmpleado}", idEmpleado.Value);
                    TempData["ErrorMessage"] = "No se encontró el perfil del empleado.";
                    return RedirectToAction(nameof(Index));
                }

                _logger.LogInformation("✅ Empleado encontrado: {Nombre}", $"{empleado.NombresEmpleado} {empleado.ApellidosEmpleado}");

                // Calcular fecha de cumpleaños para este año
                var fechaCumpleanos = new DateTime(DateTime.Now.Year, empleado.FechaNacimiento.Month, empleado.FechaNacimiento.Day);
                _logger.LogInformation("📅 Fecha de cumpleaños calculada: {Fecha}", fechaCumpleanos.ToString("dd/MM/yyyy"));

                // Crear solicitud temporal en BD para poder generar el PDF
                _logger.LogInformation("💾 Creando solicitud en BD...");
                var solicitud = new SolicitudViewModel
                {
                    Encabezado = new SolicitudEncabezadoViewModel
                    {
                        NombreEmpleado = $"{empleado.NombresEmpleado} {empleado.ApellidosEmpleado}",
                        DiasSolicitadosTotal = 1,
                        FechaIngresoSolicitud = DateTime.Now,
                        Observaciones = "Día de cumpleaños",
                        TipoFormatoPdf = tipoFormato,
                        IdAutorizador = idAutorizador,
                        IdEmpleado = empleado.IdEmpleado,
                        SolicitudLider = "No", // Solicitud de cumpleaños no requiere aprobación de líder
                        Estado = 1 // 1 = Ingresada (estado inicial)
                    },
                    Detalles = new List<SolicitudDetalleViewModel>
                    {
                        new SolicitudDetalleViewModel
                        {
                            FechaInicio = fechaCumpleanos,
                            FechaFin = fechaCumpleanos,
                            DiasHabilesTomados = 1
                        }
                    }
                };

                // Crear la solicitud en BD
                var idSolicitudCreada = await _daoSolicitud.InsertarSolicitudAsync(solicitud);
                _logger.LogInformation("💾 Solicitud creada con ID: {IdSolicitud}", idSolicitudCreada);

                if (idSolicitudCreada > 0)
                {
                    // Pasar el ID de la solicitud a la vista
                    ViewBag.IdSolicitud = idSolicitudCreada;
                    ViewBag.IdAutorizador = idAutorizador;
                    ViewBag.TipoFormato = tipoFormato;
                    ViewBag.FechaCumpleanos = empleado.FechaNacimiento;

                    await _bitacoraService.RegistrarBitacoraAsync("Vista Solicitud Cumpleaños", $"Solicitud temporal #{idSolicitudCreada} creada para previsualización");

                    _logger.LogInformation("✅ Redirigiendo a vista SolicitudCumpleanos con ID: {IdSolicitud}", idSolicitudCreada);
                    return View(new SolicitudViewModel { Encabezado = new SolicitudEncabezadoViewModel { IdSolicitud = idSolicitudCreada } });
                }
                else
                {
                    _logger.LogError("❌ No se pudo crear la solicitud (ID = 0)");
                    TempData["ErrorMessage"] = "No se pudo crear la solicitud temporal.";
                    return RedirectToAction(nameof(Index));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ ERROR al crear solicitud por cumpleaños");
                await RegistrarError("crear solicitud por cumpleaños", ex);
                TempData["ErrorMessage"] = $"Ocurrió un error al crear la solicitud: {ex.Message}";
                return RedirectToAction(nameof(Index));
            }
        }

        /*=================================================   
		==   GENERAR PDF CUMPLEAÑOS (IFRAME)           == 
		=================================================*/
        [HttpGet]
        [AuthorizeRole("Empleado", "SuperAdministrador")]
        public async Task<IActionResult> GenerarPDFCumpleanos(int idSolicitud)
        {
            try
            {
                // Generar PDF usando el ID de la solicitud
                var pdfBytes = await _pdfService.GenerarPDFSolicitudAsync(idSolicitud);

                if (pdfBytes == null || pdfBytes.Length == 0)
                {
                    return BadRequest("Error al generar el PDF");
                }

                return File(pdfBytes, "application/pdf");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al generar PDF de cumpleaños");
                return BadRequest("Error al generar el PDF");
            }
        }

        /*=================================================   
		==   ENVIAR SOLICITUD POR CUMPLEAÑOS           == 
		=================================================*/
        [HttpPost]
        [AuthorizeRole("Empleado", "SuperAdministrador")]
        public async Task<IActionResult> EnviarSolicitudCumpleanos(int idSolicitud, int idAutorizador)
        {
            try
            {
                // La solicitud ya está creada, solo enviamos el email al autorizador
                if (idSolicitud <= 0 || idAutorizador <= 0)
                {
                    TempData["ErrorMessage"] = "Parámetros inválidos.";
                    return RedirectToAction(nameof(Index));
                }

                // Obtener empleado de sesión
                var idEmpleado = HttpContext.Session.GetInt32("IdEmpleado");
                if (idEmpleado == null)
                {
                    return RedirectToAction("Index", "Login");
                }

                var empleado = await _daoEmpleado.ObtenerEmpleadoPorIdAsync(idEmpleado.Value);
                if (empleado == null)
                {
                    TempData["ErrorMessage"] = "No se encontró el perfil del empleado.";
                    return RedirectToAction(nameof(Index));
                }

                // Enviar email al autorizador
                try
                {
                    // Obtener el IdEmpleado del usuario autorizador
                    int? idEmpleadoAutorizador = null;
                    using (var connection = new SqlConnection(_configuration.GetConnectionString("DefaultConnection")))
                    {
                        await connection.OpenAsync();
                        using (var cmd = new SqlCommand("SELECT FK_IdEmpleado FROM Usuarios WHERE IdUsuario = @IdUsuario", connection))
                        {
                            cmd.Parameters.AddWithValue("@IdUsuario", idAutorizador);
                            var resultado = await cmd.ExecuteScalarAsync();
                            if (resultado != null && resultado != DBNull.Value)
                            {
                                idEmpleadoAutorizador = Convert.ToInt32(resultado);
                            }
                        }
                    }

                    if (idEmpleadoAutorizador.HasValue)
                    {
                        var autorizador = await _daoEmpleado.ObtenerEmpleadoPorIdAsync(idEmpleadoAutorizador.Value);
                        if (autorizador != null && !string.IsNullOrEmpty(autorizador.CorreoInstitucional))
                        {
                            // Calcular fecha de cumpleaños
                            var fechaCumpleanos = new DateTime(DateTime.Now.Year, empleado.FechaNacimiento.Month, empleado.FechaNacimiento.Day);
                            
                            // URL para autorizar la solicitud
                            var urlAutorizar = Url.Action("Index", "Solicitudes", null, Request.Scheme);
                            
                            // Enviar email de notificación
                            await _emailService.EnviarNotificacionNuevaSolicitudAsync(
                                nombreEmpleado: $"{empleado.NombresEmpleado} {empleado.ApellidosEmpleado}",
                                nombreAutorizador: $"{autorizador.NombresEmpleado} {autorizador.ApellidosEmpleado}",
                                correoAutorizador: autorizador.CorreoInstitucional,
                                numeroSolicitud: idSolicitud,
                                diasSolicitados: 1,
                                fechaInicio: fechaCumpleanos,
                                fechaFin: fechaCumpleanos,
                                urlAutorizar: urlAutorizar
                            );
                            
                            _logger.LogInformation("✅ Email de cumpleaños enviado exitosamente a {Email} para solicitud #{IdSolicitud}", autorizador.CorreoInstitucional, idSolicitud);
                        }
                        else
                        {
                            _logger.LogWarning("⚠️ Autorizador sin correo institucional. ID: {IdAutorizador}", idEmpleadoAutorizador.Value);
                        }
                    }
                    else
                    {
                        _logger.LogWarning("⚠️ No se encontró el empleado autorizador para el usuario ID: {IdUsuario}", idAutorizador);
                    }
                }
                catch (Exception exEmail)
                {
                    _logger.LogError(exEmail, "❌ Error al enviar el email de notificación");
                    // No lanzamos la excepción para que la solicitud se cree igual
                }

                await _bitacoraService.RegistrarBitacoraAsync("Solicitud Cumpleaños Enviada", $"Solicitud #{idSolicitud} por cumpleaños enviada al autorizador");

                TempData["SuccessMessage"] = "¡Solicitud por cumpleaños enviada exitosamente! 🎉";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                await RegistrarError("enviar solicitud por cumpleaños", ex);
                TempData["ErrorMessage"] = "Ocurrió un error al enviar la solicitud.";
                return RedirectToAction(nameof(Index));
            }
        }

        /*=================================================   
		==   MÉTODO HELPER: Obtener Días Disponibles  == 
		=================================================*/
        // NOTA: Los días disponibles ya están calculados por el SP sp_ActualizarDiasAcumuladosEmpleado
        // que se ejecuta automáticamente al aprobar/rechazar/cancelar solicitudes.
        // Este método solo devuelve el valor ya calculado en DiasVacacionesAcumulados.
        private async Task<double> CalcularDiasDisponiblesAsync(int idEmpleado)
        {
            try
            {
                // Obtener empleado
                var empleado = await _daoEmpleado.ObtenerEmpleadoPorIdAsync(idEmpleado);
                if (empleado == null) return 0;

                // El SP ya calculó: DiasAcumulados - DiasTomadosHistoricos - DiasSolicitadosEnSistema
                // Solo devolvemos el valor actualizado
                decimal diasDisponibles = empleado.DiasVacacionesAcumulados;

                // No permitir valores negativos
                return (double)(diasDisponibles < 0 ? 0 : diasDisponibles);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener días disponibles para empleado {IdEmpleado}", idEmpleado);
                return 0;
            }
        }


    }
}
