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
        public string Roles { get; set; } // "TeamLider, SubLider, etc."
        public string NombreCompleto => $"{NombresEmpleado} {ApellidosEmpleado}";
    }
}
