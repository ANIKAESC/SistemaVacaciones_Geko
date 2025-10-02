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



-------
INSERT INTO Empleados 
(TipoContrato, Pais, Departamento, Municipio, Direccion, Puesto, Codigo, DPI, Pasaporte, 
 NombresEmpleado, ApellidosEmpleado, CorreoPersonal, CorreoInstitucional, FechaIngreso, 
 DiasTomadosHistoricos, FechaNacimiento, Telefono, NIT, Genero, Salario, Foto, FK_IdEstado)
VALUES
('Planilla', 'Guatemala', 'Guatemala', 'Fraijanes', 
 'Álika Club Residencial, Km.18 Carretera a Lo de Diéguez, Fraijanes. Apartamento B202', 
 'Desarrollador', NULL, '2966289480101', NULL, 
 'Osmel David', 'Tortola Tistoj', NULL, 'otortola@digitalgeko,com', '2021-06-15', 
 37.2, '1996-03-15', NULL, NULL, NULL, NULL, NULL, 1);


INSERT INTO Empleados 
(TipoContrato, Pais, Departamento, Municipio, Direccion, Puesto, Codigo, DPI, Pasaporte, 
 NombresEmpleado, ApellidosEmpleado, CorreoPersonal, CorreoInstitucional, FechaIngreso, 
 DiasTomadosHistoricos, FechaNacimiento, Telefono, NIT, Genero, Salario, Foto, FK_IdEstado)
VALUES
('Facturado', 'Guatemala', 'Guatemala', 'Sacatepequez', 
 '5ta Avenida Casa numero 8, Carretera RN14 Kilometro 54, Condominio Casa Cardenal, Gutemala, Sacatepequez.', 
 'Desarrollador', NULL, '2486034770301', NULL, 
 'Gianni Artemio', 'De Leon Chacon', NULL, 'gchacon@digitalgeko.com', '2021-07-01', 
 33.7, '1994-01-30', NULL, NULL, NULL, NULL, NULL, 1);


INSERT INTO Empleados 
(TipoContrato, Pais, Departamento, Municipio, Direccion, Puesto, Codigo, DPI, Pasaporte, 
 NombresEmpleado, ApellidosEmpleado, CorreoPersonal, CorreoInstitucional, FechaIngreso, 
 DiasTomadosHistoricos, FechaNacimiento, Telefono, NIT, Genero, Salario, Foto, FK_IdEstado)
VALUES
('Planilla', 'Guatemala', 'Guatemala', 'Mixco', 
 '31 avenida 3-18 residencial bosques de san Nicolas zona 4 de mixco, Guatemala', 
 'Desarrollador', NULL, '3000381780101', NULL, 
 'Juan David', 'Sian Hernandez', NULL, 'dsian@igitalgeko.com', '2023-03-17', 
 24.9, '1999-08-23', NULL, NULL, NULL, NULL, NULL, 1);


INSERT INTO Empleados 
(TipoContrato, Pais, Departamento, Municipio, Direccion, Puesto, Codigo, DPI, Pasaporte, 
 NombresEmpleado, ApellidosEmpleado, CorreoPersonal, CorreoInstitucional, FechaIngreso, 
 DiasTomadosHistoricos, FechaNacimiento, Telefono, NIT, Genero, Salario, Foto, FK_IdEstado)
VALUES
('Facturado', 'Guatemala', 'Guatemala', 'Guatemala', 
 'Km. 13.5 Muxbal. Condominio Valle Bello casa 2', 
 'Lider/Gerente', NULL, '2499633530102', NULL, 
 'Rafael Alberto', 'Melara Martinez', NULL, 'rmelara@digitalgeko.com', '2024-02-01', 
 16.5, '1982-01-16', NULL, NULL, NULL, NULL, NULL, 1);


INSERT INTO Empleados 
(TipoContrato, Pais, Departamento, Municipio, Direccion, Puesto, Codigo, DPI, Pasaporte, 
 NombresEmpleado, ApellidosEmpleado, CorreoPersonal, CorreoInstitucional, FechaIngreso, 
 DiasTomadosHistoricos, FechaNacimiento, Telefono, NIT, Genero, Salario, Foto, FK_IdEstado)
VALUES
('Facturado', 'Guatemala', 'Guatemala', 'Villa Nueva', 
 'Casa 11 Mza 4 Condominio Villas de Buenaventura, zona 2 San José Villa Nueva', 
 'Desarrollador', NULL, '2585809410101', NULL, 
 'Héctor Giovanni', 'Siquiej Cardona', NULL, 'hsiquiej@digitalgeko.com', '2021-06-16', 
 52.0, '1967-11-21', NULL, NULL, NULL, NULL, NULL, 1);


 ----------- 
 exec sp_ActualizarDiasVacacionesEmpleados; 

 exec sp_ActualizarDiasAcumuladosEmpleados;

 Select * From Empleados;