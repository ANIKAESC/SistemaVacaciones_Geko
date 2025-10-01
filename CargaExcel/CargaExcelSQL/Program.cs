using System;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Text.RegularExpressions;
using ExcelDataReader;

class Program
{
    static void Main()
    {
        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

        string excelPath = @"C:\Users\erici\OneDrive\Escritorio\Empleados.xlsx";
        // Cambiar a la cadena de conexión del server
        string connectionString = "Server=BARRERAS;Database=DBProyectoGrupalDojoGeko;Trusted_Connection=True;TrustServerCertificate=True;";

        using var stream = File.Open(excelPath, FileMode.Open, FileAccess.Read);
        using var reader = ExcelReaderFactory.CreateReader(stream);
        var result = reader.AsDataSet();
        var table = result.Tables[0];

        for (int i = 1; i < table.Rows.Count; i++)
        {
            var row = table.Rows[i];

            // Extracción de datos desde columnas 0 a 20
            string tipoContrato = row[0]?.ToString();           // TipoContrato
            string pais = row[1]?.ToString();                   // Pais
            string departamento = row[2]?.ToString();           // Departamento
            string municipio = row[3]?.ToString();              // Municipio
            string direccion = row[4]?.ToString();              // Direccion
            string puesto = row[5]?.ToString();                 // Puesto
            string codigo = row[6]?.ToString();                 // Codigo
            string dpi = row[7]?.ToString();                    // DPI
            string pasaporte = row[8]?.ToString();              // Pasaporte
            string nombres = row[9]?.ToString();                // NombresEmpleado
            string apellidos = row[10]?.ToString();             // ApellidosEmpleado
            string correoPersonal = row[11]?.ToString();        // CorreoPersonal
            string correoInstitucional = row[12]?.ToString();   // CorreoInstitucional
            string fechaIngresoStr = row[13]?.ToString();       // FechaIngreso
            string vacacionesStr = row[14]?.ToString();         // DiasVacacionesAcumulados
            string fechaNacimientoStr = row[15]?.ToString();    // FechaNacimiento
            string telefono = row[16]?.ToString();              // Telefono
            string nit = row[17]?.ToString();                   // NIT
            string genero = row[18]?.ToString();                // Genero
            string salarioStr = row[19]?.ToString();            // Salario
            string estadoStr = row[20]?.ToString();             // FK_IdEstado

            // Validaciones obligatorias
            if (string.IsNullOrWhiteSpace(pais) ||
                string.IsNullOrWhiteSpace(nombres) ||
                string.IsNullOrWhiteSpace(apellidos) ||
                string.IsNullOrWhiteSpace(correoPersonal) ||
                string.IsNullOrWhiteSpace(correoInstitucional))
            {
                Console.WriteLine($"Fila {i + 1}: campos obligatorios vacíos.");
                continue;
            }

            // TipoContrato
            if (!string.IsNullOrWhiteSpace(tipoContrato) &&
                tipoContrato != "Planilla" && tipoContrato != "Facturado")
            {
                Console.WriteLine($"Fila {i + 1}: tipo de contrato inválido.");
                continue;
            }

            // Teléfono (8 dígitos)
            if (!Regex.IsMatch(telefono ?? "", @"^\d{8}$"))
            {
                Console.WriteLine($"Fila {i + 1}: teléfono inválido.");
                continue;
            }

            // DPI (13 dígitos)
            if (!string.IsNullOrWhiteSpace(dpi) && !Regex.IsMatch(dpi, @"^\d{13}$"))
            {
                Console.WriteLine($"Fila {i + 1}: DPI inválido.");
                continue;
            }

            // Pasaporte (alfanumérico)
            if (!string.IsNullOrWhiteSpace(pasaporte) && !Regex.IsMatch(pasaporte, @"^[A-Za-z0-9]+$"))
            {
                Console.WriteLine($"Fila {i + 1}: pasaporte inválido.");
                continue;
            }

            // NIT (9 o 11 dígitos)
            if (!string.IsNullOrWhiteSpace(nit) && !Regex.IsMatch(nit, @"^\d{9}$|^\d{11}$"))
            {
                Console.WriteLine($"Fila {i + 1}: NIT inválido.");
                continue;
            }

            // Género
            if (!string.IsNullOrWhiteSpace(genero) && genero != "Masculino" && genero != "Femenino")
            {
                Console.WriteLine($"Fila {i + 1}: género inválido.");
                continue;
            }

            // Salario
            if (!decimal.TryParse(salarioStr, out decimal salario) || salario < 0)
            {
                Console.WriteLine($"Fila {i + 1}: salario inválido.");
                continue;
            }

            // Estado
            if (!int.TryParse(estadoStr, out int estado) || estado < 1 || estado > 9)
            {
                Console.WriteLine($"Fila {i + 1}: estado inválido.");
                continue;
            }

            // Fecha de ingreso
            if (!DateTime.TryParse(fechaIngresoStr, out DateTime fechaIngreso))
            {
                Console.WriteLine($"Fila {i + 1}: fecha de ingreso inválida.");
                continue;
            }
            if (fechaIngreso > DateTime.Today.AddDays(1))
            {
                Console.WriteLine($"Fila {i + 1}: la fecha de ingreso no puede ser mayor a mañana.");
                continue;
            }

            //fecha de ingreso:
            if (!DateTime.TryParse(fechaNacimientoStr, out DateTime fechaNacimiento))
            {
                Console.WriteLine($"Fila {i + 1}: fecha de nacimiento inválida.");
                continue;
            }

            // Días de vacaciones acumulados
            if (!decimal.TryParse(vacacionesStr, out decimal diasVacaciones) || diasVacaciones < 0)
            {
                Console.WriteLine($"Fila {i + 1}: días de vacaciones inválidos.");
                continue;
            }

            // Ejecución del SP
            using var conn = new SqlConnection(connectionString);
            conn.Open();

            using var cmd = new SqlCommand("sp_InsertarEmpleado", conn);
            cmd.CommandType = CommandType.StoredProcedure;

            cmd.Parameters.AddWithValue("@TipoContrato", string.IsNullOrWhiteSpace(tipoContrato) ? DBNull.Value : tipoContrato);
            cmd.Parameters.AddWithValue("@Pais", pais);
            cmd.Parameters.AddWithValue("@Departamento", string.IsNullOrWhiteSpace(departamento) ? DBNull.Value : departamento);
            cmd.Parameters.AddWithValue("@Municipio", string.IsNullOrWhiteSpace(municipio) ? DBNull.Value : municipio);
            cmd.Parameters.AddWithValue("@Direccion", string.IsNullOrWhiteSpace(direccion) ? DBNull.Value : direccion);
            cmd.Parameters.AddWithValue("@Puesto", string.IsNullOrWhiteSpace(puesto) ? DBNull.Value : puesto);
            cmd.Parameters.AddWithValue("@Codigo", string.IsNullOrWhiteSpace(codigo) ? DBNull.Value : codigo);
            cmd.Parameters.AddWithValue("@DPI", string.IsNullOrWhiteSpace(dpi) ? DBNull.Value : dpi);
            cmd.Parameters.AddWithValue("@Pasaporte", string.IsNullOrWhiteSpace(pasaporte) ? DBNull.Value : pasaporte);
            cmd.Parameters.AddWithValue("@NombresEmpleado", nombres);
            cmd.Parameters.AddWithValue("@ApellidosEmpleado", apellidos);
            cmd.Parameters.AddWithValue("@CorreoPersonal", correoPersonal);
            cmd.Parameters.AddWithValue("@CorreoInstitucional", correoInstitucional);
            cmd.Parameters.AddWithValue("@FechaIngreso", fechaIngreso);
            cmd.Parameters.AddWithValue("@DiasVacacionesAcumulados", diasVacaciones);
            cmd.Parameters.AddWithValue("@FechaNacimiento", fechaNacimiento);
            cmd.Parameters.AddWithValue("@Telefono", telefono);
            cmd.Parameters.AddWithValue("@NIT", string.IsNullOrWhiteSpace(nit) ? DBNull.Value : nit);
            cmd.Parameters.AddWithValue("@Genero", string.IsNullOrWhiteSpace(genero) ? DBNull.Value : genero);
            cmd.Parameters.AddWithValue("@Salario", salario);
            cmd.Parameters.AddWithValue("@FK_IdEstado", estado);

            try
            {
                cmd.ExecuteNonQuery();
                Console.WriteLine($"Fila {i + 1}: empleado insertado correctamente.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Fila {i + 1}: error al insertar - {ex.Message}");
            }
        }

        Console.WriteLine("Proceso finalizado.");
    }
}
