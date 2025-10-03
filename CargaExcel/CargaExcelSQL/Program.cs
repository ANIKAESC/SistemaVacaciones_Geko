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

        string excelPath = @"C:\Users\Diego\Desktop\Empleados.xlsx";
        // Cambiar a la cadena de conexión del server
        string connectionString = "Server=DESKTOP-LPDU6QD\\SQLEXPRESS;Database=DBProyectoGrupalDojoGeko;Trusted_Connection=True;TrustServerCertificate=True;";

        using var stream = File.Open(excelPath, FileMode.Open, FileAccess.Read);
        using var reader = ExcelReaderFactory.CreateReader(stream);
        var result = reader.AsDataSet();
        var table = result.Tables[0];

        for (int i = 1; i < table.Rows.Count; i++)
        {
            var row = table.Rows[i];

            // Extracción de datos desde columnas 0 a 21
            string tipoContrato = row[0]?.ToString().Trim().Replace("'", "").Replace("\\", "").Replace("\r", "").Replace("\n", "").Replace("\t", "");           // TipoContrato
            string pais = row[1]?.ToString().Trim().Replace("'", "").Replace("\\", "").Replace("\r", "").Replace("\n", "").Replace("\t", "");                   // Pais
            string departamento = row[2]?.ToString().Trim().Replace("'", "").Replace("\\", "").Replace("\r", "").Replace("\n", "").Replace("\t", "");           // Departamento
            string municipio = row[3]?.ToString().Trim().Replace("'", "").Replace("\\", "").Replace("\r", "").Replace("\n", "").Replace("\t", "");              // Municipio
            string direccion = row[4]?.ToString().Trim().Replace("'", "").Replace("\\", "").Replace("\r", "").Replace("\n", "").Replace("\t", "");               // Direccion
            string puesto = row[5]?.ToString().Trim().Replace("'", "").Replace("\\", "").Replace("\r", "").Replace("\n", "").Replace("\t", "");                  // Puesto
            string codigo = row[6]?.ToString().Trim().Replace("'", "").Replace("\\", "").Replace("\r", "").Replace("\n", "").Replace("\t", "");                  // Codigo
            string dpi = row[7]?.ToString().Trim().Replace("'", "").Replace("\\", "").Replace("\r", "").Replace("\n", "").Replace("\t", "");                     // DPI
            string pasaporte = row[8]?.ToString().Trim().Replace("'", "").Replace("\\", "").Replace("\r", "").Replace("\n", "").Replace("\t", "");               // Pasaporte
            string nombres = row[9]?.ToString().Trim().Replace("'", "").Replace("\\", "").Replace("\r", "").Replace("\n", "").Replace("\t", "");                 // NombresEmpleado
            string apellidos = row[10]?.ToString().Trim().Replace("'", "").Replace("\\", "").Replace("\r", "").Replace("\n", "").Replace("\t", "");             // ApellidosEmpleado
            string correoPersonal = row[11]?.ToString().Trim().Replace("'", "").Replace("\\", "").Replace("\r", "").Replace("\n", "").Replace("\t", "");         // CorreoPersonal
            string correoInstitucional = row[12]?.ToString().Trim().Replace("'", "").Replace("\\", "").Replace("\r", "").Replace("\n", "").Replace("\t", "");    // CorreoInstitucional
            string fechaIngresoStr = row[13]?.ToString().Trim().Replace("'", "").Replace("\\", "").Replace("\r", "").Replace("\n", "").Replace("\t", "");        // FechaIngreso
            string vacacionesStr = row[14]?.ToString().Trim().Replace("'", "").Replace("\\", "").Replace("\r", "").Replace("\n", "").Replace("\t", "");          // DiasVacacionesAcumulados
            string DiasTomadosHistoricos = row[15]?.ToString().Trim().Replace("'", "").Replace("\\", "").Replace("\r", "").Replace("\n", "").Replace("\t", "");  // DiasTomadosHistoricos
            string fechaNacimientoStr = row[16]?.ToString().Trim().Replace("'", "").Replace("\\", "").Replace("\r", "").Replace("\n", "").Replace("\t", "");     // FechaNacimiento
            string telefono = row[17]?.ToString().Trim().Replace("'", "").Replace("\\", "").Replace("\r", "").Replace("\n", "").Replace("\t", "");               // Telefono
            string nit = row[18]?.ToString().Trim().Replace("'", "").Replace("\\", "").Replace("\r", "").Replace("\n", "").Replace("\t", "");                    // NIT
            string genero = row[19]?.ToString().Trim().Replace("'", "").Replace("\\", "").Replace("\r", "").Replace("\n", "").Replace("\t", "");                 // Genero
            string salarioStr = row[20]?.ToString().Trim().Replace("'", "").Replace("\\", "").Replace("\r", "").Replace("\n", "").Replace("\t", "")  ;            // Salario
            string estadoStr = row[21]?.ToString().Trim().Replace("'", "").Replace("\\", "").Replace("\r", "").Replace("\n", "").Replace("\t", "");              // FK_IdEstado

            // Validaciones obligatorias (Codigo removido, correoPersonal removido)
            if (string.IsNullOrWhiteSpace(pais) ||
                string.IsNullOrWhiteSpace(nombres) ||
                string.IsNullOrWhiteSpace(apellidos) ||
                string.IsNullOrWhiteSpace(correoInstitucional))
            {
                Console.WriteLine($"Fila {i + 1}: campos obligatorios vacíos.");
                continue;
            }

            // Al menos uno de DPI o Pasaporte debe estar presente
            if (string.IsNullOrWhiteSpace(dpi) && string.IsNullOrWhiteSpace(pasaporte))
            {
                Console.WriteLine($"Fila {i + 1}: debe tener DPI o Pasaporte.");
                continue;
            }

            // DPI (hasta 14 caracteres numéricos)
            if (!string.IsNullOrWhiteSpace(dpi) && !Regex.IsMatch(dpi, @"^\d{1,14}$"))
            {
                Console.WriteLine($"Fila {i + 1}: DPI inválido (debe ser numérico y máximo 14 caracteres).");
                continue;
            }

            // Pasaporte (alfanumérico)
            if (!string.IsNullOrWhiteSpace(pasaporte) && !Regex.IsMatch(pasaporte, @"^[A-Za-z0-9]+$"))
            {
                Console.WriteLine($"Fila {i + 1}: pasaporte inválido (debe ser alfanumérico).");
                continue;
            }

            // Teléfono (opcional, pero si se proporciona debe ser válido - 8 dígitos)
            if (!string.IsNullOrWhiteSpace(telefono) && !Regex.IsMatch(telefono, @"^\d{8}$"))
            {
                Console.WriteLine($"Fila {i + 1}: teléfono inválido (debe ser 8 dígitos).");
                continue;
            }

            // NIT (opcional, pero si se proporciona debe ser válido - 6 a 11 dígitos)
            if (!string.IsNullOrWhiteSpace(nit) && !Regex.IsMatch(nit, @"^\d{6,11}$"))
            {
                Console.WriteLine($"Fila {i + 1}: NIT inválido (debe tener entre 6 y 11 dígitos).");
                continue;
            }

            // Género (opcional, pero si se proporciona debe ser válido)
            if (!string.IsNullOrWhiteSpace(genero) && genero != "Masculino" && genero != "Femenino")
            {
                Console.WriteLine($"Fila {i + 1}: género inválido (debe ser Masculino o Femenino).");
                continue;
            }

          

            // Salario
            if (!decimal.TryParse(salarioStr.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out decimal salario) || salario < 0)
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

            // Fecha de nacimiento
            if (!DateTime.TryParse(fechaNacimientoStr, out DateTime fechaNacimiento))
            {
                Console.WriteLine($"Fila {i + 1}: fecha de nacimiento inválida.");
                continue;
            }

          

            // Días de vacaciones acumulados
            if (!decimal.TryParse(vacacionesStr.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out decimal diasVacaciones) || diasVacaciones < 0)
            {
                Console.WriteLine($"Fila {i + 1}: días de vacaciones inválidos.");
                continue;
            }


           

            // Validación y conversión de DiasTomadosHistoricos
            if (!decimal.TryParse(DiasTomadosHistoricos.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out decimal diasTomados))
            {
                diasTomados = 0; // Valor por defecto si no se puede convertir
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
            cmd.Parameters.AddWithValue("@CorreoPersonal", string.IsNullOrWhiteSpace(correoPersonal) ? DBNull.Value : correoPersonal);
            cmd.Parameters.AddWithValue("@CorreoInstitucional", correoInstitucional);
            cmd.Parameters.AddWithValue("@FechaIngreso", fechaIngreso);
            cmd.Parameters.AddWithValue("@DiasVacacionesAcumulados", diasVacaciones);
            cmd.Parameters.AddWithValue("@DiasTomadosHistoricos", diasTomados);
            cmd.Parameters.AddWithValue("@FechaNacimiento", fechaNacimiento);
            cmd.Parameters.AddWithValue("@Telefono", string.IsNullOrWhiteSpace(telefono) ? DBNull.Value : telefono);
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

        Console.WriteLine("Carga masiva finalizada. Ejecutando actualización de días acumulados...");

        using (var conn = new SqlConnection(connectionString))
        {
            conn.Open();
            using var cmd = new SqlCommand("sp_ActualizarDiasAcumuladosEmpleados", conn);
            cmd.CommandType = CommandType.StoredProcedure;

            try
            {
                cmd.ExecuteNonQuery();
                Console.WriteLine("Días acumulados actualizados correctamente.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al actualizar días acumulados: {ex.Message}");
            }
        }

        Console.WriteLine("Proceso completo finalizado ");
    }
}