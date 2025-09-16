using System.ComponentModel.DataAnnotations;

namespace ProyectoDojoGeko.Filters
{
    public class FechaNacimientoValidationAttribute : ValidationAttribute
    {
        protected override ValidationResult IsValid(object value, ValidationContext validationContext)
        {
            if (value is DateTime fechaNacimiento)
            {
                if (fechaNacimiento > DateTime.Now)
                {
                    return new ValidationResult("La fecha de nacimiento no puede ser una fecha futura.");
                }

                var edad = DateTime.Now.Year - fechaNacimiento.Year;
                if (fechaNacimiento.Date > DateTime.Now.AddYears(-edad))
                {
                    edad--;
                }

                if (edad < 18)
                {
                    return new ValidationResult("El empleado debe ser mayor de 18 años.");
                }
            }

            return ValidationResult.Success;
        }
    }
}
