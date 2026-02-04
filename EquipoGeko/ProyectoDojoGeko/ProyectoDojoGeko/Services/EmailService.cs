
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using ProyectoDojoGeko.Models;
using System.Net;
using Microsoft.Data.SqlClient;

public class EmailService
{
    // Inyectamos las opciones de configuración de EmailSettings
    private readonly EmailSettings _settings;
    private readonly ILogger<EmailService> _logger;
    private readonly string _connectionString;
    private static readonly string LogFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "email_logs.txt");

    // Constructor que recibe las opciones de configuración de EmailSettings
    public EmailService(IOptions<EmailSettings> options, ILogger<EmailService> logger, IConfiguration configuration)
    {
        _settings = options.Value;
        _logger = logger;
        _connectionString = configuration.GetConnectionString("DefaultConnection");
    }

    // Método para escribir logs en base de datos
    private async Task GuardarLogEnBDAsync(string accion, string descripcion, bool estado = true)
    {
        try
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                
                string query = @"
                    INSERT INTO Logs (FechaEntrada, Accion, Descripcion, Estado)
                    VALUES (@FechaEntrada, @Accion, @Descripcion, @Estado)";
                
                using (var command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@FechaEntrada", DateTime.Now);
                    command.Parameters.AddWithValue("@Accion", accion);
                    command.Parameters.AddWithValue("@Descripcion", descripcion);
                    command.Parameters.AddWithValue("@Estado", estado);
                    
                    await command.ExecuteNonQueryAsync();
                }
            }
        }
        catch
        {
            // Si falla guardar en BD, no hacer nada para no romper el flujo
        }
    }
    
    // Método para escribir logs en archivo (backup)
    private void EscribirLogEnArchivo(string mensaje)
    {
        try
        {
            // Intentar múltiples ubicaciones
            var ubicaciones = new[]
            {
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "email_logs.txt"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "ProyectoDojoGeko", "email_logs.txt"),
                Path.Combine(@"C:\Temp", "email_logs.txt"),
                Path.Combine(Path.GetTempPath(), "email_logs.txt")
            };

            string logMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {mensaje}{Environment.NewLine}";
            
            foreach (var ubicacion in ubicaciones)
            {
                try
                {
                    // Crear directorio si no existe
                    var directorio = Path.GetDirectoryName(ubicacion);
                    if (!Directory.Exists(directorio))
                    {
                        Directory.CreateDirectory(directorio);
                    }
                    
                    File.AppendAllText(ubicacion, logMessage);
                    break; // Si funciona, salir del loop
                }
                catch
                {
                    // Intentar siguiente ubicación
                    continue;
                }
            }
        }
        catch
        {
            // Si todo falla, no hacer nada para no romper el flujo
        }
    }

    // Creamos la función asíncrona para enviar el correo electrónico utilizando MailKit
    public async Task EnviarCorreoConMailjetAsync(string usuario, string destino, string contrasenia, string urlCambioPassword)
    {
        try
        {
            // Guardar log en BD
            await GuardarLogEnBDAsync(
                "Envío de Correo - Inicio",
                $"Iniciando envío de correo a {destino} para usuario {usuario}. " +
                $"FromEmail: {_settings.FromEmail}, " +
                $"ApiKey configurado: {(!string.IsNullOrEmpty(_settings.ApiKey) ? "SÍ" : "NO")}, " +
                $"ApiSecret configurado: {(!string.IsNullOrEmpty(_settings.ApiSecret) ? "SÍ" : "NO")}",
                true
            );
            
            // Escribir en archivo de log adicional (backup)
            EscribirLogEnArchivo("=== INICIO ENVÍO DE CORREO ===");
            EscribirLogEnArchivo($"Fecha: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            EscribirLogEnArchivo($"Destinatario: {destino}");
            EscribirLogEnArchivo($"Usuario: {usuario}");
            EscribirLogEnArchivo($"FromEmail configurado: {_settings.FromEmail}");
            EscribirLogEnArchivo($"ApiKey configurado: {(!string.IsNullOrEmpty(_settings.ApiKey) ? "SÍ" : "NO")}");
            EscribirLogEnArchivo($"ApiSecret configurado: {(!string.IsNullOrEmpty(_settings.ApiSecret) ? "SÍ" : "NO")}");
            
            _logger.LogInformation("=== INICIO ENVÍO DE CORREO ===");
            _logger.LogInformation($"Destinatario: {destino}");
            _logger.LogInformation($"Usuario: {usuario}");
            _logger.LogInformation($"FromEmail configurado: {_settings.FromEmail}");
            _logger.LogInformation($"ApiKey configurado: {(!string.IsNullOrEmpty(_settings.ApiKey) ? "SÍ" : "NO")}");
            _logger.LogInformation($"ApiSecret configurado: {(!string.IsNullOrEmpty(_settings.ApiSecret) ? "SÍ" : "NO")}");

            // Validar que el destino no sea nulo o vacío
            if (string.IsNullOrEmpty(destino))
            {
                _logger.LogError("El correo de destino está vacío");
                throw new ArgumentException("El correo de destino no puede estar vacío");
            }
            
            _logger.LogInformation("Creando mensaje MIME...");
            var mensaje = new MimeMessage();
            mensaje.From.Add(new MailboxAddress(_settings.FromName, _settings.FromEmail));
            mensaje.To.Add(MailboxAddress.Parse(destino));

            mensaje.Subject = "Bienvenido - Cambia tu contraseña";
            _logger.LogInformation("Mensaje MIME creado correctamente");
            EscribirLogEnArchivo("Mensaje MIME creado correctamente");

            // Creamos el cuerpo del mensaje en HTML
            var html = @"
<div style='
    max-width: 600px;
    margin: 40px auto;
    font-family: ""Segoe UI"", Tahoma, Geneva, Verdana, sans-serif;
    border-radius: 10px;
    overflow: hidden;
    box-shadow: 0 0 15px rgba(0,0,0,0.1);
    background: linear-gradient(to right, #f8f9fa, #ffffff);
    color: #333;
'>
    <div style='
        background-color: #007bff;
        color: #fff;
        padding: 25px 30px;
        text-align: center;
    '>
        <h1 style='margin: 0; font-size: 24px;'>¡Bienvenido a Dojo .NET 2025!</h1>
    </div>

    <div style='padding: 30px;'>

        <p style='font-size: 16px; color: black;'>
            Hola, <strong>" + usuario + @"</strong>
        </p>

        <p style='font-size: 16px; line-height: 1.6;'>
            Hemos generado una <strong>contraseña temporal</strong> para que puedas iniciar sesión en el sistema. Asegúrate de cambiarla lo antes posible por seguridad.
        </p>

        <div style='
            font-size: 22px;
            background-color: #e9ecef;
            border: 1px dashed #6c757d;
            text-align: center;
            padding: 12px 20px;
            border-radius: 6px;
            letter-spacing: 1px;
            margin: 20px 0;
            font-weight: bold;
            color: #212529;
        '>
            " + WebUtility.HtmlEncode(contrasenia) + @"
        </div>

        <p style='font-size: 16px;'>
            Para cambiar tu contraseña, haz clic en el siguiente botón:
        </p>

        <div style='text-align: center; margin: 30px 0;'>
            <a href='" + urlCambioPassword + @"' style='
                background-color: #28a745;
                color: white;
                padding: 14px 28px;
                border-radius: 6px;
                text-decoration: none;
                font-size: 16px;
                font-weight: bold;
                box-shadow: 0 4px 10px rgba(0, 0, 0, 0.1);
            '>Cambiar Contraseña</a>
        </div>

        <p style='font-size: 14px; color: #6c757d;'>
            Si tú no solicitaste este acceso o consideras que fue un error, simplemente ignora este mensaje.
        </p>

        <hr style='margin: 40px 0; border: none; border-top: 1px solid #dee2e6;' />

        <p style='text-align: center; font-size: 13px; color: #adb5bd;'>
            © 2025 Dojo .NET | Todos los derechos reservados.
        </p>
    </div>
</div>";



        var builder = new BodyBuilder { HtmlBody = html };
        mensaje.Body = builder.ToMessageBody();
            _logger.LogInformation("Cuerpo del mensaje construido");

            // Configurar el cliente SMTP de Mailjet
            using var smtp = new SmtpClient();
            
            _logger.LogInformation("Intentando conectar a Mailjet (in-v3.mailjet.com:587)...");
            EscribirLogEnArchivo("Intentando conectar a Mailjet (in-v3.mailjet.com:587)...");
            await GuardarLogEnBDAsync("Envío de Correo - Conexión", $"Intentando conectar a Mailjet para {destino}", true);
            
            // Conectar al servidor SMTP de Mailjet
            await smtp.ConnectAsync("in-v3.mailjet.com", 587, SecureSocketOptions.StartTls);
            _logger.LogInformation("✅ Conexión establecida con Mailjet");
            EscribirLogEnArchivo("✅ Conexión establecida con Mailjet");
            await GuardarLogEnBDAsync("Envío de Correo - Conexión", $"✅ Conexión establecida con Mailjet para {destino}", true);

            _logger.LogInformation("Intentando autenticar...");
            EscribirLogEnArchivo("Intentando autenticar...");
            await GuardarLogEnBDAsync("Envío de Correo - Autenticación", $"Intentando autenticar con Mailjet para {destino}", true);
            
            // Autenticarse con las credenciales de Mailjet
            await smtp.AuthenticateAsync(_settings.ApiKey, _settings.ApiSecret);
            _logger.LogInformation("✅ Autenticación exitosa");
            EscribirLogEnArchivo("✅ Autenticación exitosa");
            await GuardarLogEnBDAsync("Envío de Correo - Autenticación", $"✅ Autenticación exitosa con Mailjet para {destino}", true);

            _logger.LogInformation("Enviando mensaje...");
            EscribirLogEnArchivo("Enviando mensaje...");
            await GuardarLogEnBDAsync("Envío de Correo - Enviando", $"Enviando mensaje a {destino}", true);
            
            // Enviar el mensaje
            await smtp.SendAsync(mensaje);
            _logger.LogInformation("✅ Mensaje enviado correctamente");
            EscribirLogEnArchivo("✅ Mensaje enviado correctamente");
            await GuardarLogEnBDAsync("Envío de Correo - Éxito", $"✅ Correo enviado exitosamente a {destino} para usuario {usuario}", true);

            // Desconectar del servidor SMTP
            await smtp.DisconnectAsync(true);
            _logger.LogInformation("=== FIN ENVÍO DE CORREO EXITOSO ===");
            EscribirLogEnArchivo("=== FIN ENVÍO DE CORREO EXITOSO ===");
            EscribirLogEnArchivo("");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"❌ ERROR AL ENVIAR CORREO: {ex.Message}");
            _logger.LogError($"Tipo de excepción: {ex.GetType().Name}");
            _logger.LogError($"Stack trace: {ex.StackTrace}");
            
            EscribirLogEnArchivo($"❌ ERROR AL ENVIAR CORREO: {ex.Message}");
            EscribirLogEnArchivo($"Tipo de excepción: {ex.GetType().Name}");
            EscribirLogEnArchivo($"Stack trace: {ex.StackTrace}");
            EscribirLogEnArchivo("");
            
            // Guardar error en BD
            await GuardarLogEnBDAsync(
                "Envío de Correo - ERROR",
                $"❌ ERROR al enviar correo a {destino} para usuario {usuario}. " +
                $"Error: {ex.Message}. Tipo: {ex.GetType().Name}. " +
                $"StackTrace: {ex.StackTrace?.Substring(0, Math.Min(500, ex.StackTrace?.Length ?? 0))}",
                false
            );
            
            // Re-lanzar la excepción para que sea manejada por el controlador
            throw;
        }
    }

    public async Task EnviarCorreoResetPasswordAsync(string destino, string urlResetPassword)
    {
        try
        {
            await GuardarLogEnBDAsync(
                "Envío de Correo ResetPassword - Inicio",
                $"Iniciando envío de correo de reset a {destino}. FromEmail: {_settings.FromEmail}",
                true
            );

            if (string.IsNullOrEmpty(destino))
            {
                _logger.LogError("El correo de destino está vacío");
                throw new ArgumentException("El correo de destino no puede estar vacío");
            }

            if (string.IsNullOrEmpty(urlResetPassword))
            {
                _logger.LogError("La URL de reset está vacía");
                throw new ArgumentException("La URL de restablecimiento no puede estar vacía");
            }

            var mensaje = new MimeMessage();
            mensaje.From.Add(new MailboxAddress(_settings.FromName, _settings.FromEmail));
            mensaje.To.Add(MailboxAddress.Parse(destino));
            mensaje.Subject = "Restablecer contraseña";

            var url = WebUtility.HtmlEncode(urlResetPassword);

            var html = $@"
                <div style='max-width:600px;margin:40px auto;font-family:Segoe UI,Tahoma,Geneva,Verdana,sans-serif;border-radius:10px;overflow:hidden;box-shadow:0 0 15px rgba(0,0,0,0.1);background:#fff;color:#333;'>
                  <div style='background-color:#007bff;color:#fff;padding:25px 30px;text-align:center;'>
                    <h1 style='margin:0;font-size:22px;'>Restablecer contraseña</h1>
                  </div>

                  <div style='padding:30px;'>
                    <p style='font-size:16px;line-height:1.6;'>
                      Recibimos una solicitud para restablecer la contraseña de tu cuenta.
                    </p>

                    <p style='font-size:16px;'>Para continuar, haz clic en el siguiente botón:</p>

                    <div style='text-align:center;margin:30px 0;'>
                      <a href='{url}' style='background-color:#28a745;color:#fff;padding:14px 28px;border-radius:6px;text-decoration:none;font-size:16px;font-weight:bold;box-shadow:0 4px 10px rgba(0,0,0,0.1);'>
                        Restablecer contraseña
                      </a>
                    </div>

                    <p style='font-size:14px;color:#6c757d;'>
                      Si tú no solicitaste este cambio, ignora este correo.
                    </p>
                  </div>
                </div>";



            var builder = new BodyBuilder { HtmlBody = html };
            mensaje.Body = builder.ToMessageBody();

            using var smtp = new SmtpClient();
            await smtp.ConnectAsync("in-v3.mailjet.com", 587, SecureSocketOptions.StartTls);
            await smtp.AuthenticateAsync(_settings.ApiKey, _settings.ApiSecret);
            await smtp.SendAsync(mensaje);
            await smtp.DisconnectAsync(true);

            await GuardarLogEnBDAsync(
                "Envío de Correo ResetPassword - Éxito",
                $" Correo de reset enviado exitosamente a {destino}",
                true
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"❌ ERROR AL ENVIAR CORREO RESET: {ex.Message}");
            await GuardarLogEnBDAsync(
                "Envío de Correo ResetPassword - ERROR",
                $"❌ ERROR al enviar correo de reset a {destino}. Error: {ex.Message}. Tipo: {ex.GetType().Name}.",
                false
            );
            throw;
        }
    }

    // Método para enviar notificación al autorizador sobre nueva solicitud
    public async Task EnviarNotificacionNuevaSolicitudAsync(
        string nombreEmpleado,
        string nombreAutorizador,
        string correoAutorizador,
        int numeroSolicitud,
        decimal diasSolicitados,
        DateTime fechaInicio,
        DateTime fechaFin,
        string urlAutorizar)
    {
        try
        {
            _logger.LogInformation("=== INICIO ENVÍO DE NOTIFICACIÓN ===");
            _logger.LogInformation($"Autorizador: {nombreAutorizador}");
            _logger.LogInformation($"Correo Autorizador: {correoAutorizador}");
            _logger.LogInformation($"Solicitud #: {numeroSolicitud}");

            // Validar que el correo de destino no sea nulo o vacío
            if (string.IsNullOrEmpty(correoAutorizador))
            {
                _logger.LogError("El correo del autorizador está vacío");
                throw new ArgumentException("El correo del autorizador no puede estar vacío");
            }

            _logger.LogInformation("Creando mensaje de notificación...");
            var mensaje = new MimeMessage();
            mensaje.From.Add(new MailboxAddress(_settings.FromName, _settings.FromEmail));
            mensaje.To.Add(MailboxAddress.Parse(correoAutorizador));

            mensaje.Subject = $"Nueva Solicitud de Vacaciones Pendiente - #{numeroSolicitud}";
            _logger.LogInformation("Mensaje creado correctamente");

            // Calcular días hábiles
            var diasTexto = diasSolicitados == 1 ? "día" : "días";

            // Crear el cuerpo del mensaje en HTML
            var html = @"
<div style='
    max-width: 650px;
    margin: 40px auto;
    font-family: ""Segoe UI"", Tahoma, Geneva, Verdana, sans-serif;
    border-radius: 12px;
    overflow: hidden;
    box-shadow: 0 4px 20px rgba(0,0,0,0.15);
    background: #ffffff;
'>
    <!-- Encabezado -->
    <div style='
        background: linear-gradient(135deg, #003C9D 0%, #0056D2 100%);
        color: #fff;
        padding: 30px;
        text-align: center;
    '>
        <div style='font-size: 48px; margin-bottom: 10px;'>📋</div>
        <h1 style='margin: 0; font-size: 26px; font-weight: 600;'>Nueva Solicitud Pendiente</h1>
        <p style='margin: 10px 0 0 0; font-size: 14px; opacity: 0.9;'>Requiere tu autorización</p>
    </div>

    <!-- Contenido -->
    <div style='padding: 35px 30px;'>
        <p style='font-size: 17px; color: #2c3e50; margin: 0 0 20px 0; line-height: 1.6;'>
            Hola, <strong style='color: #003C9D;'>" + nombreAutorizador + @"</strong>
        </p>

        <p style='font-size: 16px; color: #34495e; line-height: 1.7; margin: 0 0 25px 0;'>
            <strong style='color: #003C9D;'>" + nombreEmpleado + @"</strong> ha enviado una nueva solicitud de vacaciones 
            que requiere tu revisión y autorización.
        </p>

        <!-- Tarjeta de detalles -->
        <div style='
            background: linear-gradient(135deg, #f8f9fa 0%, #e9ecef 100%);
            border-left: 5px solid #003C9D;
            border-radius: 10px;
            padding: 25px;
            margin: 25px 0;
        '>
            <h3 style='
                margin: 0 0 20px 0;
                color: #003C9D;
                font-size: 18px;
                font-weight: 600;
                display: flex;
                align-items: center;
            '>
                <span style='margin-right: 10px;'>📊</span> Detalles de la Solicitud
            </h3>
            
            <table style='width: 100%; border-collapse: collapse;'>
                <tr>
                    <td style='padding: 12px 0; border-bottom: 1px solid #dee2e6;'>
                        <strong style='color: #495057; font-size: 15px;'>
                            <span style='margin-right: 8px;'>🔢</span> Número de Solicitud:
                        </strong>
                    </td>
                    <td style='padding: 12px 0; text-align: right; border-bottom: 1px solid #dee2e6;'>
                        <span style='color: #003C9D; font-weight: 600; font-size: 16px;'>#" + numeroSolicitud + @"</span>
                    </td>
                </tr>
                <tr>
                    <td style='padding: 12px 0; border-bottom: 1px solid #dee2e6;'>
                        <strong style='color: #495057; font-size: 15px;'>
                            <span style='margin-right: 8px;'>📅</span> Días Solicitados:
                        </strong>
                    </td>
                    <td style='padding: 12px 0; text-align: right; border-bottom: 1px solid #dee2e6;'>
                        <span style='color: #28a745; font-weight: 600; font-size: 16px;'>" + diasSolicitados + @" " + diasTexto + @"</span>
                    </td>
                </tr>
                <tr>
                    <td style='padding: 12px 0; border-bottom: 1px solid #dee2e6;'>
                        <strong style='color: #495057; font-size: 15px;'>
                            <span style='margin-right: 8px;'>🗓️</span> Fecha de Inicio:
                        </strong>
                    </td>
                    <td style='padding: 12px 0; text-align: right; border-bottom: 1px solid #dee2e6;'>
                        <span style='color: #495057; font-weight: 500;'>" + fechaInicio.ToString("dd/MM/yyyy") + @"</span>
                    </td>
                </tr>
                <tr>
                    <td style='padding: 12px 0;'>
                        <strong style='color: #495057; font-size: 15px;'>
                            <span style='margin-right: 8px;'>🏁</span> Fecha de Fin:
                        </strong>
                    </td>
                    <td style='padding: 12px 0; text-align: right;'>
                        <span style='color: #495057; font-weight: 500;'>" + fechaFin.ToString("dd/MM/yyyy") + @"</span>
                    </td>
                </tr>
            </table>
        </div>

        <!-- Botón de acción -->
        <div style='text-align: center; margin: 35px 0 25px 0;'>
            <a href='" + urlAutorizar + @"' style='
                display: inline-block;
                background: linear-gradient(135deg, #28a745 0%, #20c997 100%);
                color: white;
                padding: 16px 40px;
                border-radius: 8px;
                text-decoration: none;
                font-size: 17px;
                font-weight: 600;
                box-shadow: 0 4px 15px rgba(40, 167, 69, 0.3);
                transition: all 0.3s ease;
            '>
                <span style='margin-right: 8px;'>✓</span> Revisar y Autorizar Solicitud
            </a>
        </div>

        <p style='
            font-size: 14px;
            color: #6c757d;
            text-align: center;
            line-height: 1.6;
            margin: 25px 0 0 0;
            padding: 20px;
            background: #f8f9fa;
            border-radius: 8px;
        '>
            💡 <strong>Nota:</strong> Este enlace te llevará directamente al sistema para que puedas revisar 
            la solicitud completa y tomar una decisión.
        </p>
    </div>

    <!-- Pie de página -->
    <div style='
        background: #f8f9fa;
        padding: 25px 30px;
        text-align: center;
        border-top: 1px solid #dee2e6;
    '>
        <p style='margin: 0 0 10px 0; font-size: 13px; color: #6c757d;'>
            Este es un mensaje automático del Sistema de Gestión de Vacaciones
        </p>
        <p style='margin: 0; font-size: 12px; color: #adb5bd;'>
            © 2025 GEKO Sistemas de Seguridad | Todos los derechos reservados
        </p>
    </div>
</div>";

            var builder = new BodyBuilder { HtmlBody = html };
            mensaje.Body = builder.ToMessageBody();
            _logger.LogInformation("Cuerpo del mensaje construido");

            // Configurar y enviar
            using var smtp = new SmtpClient();
            
            _logger.LogInformation("Conectando a Mailjet...");
            await smtp.ConnectAsync("in-v3.mailjet.com", 587, SecureSocketOptions.StartTls);
            _logger.LogInformation("✅ Conexión establecida");
            
            _logger.LogInformation("Autenticando...");
            await smtp.AuthenticateAsync(_settings.ApiKey, _settings.ApiSecret);
            _logger.LogInformation("✅ Autenticación exitosa");
            
            _logger.LogInformation("Enviando notificación...");
            await smtp.SendAsync(mensaje);
            _logger.LogInformation("✅ Notificación enviada correctamente");
            
            await smtp.DisconnectAsync(true);
            _logger.LogInformation("=== FIN ENVÍO DE NOTIFICACIÓN EXITOSO ===");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"❌ ERROR AL ENVIAR NOTIFICACIÓN: {ex.Message}");
            _logger.LogError($"Tipo de excepción: {ex.GetType().Name}");
            _logger.LogError($"Stack trace: {ex.StackTrace}");
            
            // Re-lanzar la excepción para que sea manejada por el controlador
            throw;
        }
    }
}