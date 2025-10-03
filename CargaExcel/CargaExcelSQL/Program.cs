using System.Data;
using System.Data.SqlClient;
using System.Net;
using System.Text.RegularExpressions;
using ExcelDataReader;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

class Program
{
    private const string ConnectionString = "Server=NEWPEGHOSTE\\SQLEXPRESS;Database=DBProyectoGrupalDojoGeko;Trusted_Connection=True;TrustServerCertificate=True;";
    private static readonly EmailService _emailService = new EmailService(new EmailSettings());

    static async Task Main()
    {
        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        while (true)
        {
            Console.Clear();
            Console.WriteLine("=== SISTEMA DE GESTIÓN DE CARGAS ===");
            Console.WriteLine("1. Cargar Empleados desde Excel");
            Console.WriteLine("2. Gestionar Usuarios");
            Console.WriteLine("3. Salir");
            Console.Write("\nSeleccione una opción (1-3): ");

            var opcion = Console.ReadLine();

            try
            {
                switch (opcion)
                {
                    case "1":
                        await CargarEmpleados();
                        break;
                    case "2":
                        await GestionarUsuarios();
                        break;
                    case "3":
                        Console.WriteLine("\n¡Hasta luego!");
                        return;
                    default:
                        MostrarMensaje("Opción no válida. Intente nuevamente.");
                        break;
                }
            }
            catch (Exception ex)
            {
                MostrarMensaje($"Error: {ex.Message}");
            }
        }
    }

    private static async Task CargarEmpleados()
    {
        Console.Clear();
        Console.WriteLine("=== CARGAR EMPLEADOS DESDE EXCEL ===");
        
        string excelPath = @"C:\Users\josep\OneDrive\Documentos\Escritorio\ProyectoGrupalGekoMayo\DojoNet\CargaExcel\Empleados.xlsx";
        
        if (!File.Exists(excelPath))
        {
            throw new FileNotFoundException("No se encontró el archivo de Excel en la ruta especificada.");
        }

        using var stream = File.Open(excelPath, FileMode.Open, FileAccess.Read);
        using var reader = ExcelReaderFactory.CreateReader(stream);
        var result = reader.AsDataSet();
        var table = result.Tables[0];
        int registrosProcesados = 0;
        int errores = 0;

        for (int i = 1; i < table.Rows.Count; i++)
        {
            try
            {
                var row = table.Rows[i];
                Console.Write($"\rProcesando fila {i + 1} de {table.Rows.Count - 1}...");

                // Extracción de datos
                string tipoContrato = LimpiarCadena(row[0]?.ToString());
                string pais = LimpiarCadena(row[1]?.ToString());
                string departamento = LimpiarCadena(row[2]?.ToString());
                string municipio = LimpiarCadena(row[3]?.ToString());
                string direccion = LimpiarCadena(row[4]?.ToString());
                string puesto = LimpiarCadena(row[5]?.ToString());
                string codigo = LimpiarCadena(row[6]?.ToString());
                string dpi = LimpiarCadena(row[7]?.ToString());
                string pasaporte = LimpiarCadena(row[8]?.ToString());
                string nombres = LimpiarCadena(row[9]?.ToString());
                string apellidos = LimpiarCadena(row[10]?.ToString());
                string correoPersonal = LimpiarCadena(row[11]?.ToString());
                string correoInstitucional = LimpiarCadena(row[12]?.ToString());
                string fechaIngresoStr = LimpiarCadena(row[13]?.ToString());
                string vacacionesStr = LimpiarCadena(row[14]?.ToString());
                string diasTomadosHistoricos = LimpiarCadena(row[15]?.ToString());
                string fechaNacimientoStr = LimpiarCadena(row[16]?.ToString());
                string telefono = LimpiarCadena(row[17]?.ToString());
                string nit = LimpiarCadena(row[18]?.ToString());
                string genero = LimpiarCadena(row[19]?.ToString());
                string salarioStr = LimpiarCadena(row[20]?.ToString());
                string estadoStr = LimpiarCadena(row[21]?.ToString());

                // Validaciones
                if (!ValidarDatosObligatorios(pais, nombres, apellidos, correoInstitucional, i) ||
                    !ValidarIdentificacion(dpi, pasaporte, i) ||
                    !ValidarFormatoDatos(telefono, nit, genero, i) ||
                    !ValidarFechas(fechaIngresoStr, fechaNacimientoStr, i) ||
                    !ValidarNumeros(vacacionesStr, salarioStr, estadoStr, i, out decimal diasVacaciones, out decimal salario, out int estado))
                {
                    errores++;
                    continue;
                }

                // Insertar empleado
                if (await InsertarEmpleado(
                    tipoContrato, pais, departamento, municipio, direccion, puesto, codigo,
                    dpi, pasaporte, nombres, apellidos, correoPersonal, correoInstitucional,
                    DateTime.Parse(fechaIngresoStr), diasVacaciones, 
                    decimal.Parse(diasTomadosHistoricos), DateTime.Parse(fechaNacimientoStr),
                    telefono, nit, genero, salario, estado, i + 1))
                {
                    registrosProcesados++;
                }
                else
                {
                    errores++;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\nError en fila {i + 1}: {ex.Message}");
                errores++;
            }
        }

        Console.WriteLine($"\n\nProceso completado. Registros procesados: {registrosProcesados}, Errores: {errores}");
        Console.WriteLine("Presione cualquier tecla para continuar...");
        Console.ReadKey();
    }

    private static async Task GestionarUsuarios()
    {
        Console.Clear();
        Console.WriteLine("=== GESTIÓN DE USUARIOS ===");
        Console.WriteLine("1. Crear usuarios para empleados sin cuenta");
        Console.WriteLine("2. Volver al menú principal");
        Console.Write("\nSeleccione una opción: ");

        var opcion = Console.ReadLine();

        switch (opcion)
        {
            case "1":
                var usuarioManager = new UsuarioManager(ConnectionString, _emailService);
                await usuarioManager.CrearUsuariosParaEmpleados();
                break;
            case "2":
                return;
            default:
                MostrarMensaje("Opción no válida.");
                break;
        }
    }

    // Clases internas
    public class UsuarioManager
    {
        private readonly string _connectionString;
        private readonly EmailService _emailService;
        private const string BaseUrl = "https://localhost:7261";

        public UsuarioManager(string connectionString, EmailService emailService)
        {
            _connectionString = connectionString;
            _emailService = emailService;
        }

        public async Task CrearUsuariosParaEmpleados()
        {
            try
            {
                var empleados = await ObtenerEmpleadosSinUsuario();
                Console.WriteLine($"\nSe encontraron {empleados.Count} empleados sin usuario.");

                foreach (var empleado in empleados)
                {
                    try
                    {
                        var contrasena = GenerarContraseniaAleatoria();
                        var urlCambioPassword = $"{BaseUrl}/Login/IndexCambioContrasenia/{empleado.IdEmpleado}";

                        await CrearUsuario(
                            empleado.CorreoInstitucional,
                            contrasena,
                            empleado.IdEmpleado);

                        await _emailService.EnviarCorreoBienvenidaAsync(
                            $"{empleado.Nombres} {empleado.Apellidos}",
                            empleado.CorreoInstitucional,
                            urlCambioPassword,
                            contrasena
                        );

                        Console.WriteLine($"Usuario creado para: {empleado.CorreoInstitucional}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error al crear usuario para empleado {empleado.IdEmpleado}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error general: {ex.Message}");
                throw;
            }
        }

        private async Task<List<Empleado>> ObtenerEmpleadosSinUsuario()
        {
            var empleados = new List<Empleado>();

            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(
                @"SELECT e.IdEmpleado, e.NombresEmpleado, e.ApellidosEmpleado, e.CorreoInstitucional 
                  FROM Empleados e
                  LEFT JOIN Usuarios u ON e.IdEmpleado = u.FK_IdEmpleado
                  WHERE u.IdUsuario IS NULL", 
                conn);

            await conn.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                empleados.Add(new Empleado
                {
                    IdEmpleado = reader.GetInt32(0),
                    Nombres = reader.GetString(1),
                    Apellidos = reader.GetString(2),
                    CorreoInstitucional = reader.GetString(3)
                });
            }

            return empleados;
        }

        private async Task CrearUsuario(string email, string contrasena, int idEmpleado)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            using var cmd = new SqlCommand("sp_InsertarUsuario", conn)
            {
                CommandType = CommandType.StoredProcedure
            };

            cmd.Parameters.AddWithValue("@Username", email);
            cmd.Parameters.AddWithValue("@Contrasenia", contrasena);
            cmd.Parameters.AddWithValue("@FK_IdEstado", 2); // 2 = Pendiente
            cmd.Parameters.AddWithValue("@FK_IdEmpleado", idEmpleado);
            cmd.Parameters.AddWithValue("@FechaExpiracionContrasenia", DateTime.UtcNow.AddHours(1));

            await cmd.ExecuteNonQueryAsync();
        }

        private string GenerarContraseniaAleatoria(int longitud = 12)
        {
            const string caracteres = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789!@#$%^&*()_-+=<>?";
            var random = new Random();
            return new string(Enumerable.Repeat(caracteres, longitud)
                .Select(s => s[random.Next(s.Length)]).ToArray());
        }
    }

    // Clases de modelo
    public class Empleado
    {
        public int IdEmpleado { get; set; }
        public string Nombres { get; set; }
        public string Apellidos { get; set; }
        public string CorreoInstitucional { get; set; }
    }

    public class EmailSettings
    {
        public string ApiKey { get; set; } = "0c44c16fe802fec2472aa99bf028e579";
        public string ApiSecret { get; set; } = "db55f93cb8478254109991c5f63e6382";
        public string FromEmail { get; set; } = "jperalta@digitalgeko.com";
        public string FromName { get; set; } = "Dojo Juniors Geko";
    }

    public class EmailService
    {
        private readonly EmailSettings _settings;

        public EmailService(EmailSettings settings)
        {
            _settings = settings;
        }

        public async Task EnviarCorreoBienvenidaAsync(string usuario, string destino, string urlCambioPassword, string contrasenia)
        {
            var mensaje = new MimeMessage();
            mensaje.From.Add(new MailboxAddress(_settings.FromName, _settings.FromEmail));
            mensaje.To.Add(MailboxAddress.Parse(destino));
            mensaje.Subject = "Bienvenido - Tus credenciales de acceso";

            var html = $@"
            <div style='max-width: 600px; margin: 40px auto; font-family: ""Segoe UI"", Tahoma, Geneva, Verdana, sans-serif; border-radius: 10px; overflow: hidden; box-shadow: 0 0 15px rgba(0,0,0,0.1); background: linear-gradient(to right, #f8f9fa, #ffffff); color: #333;'>
                <div style='background-color: #007bff; color: #fff; padding: 25px 30px; text-align: center;'>
                    <h1 style='margin: 0; font-size: 24px;'>¡Bienvenido a Dojo .NET 2025!</h1>
                </div>
                <div style='padding: 30px;'>
                    <p style='font-size: 16px; color: black;'>
                        Hola, <strong>{WebUtility.HtmlEncode(usuario)}</strong>
                    </p>
                    <p style='font-size: 16px; line-height: 1.6;'>
                        Hemos generado una <strong>contraseña temporal</strong> para que puedas iniciar sesión en el sistema. Asegúrate de cambiarla lo antes posible por seguridad.
                    </p>
                    <div style='font-size: 22px; background-color: #e9ecef; border: 1px dashed #6c757d; text-align: center; padding: 12px 20px; border-radius: 6px; letter-spacing: 1px; margin: 20px 0; font-weight: bold; color: #212529;'>
                        {WebUtility.HtmlEncode(contrasenia)}
                    </div>
                    <p style='font-size: 16px;'>
                        Para cambiar tu contraseña, haz clic en el siguiente botón:
                    </p>
                    <div style='text-align: center; margin: 30px 0;'>
                        <a href='{urlCambioPassword}' style='background-color: #28a745; color: white; padding: 14px 28px; border-radius: 6px; text-decoration: none; font-size: 16px; font-weight: bold; box-shadow: 0 4px 10px rgba(0, 0, 0, 0.1);'>Cambiar Contraseña</a>
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

            using var smtp = new SmtpClient();
            await smtp.ConnectAsync("in-v3.mailjet.com", 587, SecureSocketOptions.StartTls);
            await smtp.AuthenticateAsync(_settings.ApiKey, _settings.ApiSecret);
            await smtp.SendAsync(mensaje);
            await smtp.DisconnectAsync(true);
        }
    }

    // Métodos auxiliares
    private static string LimpiarCadena(string valor)
    {
        return valor?.Trim().Replace("'", "").Replace("\\", "").Replace("\r", "").Replace("\n", "").Replace("\t", "");
    }

    private static void MostrarMensaje(string mensaje)
    {
        Console.WriteLine($"\n{mensaje}");
        Console.WriteLine("Presione cualquier tecla para continuar...");
        Console.ReadKey();
    }

    private static bool ValidarDatosObligatorios(string pais, string nombres, string apellidos, string correoInstitucional, int numFila)
    {
        if (string.IsNullOrWhiteSpace(pais) || string.IsNullOrWhiteSpace(nombres) || 
            string.IsNullOrWhiteSpace(apellidos) || string.IsNullOrWhiteSpace(correoInstitucional))
        {
            Console.WriteLine($"\nFila {numFila + 1}: Error - Faltan campos obligatorios");
            return false;
        }
        return true;
    }

    private static bool ValidarIdentificacion(string dpi, string pasaporte, int numFila)
    {
        if (string.IsNullOrWhiteSpace(dpi) && string.IsNullOrWhiteSpace(pasaporte))
        {
            Console.WriteLine($"\nFila {numFila + 1}: Error - Se requiere al menos DPI o Pasaporte");
            return false;
        }
        return true;
    }

    private static bool ValidarFormatoDatos(string telefono, string nit, string genero, int numFila)
    {
        // Validar teléfono (si está presente)
        if (!string.IsNullOrWhiteSpace(telefono) && !Regex.IsMatch(telefono, @"^\d{8}$"))
        {
            Console.WriteLine($"\nFila {numFila + 1}: Error - Formato de teléfono inválido (deben ser 8 dígitos)");
            return false;
        }

        // Validar NIT (si está presente)
        if (!string.IsNullOrWhiteSpace(nit) && !Regex.IsMatch(nit, @"^\d{6,11}$"))
        {
            Console.WriteLine($"\nFila {numFila + 1}: Error - Formato de NIT inválido (debe tener entre 6 y 11 dígitos)");
            return false;
        }

        // Validar género (si está presente)
        if (!string.IsNullOrWhiteSpace(genero) && genero != "Masculino" && genero != "Femenino")
        {
            Console.WriteLine($"\nFila {numFila + 1}: Error - Género debe ser 'Masculino' o 'Femenino'");
            return false;
        }

        return true;
    }

    private static bool ValidarFechas(string fechaIngresoStr, string fechaNacimientoStr, int numFila)
    {
        if (!DateTime.TryParse(fechaIngresoStr, out DateTime fechaIngreso))
        {
            Console.WriteLine($"\nFila {numFila + 1}: Error - Formato de fecha de ingreso inválido");
            return false;
        }

        if (fechaIngreso > DateTime.Today.AddDays(1))
        {
            Console.WriteLine($"\nFila {numFila + 1}: Error - La fecha de ingreso no puede ser futura");
            return false;
        }

        if (!DateTime.TryParse(fechaNacimientoStr, out _))
        {
            Console.WriteLine($"\nFila {numFila + 1}: Error - Formato de fecha de nacimiento inválido");
            return false;
        }

        return true;
    }

    private static bool ValidarNumeros(string vacacionesStr, string salarioStr, string estadoStr, int numFila, 
        out decimal diasVacaciones, out decimal salario, out int estado)
    {
        diasVacaciones = 0;
        salario = 0;
        estado = 0;

        // Validar días de vacaciones
        if (!decimal.TryParse(vacacionesStr.Replace(',', '.'), 
            System.Globalization.NumberStyles.Any, 
            System.Globalization.CultureInfo.InvariantCulture, 
            out diasVacaciones) || diasVacaciones < 0)
        {
            Console.WriteLine($"\nFila {numFila + 1}: Error - Días de vacaciones inválidos");
            return false;
        }

        // Validar salario
        if (!decimal.TryParse(salarioStr.Replace(',', '.'), 
            System.Globalization.NumberStyles.Any, 
            System.Globalization.CultureInfo.InvariantCulture, 
            out salario) || salario < 0)
        {
            Console.WriteLine($"\nFila {numFila + 1}: Error - Salario inválido");
            return false;
        }

        // Validar estado
        if (!int.TryParse(estadoStr, out estado) || estado < 1 || estado > 9)
        {
            Console.WriteLine($"\nFila {numFila + 1}: Error - Estado inválido (debe ser entre 1 y 9)");
            return false;
        }

        return true;
    }

    private static async Task<bool> InsertarEmpleado(
        string tipoContrato, string pais, string departamento, string municipio, 
        string direccion, string puesto, string codigo, string dpi, string pasaporte,
        string nombres, string apellidos, string correoPersonal, string correoInstitucional,
        DateTime fechaIngreso, decimal diasVacaciones, decimal diasTomados, 
        DateTime fechaNacimiento, string telefono, string nit, string genero,
        decimal salario, int estado, int numeroFila)
    {
        using var conn = new SqlConnection(ConnectionString);
        await conn.OpenAsync();

        using var cmd = new SqlCommand("sp_InsertarEmpleado", conn)
        {
            CommandType = CommandType.StoredProcedure
        };

        // Agregar parámetros
        cmd.Parameters.AddWithValue("@TipoContrato", string.IsNullOrEmpty(tipoContrato) ? DBNull.Value : (object)tipoContrato);
        cmd.Parameters.AddWithValue("@Pais", pais);
        cmd.Parameters.AddWithValue("@Departamento", string.IsNullOrEmpty(departamento) ? DBNull.Value : (object)departamento);
        cmd.Parameters.AddWithValue("@Municipio", string.IsNullOrEmpty(municipio) ? DBNull.Value : (object)municipio);
        cmd.Parameters.AddWithValue("@Direccion", string.IsNullOrEmpty(direccion) ? DBNull.Value : (object)direccion);
        cmd.Parameters.AddWithValue("@Puesto", string.IsNullOrEmpty(puesto) ? DBNull.Value : (object)puesto);
        cmd.Parameters.AddWithValue("@Codigo", string.IsNullOrEmpty(codigo) ? DBNull.Value : (object)codigo);
        cmd.Parameters.AddWithValue("@DPI", string.IsNullOrEmpty(dpi) ? DBNull.Value : (object)dpi);
        cmd.Parameters.AddWithValue("@Pasaporte", string.IsNullOrEmpty(pasaporte) ? DBNull.Value : (object)pasaporte);
        cmd.Parameters.AddWithValue("@NombresEmpleado", nombres);
        cmd.Parameters.AddWithValue("@ApellidosEmpleado", apellidos);
        cmd.Parameters.AddWithValue("@CorreoPersonal", string.IsNullOrEmpty(correoPersonal) ? DBNull.Value : (object)correoPersonal);
        cmd.Parameters.AddWithValue("@CorreoInstitucional", correoInstitucional);
        cmd.Parameters.AddWithValue("@FechaIngreso", fechaIngreso);
        cmd.Parameters.AddWithValue("@DiasVacacionesAcumulados", diasVacaciones);
        cmd.Parameters.AddWithValue("@DiasTomadosHistoricos", diasTomados);
        cmd.Parameters.AddWithValue("@FechaNacimiento", fechaNacimiento);
        cmd.Parameters.AddWithValue("@Telefono", string.IsNullOrEmpty(telefono) ? DBNull.Value : (object)telefono);
        cmd.Parameters.AddWithValue("@NIT", string.IsNullOrEmpty(nit) ? DBNull.Value : (object)nit);
        cmd.Parameters.AddWithValue("@Genero", string.IsNullOrEmpty(genero) ? DBNull.Value : (object)genero);
        cmd.Parameters.AddWithValue("@Salario", salario);
        cmd.Parameters.AddWithValue("@FK_IdEstado", estado);

        try
        {
            await cmd.ExecuteNonQueryAsync();
            Console.Write($"\rFila {numeroFila}: Empleado insertado correctamente".PadRight(50));
            return true;
        }
        catch (Exception ex)
        {
            Console.Write($"\rFila {numeroFila}: Error al insertar - {ex.Message}".PadRight(80));
            return false;
        }
    }
}