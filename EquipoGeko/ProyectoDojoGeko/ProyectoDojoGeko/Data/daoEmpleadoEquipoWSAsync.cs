using System.Data;
using Microsoft.Data.SqlClient;
using ProyectoDojoGeko.Models;

namespace ProyectoDojoGeko.Data
{
    public class daoEmpleadoEquipoWSAsync
    {
        private readonly string _connectionString;

        public daoEmpleadoEquipoWSAsync(string connectionString)
        {
            _connectionString = connectionString;
        }

        // Asignar empleado a equipo
        public async Task<bool> AsignarEmpleadoAEquipoAsync(int idEmpleado, int idEquipo)
        {
            var parametros = new[]
            {
                new SqlParameter("@FK_IdEquipo", idEquipo),
                new SqlParameter("@FK_IdEmpleado", idEmpleado),
            };

            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                using (SqlCommand cmd = new SqlCommand("sp_AsignarEmpleadoAEquipo", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddRange(parametros);
                    int rowsAffected = await cmd.ExecuteNonQueryAsync();
                    return rowsAffected > 0;
                }
            }
        }

        // Remover empleado de equipo
        public async Task<bool> RemoverEmpleadoDeEquipoAsync(int idEmpleado, int idEquipo)
        {
            var parametros = new[]
            {
                new SqlParameter("@FK_IdEmpleado", idEmpleado),
                new SqlParameter("@FK_IdEquipo", idEquipo)
            };

            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                using (SqlCommand cmd = new SqlCommand("sp_RemoverEmpleadoDeEquipo", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddRange(parametros);
                    int rowsAffected = await cmd.ExecuteNonQueryAsync();
                    return rowsAffected > 0;
                }
            }
        }

        // Listar empleados por equipo
        public async Task<List<EmpleadoViewModel>> ListarEmpleadosPorEquipoAsync(int idEquipo)
        {
            var empleados = new List<EmpleadoViewModel>();
            string procedure = "sp_ListarEmpleadosPorEquipo";

            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                using (SqlCommand cmd = new SqlCommand(procedure, conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@FK_IdEquipo", idEquipo);

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            empleados.Add(new EmpleadoViewModel
                            {
                                IdEmpleado = reader.GetInt32(reader.GetOrdinal("IdEmpleado")),
                                NombresEmpleado = reader.GetString(reader.GetOrdinal("NombresEmpleado")),
                                ApellidosEmpleado = reader.GetString(reader.GetOrdinal("ApellidosEmpleado")),
                                // Agrega aquí otras propiedades necesarias
                            });
                        }
                    }
                }
            }
            return empleados;
        }

        // Actualizar días de vacaciones acumulados para un empleado
        public async Task<bool> ActualizarEmpleadoAsync(EmpleadoViewModel empleado)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                var query = @"UPDATE Empleados 
                      SET DiasVacacionesAcumulados = @DiasVacacionesAcumulados
                      WHERE IdEmpleado = @IdEmpleado";

                using (var command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@DiasVacacionesAcumulados", empleado.DiasVacacionesAcumulados);
                    command.Parameters.AddWithValue("@IdEmpleado", empleado.IdEmpleado);

                    int result = await command.ExecuteNonQueryAsync();
                    return result > 0;
                }
            }
        }



    }
}