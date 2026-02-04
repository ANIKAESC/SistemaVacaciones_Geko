-- Script para verificar el campo de contraseña
USE DBProyectoGrupalDojoGeko;
GO

-- 1. Verificar la longitud del campo Contrasenia
SELECT 
    COLUMN_NAME,
    DATA_TYPE,
    CHARACTER_MAXIMUM_LENGTH,
    IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'Usuarios' 
AND COLUMN_NAME = 'Contrasenia';
GO

-- 2. Ver la contraseña del usuario específico
SELECT 
    IdUsuario,
    Username,
    LEN(Contrasenia) as LongitudHash,
    DATALENGTH(Contrasenia) as BytesHash,
    LEFT(Contrasenia, 10) as PrimerosCaracteres,
    FechaExpiracionContrasenia
FROM Usuarios
WHERE Username = 'aescoto@digitalgeko.com';
GO

-- 3. Verificar si hay espacios o caracteres raros
SELECT 
    IdUsuario,
    Username,
    CASE 
        WHEN Contrasenia LIKE '% ' THEN 'Tiene espacios al final'
        WHEN Contrasenia LIKE ' %' THEN 'Tiene espacios al inicio'
        WHEN LEN(Contrasenia) != DATALENGTH(Contrasenia) THEN 'Tiene caracteres especiales'
        ELSE 'OK'
    END as EstadoHash
FROM Usuarios
WHERE Username = 'aescoto@digitalgeko.com';
GO
