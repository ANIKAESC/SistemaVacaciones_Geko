using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Data;
using ProyectoDojoGeko.Data;
public class FirmaController : Controller
{
    private readonly IConfiguration _cfg;

    public FirmaController(IConfiguration cfg)
    {
        _cfg = cfg;
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SubirFirma(IFormFile firma)
    {
        if (firma == null || firma.Length == 0)
            return BadRequest("Debe subir un archivo.");

        var mime = firma.ContentType?.ToLowerInvariant() ?? "";
        if (mime != "image/png" && mime != "image/jpeg")
            return BadRequest("Solo se permite PNG o JPG.");

        if (firma.Length > 2 * 1024 * 1024)
            return BadRequest("La firma no debe pesar más de 2MB.");

        byte[] bytes;
        using (var ms = new MemoryStream())
        {
            await firma.CopyToAsync(ms);
            bytes = ms.ToArray();
        }

        // Identificador del usuario (ajusta a tu auth real)
        var userId = User.Identity?.Name;
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized("No se pudo identificar al usuario.");

        var cs = _cfg.GetConnectionString("DefaultConnection");

        using var conn = new SqlConnection(cs);
        using var cmd = new SqlCommand("dbo.UserSignatures_Upsert", conn);
        cmd.CommandType = CommandType.StoredProcedure;

        cmd.Parameters.Add(new SqlParameter("@UserId", SqlDbType.NVarChar, 128) { Value = userId });
        cmd.Parameters.Add(new SqlParameter("@SignatureImage", SqlDbType.VarBinary, -1) { Value = bytes });
        cmd.Parameters.Add(new SqlParameter("@MimeType", SqlDbType.NVarChar, 50) { Value = mime });

        await conn.OpenAsync();
        await cmd.ExecuteNonQueryAsync();

        return Ok("Firma guardada.");
    }
}


