-- Script para corregir el problema de históricos en vista de autorizador
-- Basado en la estructura real de los SPs existentes

USE DBProyectoGrupalDojoGeko;
GO

PRINT '=== CORRIGIENDO SPs DE HISTÓRICO AUTORIZADOR ===';
GO

-- =============================================
-- EXPLICACIÓN DEL PROBLEMA:
-- =============================================
-- Hay 2 vistas que usan el MISMO método ObtenerSolicitudEncabezadoAutorizadorAsync():
-- 1. Vista NORMAL: Llama al método y filtra en C# solo estado 1 (pendientes)
-- 2. Vista HISTÓRICO: Llama al método y NO filtra (debe mostrar TODOS los estados)
--
-- PROBLEMA: El SP actual filtra por estado 1, entonces el histórico solo recibe pendientes
-- SOLUCIÓN: Quitar el filtro del SP y dejar que C# haga el filtro cuando sea necesario
-- =============================================

-- =============================================
-- 1. SP PARA AUTORIZADORES ESPECÍFICOS (CON PARÁMETRO)
-- =============================================
-- PROBLEMA: El SP actual NO recibe parámetro @FK_IdAutorizador Y filtra solo estado 1
-- SOLUCIÓN: Agregar el parámetro y QUITAR el filtro de estado

IF OBJECT_ID('sp_ListarSolicitudEncabezado_Autorizador', 'P') IS NOT NULL
    DROP PROCEDURE sp_ListarSolicitudEncabezado_Autorizador;
GO

CREATE PROCEDURE sp_ListarSolicitudEncabezado_Autorizador
    @FK_IdAutorizador INT
AS
BEGIN
    SET NOCOUNT ON;
    
    -- Ahora SÍ filtra por autorizador y muestra TODAS las solicitudes (no solo estado 1)
    SELECT 
        se.IdSolicitud,
        se.FK_IdEmpleado,
        e.NombresEmpleado,
        se.DiasSolicitadosTotal,
        se.FechaIngresoSolicitud,
        se.FK_IdEstadoSolicitud,
        se.FK_IdAutorizador
    FROM 
        SolicitudEncabezado se
    INNER JOIN 
        Empleados e ON se.FK_IdEmpleado = e.IdEmpleado
    WHERE 
        se.FK_IdAutorizador = @FK_IdAutorizador  -- Filtra por autorizador
        -- ❌ QUITAMOS: AND se.FK_IdEstadoSolicitud = 1
        -- ✅ Ahora muestra TODAS las solicitudes del autorizador (histórico completo)
    ORDER BY 
        se.FechaIngresoSolicitud DESC;
END;
GO

PRINT '✅ SP sp_ListarSolicitudEncabezado_Autorizador actualizado (con parámetro y sin filtro de estado)';
GO

-- =============================================
-- 2. SP PARA ADMIN/RRHH (SIN PARÁMETRO)
-- =============================================
-- PROBLEMA: Filtra solo estado 1 (Ingresada)
-- SOLUCIÓN: Quitar el filtro de estado para mostrar TODO

IF OBJECT_ID('sp_ListarSolicitudEncabezado_Autorizador_Admin', 'P') IS NOT NULL
    DROP PROCEDURE sp_ListarSolicitudEncabezado_Autorizador_Admin;
GO

CREATE PROCEDURE sp_ListarSolicitudEncabezado_Autorizador_Admin
AS
BEGIN
    SET NOCOUNT ON;
    
    -- Muestra TODAS las solicitudes sin filtro de estado
    SELECT 
        se.IdSolicitud,
        se.FK_IdEmpleado,
        e.NombresEmpleado,
        se.DiasSolicitadosTotal,
        se.FechaIngresoSolicitud,
        se.FK_IdEstadoSolicitud,
        se.FK_IdAutorizador
    FROM 
        SolicitudEncabezado se
    INNER JOIN 
        Empleados e ON se.FK_IdEmpleado = e.IdEmpleado
    -- ❌ QUITAMOS: WHERE se.FK_IdEstadoSolicitud = 1
    -- ✅ Ahora muestra TODAS las solicitudes (histórico completo)
    ORDER BY 
        se.FechaIngresoSolicitud DESC;
END;
GO

PRINT '✅ SP sp_ListarSolicitudEncabezado_Autorizador_Admin actualizado (sin filtro de estado)';
GO

-- =============================================
-- 3. VERIFICACIÓN
-- =============================================
PRINT '=== VERIFICACIÓN DE SOLICITUDES POR ESTADO ===';
GO

SELECT 
    FK_IdEstadoSolicitud AS Estado,
    COUNT(*) AS Cantidad
FROM SolicitudEncabezado
GROUP BY FK_IdEstadoSolicitud
ORDER BY FK_IdEstadoSolicitud;
GO

PRINT '';
PRINT '✅ Script completado exitosamente!';
PRINT '';
PRINT 'CAMBIOS REALIZADOS:';
PRINT '1. sp_ListarSolicitudEncabezado_Autorizador ahora:';
PRINT '   - Recibe parámetro @FK_IdAutorizador';
PRINT '   - Muestra TODAS las solicitudes del autorizador (no solo estado 1)';
PRINT '';
PRINT '2. sp_ListarSolicitudEncabezado_Autorizador_Admin ahora:';
PRINT '   - Muestra TODAS las solicitudes (no solo estado 1)';
PRINT '';
PRINT 'Ahora los históricos deberían aparecer en producción.';
GO
