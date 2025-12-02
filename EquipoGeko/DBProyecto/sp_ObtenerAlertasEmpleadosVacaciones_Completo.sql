CREATE OR ALTER PROCEDURE sp_ObtenerAlertasEmpleadosVacaciones_Completo
AS
BEGIN
    SET NOCOUNT ON;

    WITH CalculoVacacionesCompleto AS (
        SELECT 
            e.IdEmpleado,
            e.NombresEmpleado,
            e.ApellidosEmpleado,
            e.Codigo,
            e.FechaIngreso,
            e.FK_IdEstado,
            
            -- 1. CALCULAR años trabajados (con decimales)
            CAST(DATEDIFF(DAY, e.FechaIngreso, GETDATE()) AS DECIMAL(10,2)) / 365.25 AS AniosTrabajados,
            
            -- 2. CALCULAR días acumulados total (años * 15 días por año)
            (CAST(DATEDIFF(DAY, e.FechaIngreso, GETDATE()) AS DECIMAL(10,2)) / 365.25) * 15 AS DiasAcumuladosTotal,
            
            -- 3. CALCULAR días ya tomados (solicitudes aprobadas/vigentes/finalizadas + históricos)
            ISNULL((
                SELECT SUM(se.DiasSolicitadosTotal)
                FROM SolicitudEncabezado se
                WHERE se.FK_IdEmpleado = e.IdEmpleado
                AND se.FK_IdEstadoSolicitud IN (2, 3, 5)
            ), 0) + ISNULL(e.DiasTomadosHistoricos, 0) AS DiasYaTomados
            
        FROM Empleados e
        WHERE e.FK_IdEstado = 1 -- Solo empleados activos
        AND DATEDIFF(DAY, e.FechaIngreso, GETDATE()) > 0 -- Que tengan al menos 1 día trabajado
    ),
    
    CalculoFinal AS (
        SELECT 
            IdEmpleado,
            NombresEmpleado,
            ApellidosEmpleado,
            Codigo,
            FechaIngreso,
            AniosTrabajados,
            ROUND(DiasAcumuladosTotal, 0) AS DiasAcumuladosTotal, --  Redondeado
            ROUND(DiasYaTomados, 0) AS DiasYaTomados,             --  Redondeado
            CASE 
                WHEN ROUND(DiasAcumuladosTotal - DiasYaTomados, 0) < 0 
                    THEN 0 
                ELSE ROUND(DiasAcumuladosTotal - DiasYaTomados, 0) 
            END AS DiasDisponibles                              --  Redondeado y protegido de negativos
        FROM CalculoVacacionesCompleto
    )
    
    -- RESULTADO: Empleados activos con más de 14 días disponibles
    SELECT 
        IdEmpleado,
        NombresEmpleado,
        ApellidosEmpleado,
        Codigo,
        FechaIngreso,
        CAST(AniosTrabajados AS DECIMAL(10,1)) AS AniosTrabajados,
        DiasAcumuladosTotal,
        DiasYaTomados,
        DiasDisponibles,
        'Empleado con más de 14 días de vacaciones disponibles' AS TipoNotificacion
    FROM CalculoFinal
    WHERE DiasDisponibles > 14
    
    UNION ALL
    
    -- ADICIONAL: Empleados próximos a salir que tomaron vacaciones
    SELECT 
        e.IdEmpleado,
        e.NombresEmpleado,
        e.ApellidosEmpleado,
        e.Codigo,
        e.FechaIngreso,
        0 as AniosTrabajados,
        0 as DiasAcumuladosTotal,
        0 as DiasYaTomados,
        0 as DiasDisponibles,
        'Empleado próximo a salir (con vacaciones tomadas)' AS TipoNotificacion
    FROM Empleados e
    WHERE e.FK_IdEstado <> 1 -- No activos
    AND EXISTS (
        SELECT 1 FROM SolicitudEncabezado se 
        WHERE se.FK_IdEmpleado = e.IdEmpleado 
        AND se.FK_IdEstadoSolicitud IN (2,3,5)
        AND se.DiasSolicitadosTotal > 0
    )
    
    ORDER BY DiasDisponibles DESC;
END;
GO

