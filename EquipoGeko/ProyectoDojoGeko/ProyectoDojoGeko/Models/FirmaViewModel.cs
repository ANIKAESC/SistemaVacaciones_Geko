using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProyectoDojoGeko.Models
{
    /// <summary>
    /// Modelo para gestionar las firmas digitales de los usuarios
    /// </summary>
    public class FirmaViewModel
    {
        [Key]
        public int IdFirma { get; set; }

        [Required]
        [Display(Name = "Usuario")]
        public int FK_IdUsuario { get; set; }

        [Column(TypeName = "varbinary(max)")]
        public byte[]? ImagenFirmaData { get; set; }

        [Required]
        [StringLength(255)]
        [Display(Name = "Nombre del Archivo")]
        public string NombreArchivo { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        [Display(Name = "Tipo de Contenido")]
        public string ContentType { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Tamaño del Archivo")]
        public int TamanoArchivo { get; set; }

        [Display(Name = "Fecha de Creación")]
        public DateTime FechaCreacion { get; set; }

        [Display(Name = "Fecha de Actualización")]
        public DateTime? FechaActualizacion { get; set; }

        // Propiedad no mapeada para el archivo de upload
        [NotMapped]
        [Display(Name = "Imagen de Firma")]
        public IFormFile? ImagenFirma { get; set; }

        // Propiedad para mostrar la imagen en base64 en la vista
        [NotMapped]
        public string? ImagenBase64 
        { 
            get 
            {
                if (ImagenFirmaData != null && ImagenFirmaData.Length > 0)
                {
                    return $"data:{ContentType};base64,{Convert.ToBase64String(ImagenFirmaData)}";
                }
                return null;
            }
        }
    }
}
