using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ProyectoDojoGeko.Data;
using ProyectoDojoGeko.Dtos.Login.Requests;
using ProyectoDojoGeko.Helper;
using ProyectoDojoGeko.Models;
using ProyectoDojoGeko.Models.Usuario;
using ProyectoDojoGeko.Services.Interfaces;

namespace ProyectoDojoGeko.Controllers
{
    public class LoginController : Controller
    {
        private readonly daoTokenUsuario _daoTokenUsuario;
        private readonly daoBitacoraWSAsync _daoBitacora;
        private readonly daoUsuarioWSAsync _daoUsuario;
        private readonly daoEmpleadoWSAsync _daoEmpleado;
        private readonly daoEmpleadosEmpresaDepartamentoWSAsync _daoEmpleadoEmpresaDepartamento;
        private readonly EmailService _emailService;
        private readonly daoUsuariosRolWSAsync _daoRolUsuario;
        private readonly daoRolesWSAsync _daoRol;
        private readonly daoRolPermisosWSAsync _daoRolPermisos;
        private readonly ILoggingService _loggingService;

        public LoginController(
            EmailService emailService,
            daoTokenUsuario daoTokenUsuario,
            daoBitacoraWSAsync daoBitacora,
            daoUsuarioWSAsync daoUsuario,
            daoEmpleadoWSAsync daoEmpleado,
            daoEmpleadosEmpresaDepartamentoWSAsync daoEmpleadoEmpresaDepartamento,
            daoUsuariosRolWSAsync daoRolUsuario,
            daoRolesWSAsync daoRol,
            daoRolPermisosWSAsync daoRolPermisos,
            ILoggingService loggingService)
        {
            _emailService = emailService;
            _daoTokenUsuario = daoTokenUsuario;
            _daoBitacora = daoBitacora;
            _daoUsuario = daoUsuario;
            _daoEmpleado = daoEmpleado;
            _daoEmpleadoEmpresaDepartamento = daoEmpleadoEmpresaDepartamento;
            _daoRolUsuario = daoRolUsuario;
            _daoRol = daoRol;
            _daoRolPermisos = daoRolPermisos;
            _loggingService = loggingService;
        }
        public IActionResult Index() => View();

        [HttpGet]
        public IActionResult IndexCambioContrasenia() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginRequest request)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.Mensaje = "Datos de inicio de sesión inválidos";
                return RedirectToAction("Index", "Login");
            }

            try
            {
                // Validar usuario y contraseña
                var usuarioValido = _daoTokenUsuario.ValidarUsuario(request.Usuario, request.Password);

                if (usuarioValido == null)
                {
                    ViewBag.Mensaje = "Usuario o contraseña incorrectos.";
                    return RedirectToAction("Index", "Login");
                }

                // Obtener información del usuario
                var usuario = await _daoUsuario.ObtenerUsuarioPorIdAsync(usuarioValido.IdUsuario);
                if (usuario == null)
                {
                    ViewBag.Mensaje = "Usuario no encontrado.";
                    return RedirectToAction("Index", "Login");
                }

                // Verificar si el usuario está activo
                if (usuario.FK_IdEstado != 1) // Asumiendo que 1 es el estado activo
                {
                    ViewBag.Mensaje = "Su cuenta no está activa. Por favor, contacte al administrador.";
                    return RedirectToAction("Index", "Login");
                }

                // Obtener roles del usuario
                var rolesUsuario = await _daoRolUsuario.ObtenerUsuariosRolPorIdUsuarioAsync(usuarioValido.IdUsuario);
                if (rolesUsuario == null || !rolesUsuario.Any())
                {
                    ViewBag.Mensaje = "El usuario no tiene roles asignados.";
                    return RedirectToAction("Index", "Login");
                }

                // Obtener información del empleado
                var empleados = await _daoEmpleado.ObtenerEmpleadoPorIdAsync(usuarioValido.FK_IdEmpleado);
                if (empleados == null)
                {
                    ViewBag.Mensaje = "No se encontró información del empleado.";
                    return RedirectToAction("Index", "Login");
                }

                // Generar token JWT
                var jwtHelper = new JwtHelper();
                var rolPrincipal = await _daoRol.ObtenerRolPorIdAsync(rolesUsuario.First().FK_IdRol);
                var tokenModel = jwtHelper.GenerarToken(
                    usuarioValido.IdUsuario, 
                    usuarioValido.Username, 
                    rolesUsuario.First().FK_IdRol, 
                    rolPrincipal?.NombreRol ?? "Usuario");

                if (tokenModel == null)
                {
                    ViewBag.Mensaje = "Error al generar el token de autenticación.";
                    return RedirectToAction("Index", "Login");
                }

                // Guardar token en la base de datos
                _daoTokenUsuario.RevocarToken(usuarioValido.IdUsuario);
                _daoTokenUsuario.GuardarToken(new TokenUsuarioViewModel
                {
                    FK_IdUsuario = usuarioValido.IdUsuario,
                    Token = tokenModel.Token,
                    FechaCreacion = tokenModel.FechaCreacion,
                    TiempoExpira = tokenModel.TiempoExpira
                });

                // Configurar datos de sesión
                var nombreCompletoEmpleado = $"{empleados.NombresEmpleado} {empleados.ApellidosEmpleado}";
                var roles = new List<string>();
                
                foreach (var rol in rolesUsuario)
                {
                    var r = await _daoRol.ObtenerRolPorIdAsync(rol.FK_IdRol);
                    if (r != null)
                    {
                        roles.Add(r.NombreRol);
                    }
                }

                // Obtener la empresa del empleado
                int idEmpresa = 0;
                try
                {
                    // Obtener la relación empleado-empresa
                    var empleadoEmpresa = await _daoEmpleadoEmpresaDepartamento.ObtenerEmpleadoPorIdAsync(empleados.IdEmpleado);
                    if (empleadoEmpresa != null)
                    {
                        idEmpresa = empleadoEmpresa.FK_IdEmpresa;
                    }
                }
                catch
                {
                    // Si hay un error, usar 0 como valor por defecto
                    idEmpresa = 0;
                }

                // Configurar la sesión
                HttpContext.Session.SetString("TipoContrato", empleados.TipoContrato);
                HttpContext.Session.SetInt32("IdEmpleado", empleados.IdEmpleado);
                HttpContext.Session.SetString("CodigoEmpleado", empleados.CodigoEmpleado);
                HttpContext.Session.SetString("NombreCompletoEmpleado", nombreCompletoEmpleado);
                HttpContext.Session.SetString("Token", tokenModel.Token);
                HttpContext.Session.SetInt32("IdUsuario", usuarioValido.IdUsuario);
                HttpContext.Session.SetString("Usuario", usuarioValido.Username);
                HttpContext.Session.SetString("Rol", rolPrincipal?.NombreRol ?? "Usuario");
                HttpContext.Session.SetString("Roles", string.Join(",", roles));
                HttpContext.Session.SetInt32("IdSistema", 1); // Ajustar según sea necesario
                HttpContext.Session.SetInt32("IdEmpresa", idEmpresa);

                // Registrar en bitácora
                await _daoBitacora.InsertarBitacoraAsync(new BitacoraViewModel
                {
                    Accion = "Login",
                    Descripcion = $"Inicio de sesión exitoso para el usuario {usuarioValido.Username}.",
                    FK_IdUsuario = usuarioValido.IdUsuario,
                    FK_IdSistema = 1 // Ajustar según sea necesario
                });

                return RedirectToAction("Dashboard", "Dashboard");
            }
            catch (Exception e)
            {
                await _loggingService.RegistrarLogAsync(new LogViewModel
                {
                    Accion = "Error Login",
                    Descripcion = $"Error en el proceso de login para usuario {request.Usuario}: {e.Message}",
                    Estado = false
                });

                ViewBag.Mensaje = "Error al procesar la solicitud. Por favor, inténtelo de nuevo.";
                return RedirectToAction("Index", "Login");
            }
        }


        [HttpPost]
        public async Task<IActionResult> LoginCambioContrasenia(string usuario, string password)
        {
            try
            {
                var usuarioValido = _daoTokenUsuario.ValidarUsuarioCambioContrasenia(usuario, password);

                if (usuarioValido == null)
                {
                    ViewBag.Mensaje = "Usuario o clave incorrectos.";
                    return View("IndexCambioContrasenia");
                }

                var jwtHelper = new JwtHelper();
                var rolesUsuario = await _daoRolUsuario.ObtenerUsuariosRolPorIdUsuarioAsync(usuarioValido.IdUsuario);

                if (rolesUsuario == null || !rolesUsuario.Any())
                {
                    ViewBag.Mensaje = "Usuario no tiene rol asignado o no está activo.";
                    return RedirectToAction("IndexCambioContrasenia", "Login");
                }

                var rolUsuario = rolesUsuario.First();
                var idRol = rolUsuario.FK_IdRol;
                var roles = await _daoRol.ObtenerRolPorIdAsync(idRol);

                if (roles == null)
                {
                    ViewBag.Mensaje = "El Rol no existe.";
                    return RedirectToAction("IndexCambioContrasenia", "Login");
                }

                /*var sistemaRol = await _daoRolPermisos.ObtenerRolPermisosPorIdRolAsync(idRol);

                if (sistemaRol == null || !sistemaRol.Any())
                {
                    ViewBag.Mensaje = "El sistema asociado a este rol no existe.";
                    return RedirectToAction("IndexCambioContrasenia", "Login");
                }*/

                //var idSistema = sistemaRol.First().FK_IdSistema;
                var idSistema = 0;
                var rol = roles.NombreRol;

                var tokenModel = jwtHelper.GenerarToken(usuarioValido.IdUsuario, usuarioValido.Username, idRol, rol);

                if (tokenModel != null)
                {
                    _daoTokenUsuario.GuardarToken(new TokenUsuarioViewModel
                    {
                        FK_IdUsuario = usuarioValido.IdUsuario,
                        Token = tokenModel.Token,
                        FechaCreacion = tokenModel.FechaCreacion,
                        TiempoExpira = tokenModel.TiempoExpira
                    });

                    HttpContext.Session.SetString("Token", tokenModel.Token);
                    HttpContext.Session.SetInt32("IdUsuario", usuarioValido.IdUsuario);
                    HttpContext.Session.SetString("Usuario", usuario);
                    HttpContext.Session.SetString("Rol", rol);
                    HttpContext.Session.SetInt32("IdSistema", idSistema);

                    await _daoBitacora.InsertarBitacoraAsync(new BitacoraViewModel
                    {
                        Accion = "Login Cambio Contraseña",
                        Descripcion = $"Inicio de sesión con cambio de contraseña para el usuario {usuario}.",
                        FK_IdUsuario = usuarioValido.IdUsuario,
                        FK_IdSistema = idSistema
                    });

                    return RedirectToAction(nameof(CambioContrasena));
                }
                else
                {
                    ViewBag.Mensaje = "No se pudo generar el token.";
                    return RedirectToAction(nameof(LoginCambioContrasenia));
                }
            }
            catch (Exception e)
            {
                await _loggingService.RegistrarLogAsync(new LogViewModel
                {
                    Accion = "Error LoginCambioContrasenia",
                    Descripcion = $"Error en el proceso de login para usuario {usuario}: {e.Message}",
                    Estado = false
                });

                ViewBag.Mensaje = "Error al procesar la solicitud. Por favor, inténtelo de nuevo.";
                return View("IndexCambioContrasenia");
            }
        }
        [HttpPost]
        public async Task<IActionResult> SolicitarNuevaContrasenia(string username)
        {
            try
            {
                var usuarios = await _daoUsuario.ObtenerUsuariosAsync();
                if (usuarios == null)
                    return Json(new { success = false, message = "Usuario no encontrado" });

                var usuario = usuarios.FirstOrDefault(u => u.Username == username);
                if (usuario == null)
                    return Json(new { success = false, message = "Usuario no encontrado" });

                var empleados = await _daoEmpleado.ObtenerEmpleadoAsync();
                var correo = empleados.FirstOrDefault(e => e.IdEmpleado == usuario.FK_IdEmpleado);
                if (correo == null)
                    return Json(new { success = false, message = "No se han encontrado correos asociados al usuario" });

                string nuevaContrasenia = daoUsuarioWSAsync.GenerarContraseniaAleatoria();
                //string hash = BCrypt.Net.BCrypt.HashPassword(nuevaContrasenia);
                DateTime nuevaExpiracion = DateTime.UtcNow.AddHours(1);

                await _daoUsuario.ActualizarContraseniaExpiracionAsync(usuario.IdUsuario, nuevaContrasenia, nuevaExpiracion);

                string urlCambioPassword = "https://localhost:7001/CambioContrasena";

                await _emailService.EnviarCorreoConMailjetAsync(usuario.Username, correo.CorreoInstitucional, nuevaContrasenia, urlCambioPassword);

                return Json(new { success = true, message = "Se ha enviado una nueva contraseña a tu correo." });
            }
            catch (Exception e)
            {
                await _loggingService.RegistrarLogAsync(new LogViewModel
                {
                    Accion = "Error SolicitarNuevaContrasenia",
                    Descripcion = $"Error al generar nueva contraseña para {username}: {e.Message}",
                    Estado = false
                });

                return Json(new { success = false, message = "Error al procesar la solicitud." });
            }
        }

        [HttpGet]
        public IActionResult CambioContrasena()
        {
            try
            {
                return View();
            }
            catch (Exception e)
            {
                _loggingService.RegistrarLogAsync(new LogViewModel
                {
                    Accion = "Error CambioContrasena GET",
                    Descripcion = $"Error al cargar la página de cambio de contraseña: {e.Message}",
                    Estado = false
                });

                ViewBag.Mensaje = "Error al cargar la página. Por favor, inténtelo de nuevo.";
                return View();
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CambioContrasena(string contraseñaActual, string nuevaContraseña, string confirmarContraseña)
        {
            try
            {
                var idUsuario = HttpContext.Session.GetInt32("IdUsuario");
                var usuario = HttpContext.Session.GetString("Usuario");
                var idSistema = HttpContext.Session.GetInt32("IdSistema");

                if (idUsuario == null || string.IsNullOrEmpty(usuario))
                    return RedirectToAction("Index", "Login");

                ViewBag.Usuario = usuario;

                if (string.IsNullOrEmpty(contraseñaActual) || string.IsNullOrEmpty(nuevaContraseña) || string.IsNullOrEmpty(confirmarContraseña))
                {
                    ViewBag.Mensaje = "Todos los campos son obligatorios.";
                    return View();
                }

                if (nuevaContraseña != confirmarContraseña)
                {
                    ViewBag.Mensaje = "Las contraseñas nuevas no coinciden.";
                    return View();
                }

                if (nuevaContraseña.Length < 8)
                {
                    ViewBag.Mensaje = "La nueva contraseña debe tener al menos 8 caracteres.";
                    return View();
                }

                bool tieneMayuscula = nuevaContraseña.Any(char.IsUpper);
                bool tieneMinuscula = nuevaContraseña.Any(char.IsLower);
                bool tieneNumero = nuevaContraseña.Any(char.IsDigit);

                if (!tieneMayuscula || !tieneMinuscula || !tieneNumero)
                {
                    ViewBag.Mensaje = "La contraseña debe contener al menos una letra mayúscula, una minúscula y un número.";
                    return View();
                }

                var usuarioValido = _daoTokenUsuario.ValidarUsuarioCambioContrasenia(usuario, contraseñaActual);

                if (usuarioValido == null)
                {
                    ViewBag.Mensaje = "La contraseña actual es incorrecta.";
                    return View();
                }

                if (contraseñaActual == nuevaContraseña)
                {
                    ViewBag.Mensaje = "La nueva contraseña debe ser diferente a la actual.";
                    return View();
                }

                string hashNuevaContraseña = BCrypt.Net.BCrypt.HashPassword(nuevaContraseña);

                try
                {
                    _daoTokenUsuario.GuardarContrasenia(usuarioValido.IdUsuario, hashNuevaContraseña);

                    await _daoBitacora.InsertarBitacoraAsync(new BitacoraViewModel
                    {
                        Accion = "Cambio de Contraseña",
                        Descripcion = $"El usuario {usuario} ha cambiado su contraseña exitosamente.",
                        FK_IdUsuario = usuarioValido.IdUsuario,
                        FK_IdSistema = idSistema ?? 1
                    });

                    _daoTokenUsuario.RevocarToken(usuarioValido.IdUsuario);
                    HttpContext.Session.Clear();

                    TempData["MensajeExito"] = "Contraseña cambiada exitosamente. Por favor, inicie sesión nuevamente.";
                    return RedirectToAction("Index", "Login");
                }
                catch (Exception ex)
                {
                    await _loggingService.RegistrarLogAsync(new LogViewModel
                    {
                        Accion = "Error GuardarContrasenia",
                        Descripcion = $"Error al guardar la nueva contraseña para el usuario {usuario}: {ex.Message}",
                        Estado = false
                    });

                    ViewBag.Mensaje = "Error al actualizar la contraseña. Por favor, inténtelo de nuevo.";
                    return View();
                }
            }
            catch (Exception e)
            {
                await _loggingService.RegistrarLogAsync(new LogViewModel
                {
                    Accion = "Error CambioContrasena POST",
                    Descripcion = $"Error al procesar el cambio de contraseña: {e.Message}",
                    Estado = false
                });

                ViewBag.Mensaje = "Error al procesar la solicitud. Por favor, inténtelo de nuevo.";
                return View();
            }
        }

        [HttpGet]
        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Index");
        }
    }
}