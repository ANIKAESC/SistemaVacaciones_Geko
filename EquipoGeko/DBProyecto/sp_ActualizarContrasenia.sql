-- =============================================
-- Stored Procedure: sp_ActualizarContrasenia
-- Descripción: Actualiza la contraseña de un usuario y limpia la fecha de expiración
-- Autor: Sistema
-- Fecha: 2026-02-03
-- =============================================

USE DBProyectoGrupalDojoGeko;
GO

-- Eliminar el SP si ya existe
IF OBJECT_ID('sp_ActualizarContrasenia', 'P') IS NOT NULL
    DROP PROCEDURE sp_ActualizarContrasenia;
GO

CREATE PROCEDURE sp_ActualizarContrasenia
    @IdUsuario INT,
    @NuevaContrasenia NVARCHAR(MAX)
AS
BEGIN
    SET NOCOUNT ON;
    
    BEGIN TRY
        -- Actualizar la contraseña y limpiar la fecha de expiración
        -- Esto asegura que las contraseñas actualizadas no tengan restricciones de expiración
        UPDATE Usuarios 
        SET 
            Contrasenia = @NuevaContrasenia,
            FechaExpiracionContrasenia = NULL
        WHERE IdUsuario = @IdUsuario;
        
        -- Verificar si se actualizó algún registro
        IF @@ROWCOUNT = 0
        BEGIN
            RAISERROR('No se encontró el usuario con el ID especificado.', 16, 1);
            RETURN -1;
        END
        
        RETURN 0; -- Éxito
    END TRY
    BEGIN CATCH
        -- Manejo de errores
        DECLARE @ErrorMessage NVARCHAR(4000) = ERROR_MESSAGE();
        DECLARE @ErrorSeverity INT = ERROR_SEVERITY();
        DECLARE @ErrorState INT = ERROR_STATE();
        
        RAISERROR(@ErrorMessage, @ErrorSeverity, @ErrorState);
        RETURN -1;
    END CATCH
END;
GO

PRINT 'Stored Procedure sp_ActualizarContrasenia creado exitosamente.';
GO
