using System.Data;
using System.Data.SqlClient;
using System.IO.Compression;
using ProyectoDojoGeko.Helper;
using ProyectoDojoGeko.Services.Interfaces;
using QuestPDF.Fluent;

/*===============================================
==   Service: PdfSolicitudService               = 
=================================================*/

/***Generación de PDF: Usa QuestPDF para generar PDFs nativamente sin dependencias externas
**Compresión Brotli: Reduce significativamente el tamaño de almacenamiento
**Almacenamiento en DB: PDFs comprimidos se guardan en tabla `SolicitudPDF`
**Control de Descarga: Permite descarga solo hasta que se apruebe la solicitud
**Gestión Automática: Se crea el PDF al crear la solicitud y se restringe al aprobar*/

namespace ProyectoDojoGeko.Services
{

    public class PdfSolicitudService : IPdfSolicitudService
    {
        private readonly string _connectionString;
        private readonly ILogger<PdfSolicitudService> _logger;

        public PdfSolicitudService(
            IConfiguration configuration,
            ILogger<PdfSolicitudService> logger)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection");
            _logger = logger;
        }

        public async Task<byte[]> GenerarPDFSolicitudAsync(int idSolicitud)
        {
            try
            {
                _logger.LogInformation("Iniciando generación de PDF para solicitud {IdSolicitud}", idSolicitud);
                
                // 1. Obtener datos de la solicitud
                var datosSolicitud = await ObtenerDatosSolicitudAsync(idSolicitud);
                if (datosSolicitud == null)
                {
                    _logger.LogError("No se encontraron datos para la solicitud {IdSolicitud}", idSolicitud);
                    throw new Exception($"No se encontró la solicitud con ID: {idSolicitud}");
                }

                _logger.LogInformation("Datos obtenidos para solicitud {IdSolicitud}. Tipo formato: {TipoFormato}", 
                    idSolicitud, datosSolicitud.TipoFormato);

                // 2. Generar PDF usando QuestPDF con el formato seleccionado
                _logger.LogInformation("📄 Antes de crear documento - Firma Empleado: {FirmaEmpleado}, Firma Autorizador: {FirmaAutorizador}", 
                    datosSolicitud.FirmaEmpleado != null ? $"{datosSolicitud.FirmaEmpleado.Length} bytes" : "NULL",
                    datosSolicitud.FirmaAutorizador != null ? $"{datosSolicitud.FirmaAutorizador.Length} bytes" : "NULL");
                
                var documento = SolicitudPdfDocumentFactory.CrearDocumento(datosSolicitud);
                _logger.LogInformation("Documento creado. Generando PDF...");
                
                var pdfBytes = documento.GeneratePdf();
                _logger.LogInformation("PDF generado exitosamente. Tamaño: {Size} bytes", pdfBytes.Length);

                return pdfBytes;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generando PDF para solicitud {IdSolicitud}. Mensaje: {Message}", 
                    idSolicitud, ex.Message);
                throw;
            }
        }

        public async Task<bool> GuardarPDFEnBaseDatosAsync(int idSolicitud, byte[] pdfBytes)
        {
            try
            {
                _logger.LogInformation("Iniciando guardado de PDF para solicitud {IdSolicitud}. Tamaño original: {Size} bytes", 
                    idSolicitud, pdfBytes.Length);
                
                // Comprimir con Brotli
                var compressedBytes = ComprimirConBrotli(pdfBytes);
                _logger.LogInformation("PDF comprimido. Tamaño comprimido: {Size} bytes. Reducción: {Reduction}%", 
                    compressedBytes.Length, 
                    Math.Round((1 - (double)compressedBytes.Length / pdfBytes.Length) * 100, 2));
                
                var nombreArchivo = $"Solicitud_Vacaciones_{idSolicitud}_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";

                using var connection = new SqlConnection(_connectionString);
                using var command = new SqlCommand("sp_InsertarSolicitudPDF", connection);

                command.CommandType = CommandType.StoredProcedure;
                command.Parameters.AddWithValue("@FK_IdSolicitud", idSolicitud);
                command.Parameters.AddWithValue("@NombreArchivo", nombreArchivo);
                command.Parameters.AddWithValue("@ContenidoPDFComprimido", compressedBytes);
                command.Parameters.AddWithValue("@TamanoOriginal", pdfBytes.Length);
                command.Parameters.AddWithValue("@TamanoComprimido", compressedBytes.Length);

                await connection.OpenAsync();
                _logger.LogInformation("Ejecutando stored procedure sp_InsertarSolicitudPDF...");
                
                var result = await command.ExecuteScalarAsync();
                
                bool success = result != null && Convert.ToInt32(result) > 0;
                _logger.LogInformation("PDF guardado en BD. Resultado: {Success}", success);
                
                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error guardando PDF en base de datos para solicitud {IdSolicitud}. Mensaje: {Message}", 
                    idSolicitud, ex.Message);
                return false;
            }
        }

        public async Task<(byte[] contenido, string nombreArchivo)?> ObtenerPDFSolicitudAsync(int idSolicitud)
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);
                using var command = new SqlCommand("sp_ObtenerSolicitudPDF", connection);

                command.CommandType = CommandType.StoredProcedure;
                command.Parameters.AddWithValue("@FK_IdSolicitud", idSolicitud);

                await connection.OpenAsync();
                using var reader = await command.ExecuteReaderAsync();

                if (await reader.ReadAsync())
                {
                    var estadoSolicitud = reader.GetInt32("FK_IdEstadoSolicitud");
                    var estadoPdf = reader.GetInt32("FK_IdEstado");

                    // Verificar si la descarga está permitida
                    if (estadoSolicitud == 2 && estadoPdf == 4) // 2=Autorizada, 4=Restringido
                    {
                        return null; // Descarga restringida
                    }

                    var contenidoComprimido = (byte[])reader["ContenidoPDFComprimido"];
                    var nombreArchivo = reader.GetString("NombreArchivo");

                    // Descomprimir
                    var contenidoDescomprimido = DescomprimirBrotli(contenidoComprimido);

                    return (contenidoDescomprimido, nombreArchivo);
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo PDF para solicitud {IdSolicitud}", idSolicitud);
                return null;
            }
        }

        public async Task RestringirDescargaPDFAsync(int idSolicitud)
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);
                using var command = new SqlCommand("sp_RestringirDescargaPDF", connection);

                command.CommandType = CommandType.StoredProcedure;
                command.Parameters.AddWithValue("@FK_IdSolicitud", idSolicitud);

                await connection.OpenAsync();
                await command.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error restringiendo descarga PDF para solicitud {IdSolicitud}", idSolicitud);
                throw;
            }
        }

        private async Task<DatosSolicitudPDF> ObtenerDatosSolicitudAsync(int idSolicitud)
        {
            using var connection = new SqlConnection(_connectionString);
            using var command = new SqlCommand(@"
                SELECT 
                    se.IdSolicitud,
                    se.NombresEmpleado,
                    se.DiasSolicitadosTotal,
                    se.FechaIngresoSolicitud,
                    se.Observaciones,
                    se.TipoFormatoPdf,
                    se.FK_IdEmpleado,
                    se.FK_IdAutorizador,
                    se.FK_IdEstadoSolicitud,
                    e.Puesto,
                    e.Departamento,
                    MIN(sd.FechaInicio) as FechaInicio,
                    MAX(sd.FechaFin) as FechaFin
                FROM SolicitudEncabezado se
                INNER JOIN Empleados e ON se.FK_IdEmpleado = e.IdEmpleado
                LEFT JOIN SolicitudDetalle sd ON se.IdSolicitud = sd.FK_IdSolicitud
                WHERE se.IdSolicitud = @IdSolicitud
                GROUP BY se.IdSolicitud, se.NombresEmpleado, se.DiasSolicitadosTotal, 
                         se.FechaIngresoSolicitud, se.Observaciones, se.TipoFormatoPdf, 
                         se.FK_IdEmpleado, se.FK_IdAutorizador, se.FK_IdEstadoSolicitud, e.Puesto, e.Departamento", connection);

            command.Parameters.AddWithValue("@IdSolicitud", idSolicitud);

            await connection.OpenAsync();
            using var reader = await command.ExecuteReaderAsync();

            if (await reader.ReadAsync())
            {
                var tipoFormato = reader.IsDBNull(reader.GetOrdinal("TipoFormatoPdf")) ? 1 : reader.GetInt32("TipoFormatoPdf");
                var idEmpleado = reader.GetInt32("FK_IdEmpleado");
                var idAutorizador = reader.IsDBNull(reader.GetOrdinal("FK_IdAutorizador")) ? (int?)null : reader.GetInt32("FK_IdAutorizador");
                var estadoSolicitud = reader.GetInt32("FK_IdEstadoSolicitud");
                
                var datos = new DatosSolicitudPDF
                {
                    IdSolicitud = reader.GetInt32("IdSolicitud"),
                    NombreEmpleado = reader.GetString("NombresEmpleado"),
                    Puesto = reader.IsDBNull(reader.GetOrdinal("Puesto")) ? "" : reader.GetString("Puesto"),
                    Departamento = reader.IsDBNull(reader.GetOrdinal("Departamento")) ? "" : reader.GetString("Departamento"),
                    DiasSolicitados = reader.GetDecimal("DiasSolicitadosTotal"),
                    FechaSolicitud = reader.GetDateTime("FechaIngresoSolicitud"),
                    FechaInicio = reader.IsDBNull(reader.GetOrdinal("FechaInicio")) ? null : reader.GetDateTime("FechaInicio"),
                    FechaFin = reader.IsDBNull(reader.GetOrdinal("FechaFin")) ? null : reader.GetDateTime("FechaFin"),
                    Observaciones = reader.IsDBNull(reader.GetOrdinal("Observaciones")) ? "" : reader.GetString("Observaciones"),
                    TipoFormato = (TipoFormatoPdf)tipoFormato
                };

                // Cerrar el reader antes de hacer nuevas consultas
                await reader.CloseAsync();

                // Cargar los detalles de períodos de vacaciones
                using var commandDetalles = new SqlCommand(@"
                    SELECT FechaInicio, FechaFin, DiasHabilesTomados
                    FROM SolicitudDetalle
                    WHERE FK_IdSolicitud = @IdSolicitud
                    ORDER BY FechaInicio", connection);
                commandDetalles.Parameters.AddWithValue("@IdSolicitud", idSolicitud);
                
                using var readerDetalles = await commandDetalles.ExecuteReaderAsync();
                while (await readerDetalles.ReadAsync())
                {
                    datos.Detalles.Add(new DetallePeriodoVacaciones
                    {
                        FechaInicio = readerDetalles.GetDateTime("FechaInicio"),
                        FechaFin = readerDetalles.GetDateTime("FechaFin"),
                        DiasHabiles = readerDetalles.GetDecimal("DiasHabilesTomados")
                    });
                }
                await readerDetalles.CloseAsync();

                // Obtener firma del empleado
                datos.FirmaEmpleado = await ObtenerFirmaPorEmpleadoAsync(connection, idEmpleado);
                
                if (datos.FirmaEmpleado != null)
                {
                    _logger.LogInformation("✅ Firma del empleado cargada en datos. Tamaño: {Size} bytes", datos.FirmaEmpleado.Length);
                }
                else
                {
                    _logger.LogWarning("❌ No se cargó firma del empleado en datos");
                }

                // Obtener firma y nombre del autorizador SOLO si la solicitud fue autorizada
                // Estados BD: 1=Ingresada, 2=Autorizada, 3=Vigente, 4=Finalizada, 5=Cancelada, 6=Rechazada
                // Solo mostramos firma del autorizador si está en estado 2 (Autorizada) o superior, EXCEPTO Rechazada (6)
                if (idAutorizador.HasValue && estadoSolicitud >= 2 && estadoSolicitud != 6)
                {
                    datos.FirmaAutorizador = await ObtenerFirmaPorUsuarioAsync(connection, idAutorizador.Value);
                    datos.NombreAutorizador = await ObtenerNombreAutorizadorAsync(connection, idAutorizador.Value);
                    
                    if (datos.FirmaAutorizador != null)
                    {
                        _logger.LogInformation("✅ Firma del autorizador cargada en datos (Estado: {Estado}). Tamaño: {Size} bytes", 
                            estadoSolicitud, datos.FirmaAutorizador.Length);
                    }
                }
                else if (idAutorizador.HasValue)
                {
                    // Solo cargar el nombre del autorizador, no la firma
                    datos.NombreAutorizador = await ObtenerNombreAutorizadorAsync(connection, idAutorizador.Value);
                    _logger.LogInformation("ℹ️ Solicitud en estado {Estado}. No se carga firma del autorizador.", estadoSolicitud);
                }

                return datos;
            }

            return null;
        }

        private async Task<byte[]?> ObtenerFirmaPorEmpleadoAsync(SqlConnection connection, int idEmpleado)
        {
            try
            {
                using var command = new SqlCommand(@"
                    SELECT us.SignatureImage
                    FROM UserSignatures us
                    INNER JOIN Usuarios u ON us.UserId = u.Username
                    WHERE u.FK_IdEmpleado = @IdEmpleado", connection);

                command.Parameters.AddWithValue("@IdEmpleado", idEmpleado);

                var result = await command.ExecuteScalarAsync();
                
                if (result != null && result != DBNull.Value)
                {
                    _logger.LogInformation("Firma encontrada para empleado {IdEmpleado}. Tamaño: {Size} bytes", 
                        idEmpleado, ((byte[])result).Length);
                    return result as byte[];
                }
                
                _logger.LogWarning("No se encontró firma para empleado {IdEmpleado}", idEmpleado);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener firma para empleado {IdEmpleado}", idEmpleado);
                return null;
            }
        }

        private async Task<byte[]?> ObtenerFirmaPorUsuarioAsync(SqlConnection connection, int idUsuario)
        {
            try
            {
                using var command = new SqlCommand(@"
                    SELECT us.SignatureImage
                    FROM UserSignatures us
                    INNER JOIN Usuarios u ON us.UserId = u.Username
                    WHERE u.IdUsuario = @IdUsuario", connection);

                command.Parameters.AddWithValue("@IdUsuario", idUsuario);

                var result = await command.ExecuteScalarAsync();
                
                if (result != null && result != DBNull.Value)
                {
                    _logger.LogInformation("Firma encontrada para usuario {IdUsuario}. Tamaño: {Size} bytes", 
                        idUsuario, ((byte[])result).Length);
                    return result as byte[];
                }
                
                _logger.LogWarning("No se encontró firma para usuario {IdUsuario}", idUsuario);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener firma para usuario {IdUsuario}", idUsuario);
                return null;
            }
        }

        private async Task<string?> ObtenerNombreAutorizadorAsync(SqlConnection connection, int idUsuario)
        {
            try
            {
                using var command = new SqlCommand(@"
                    SELECT CONCAT(e.NombresEmpleado, ' ', e.ApellidosEmpleado) as NombreCompleto
                    FROM Usuarios u
                    INNER JOIN Empleados e ON u.FK_IdEmpleado = e.IdEmpleado
                    WHERE u.IdUsuario = @IdUsuario", connection);

                command.Parameters.AddWithValue("@IdUsuario", idUsuario);

                var result = await command.ExecuteScalarAsync();
                return result?.ToString();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "No se pudo obtener nombre del autorizador {IdUsuario}", idUsuario);
                return null;
            }
        }


        private byte[] ComprimirConBrotli(byte[] data)
        {
            using var output = new MemoryStream();
            using var brotliStream = new BrotliStream(output, CompressionLevel.Optimal);
            brotliStream.Write(data, 0, data.Length);
            brotliStream.Close();
            return output.ToArray();
        }

        private byte[] DescomprimirBrotli(byte[] compressedData)
        {
            using var input = new MemoryStream(compressedData);
            using var brotliStream = new BrotliStream(input, CompressionMode.Decompress);
            using var output = new MemoryStream();
            brotliStream.CopyTo(output);
            return output.ToArray();
        }
    }

}
