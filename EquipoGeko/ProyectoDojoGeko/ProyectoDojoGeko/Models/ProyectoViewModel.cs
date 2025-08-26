using Microsoft.AspNetCore.Http.HttpResults;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProyectoDojoGeko.Models
{
    [Table("Proyectos")]
    public class ProyectoViewModel
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("IdProyecto")]
        public int IdProyecto { get; set; }

        [Required(ErrorMessage = "El nombre del proyecto es obligatorio.")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "El nombre del proyecto debe tener entre {2} y {1} caracteres.")]
        [Column("Nombre")]
        public string Nombre { get; set; }

        [StringLength(255, ErrorMessage = "La descripción no puede exceder los 255 caracteres.")]
        [Column("Descripcion")]
        public string? Descripcion { get; set; }

        [Required(ErrorMessage = "El campo Fecha de inicio es requerido")]
        [Column("FechaInicio")]
        public DateTime? FechaInicio { get; set; }

        [Required(ErrorMessage = "El campo Estado es requerido")]
        [Column("FK_IdEstado")]
        public int FK_IdEstado { get; set; } 

        
    }
}
