using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ProyectoDojoGeko.Helper
{
    /// <summary>
    /// Tipos de formato para PDFs de solicitud
    /// </summary>
    public enum TipoFormatoPdf
    {
        /// <summary>
        /// Formato GDG - Solicitud de Días de Disponibilidad
        /// </summary>
        FormatoGDG = 1,

        /// <summary>
        /// Formato Digital Geko Corp - Solicitud de Días de Vacaciones
        /// </summary>
        FormatoDigitalGeko = 2
    }

    /// <summary>
    /// Modelo de datos para el PDF de solicitud
    /// </summary>
    public class DatosSolicitudPDF
    {
        public int IdSolicitud { get; set; }
        public string NombreEmpleado { get; set; } = string.Empty;
        public string Puesto { get; set; } = string.Empty;
        public string Departamento { get; set; } = string.Empty;
        public decimal DiasSolicitados { get; set; }
        public DateTime FechaSolicitud { get; set; }
        public DateTime? FechaInicio { get; set; }
        public DateTime? FechaFin { get; set; }
        public string Observaciones { get; set; } = string.Empty;
        public TipoFormatoPdf TipoFormato { get; set; } = TipoFormatoPdf.FormatoGDG;
        
        // Detalles de períodos de vacaciones
        public List<DetallePeriodoVacaciones> Detalles { get; set; } = new List<DetallePeriodoVacaciones>();
        
        // Firmas digitales
        public byte[]? FirmaEmpleado { get; set; }
        public byte[]? FirmaAutorizador { get; set; }
        public string? NombreAutorizador { get; set; }
    }

    /// <summary>
    /// Detalle de un período de vacaciones
    /// </summary>
    public class DetallePeriodoVacaciones
    {
        public DateTime FechaInicio { get; set; }
        public DateTime FechaFin { get; set; }
        public decimal DiasHabiles { get; set; }
    }

    /// <summary>
    /// Documento QuestPDF para generar solicitudes de vacaciones - Formato GDG
    /// </summary>
    public class SolicitudPdfDocumentoGDG : IDocument
    {
        private readonly DatosSolicitudPDF _datos;

        public SolicitudPdfDocumentoGDG(DatosSolicitudPDF datos)
        {
            _datos = datos;
            
            // Debug: Verificar si las firmas están presentes
            System.Diagnostics.Debug.WriteLine($"🔍 PDF Constructor GDG - Firma Empleado: {(_datos.FirmaEmpleado != null ? $"{_datos.FirmaEmpleado.Length} bytes" : "NULL")}");
            System.Diagnostics.Debug.WriteLine($"🔍 PDF Constructor GDG - Firma Autorizador: {(_datos.FirmaAutorizador != null ? $"{_datos.FirmaAutorizador.Length} bytes" : "NULL")}");
            System.Diagnostics.Debug.WriteLine($"🔍 PDF Constructor GDG - Nombre Empleado: {_datos.NombreEmpleado}");
        }

        public DocumentMetadata GetMetadata() => DocumentMetadata.Default;

        public void Compose(IDocumentContainer container)
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(50);
                page.DefaultTextStyle(x => x.FontSize(12).FontFamily("Arial"));

                page.Content().Column(col =>
                {
                    col.Spacing(10);

                    // Título centrado
                    col.Item().AlignCenter().Text("SOLICITUD DE DIAS DE DISPONIBILIDAD. GDG")
                        .FontSize(16)
                        .Bold();

                    col.Item().PaddingTop(20);

                    // Fecha alineada a la derecha
                    col.Item().AlignRight()
                        .Text($"Fecha: {_datos.FechaSolicitud.ToLocalTime():dd/MM/yyyy}")
                        .FontSize(12);

                    col.Item().PaddingTop(20);

                    // Encabezado
                    col.Item().Text("Señores");
                    col.Item().Text("Grupo Digital de Guatemala");
                    col.Item().Text("Presente.");

                    col.Item().PaddingTop(20);

                    // Cuerpo del documento
                    col.Item().AlignCenter()
                    .DefaultTextStyle(x => x.LineHeight(1.5f))
                    .Text(text =>
                    {
                        text.Span("Por este medio hago de su conocimiento que el (la) señor (ita) ");
                        text.Span(_datos.NombreEmpleado).Underline().Bold();
                        text.Span(" que aporta su industria como: ");
                        text.Span(_datos.Puesto).Underline().Bold();
                        text.Span(" en el Departamento de ");
                        text.Span(_datos.Departamento).Underline().Bold();
                        text.Span(" tomará ");
                        text.Span(_datos.DiasSolicitados.ToString("0.00")).Underline().Bold();
                        text.Span(" días de disponibilidad, correspondientes al periodo: ");

                        // Usar detalles si existen, sino usar fechas MIN/MAX
                        string periodoCorrespondiente;
                        if (_datos.Detalles != null && _datos.Detalles.Any())
                        {
                            var fechaMin = _datos.Detalles.Min(d => d.FechaInicio);
                            var fechaMax = _datos.Detalles.Max(d => d.FechaFin);
                            periodoCorrespondiente = $"{fechaMin:dd/MM/yyyy} AL {fechaMax:dd/MM/yyyy}";
                        }
                        else
                        {
                            periodoCorrespondiente = _datos.FechaInicio.HasValue && _datos.FechaFin.HasValue
                                ? $"{_datos.FechaInicio.Value.ToLocalTime():dd/MM/yyyy} AL {_datos.FechaFin.Value.ToLocalTime():dd/MM/yyyy}"
                                : "Por definir";
                        }

                        text.Span(periodoCorrespondiente).Underline().Bold();
                    });

                    col.Item().PaddingTop(20);

                    // Períodos de vacaciones (detalles) - Formato GDG
                    if (_datos.Detalles != null && _datos.Detalles.Any())
                    {
                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(1);
                                columns.RelativeColumn(1);
                                columns.RelativeColumn(1);
                            });

                            table.Header(header =>
                            {
                                header.Cell().Border(1).Background("#E0E0E0").Padding(5)
                                    .Text("Inicio").FontSize(12).Bold();
                                header.Cell().Border(1).Background("#E0E0E0").Padding(5)
                                    .Text("Fin").FontSize(12).Bold();
                                header.Cell().Border(1).Background("#E0E0E0").Padding(5)
                                    .Text("Días").FontSize(12).Bold();
                            });

                            foreach (var detalle in _datos.Detalles)
                            {
                                table.Cell().Border(1).Padding(5)
                                    .Text(detalle.FechaInicio.ToString("dd/MM/yyyy")).FontSize(11);
                                table.Cell().Border(1).Padding(5)
                                    .Text(detalle.FechaFin.ToString("dd/MM/yyyy")).FontSize(11);
                                table.Cell().Border(1).Padding(5)
                                    .Text(detalle.DiasHabiles.ToString("0.00")).FontSize(11);
                            }
                        });
                    }
                    else
                    {
                        col.Item().AlignCenter()
                        .DefaultTextStyle(x => x.FontSize(14).Bold())
                        .Text(text =>
                        {
                            var fechaInicio = _datos.FechaInicio?.ToLocalTime().ToString("dd/MM/yyyy") ?? "___/___/___";
                            var fechaFin = _datos.FechaFin?.ToLocalTime().ToString("dd/MM/yyyy") ?? "___/___/___";
                            text.Span($"LOS DÍAS LOS TOMARÁ DEL {fechaInicio} AL {fechaFin} DEL AÑO EN CURSO.");
                        });
                    }

                    col.Item().PaddingTop(30);

                    // Observaciones
                    col.Item().Column(obsCol =>
                    {
                        obsCol.Item().Text("Observaciones:").Bold();
                        obsCol.Item().Border(1).BorderColor(Colors.Black)
                            .MinHeight(80)
                            .Padding(10)
                            .Text(_datos.Observaciones);
                    });

                    col.Item().PaddingTop(60);

                    // Firmas
                    col.Item().Row(row =>
                    {
                        // Firma del Autorizador (Izquierda)
                        row.RelativeItem().Column(c =>
                        {
                            // Mostrar imagen de firma si existe
                            if (_datos.FirmaAutorizador != null && _datos.FirmaAutorizador.Length > 0)
                            {
                                c.Item().PaddingBottom(5).AlignCenter().Width(200).Height(60)
                                    .Image(_datos.FirmaAutorizador, ImageScaling.FitArea);
                            }
                            else
                            {
                                c.Item().Height(60);
                            }
                            
                            c.Item().BorderBottom(1).BorderColor(Colors.Black);
                            c.Item().PaddingTop(5).AlignCenter()
                                .Text("(f) Director o encargado");
                        });

                        row.Spacing(40);

                        // Firma del Empleado (Derecha)
                        row.RelativeItem().Column(c =>
                        {
                            // Mostrar imagen de firma si existe
                            if (_datos.FirmaEmpleado != null && _datos.FirmaEmpleado.Length > 0)
                            {
                                c.Item().PaddingBottom(5).AlignCenter().Width(200).Height(60)
                                    .Image(_datos.FirmaEmpleado, ImageScaling.FitArea);
                            }
                            else
                            {
                                c.Item().Height(60);
                            }
                            
                            c.Item().BorderBottom(1).BorderColor(Colors.Black);
                            c.Item().PaddingTop(5).AlignCenter()
                                .Text("(f) Socio Industrial");
                        });
                    });
                });
            });
        }
    }

    /// <summary>
    /// Documento QuestPDF para generar solicitudes de vacaciones - Formato Digital Geko Corp
    /// </summary>
    public class SolicitudPdfDocumentoDigitalGeko : IDocument
    {
        private readonly DatosSolicitudPDF _datos;

        public SolicitudPdfDocumentoDigitalGeko(DatosSolicitudPDF datos)
        {
            _datos = datos;
        }

        public DocumentMetadata GetMetadata() => DocumentMetadata.Default;

        public void Compose(IDocumentContainer container)
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(50);
                page.DefaultTextStyle(x => x.FontSize(12).FontFamily("Arial"));

                page.Content().Column(col =>
                {
                    col.Spacing(15);

                    // Título centrado
                    col.Item().AlignCenter().Text("SOLICITUD DE DÍAS DE VACACIONES")
                        .FontSize(16)
                        .Bold();

                    col.Item().PaddingTop(20);

                    // Fecha alineada a la derecha
                    col.Item().AlignRight()
                        .Text($"Fecha: {_datos.FechaSolicitud.ToLocalTime():dd/MM/yyyy}")
                        .FontSize(12);

                    col.Item().PaddingTop(20);

                    // Encabezado
                    col.Item().Text("Señores");
                    col.Item().Text("Digital Geko Corp S.A.");
                    col.Item().Text("Presente");

                    col.Item().PaddingTop(20);

                    // Cuerpo del documento (mismo estilo que GDG)
                    col.Item().Text(text =>
                    {
                        text.Span("Por este medio hago de su conocimiento que el (la) señor (ita) ");
                        text.Span(_datos.NombreEmpleado).Underline().Bold();
                        text.Span(" que aporta su empresa como: ");
                        text.Span(_datos.Puesto).Underline().Bold();
                        text.Span(" en el Departamento de ");
                        text.Span(_datos.Departamento).Underline().Bold();
                        text.Span(" tomará ");
                        text.Span(_datos.DiasSolicitados.ToString("0.00")).Underline().Bold();
                        text.Span(" días de vacaciones, correspondientes al periodo: ");

                        // Usar detalles si existen, sino usar fechas MIN/MAX
                        string periodoCorrespondiente;
                        if (_datos.Detalles != null && _datos.Detalles.Any())
                        {
                            var fechaMin = _datos.Detalles.Min(d => d.FechaInicio);
                            var fechaMax = _datos.Detalles.Max(d => d.FechaFin);
                            periodoCorrespondiente = $"{fechaMin:dd/MM/yyyy} AL {fechaMax:dd/MM/yyyy}";
                        }
                        else
                        {
                            periodoCorrespondiente = _datos.FechaInicio.HasValue && _datos.FechaFin.HasValue
                                ? $"{_datos.FechaInicio.Value.ToLocalTime():dd/MM/yyyy} AL {_datos.FechaFin.Value.ToLocalTime():dd/MM/yyyy}"
                                : "Por definir";
                        }

                        text.Span(periodoCorrespondiente).Underline().Bold();
                    });

                    col.Item().PaddingTop(20);

                    // Períodos de vacaciones (detalles) - Formato Digital Geko
                    if (_datos.Detalles != null && _datos.Detalles.Any())
                    {
                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(1);
                                columns.RelativeColumn(1);
                                columns.RelativeColumn(1);
                            });

                            table.Header(header =>
                            {
                                header.Cell().Border(1).Background("#E0E0E0").Padding(5)
                                    .Text("Inicio").FontSize(12).Bold();
                                header.Cell().Border(1).Background("#E0E0E0").Padding(5)
                                    .Text("Fin").FontSize(12).Bold();
                                header.Cell().Border(1).Background("#E0E0E0").Padding(5)
                                    .Text("Días").FontSize(12).Bold();
                            });

                            foreach (var detalle in _datos.Detalles)
                            {
                                table.Cell().Border(1).Padding(5)
                                    .Text(detalle.FechaInicio.ToString("dd/MM/yyyy")).FontSize(11);
                                table.Cell().Border(1).Padding(5)
                                    .Text(detalle.FechaFin.ToString("dd/MM/yyyy")).FontSize(11);
                                table.Cell().Border(1).Padding(5)
                                    .Text(detalle.DiasHabiles.ToString("0.00")).FontSize(11);
                            }
                        });
                    }
                    else
                    {
                        col.Item().AlignCenter()
                        .DefaultTextStyle(x => x.FontSize(14).Bold())
                        .Text(text =>
                        {
                            var fechaInicio = _datos.FechaInicio?.ToLocalTime().ToString("dd/MM/yyyy") ?? "___/___/___";
                            var fechaFin = _datos.FechaFin?.ToLocalTime().ToString("dd/MM/yyyy") ?? "___/___/___";
                            text.Span($"LOS DÍAS LOS TOMARÁ DEL {fechaInicio} AL {fechaFin} DEL AÑO EN CURSO.");
                        });
                    }

                    col.Item().PaddingTop(30);

                    // Observaciones
                    col.Item().Column(obsCol =>
                    {
                        obsCol.Item().Text("Observaciones:").Bold();
                        obsCol.Item().PaddingTop(5).Text(_datos.Observaciones);
                    });

                    col.Item().PaddingTop(60);

                    // Firmas
                    col.Item().Row(row =>
                    {
                        // Firma del Autorizador (Izquierda)
                        row.RelativeItem().Column(c =>
                        {
                            // Mostrar imagen de firma si existe
                            if (_datos.FirmaAutorizador != null && _datos.FirmaAutorizador.Length > 0)
                            {
                                c.Item().PaddingBottom(5).AlignCenter().Width(200).Height(60)
                                    .Image(_datos.FirmaAutorizador, ImageScaling.FitArea);
                            }
                            else
                            {
                                c.Item().Height(60);
                            }
                            
                            c.Item().BorderBottom(1).BorderColor(Colors.Black);
                            c.Item().PaddingTop(5).AlignCenter()
                                .Text("(f) Director o encargado");
                        });

                        row.Spacing(40);

                        // Firma del Empleado (Derecha)
                        row.RelativeItem().Column(c =>
                        {
                            // Mostrar imagen de firma si existe
                            if (_datos.FirmaEmpleado != null && _datos.FirmaEmpleado.Length > 0)
                            {
                                c.Item().PaddingBottom(5).AlignCenter().Width(200).Height(60)
                                    .Image(_datos.FirmaEmpleado, ImageScaling.FitArea);
                            }
                            else
                            {
                                c.Item().Height(60);
                            }
                            
                            c.Item().BorderBottom(1).BorderColor(Colors.Black);
                            c.Item().PaddingTop(5).AlignCenter()
                                .Text("(f) Profesional");
                        });
                    });
                });
            });
        }
    }

    /// <summary>
    /// Factory para crear el documento PDF según el tipo de formato
    /// </summary>
    public static class SolicitudPdfDocumentFactory
    {
        public static IDocument CrearDocumento(DatosSolicitudPDF datos)
        {
            return datos.TipoFormato switch
            {
                TipoFormatoPdf.FormatoGDG => new SolicitudPdfDocumentoGDG(datos),
                TipoFormatoPdf.FormatoDigitalGeko => new SolicitudPdfDocumentoDigitalGeko(datos),
                _ => new SolicitudPdfDocumentoGDG(datos) // Por defecto GDG
            };
        }
    }
}