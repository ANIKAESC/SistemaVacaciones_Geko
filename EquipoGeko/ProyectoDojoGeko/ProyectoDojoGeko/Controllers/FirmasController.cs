using Microsoft.AspNetCore.Mvc;
using ProyectoDojoGeko.Data;
using ProyectoDojoGeko.Filters;
using ProyectoDojoGeko.Models;
using ProyectoDojoGeko.Services;
using ProyectoDojoGeko.Services.Interfaces;

namespace ProyectoDojoGeko.Controllers
{
    [AuthorizeSession]
    public class FirmasController : Controller
    {
        private readonly SignatureRepository _signatureRepository;
        private readonly ILoggingService _loggingService;
        private readonly IBitacoraService _bitacoraService;

        public FirmasController(
            SignatureRepository signatureRepository,
            ILoggingService loggingService,
            IBitacoraService bitacoraService)
        {
            _signatureRepository = signatureRepository;
            _loggingService = loggingService;
            _bitacoraService = bitacoraService;
        }

        // GET: Firmas/MiFirma
        [HttpGet]
        public async Task<IActionResult> MiFirma()
        {
            try
            {
                var idUsuario = HttpContext.Session.GetInt32("IdUsuario");
                if (!idUsuario.HasValue)
                {
                    return RedirectToAction("Login", "Account");
                }

                var firma = await _signatureRepository.ObtenerFirmaPorUsuarioAsync(idUsuario.Value);

                return View(firma ?? new FirmaViewModel { FK_IdUsuario = idUsuario.Value });
            }
            catch (Exception ex)
            {
                await _loggingService.RegistrarLogAsync(new LogViewModel
                {
                    Accion = "Error - Ver Firma",
                    Descripcion = $"Error al cargar la firma: {ex.Message}",
                    Estado = false
                });

                TempData["ErrorMessage"] = "Ocurrió un error al cargar tu firma.";
                return RedirectToAction("Index", "Home");
            }
        }

        // POST: Firmas/SubirFirma
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubirFirma(IFormFile imagenFirma)
        {
            try
            {
                var idUsuario = HttpContext.Session.GetInt32("IdUsuario");
                if (!idUsuario.HasValue)
                {
                    return RedirectToAction("Login", "Account");
                }

                // Validaciones
                if (imagenFirma == null || imagenFirma.Length == 0)
                {
                    TempData["ErrorMessage"] = "Por favor, selecciona una imagen de firma.";
                    return RedirectToAction(nameof(MiFirma));
                }

                // Validar tipo de archivo
                var allowedTypes = new[] { "image/jpeg", "image/jpg", "image/png", "image/gif" };
                if (!allowedTypes.Contains(imagenFirma.ContentType.ToLower()))
                {
                    TempData["ErrorMessage"] = "Solo se permiten imágenes en formato JPG, PNG o GIF.";
                    return RedirectToAction(nameof(MiFirma));
                }

                // Validar tamaño (máximo 2MB)
                if (imagenFirma.Length > 2 * 1024 * 1024)
                {
                    TempData["ErrorMessage"] = "La imagen no debe superar los 2MB.";
                    return RedirectToAction(nameof(MiFirma));
                }

                // Convertir imagen a bytes
                using var memoryStream = new MemoryStream();
                await imagenFirma.CopyToAsync(memoryStream);
                var imageBytes = memoryStream.ToArray();

                // Crear modelo de firma
                var firma = new FirmaViewModel
                {
                    FK_IdUsuario = idUsuario.Value,
                    ImagenFirmaData = imageBytes,
                    NombreArchivo = imagenFirma.FileName,
                    ContentType = imagenFirma.ContentType,
                    TamanoArchivo = (int)imagenFirma.Length
                };

                // Guardar en la base de datos
                var resultado = await _signatureRepository.GuardarFirmaAsync(firma);

                if (resultado)
                {
                    await _bitacoraService.RegistrarBitacoraAsync(
                        "Firma Actualizada",
                        $"Usuario {HttpContext.Session.GetString("NombreCompletoEmpleado")} actualizó su firma digital"
                    );

                    TempData["SuccessMessage"] = "Tu firma se ha guardado exitosamente.";
                }
                else
                {
                    TempData["ErrorMessage"] = "No se pudo guardar la firma. Intenta nuevamente.";
                }

                return RedirectToAction(nameof(MiFirma));
            }
            catch (Exception ex)
            {
                await _loggingService.RegistrarLogAsync(new LogViewModel
                {
                    Accion = "Error - Subir Firma",
                    Descripcion = $"Error al guardar la firma: {ex.Message}",
                    Estado = false
                });

                TempData["ErrorMessage"] = "Ocurrió un error al guardar tu firma.";
                return RedirectToAction(nameof(MiFirma));
            }
        }

        // POST: Firmas/EliminarFirma
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EliminarFirma()
        {
            try
            {
                var idUsuario = HttpContext.Session.GetInt32("IdUsuario");
                if (!idUsuario.HasValue)
                {
                    return RedirectToAction("Login", "Account");
                }

                var resultado = await _signatureRepository.EliminarFirmaAsync(idUsuario.Value);

                if (resultado)
                {
                    await _bitacoraService.RegistrarBitacoraAsync(
                        "Firma Eliminada",
                        $"Usuario {HttpContext.Session.GetString("NombreCompletoEmpleado")} eliminó su firma digital"
                    );

                    TempData["SuccessMessage"] = "Tu firma se ha eliminado exitosamente.";
                }
                else
                {
                    TempData["ErrorMessage"] = "No se pudo eliminar la firma.";
                }

                return RedirectToAction(nameof(MiFirma));
            }
            catch (Exception ex)
            {
                await _loggingService.RegistrarLogAsync(new LogViewModel
                {
                    Accion = "Error - Eliminar Firma",
                    Descripcion = $"Error al eliminar la firma: {ex.Message}",
                    Estado = false
                });

                TempData["ErrorMessage"] = "Ocurrió un error al eliminar tu firma.";
                return RedirectToAction(nameof(MiFirma));
            }
        }

        // GET: Firmas/ObtenerImagen
        [HttpGet]
        public async Task<IActionResult> ObtenerImagen()
        {
            try
            {
                var idUsuario = HttpContext.Session.GetInt32("IdUsuario");
                if (!idUsuario.HasValue)
                {
                    return NotFound();
                }

                var firma = await _signatureRepository.ObtenerFirmaPorUsuarioAsync(idUsuario.Value);

                if (firma == null || firma.ImagenFirmaData == null)
                {
                    return NotFound();
                }

                return File(firma.ImagenFirmaData, firma.ContentType);
            }
            catch
            {
                return NotFound();
            }
        }
    }
}
