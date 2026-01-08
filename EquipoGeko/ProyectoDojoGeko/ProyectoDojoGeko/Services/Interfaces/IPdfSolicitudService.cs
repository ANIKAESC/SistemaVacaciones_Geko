namespace ProyectoDojoGeko.Services.Interfaces
{
    /// <summary>
    /// Servicio para la generación, almacenamiento y gestión de PDFs de solicitudes de vacaciones
    /// </summary>
    public interface IPdfSolicitudService
    {
        /// <summary>
        /// Genera un PDF para una solicitud específica
        /// </summary>
        /// <param name="idSolicitud">ID de la solicitud</param>
        /// <returns>Bytes del PDF generado</returns>
        Task<byte[]> GenerarPDFSolicitudAsync(int idSolicitud);

        /// <summary>
        /// Guarda el PDF comprimido en la base de datos
        /// </summary>
        /// <param name="idSolicitud">ID de la solicitud</param>
        /// <param name="pdfBytes">Bytes del PDF a guardar</param>
        /// <returns>True si se guardó correctamente</returns>
        Task<bool> GuardarPDFEnBaseDatosAsync(int idSolicitud, byte[] pdfBytes);

        /// <summary>
        /// Obtiene el PDF de una solicitud desde la base de datos
        /// </summary>
        /// <param name="idSolicitud">ID de la solicitud</param>
        /// <returns>Tupla con el contenido del PDF y el nombre del archivo, o null si no está disponible</returns>
        Task<(byte[] contenido, string nombreArchivo)?> ObtenerPDFSolicitudAsync(int idSolicitud);

        /// <summary>
        /// Restringe la descarga del PDF cuando la solicitud es autorizada
        /// </summary>
        /// <param name="idSolicitud">ID de la solicitud</param>
        Task RestringirDescargaPDFAsync(int idSolicitud);
    }
}
