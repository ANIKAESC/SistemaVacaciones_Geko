using Microsoft.Data.SqlClient;
using ProyectoDojoGeko.Models.Usuario;

namespace ProyectoDojoGeko.Data
{
    public class daoTokenUsuario
    {
        // Cadena de conexión a la base de datos
        private readonly string _connectionString;

        // Constructor para inicializar la cadena de conexión
        public daoTokenUsuario(string connectionString)
        {
            _connectionString = connectionString;
        }

        // Método(función) para guardar la nueva contraseña en la base de datos
        public void GuardarContrasenia(int idUsuario, string nuevaContrasenia)
        {
            // Creamos una conexión a la base de datos usando la cadena de conexión proporcionada
            using (var connection = new SqlConnection(_connectionString))
            {
                // Abrimos la conexión a la base de datos
                connection.Open();
                // Usamos el stored procedure para actualizar la contraseña y limpiar la fecha de expiración
                using (var command = new SqlCommand("sp_ActualizarContrasenia", connection))
                {
                    command.CommandType = System.Data.CommandType.StoredProcedure;
                    // Asignamos los parámetros al comando
                    command.Parameters.AddWithValue("@IdUsuario", idUsuario);
                    command.Parameters.AddWithValue("@NuevaContrasenia", nuevaContrasenia);
                    // Ejecutamos el comando para actualizar la contraseña en la base de datos
                    command.ExecuteNonQuery();
                }
            }
        }

        // Método para validar un usuario con su nombre de usuario y contraseña
        public void GuardarToken(TokenUsuarioViewModel tokenUsuario)
        {
            // Consulta SQL para insertar un nuevo token de usuario
            string queryToken = "INSERT INTO TokenUsuario (FK_IdUsuario, Token, FechaCreacion, TiempoExpira) VALUES (@FK_IdUsuario, @Token, @FechaCreacion, @TiempoExpira)";

            // Creamos una conexión a la base de datos usando la cadena de conexión proporcionada
            using (var connection = new SqlConnection(_connectionString))
            {
                // Abrimos la conexión a la base de datos
                connection.Open();

                // Creamos un comando SQL para ejecutar la consulta de inserción
                using (var command = new SqlCommand(queryToken, connection))
                {
                    // Asignamos la conexión al comando
                    command.Connection = connection;
                    // Asignamos los parámetros al comando
                    command.Parameters.AddWithValue("@FK_IdUsuario", tokenUsuario.FK_IdUsuario);
                    command.Parameters.AddWithValue("@Token", tokenUsuario.Token);
                    command.Parameters.AddWithValue("@FechaCreacion", tokenUsuario.FechaCreacion);
                    command.Parameters.AddWithValue("@TiempoExpira", tokenUsuario.TiempoExpira);
                    // Ejecutamos el comando para insertar el token en la base de datos
                    command.ExecuteNonQuery();
                }
            }
        }

        // Validar un usuario por nombre de usuario y contraseña
        public UsuarioViewModel ValidarUsuario(string usuario, string claveIngresada)
        {
            Console.WriteLine($"=== DEBUG LOGIN ===");
            Console.WriteLine($"Usuario recibido: '{usuario}'");
            Console.WriteLine($"Clave recibida: '{claveIngresada}'");

            UsuarioViewModel user = null;

            using (var conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                var cmd = new SqlCommand(@"
                    SELECT IdUsuario, Username, contrasenia, FK_IdEstado, FK_IdEmpleado
                    FROM Usuarios
                    WHERE Username = @usuario AND FK_IdEstado = 1", conn);

                cmd.Parameters.AddWithValue("@usuario", usuario);

                using (var reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        Console.WriteLine("Usuario encontrado en BD");
                        string hashGuardado = reader["contrasenia"].ToString()?.Trim();
                        string clavePlana = claveIngresada?.Trim();
                        Console.WriteLine($"Hash en BD: {hashGuardado}");
                        Console.WriteLine($"Hash length: {(hashGuardado != null ? hashGuardado.Length : 0)}");
                        Console.WriteLine($"Hash startsWith $2: {(hashGuardado != null && hashGuardado.StartsWith("$2") ? "SI" : "NO")}");
                        Console.WriteLine($"Clave length: {(clavePlana != null ? clavePlana.Length : 0)}");

                        // En caso de ser el usuario AdminDev, este no tiene un hash, por lo tanto debemos saltar la validación para este usuario
                        if (usuario == "AdminDev")
                        {
                            Console.WriteLine("Validación exitosa - creando usuario");
                            user = new UsuarioViewModel
                            {
                                IdUsuario = reader.GetInt32(reader.GetOrdinal("IdUsuario")),
                                Username = reader.GetString(reader.GetOrdinal("Username")),
                                FK_IdEstado = reader.GetInt32(reader.GetOrdinal("FK_IdEstado")),
                                FK_IdEmpleado = reader.GetInt32(reader.GetOrdinal("FK_IdEmpleado"))
                            };
                            return user;
                        }

                        bool esValido = BCrypt.Net.BCrypt.Verify(clavePlana, hashGuardado);
                        Console.WriteLine($"BCrypt.Verify resultado: {esValido}");

                        if (esValido)
                        {
                            Console.WriteLine("Validación exitosa - creando usuario");
                            user = new UsuarioViewModel
                            {
                                IdUsuario = reader.GetInt32(reader.GetOrdinal("IdUsuario")),
                                Username = reader.GetString(reader.GetOrdinal("Username")),
                                FK_IdEstado = reader.GetInt32(reader.GetOrdinal("FK_IdEstado")),
                                FK_IdEmpleado = reader.GetInt32(reader.GetOrdinal("FK_IdEmpleado"))

                            };
                        }
                        else
                        {
                            Console.WriteLine("BCrypt.Verify falló");
                        }
                    }
                    else
                    {
                        Console.WriteLine("Usuario NO encontrado en BD");
                    }
                }
            }

            Console.WriteLine($"Retornando usuario: {(user != null ? "VÁLIDO" : "NULL")}");
            return user;
        }

        // Método para validar el usuario nuevo que va a cambiar su contraseña
        public UsuarioViewModel ValidarUsuarioCambioContrasenia(string usuario, string claveIngresada)
        {
            Console.WriteLine($"=== DEBUG VALIDAR USUARIO CAMBIO CONTRASEÑA ===");
            Console.WriteLine($"Usuario recibido: '{usuario}'");
            Console.WriteLine($"Clave recibida (antes Trim): '{claveIngresada}'");

            UsuarioViewModel user = null;
            using (var conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                var cmd = new SqlCommand(@"

                    SELECT TOP 1 IdUsuario, Username, contrasenia, FK_IdEstado, FK_IdEmpleado, FechaExpiracionContrasenia
                    FROM Usuarios
                    WHERE Username = @usuario", conn);
                cmd.Parameters.AddWithValue("@usuario", usuario);


                using (var reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {

                        Console.WriteLine("Usuario encontrado en BD");
                        string hashGuardado = reader["contrasenia"].ToString()?.Trim();
                        string clavePLana = claveIngresada?.Trim();

                        try
                        {
                            bool esValido = BCrypt.Net.BCrypt.Verify(clavePLana, hashGuardado);
                            Console.WriteLine($"BCrypt.Verify resultado: {esValido}");
                            if (!esValido)
                            {
                                Console.WriteLine("BCrypt.Verify falló: contraseña incorrecta");
                                return null;
                            }

                            int FK_IdEstado = reader.GetInt32(reader.GetOrdinal("FK_IdEstado"));

                            // Validar expiración de la contraseña (hora local)
                            object objFechaExp = reader["FechaExpiracionContrasenia"];
                            DateTime? fechaExp = objFechaExp != DBNull.Value ? (DateTime?)Convert.ToDateTime(objFechaExp) : null;
                            if (fechaExp.HasValue && DateTime.Now > fechaExp.Value)
                            {
                                Console.WriteLine($"Contraseña expirada: FechaExpiracion={fechaExp.Value}, Ahora={DateTime.Now}");
                                return null;
                            }

                            Console.WriteLine("Validación exitosa - creando usuario");
                            user = new UsuarioViewModel
                            {
                                IdUsuario = reader.GetInt32(reader.GetOrdinal("IdUsuario")),
                                Username = reader.GetString(reader.GetOrdinal("Username")),
                                FK_IdEstado = FK_IdEstado,
                                FK_IdEmpleado = reader.GetInt32(reader.GetOrdinal("FK_IdEmpleado"))
                            };
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error en BCrypt.Verify: {ex.GetType().Name} - {ex.Message}");
                        }

                    }
                    else
                    {
                        Console.WriteLine("Usuario NO encontrado en BD");
                    }
                }
            }
            Console.WriteLine($"Retornando usuario: {(user != null ? "VÁLIDO" : "NULL")}");
            return user;
        }


        // Método para validar un token de usuario
        public bool ValidarToken(string token)
        {
            using (var conn = new SqlConnection(_connectionString))
            {
                conn.Open();

                var cmd = new SqlCommand("sp_ValidarToken", conn);
                cmd.CommandType = System.Data.CommandType.StoredProcedure;
                cmd.Parameters.AddWithValue("@Token", token);

                int result = Convert.ToInt32(cmd.ExecuteScalar());
                return result > 0;
            }
        }

        // Método para revocar un token de usuario
        public void RevocarToken(int Id)
        {
            using (var conn = new SqlConnection(_connectionString))
            {
                conn.Open();

                var cmd = new SqlCommand("sp_RevocarToken", conn);
                cmd.CommandType = System.Data.CommandType.StoredProcedure;
                cmd.Parameters.AddWithValue("@FK_IdUsuario", Id);

                cmd.ExecuteNonQuery();
            }
        }
    }
}
