---- SP: Actualizar días acumulados de vacaciones de empleados
CREATE OR ALTER PROCEDURE sp_ActualizarDiasAcumuladosEmpleados
AS
BEGIN
    SET NOCOUNT ON;

    ;WITH CalculoVacaciones AS (
        SELECT 
            e.IdEmpleado,

            ------------------------------------------------------
            -- Cálculo de meses completos trabajados:
            -- 1. DATEDIFF(MONTH, FechaIngreso, GETDATE()) da meses totales.
            -- 2. Se resta 1 mes si aún no ha llegado el día del mes de ingreso.
            -- 3. Luego se multiplica por 1.25 para obtener días acumulados.
            ------------------------------------------------------
            (
                (
                    DATEDIFF(MONTH, e.FechaIngreso, GETDATE()) 
                    - CASE 
                        WHEN DAY(GETDATE()) < DAY(e.FechaIngreso) THEN 1 
                        ELSE 0 
                      END
                ) * 1.25
            ) AS DiasAcumuladosTotal,

            ------------------------------------------------------
            -- Días ya tomados (solicitudes aprobadas o finalizadas + históricos)
            ------------------------------------------------------
            ISNULL((
                SELECT SUM(se.DiasSolicitadosTotal)
                FROM SolicitudEncabezado se
                WHERE se.FK_IdEmpleado = e.IdEmpleado
                  AND se.FK_IdEstadoSolicitud IN (1, 2, 3, 5)
            ), 0) + ISNULL(e.DiasTomadosHistoricos, 0) AS DiasYaTomados

        FROM Empleados e
        WHERE e.FK_IdEstado = 1
          AND DATEDIFF(DAY, e.FechaIngreso, GETDATE()) > 0
    )

    ------------------------------------------------------
    -- Actualizar días acumulados en la tabla Empleados
    ------------------------------------------------------
    UPDATE e
    SET e.DiasVacacionesAcumulados =
        CASE 
            WHEN (c.DiasAcumuladosTotal - c.DiasYaTomados) < 0 THEN 0
            ELSE (c.DiasAcumuladosTotal - c.DiasYaTomados)
        END
    FROM Empleados e
    INNER JOIN CalculoVacaciones c ON e.IdEmpleado = c.IdEmpleado;

END
GO

EXEC sp_ActualizarDiasAcumuladosEmpleados