using Microsoft.Data.SqlClient;
using ProyectoDojoGeko.Models;
using System.Data;

namespace ProyectoDojoGeko.Data
{
    /// <summary>
    /// Repositorio para gestionar las firmas digitales de los usuarios
    /// Usa la tabla UserSignatures existente
    /// </summary>
    public class SignatureRepository
    {
        private readonly string _connectionString;

        public SignatureRepository(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")!;
        }

        /// <summary>
        /// Obtiene la firma de un usuario por su ID de Usuario
        /// </summary>
        public async Task<FirmaViewModel?> ObtenerFirmaPorUsuarioAsync(int idUsuario)
        {
            // Primero obtener el UserId (string) desde la tabla Usuarios
            string? userId = await ObtenerUserIdStringAsync(idUsuario);
            if (userId == null)
                return null;

            using var connection = new SqlConnection(_connectionString);
            using var command = new SqlCommand("UserSignatures_Get", connection);
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.AddWithValue("@UserId", userId);

            await connection.OpenAsync();
            using var reader = await command.ExecuteReaderAsync();

            if (await reader.ReadAsync())
            {
                var imageData = (byte[])reader["SignatureImage"];
                return new FirmaViewModel
                {
                    FK_IdUsuario = idUsuario,
                    ImagenFirmaData = imageData,
                    NombreArchivo = $"firma_{userId}.png",
                    ContentType = reader.GetString("MimeType"),
                    TamanoArchivo = imageData.Length,
                    FechaActualizacion = reader.GetDateTime("UpdatedAt")
                };
            }

            return null;
        }

        /// <summary>
        /// Obtiene el UserId (string) desde la tabla Usuarios por IdUsuario (int)
        /// </summary>
        private async Task<string?> ObtenerUserIdStringAsync(int idUsuario)
        {
            using var connection = new SqlConnection(_connectionString);
            using var command = new SqlCommand(@"
                SELECT Username 
                FROM Usuarios 
                WHERE IdUsuario = @IdUsuario", connection);

            command.Parameters.AddWithValue("@IdUsuario", idUsuario);

            await connection.OpenAsync();
            var result = await command.ExecuteScalarAsync();
            return result?.ToString();
        }

        /// <summary>
        /// Guarda o actualiza la firma de un usuario usando el stored procedure
        /// </summary>
        public async Task<bool> GuardarFirmaAsync(FirmaViewModel firma)
        {
            // Obtener el UserId (string) desde la tabla Usuarios
            string? userId = await ObtenerUserIdStringAsync(firma.FK_IdUsuario);
            if (userId == null)
                return false;

            using var connection = new SqlConnection(_connectionString);
            using var command = new SqlCommand("UserSignatures_Upsert", connection);
            command.CommandType = CommandType.StoredProcedure;

            command.Parameters.AddWithValue("@UserId", userId);
            
            var imagenParam = new SqlParameter("@SignatureImage", SqlDbType.VarBinary, -1);
            imagenParam.Value = firma.ImagenFirmaData ?? (object)DBNull.Value;
            command.Parameters.Add(imagenParam);

            command.Parameters.AddWithValue("@MimeType", firma.ContentType);

            await connection.OpenAsync();
            await command.ExecuteNonQueryAsync();
            
            return true;
        }

        /// <summary>
        /// Elimina la firma de un usuario
        /// </summary>
        public async Task<bool> EliminarFirmaAsync(int idUsuario)
        {
            // Obtener el UserId (string) desde la tabla Usuarios
            string? userId = await ObtenerUserIdStringAsync(idUsuario);
            if (userId == null)
                return false;

            using var connection = new SqlConnection(_connectionString);
            using var command = new SqlCommand(@"
                DELETE FROM UserSignatures
                WHERE UserId = @UserId", connection);

            command.Parameters.AddWithValue("@UserId", userId);

            await connection.OpenAsync();
            return await command.ExecuteNonQueryAsync() > 0;
        }

        /// <summary>
        /// Obtiene solo los bytes de la imagen de firma (para usar en PDFs)
        /// </summary>
        public async Task<byte[]?> ObtenerImagenFirmaAsync(int idUsuario)
        {
            var firma = await ObtenerFirmaPorUsuarioAsync(idUsuario);
            return firma?.ImagenFirmaData;
        }
    }
}
