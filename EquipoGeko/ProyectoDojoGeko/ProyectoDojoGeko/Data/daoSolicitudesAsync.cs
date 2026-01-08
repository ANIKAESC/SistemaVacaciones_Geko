using ProyectoDojoGeko.Models;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using ProyectoDojoGeko.Dtos.Solicitudes;

namespace ProyectoDojoGeko.Data
{
    public class daoSolicitudesAsync
    {
        private readonly string _connectionString;

        public daoSolicitudesAsync(string connectionString)
        {
            _connectionString = connectionString;
        }

        // Método para mapear encabezados de solicitud
        private SolicitudEncabezadoViewModel _mapearSolicitud(SqlDataReader reader)
        {
            return new SolicitudEncabezadoViewModel
            {
                IdSolicitud = Convert.ToInt32(reader["IdSolicitud"]),
                IdEmpleado = Convert.ToInt32(reader["FK_IdEmpleado"]),
                NombreEmpleado = reader["NombresEmpleado"].ToString() ?? "",
                DiasSolicitadosTotal = Convert.ToDecimal(reader["DiasSolicitadosTotal"]),
                FechaIngresoSolicitud = Convert.ToDateTime(reader["FechaIngresoSolicitud"])
            };
        }

        // Método para mapear encabezados de solicitud para la vista de Autorizador
        private SolicitudEncabezadoViewModel _mapearSolicitudAutorizador(SqlDataReader reader)
        {
            return new SolicitudEncabezadoViewModel
            {
                IdSolicitud = Convert.ToInt32(reader["IdSolicitud"]),
                IdEmpleado = Convert.ToInt32(reader["FK_IdEmpleado"]),
                NombreEmpleado = reader["NombresEmpleado"].ToString() ?? "",
                DiasSolicitadosTotal = Convert.ToDecimal(reader["DiasSolicitadosTotal"]),
                FechaIngresoSolicitud = Convert.ToDateTime(reader["FechaIngresoSolicitud"]),
                Estado = Convert.ToInt32(reader["FK_IdEstadoSolicitud"])
            };
        }

        // Método para mapear encabezados de solicitud Result
        private SolicitudEncabezadoResult _mapearSolicitudResult(SqlDataReader reader)
        {
            return new SolicitudEncabezadoResult
            {
                IdSolicitud = Convert.ToInt32(reader["IdSolicitud"]),
                IdEmpleado = Convert.ToInt32(reader["FK_IdEmpleado"]),
                NombreEmpleado = reader["NombresEmpleado"].ToString() ?? "",
                DiasSolicitadosTotal = Convert.ToDecimal(reader["DiasSolicitadosTotal"]),
                FechaIngresoSolicitud = Convert.ToDateTime(reader["FechaIngresoSolicitud"]),
                FechaInicio = Convert.ToDateTime(reader["FechaInicio"]),
                FechaFin = Convert.ToDateTime(reader["FechaFin"]),
                NombreEmpresa = reader["NombreEmpresa"].ToString() ?? "",
                Estado = Convert.ToInt32(reader["FK_IdEstadoSolicitud"])
            };
        }

        // Método para insertar una nueva solicitud de vacaciones
        public async Task<int> InsertarSolicitudAsync(SolicitudViewModel solicitud)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                // 1. Insertar encabezado y obtener el ID generado
                int idSolicitud;
                using (var cmdEnc = new SqlCommand("sp_InsertarSolicitudEncabezado", connection))
                {
                    cmdEnc.CommandType = CommandType.StoredProcedure;
                    cmdEnc.Parameters.AddWithValue("@IdEmpleado", solicitud.Encabezado.IdEmpleado);
                    cmdEnc.Parameters.AddWithValue("@NombresEmpleado", solicitud.Encabezado.NombreEmpleado);
                    cmdEnc.Parameters.AddWithValue("@DiasSolicitadosTotal", solicitud.Encabezado.DiasSolicitadosTotal);
                    cmdEnc.Parameters.AddWithValue("@FechaIngresoSolicitud", solicitud.Encabezado.FechaIngresoSolicitud);
                    cmdEnc.Parameters.AddWithValue("@SolicitudLider", solicitud.Encabezado.SolicitudLider);
                    cmdEnc.Parameters.AddWithValue("@Observaciones", 
                        string.IsNullOrWhiteSpace(solicitud.Encabezado.Observaciones) ? 
                        (object)DBNull.Value : solicitud.Encabezado.Observaciones);
                    cmdEnc.Parameters.AddWithValue("@Estado", solicitud.Encabezado.Estado);
                    
                    // Agregar parámetros del PDF firmado (actualmente no se usa, siempre NULL)
                    var docFirmadoParam = new SqlParameter("@DocumentoFirmado", SqlDbType.VarBinary, -1);
                    docFirmadoParam.Value = DBNull.Value; // Siempre NULL porque no se usa el upload manual
                    cmdEnc.Parameters.Add(docFirmadoParam);
                    
                    var docContentTypeParam = new SqlParameter("@DocumentoContentType", SqlDbType.NVarChar, 100);
                    docContentTypeParam.Value = DBNull.Value; // Siempre NULL porque no se usa el upload manual
                    cmdEnc.Parameters.Add(docContentTypeParam);
                    
                    // Agregar tipo de formato PDF (1 = GDG, 2 = Digital Geko Corp)
                    cmdEnc.Parameters.AddWithValue("@TipoFormatoPdf", solicitud.Encabezado.TipoFormatoPdf);
                    
                    // IdAutorizador ya viene como IdUsuario desde el controlador
                    object idUsuarioAutorizador = DBNull.Value;
                    if (solicitud.Encabezado.IdAutorizador.HasValue && solicitud.Encabezado.IdAutorizador.Value > 0)
                    {
                        idUsuarioAutorizador = solicitud.Encabezado.IdAutorizador.Value;
                    }
                    
                    cmdEnc.Parameters.AddWithValue("@IdAutorizador", idUsuarioAutorizador);

                    // SP retorna el ID con SELECT SCOPE_IDENTITY()
                    idSolicitud = Convert.ToInt32(await cmdEnc.ExecuteScalarAsync());
                }

                // 2. Insertar detalles usando el ID del encabezado
                foreach (var detalle in solicitud.Detalles)
                {
                    using (var cmdDet = new SqlCommand("sp_InsertarSolicitudDetalle", connection))
                    {
                        cmdDet.CommandType = CommandType.StoredProcedure;
                        cmdDet.Parameters.AddWithValue("@IdSolicitud", idSolicitud);
                        cmdDet.Parameters.AddWithValue("@FechaInicio", detalle.FechaInicio);
                        cmdDet.Parameters.AddWithValue("@FechaFin", detalle.FechaFin);
                        cmdDet.Parameters.AddWithValue("@DiasHabiles", detalle.DiasHabilesTomados);

                        await cmdDet.ExecuteNonQueryAsync();
                    }
                }

                return idSolicitud;
            }
        }

        // Método para autorizar una solicitud 
        public async Task<bool> AutorizarSolicitud(int idSolicitud)
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);
                var query = "sp_AutorizarSolicitud";

                using var procedure = new SqlCommand(query, connection)
                {
                    CommandType = CommandType.StoredProcedure
                };

                procedure.Parameters.AddWithValue("@IdSolicitud", idSolicitud);

                await connection.OpenAsync();

                procedure.ExecuteNonQuery();

                return true;
                
            }
            catch (Exception ex)
            {
                Console.Write(ex.ToString());
                return false;
            }
        }

        // Método para cancelar una solicitud
        public async Task<bool> CancelarSolicitudAsync(int idSolicitud)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                var query = @"UPDATE Solicitudes 
                      SET Estado = 4 -- Cancelada
                      WHERE IdSolicitud = @IdSolicitud";

                using (var command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@IdSolicitud", idSolicitud);

                    int result = await command.ExecuteNonQueryAsync();
                    return result > 0;
                }
            }
        }

        #region Métodos GET de encabezado de solicitud

        public async Task<List<SolicitudViewModel>> ObtenerTodasLasSolicitudesAsync()
        {
            var solicitudes = new List<SolicitudViewModel>();

            try {

                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    // 1. Obtener todos los encabezados
                    using (var command = new SqlCommand("sp_ListarSolicitudEncabezado", connection))
                    {
                        command.CommandType = CommandType.StoredProcedure;

                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                var solicitud = new SolicitudViewModel
                                {
                                    Encabezado = new SolicitudEncabezadoViewModel
                                    {
                                        IdSolicitud = Convert.ToInt32(reader["IdSolicitud"]),
                                        IdEmpleado = reader["IdEmpleado"] != DBNull.Value ? Convert.ToInt32(reader["IdEmpleado"]) : 0,
                                        NombreEmpleado = null,
                                        DiasSolicitadosTotal = (decimal)reader["DiasSolicitadosTotal"],
                                        FechaIngresoSolicitud = (DateTime)reader["FechaIngresoSolicitud"],
                                        Estado = Convert.ToInt32(reader["Estado"])
                                    },
                                    Detalles = new List<SolicitudDetalleViewModel>()
                                };
                                solicitudes.Add(solicitud);
                            }
                        }
                    }

                    // 2. Obtener detalles para cada solicitud
                    foreach (var solicitud in solicitudes)
                    {
                        using (var cmdDetalle = new SqlCommand("sp_ObtenerDetallesPorSolicitud", connection))
                        {
                            cmdDetalle.CommandType = CommandType.StoredProcedure;
                            cmdDetalle.Parameters.AddWithValue("@IdSolicitud", solicitud.Encabezado.IdSolicitud);

                            using (var readerDetalle = await cmdDetalle.ExecuteReaderAsync())
                            {
                                while (await readerDetalle.ReadAsync())
                                {
                                    solicitud.Detalles.Add(new SolicitudDetalleViewModel
                                    {
                                        IdSolicitudDetalle = (int)readerDetalle["IdSolicitudDetalle"],
                                        FechaInicio = (DateTime)readerDetalle["FechaInicio"],
                                        FechaFin = (DateTime)readerDetalle["FechaFin"],
                                        DiasHabilesTomados = (decimal)readerDetalle["DiasHabilesTomados"]
                                    });
                                }
                            }
                        }


                    }
                }

            }
            catch (Exception)
            {
                throw;
            }


            return solicitudes;

        }

        // Obtenemos a todos los compañeros (empleados) que tiene un empleado por medio de su propio IdEmpleado
        // para buscar a los autorizadores
        /*public async Task<List<AutorizadorViewModel>> ObtenerAutorizadoresPorEmpleadoAsync(int idEmpleado)
        {
            var autorizadores = new List<AutorizadorViewModel>();
            try
            {
                using var connection = new SqlConnection(_connectionString);
                using var procedure = new SqlCommand("sp_ObtenerCompanerosDeEquipo", connection)
                {
                    CommandType = CommandType.StoredProcedure
                };
                procedure.Parameters.AddWithValue("@IdEmpleado", idEmpleado);

                await connection.OpenAsync();
                using SqlDataReader reader = await procedure.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    string roles = reader["Roles"]?.ToString() ?? "Sin rol";

                    // Solo incluir si es TeamLider o SubLider
                    if (roles.Contains("TeamLider") || roles.Contains("SubLider"))
                    {
                        autorizadores.Add(new AutorizadorViewModel
                        {
                            IdEmpleado = Convert.ToInt32(reader["IdEmpleado"]),
                            Nombres = reader["NombresEmpleado"].ToString() ?? "",
                            Apellidos = reader["ApellidosEmpleado"].ToString() ?? "",
                            Correo = reader["CorreoInstitucional"].ToString() ?? "",
                            Puesto = reader["Puesto"].ToString() ?? "",
                            Roles = roles,
                            EsUsuarioActual = Convert.ToBoolean(reader["EsUsuarioActual"])
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Error al obtener los autorizadores. Por favor, intente nuevamente más tarde.", ex);
            }
            return autorizadores;
        }*/


        //JuniorDev | Método para obtener encabezado de solicitud por autorizador (IdAutorizador)
        public async Task<List<SolicitudEncabezadoViewModel>> ObtenerSolicitudEncabezadoAutorizadorAsync(int? IdAutorizador = null)
        {
            var solicitudes = new List<SolicitudEncabezadoViewModel>();
            try
            {
                using var connection = new SqlConnection(_connectionString);

                string query;
                if(IdAutorizador == null)
                {
                    query = "sp_ListarSolicitudEncabezado_Autorizador_Admin";
                }
                else
                {
                    query = "sp_ListarSolicitudEncabezado_Autorizador";
                }

                using var procedure = new SqlCommand(query, connection)
                {
                    CommandType = CommandType.StoredProcedure
                };
                
                if (IdAutorizador != null) procedure.Parameters.AddWithValue("@FK_IdAutorizador", IdAutorizador);
                await connection.OpenAsync();
                using SqlDataReader reader = await procedure.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    solicitudes.Add(_mapearSolicitudAutorizador(reader)); // Se añade la solicitudeEncabezado mapeada
                }

            }
            catch (Exception ex)
            {
                throw new Exception("Error al obtener los encabezados de las solicitudes por IdAutorizador faltantes de autorizar", ex);
            }

            return solicitudes;
        }
        public async Task<List<SolicitudEncabezadoViewModel>> ObtenerSolicitudEncabezadoAsync()
        {
            var solicitudes = new List<SolicitudEncabezadoViewModel>();
            try
            {
                using var connection = new SqlConnection(_connectionString);
                using var procedure = new SqlCommand("sp_ListarSolicitudEncabezado", connection)
                {
                    CommandType = CommandType.StoredProcedure
                };
                await connection.OpenAsync();
                using SqlDataReader reader = await procedure.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    solicitudes.Add(_mapearSolicitud(reader)); // Se añade la solicitudeEncabezado mapeada
                }

            }
            catch (Exception ex)
            {
                throw new Exception("Error al obtener los encabezados de las solicitudes", ex);
            }

            return solicitudes;
        }

        public async Task<List<SolicitudEncabezadoResult>> ObtenerSolicitudEncabezadoCamposAsync()
        {
            var solicitudes = new List<SolicitudEncabezadoResult>();
            try
            {
                using var connection = new SqlConnection(_connectionString);
                using var procedure = new SqlCommand("sp_ListarSolicitudEncabezado_Campos", connection)
                {
                    CommandType = CommandType.StoredProcedure
                };
                await connection.OpenAsync();
                using SqlDataReader reader = await procedure.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    solicitudes.Add(_mapearSolicitudResult(reader)); // Se añade la solicitudeEncabezado mapeada
                }

            }
            catch (Exception ex)
            {
                throw new Exception("Error al obtener los encabezados de las solicitudes", ex);
            }

            return solicitudes;
        }
             

    #endregion


        /*ErickDev: Método para obtener detalle de solicitud*/
        /*--------*/

        public async Task<SolicitudViewModel> ObtenerDetalleSolicitudAsync(int idSolicitud)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                using (var command = new SqlCommand("sp_ObtenerDetalleSolicitud", connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.AddWithValue("@IdSolicitud", idSolicitud);

                    SolicitudViewModel solicitud = null;

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            // Verificar si las columnas existen
                            int? idAutorizador = null;
                            string solicitudLider = null;
                            string motivoRechazo = null;
                            
                            try
                            {
                                var ordinalAutorizador = reader.GetOrdinal("FK_IdAutorizador");
                                if (!await reader.IsDBNullAsync(ordinalAutorizador))
                                {
                                    idAutorizador = reader.GetInt32(ordinalAutorizador);
                                }
                            }
                            catch (IndexOutOfRangeException)
                            {
                                // Columna no existe, usar null
                            }
                            
                            try
                            {
                                var ordinalSolicitudLider = reader.GetOrdinal("SolicitudLider");
                                if (!await reader.IsDBNullAsync(ordinalSolicitudLider))
                                {
                                    solicitudLider = reader.GetString(ordinalSolicitudLider);
                                }
                            }
                            catch (IndexOutOfRangeException)
                            {
                                // Columna no existe, usar null
                            }
                            
                            try
                            {
                                var ordinalMotivoRechazo = reader.GetOrdinal("MotivoRechazo");
                                if (!await reader.IsDBNullAsync(ordinalMotivoRechazo))
                                {
                                    motivoRechazo = reader.GetString(ordinalMotivoRechazo);
                                }
                            }
                            catch (IndexOutOfRangeException)
                            {
                                // Columna no existe, usar null
                            }

                            solicitud = new SolicitudViewModel
                            {
                                Encabezado = new SolicitudEncabezadoViewModel
                                {
                                    IdSolicitud = Convert.ToInt32(reader["IdSolicitud"]),
                                    IdEmpleado = Convert.ToInt32(reader["IdEmpleado"]),
                                    NombreEmpleado = null, // Se asignará en el controlador si es necesario
                                    DiasSolicitadosTotal = (decimal)reader["DiasSolicitadosTotal"],
                                    FechaIngresoSolicitud = (DateTime)reader["FechaIngresoSolicitud"],
                                    Observaciones = reader["Observaciones"] != DBNull.Value ? reader["Observaciones"].ToString() : null,
                                    DocumentoFirmadoData = !await reader.IsDBNullAsync(reader.GetOrdinal("DocumentoFirmado"))
                                        ? await reader.GetFieldValueAsync<byte[]>(reader.GetOrdinal("DocumentoFirmado"))
                                        : null,
                                    DocumentoContentType = reader["DocumentoContentType"] != DBNull.Value ? reader["DocumentoContentType"].ToString() : null,
                                    Estado = Convert.ToInt32(reader["Estado"]),
                                    IdAutorizador = idAutorizador,
                                    SolicitudLider = solicitudLider,
                                    MotivoRechazo = motivoRechazo,
                                },
                                Detalles = new List<SolicitudDetalleViewModel>()
                            };
                        }

                        if (solicitud == null) return null;

                        await reader.NextResultAsync();
                        while (await reader.ReadAsync())
                        {
                            solicitud.Detalles.Add(new SolicitudDetalleViewModel
                            {
                                IdSolicitudDetalle = (int)reader["IdSolicitudDetalle"],
                                FechaInicio = (DateTime)reader["FechaInicio"],
                                FechaFin = (DateTime)reader["FechaFin"],
                                DiasHabilesTomados = (decimal)reader["DiasHabilesTomados"]
                            });
                        }
                    }
                    return solicitud;
                }
            }
        }
        /*-------------*/
        /*End ErickDev*/

        // Método para obtener encabezado de solicitud por empleado (IdEmpleado)
        public async Task<List<SolicitudViewModel>> ObtenerSolicitudesPorEmpleadoAsync(int idEmpleado)
        {
            var solicitudes = new List<SolicitudViewModel>();

            // 1. Obtener encabezados
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                using (var command = new SqlCommand("sp_ObtenerSolicitudesPorEmpleado", connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.AddWithValue("@IdEmpleado", idEmpleado);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            var solicitud = new SolicitudViewModel
                            {
                                Encabezado = new SolicitudEncabezadoViewModel
                                {
                                    IdSolicitud = Convert.ToInt32(reader["IdSolicitud"]),
                                    IdEmpleado = Convert.ToInt32(reader["IdEmpleado"]),
                                    NombreEmpleado = null,
                                    DiasSolicitadosTotal = (decimal)reader["DiasSolicitadosTotal"],
                                    FechaIngresoSolicitud = (DateTime)reader["FechaIngresoSolicitud"],
                                    Estado = Convert.ToInt32(reader["Estado"])
                                },
                                Detalles = new List<SolicitudDetalleViewModel>()
                            };
                            solicitudes.Add(solicitud);
                        }
                    }
                }

                // 2. Para cada solicitud, obtener los detalles
                foreach (var solicitud in solicitudes)
                {
                    using (var cmdDetalle = new SqlCommand("sp_ObtenerDetallesPorSolicitud", connection))
                    {
                        cmdDetalle.CommandType = CommandType.StoredProcedure;
                        cmdDetalle.Parameters.AddWithValue("@IdSolicitud", solicitud.Encabezado.IdSolicitud);

                        using (var readerDetalle = await cmdDetalle.ExecuteReaderAsync())
                        {
                            while (await readerDetalle.ReadAsync())
                            {
                                solicitud.Detalles.Add(new SolicitudDetalleViewModel
                                {
                                    IdSolicitudDetalle = (int)readerDetalle["IdSolicitudDetalle"],
                                    FechaInicio = (DateTime)readerDetalle["FechaInicio"],
                                    FechaFin = (DateTime)readerDetalle["FechaFin"],
                                    DiasHabilesTomados = (decimal)readerDetalle["DiasHabilesTomados"]
                                });
                            }
                        }
                    }
                }
            }

            return solicitudes;
        }



        // Método para obtener solicitudes por equipo
        public async Task<List<SolicitudEncabezadoViewModel>> ObtenerSolicitudesPorEquipoAsync(int idEquipo)
        {
            var solicitudes = new List<SolicitudEncabezadoViewModel>();
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                using (var command = new SqlCommand("sp_ListarSolicitudesPorEquipo", connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.AddWithValue("@IdEquipo", idEquipo);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            solicitudes.Add(_mapearSolicitud(reader));
                        }
                    }
                }
            }
            return solicitudes;
        }

        /*public async Task<List<SolicitudEncabezadoViewModel>> ObtenerSolicitudEncabezadoAsync()
        {
            var solicitudes = new List<SolicitudEncabezadoViewModel>();
            try
            {
                using var connection = new SqlConnection(_connectionString);
                using var procedure = new SqlCommand("sp_ListarSolicitudEncabezado", connection)
                {
                    CommandType = CommandType.StoredProcedure
                };
                await connection.OpenAsync();
                using SqlDataReader reader = await procedure.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    solicitudes.Add(_mapearSolicitud(reader)); // Se añade la solicitudeEncabezado mapeada
                }

            }
            catch (Exception ex)
            {
                throw new Exception("Error al obtener los encabezados de las solicitudes", ex);
            }

            return solicitudes;
       }*/

        // Método simplificado para actualizar solo el estado
        public async Task<bool> ActualizarEstadoSolicitudAsync(int idSolicitud, int nuevoEstado)
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();
                
                var query = @"UPDATE SolicitudEncabezado 
                              SET FK_IdEstadoSolicitud = @NuevoEstado 
                              WHERE IdSolicitud = @IdSolicitud";
                
                using var command = new SqlCommand(query, connection);
                command.Parameters.AddWithValue("@IdSolicitud", idSolicitud);
                command.Parameters.AddWithValue("@NuevoEstado", nuevoEstado);
                
                var rowsAffected = await command.ExecuteNonQueryAsync();
                Console.WriteLine($"✅ Estado actualizado. Solicitud: {idSolicitud}, Nuevo Estado: {nuevoEstado}, Filas afectadas: {rowsAffected}");
                return rowsAffected > 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error al actualizar estado: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> ActualizarEstadoSolicitud(int idSolicitud, int nuevoEstado, int idAutorizador, string motivoRechazo = null)
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();
                
                var query = @"UPDATE SolicitudEncabezado 
                              SET FK_IdEstadoSolicitud = @NuevoEstado,
                                  FK_IdAutorizador = @IdAutorizador,
                                  MotivoRechazo = @MotivoRechazo
                              WHERE IdSolicitud = @IdSolicitud";
                
                using var command = new SqlCommand(query, connection);
                command.Parameters.AddWithValue("@IdSolicitud", idSolicitud);
                command.Parameters.AddWithValue("@NuevoEstado", nuevoEstado);
                command.Parameters.AddWithValue("@IdAutorizador", idAutorizador);
                command.Parameters.AddWithValue("@MotivoRechazo", motivoRechazo ?? (object)DBNull.Value);

                var rowsAffected = await command.ExecuteNonQueryAsync();
                Console.WriteLine($"✅ Estado actualizado con autorizador. Solicitud: {idSolicitud}, Estado: {nuevoEstado}, Autorizador: {idAutorizador}, Filas: {rowsAffected}");
                return rowsAffected > 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error al actualizar estado con autorizador: {ex.Message}");
                Console.WriteLine(ex.ToString());
                return false;
            }
        }

        public async Task<bool> CancelarSolicitud(int idSolicitud)
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);
                var query = "sp_CancelarSolicitud";
                using var procedure = new SqlCommand(query, connection)
                {
                    CommandType = CommandType.StoredProcedure
                };
                procedure.Parameters.AddWithValue("@IdSolicitud", idSolicitud);
                await connection.OpenAsync();
                await procedure.ExecuteNonQueryAsync();
                return true;
            }
            catch (Exception ex)
            {
                Console.Write(ex.ToString());
                return false;
            }
        }

        /// <summary>
        /// Actualiza una solicitud existente (encabezado y detalles)
        /// </summary>
        public async Task<bool> ActualizarSolicitudAsync(SolicitudViewModel solicitud)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();
            using var transaction = connection.BeginTransaction();

            try
            {
                // 1. Actualizar el encabezado
                using var cmdEnc = new SqlCommand(@"
                    UPDATE SolicitudEncabezado
                    SET 
                        DiasSolicitadosTotal = @DiasSolicitadosTotal,
                        Observaciones = @Observaciones,
                        FK_IdAutorizador = @IdAutorizador,
                        TipoFormatoPdf = @TipoFormatoPdf
                    WHERE IdSolicitud = @IdSolicitud", connection, transaction);

                cmdEnc.Parameters.AddWithValue("@IdSolicitud", solicitud.Encabezado.IdSolicitud);
                cmdEnc.Parameters.AddWithValue("@DiasSolicitadosTotal", solicitud.Encabezado.DiasSolicitadosTotal);
                cmdEnc.Parameters.AddWithValue("@Observaciones", solicitud.Encabezado.Observaciones ?? (object)DBNull.Value);
                cmdEnc.Parameters.AddWithValue("@TipoFormatoPdf", solicitud.Encabezado.TipoFormatoPdf);

                // Convertir IdEmpleado a IdUsuario para el autorizador (igual que en Insertar)
                object idUsuarioAutorizador = DBNull.Value;
                if (solicitud.Encabezado.IdAutorizador.HasValue && solicitud.Encabezado.IdAutorizador.Value > 0)
                {
                    using (var cmdUsuario = new SqlCommand(
                        "SELECT IdUsuario FROM Usuarios WHERE FK_IdEmpleado = @IdEmpleado", connection, transaction))
                    {
                        cmdUsuario.Parameters.AddWithValue("@IdEmpleado", solicitud.Encabezado.IdAutorizador.Value);
                        var resultado = await cmdUsuario.ExecuteScalarAsync();
                        if (resultado != null)
                        {
                            idUsuarioAutorizador = resultado;
                        }
                    }
                }
                cmdEnc.Parameters.AddWithValue("@IdAutorizador", idUsuarioAutorizador);

                await cmdEnc.ExecuteNonQueryAsync();

                // 2. Eliminar los detalles antiguos
                using var cmdDel = new SqlCommand(@"
                    DELETE FROM SolicitudDetalle 
                    WHERE FK_IdSolicitud = @IdSolicitud", connection, transaction);
                cmdDel.Parameters.AddWithValue("@IdSolicitud", solicitud.Encabezado.IdSolicitud);
                await cmdDel.ExecuteNonQueryAsync();

                // 3. Insertar los nuevos detalles
                foreach (var detalle in solicitud.Detalles)
                {
                    using var cmdDet = new SqlCommand(@"
                        INSERT INTO SolicitudDetalle 
                        (FK_IdSolicitud, FechaInicio, FechaFin, DiasHabilesTomados)
                        VALUES (@IdSolicitud, @FechaInicio, @FechaFin, @DiasHabiles)", connection, transaction);

                    cmdDet.Parameters.AddWithValue("@IdSolicitud", solicitud.Encabezado.IdSolicitud);
                    cmdDet.Parameters.AddWithValue("@FechaInicio", detalle.FechaInicio);
                    cmdDet.Parameters.AddWithValue("@FechaFin", detalle.FechaFin);
                    cmdDet.Parameters.AddWithValue("@DiasHabiles", detalle.DiasHabilesTomados);

                    await cmdDet.ExecuteNonQueryAsync();
                }

                await transaction.CommitAsync();
                return true;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                Console.WriteLine($"Error al actualizar solicitud: {ex.Message}");
                throw;
            }
        }
    }
}