using Microsoft.AspNetCore.Mvc;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using ProyectoDojoGeko.Helper;
using QuestPDF.Helpers;
using ProyectoDojoGeko.Data;
using ProyectoDojoGeko.Services.Interfaces;

public class PdfController : Controller
{
    private readonly IPdfSolicitudService _pdfService;

    public PdfController(IPdfSolicitudService pdfService)
    {
        _pdfService = pdfService;
    }

    [HttpGet]
    public async Task<IActionResult> BoletaDiasDisponibilidad(int id, bool download = false)
    {
        // Usar el servicio que genera el PDF con firmas
        var pdfBytes = await _pdfService.GenerarPDFSolicitudAsync(id);
        
        if (pdfBytes == null || pdfBytes.Length == 0)
        {
            return NotFound("No se pudo generar el PDF");
        }
        
        // Si download=true, forzar descarga. Si no, mostrar inline en el navegador
        if (download)
        {
            return File(pdfBytes, "application/pdf", $"Solicitud_{id}.pdf");
        }
        else
        {
            // Sin el tercer parámetro, el navegador intentará mostrar el PDF inline
            Response.Headers.Add("Content-Disposition", "inline; filename=Solicitud_" + id + ".pdf");
            return File(pdfBytes, "application/pdf");
        }
    }
}

// Modelo simple para el PDF (lo refinamos después)

    public class BoletaPdfModel
    {
        public string Fecha { get; set; } = "";
        public string Nombre { get; set; } = "";
        public string Puesto { get; set; } = "";
        public string Departamento { get; set; } = "";
        public string Periodo { get; set; } = "";
        public string Observaciones { get; set; } = "";

        public byte[]? FirmaDirector { get; set; }
        public byte[]? FirmaSocio { get; set; }
    }

public class BoletaDiasPdfDocument : IDocument
{
    private readonly BoletaPdfModel _m;

    public BoletaDiasPdfDocument(BoletaPdfModel model)
    {
        _m = model;
    }

    public DocumentMetadata GetMetadata() => DocumentMetadata.Default;

    public void Compose(IDocumentContainer container)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.Letter);
            page.Margin(50);
            page.DefaultTextStyle(x => x.FontSize(12));

            page.Content().Column(col =>
            {
                col.Spacing(25);

                // Fecha (arriba a la derecha)
                col.Item().AlignRight()
                    .Text($"Fecha: {_m.Fecha}");

                // Encabezado
                col.Item().Text("Señores");
                col.Item().Text("Grupo Digital de Guatemala");
                col.Item().Text("Presente.");

                // Cuerpo
                col.Item().PaddingTop(20).Text(text =>
                {
                    text.Span("Por este medio hago de su conocimiento que el (la) señor (ita) ");
                    text.Span(_m.Nombre).SemiBold();
                    text.Span(", que aporta su industria como ");
                    text.Span(_m.Puesto).SemiBold();
                    text.Span(" en el Departamento ");
                    text.Span(_m.Departamento).SemiBold();
                    text.Span(", tomará días de disponibilidad, correspondientes al periodo:");
                });

                // Periodo
                col.Item().AlignCenter()
                    .Text(_m.Periodo)
                    .SemiBold();

                // Observaciones
                col.Item().PaddingTop(10)
                    .Text($"Observaciones: {_m.Observaciones}");

                // Espacio para firmas
                col.Item().PaddingTop(60).Row(row =>
                {
                    row.RelativeItem().Column(c =>
                    {
                        if (_m.FirmaDirector != null)
 
                            c.Item().Height(80).Image(_m.FirmaDirector);


                        c.Item().PaddingTop(5)
                            .Text("__________________________________");
                        c.Item().Text("(f) Director o encargado");
                    });

                    row.RelativeItem().Column(c =>
                    {
                        if (_m.FirmaSocio != null)
                            c.Item().Height(80).Image(_m.FirmaSocio);


                        c.Item().PaddingTop(5)
                            .Text("__________________________________");
                        c.Item().Text("(f) Socio Industrial");
                    });
                });
            });
        });
    }
}
