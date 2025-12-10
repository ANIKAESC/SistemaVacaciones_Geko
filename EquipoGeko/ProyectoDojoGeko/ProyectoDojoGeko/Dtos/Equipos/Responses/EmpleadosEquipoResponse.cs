using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace ProyectoDojoGeko.Dtos.Equipos.Responses
{
    public class EmpleadosEquipoResponse
    {
        public int IdEmpleado { get; set; }
        public string NombresEmpleado { get; set; }
        public string ApellidosEmpleado { get; set; }

        // Los roles pueden ir vacios si el empleado no tiene ningun rol asignado en el equipo
        public string? Rol { get; set; } // "TeamLider, SubLider, etc."
        public string NombreCompleto => $"{NombresEmpleado} {ApellidosEmpleado}";
    }
}
