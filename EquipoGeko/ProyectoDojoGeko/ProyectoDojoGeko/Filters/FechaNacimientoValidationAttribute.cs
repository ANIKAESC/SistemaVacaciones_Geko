using System.ComponentModel.DataAnnotations;

namespace ProyectoDojoGeko.Filters
{
    public class FechaNacimientoValidationAttribute : ValidationAttribute
    {
        protected override ValidationResult IsValid(object value, ValidationContext validationContext)
        {
            if (value is DateTime fechaNacimiento)
            {
                var today = DateTime.Today;
                if (fechaNacimiento.Date > today)
                {
                    return new ValidationResult("La fecha de nacimiento no puede ser una fecha futura.");
                }

                var edad = today.Year - fechaNacimiento.Year;
                if (fechaNacimiento.Date > today.AddYears(-edad))
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
