---- SP Actualizar dias acumulados de vacaciones de empleados
CREATE OR ALTER PROCEDURE sp_ActualizarDiasAcumuladosEmpleados
AS
BEGIN
    -- Evita que SQL Server envíe mensajes de “n” filas afectadas
    SET NOCOUNT ON;

    ------------------------------------------------------
    -- 1. Crear un CTE (Common Table Expression) temporal
    --    para calcular los días acumulados y los días ya tomados
    ------------------------------------------------------
    ;WITH CalculoVacaciones AS (
        SELECT 
            e.IdEmpleado,  -- Identificador del empleado

            ------------------------------------------------------
            -- Calcular días acumulados totales:
            -- 1 año = 365.25 días (para considerar bisiestos aproximados)
            -- Multiplicamos por 15 días de vacaciones por año trabajado
            ------------------------------------------------------
            (CAST(DATEDIFF(DAY, e.FechaIngreso, GETDATE()) AS DECIMAL(10,2)) / 365.25) * 15 AS DiasAcumuladosTotal,

            ------------------------------------------------------
            -- Calcular días ya tomados:
            -- Suma de días solicitados en solicitudes aprobadas/vigentes/finalizadas
            -- + los días históricos que vienen del campo DiasTomadosHistoricos
            ------------------------------------------------------
            ISNULL((
                SELECT SUM(se.DiasSolicitadosTotal)
                FROM SolicitudEncabezado se
                WHERE se.FK_IdEmpleado = e.IdEmpleado
                  AND se.FK_IdEstadoSolicitud IN (1, 2, 3, 5) -- Estados válidos
            ), 0) + ISNULL(e.DiasTomadosHistoricos, 0) AS DiasYaTomados

        FROM Empleados e
        WHERE e.FK_IdEstado = 1               -- Solo empleados activos
          AND DATEDIFF(DAY, e.FechaIngreso, GETDATE()) > 0 -- Que tengan al menos 1 día trabajado
    )

    ------------------------------------------------------
    -- 2. Actualizar la columna DiasVacacionesAcumulados
    --    en la tabla Empleados usando el CTE calculado
    ------------------------------------------------------
    UPDATE e
    SET e.DiasVacacionesAcumulados = 
        CASE 
            -- Si el cálculo da negativo, asignamos 0
            WHEN ROUND(c.DiasAcumuladosTotal - c.DiasYaTomados, 0) < 0 
                THEN 0 
            -- Si no, redondeamos al entero más cercano
            ELSE ROUND(c.DiasAcumuladosTotal - c.DiasYaTomados, 0)
        END
    FROM Empleados e
    INNER JOIN CalculoVacaciones c ON e.IdEmpleado = c.IdEmpleado;

END
GO


 ----------- 
 exec sp_ActualizarDiasVacacionesEmpleados; 

 exec sp_ActualizarDiasAcumuladosEmpleados;

 Select * From Empleados;