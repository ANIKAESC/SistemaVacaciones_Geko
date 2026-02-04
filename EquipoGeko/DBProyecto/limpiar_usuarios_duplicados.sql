-- Script para limpiar usuarios duplicados
USE DBProyectoGrupalDojoGeko;
GO

-- 1. Ver todos los usuarios duplicados
SELECT 
    Username,
    COUNT(*) as CantidadDuplicados
FROM Usuarios
GROUP BY Username
HAVING COUNT(*) > 1;
GO

-- 2. Ver detalles de los usuarios duplicados de aescoto@digitalgeko.com
SELECT 
    IdUsuario,
    Username,
    FK_IdEmpleado,
    FK_IdEstado,
    LEFT(Contrasenia, 15) as HashInicio,
    FechaExpiracionContrasenia,
    FechaCreacion
FROM Usuarios
WHERE Username = 'aescoto@digitalgeko.com'
ORDER BY IdUsuario;
GO

-- 3. OPCIÓN A: Eliminar los usuarios duplicados (1016 y 1017) y mantener solo el 1015
-- IMPORTANTE: Ejecuta esto solo si estás seguro de que 1015 es el usuario correcto
/*
BEGIN TRANSACTION;

-- Eliminar tokens de los usuarios duplicados
DELETE FROM TokenUsuario WHERE FK_IdUsuario IN (1016, 1017);

-- Eliminar relaciones de roles de los usuarios duplicados
DELETE FROM UsuariosRol WHERE FK_IdUsuario IN (1016, 1017);

-- Eliminar los usuarios duplicados
DELETE FROM Usuarios WHERE IdUsuario IN (1016, 1017);

-- Si todo está bien, confirma la transacción
COMMIT;
-- Si algo salió mal, ejecuta: ROLLBACK;
*/
GO

-- 4. OPCIÓN B: Desactivar los usuarios duplicados en lugar de eliminarlos
/*
UPDATE Usuarios
SET FK_IdEstado = 2  -- Asumiendo que 2 es "Inactivo"
WHERE IdUsuario IN (1016, 1017);
GO
*/

-- 5. Verificar que solo quede un usuario activo
SELECT 
    IdUsuario,
    Username,
    FK_IdEstado,
    FechaExpiracionContrasenia
FROM Usuarios
WHERE Username = 'aescoto@digitalgeko.com'
AND FK_IdEstado = 1;  -- Solo activos
GO
