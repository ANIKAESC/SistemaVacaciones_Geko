CREATE TABLE EmpresasProyecto (
    IdEmpresaProyecto INT IDENTITY(1,1) PRIMARY KEY,
    FK_IdEmpresa INT NOT NULL,
    FK_IdProyecto INT NOT NULL,
    FOREIGN KEY (FK_IdEmpresa) REFERENCES Empresas(IdEmpresa),
    FOREIGN KEY (FK_IdProyecto) REFERENCES Proyectos(IdProyecto)
);
GO

INSERT INTO Proyectos (Nombre, Descripcion, FechaInicio, FK_IdEstado)
VALUES
('TPP Contenidos', NULL, NULL, 1),
('Contabilidad', NULL, NULL, 1),
('TPP Recursos Humanos', NULL, NULL, 1),
('TPP Comercial', NULL, NULL, 1),
('Promerica Guatemala', NULL, NULL, 1),
('Promerica El Salvador', NULL, NULL, 1),
('BAM', NULL, NULL, 1),
('Ippon', NULL, NULL, 1),
('Administrativo', NULL, NULL, 1);

INSERT INTO Empresas (Nombre, Codigo)
VALUES
('Digital Geko Corp S.A', 'DIGEKO'),
('Technology Product Performance S.A.', 'TECPRO'),
('Easy Go S.A.', 'EASYGO'),
('Oasis Digital', 'OASIS'),
('Grupo Digital de Guatemala','GRUDIGUA');

-- ==========================================
-- RELACIONES ENTRE EMPRESAS Y PROYECTOS
-- ==========================================

-- Technology Product Performance S.A
INSERT INTO EmpresasProyecto (FK_IdEmpresa, FK_IdProyecto)
SELECT e.IdEmpresa, p.IdProyecto
FROM Empresas e, Proyectos p
WHERE e.Nombre = 'Technology Product Performance S.A'
  AND p.Nombre IN (
    'Let''s Advertise',
    'TPP Contenidos',
    'Contabilidad',
    'TPP Recursos Humanos',
    'TPP Comercial'
  );

-- Digital Geko Corp S.A
INSERT INTO EmpresasProyecto (FK_IdEmpresa, FK_IdProyecto)
SELECT e.IdEmpresa, p.IdProyecto
FROM Empresas e, Proyectos p
WHERE e.Nombre = 'Digital Geko Corp S.A'
  AND p.Nombre IN (
    'RRHH',
    'Promerica Guatemala',
    'Promerica El Salvador',
    'BAM',
    'Ippon',
    'Administrativo'
  );

-- Grupo Digital de Guatemala
INSERT INTO EmpresasProyecto (FK_IdEmpresa, FK_IdProyecto)
SELECT e.IdEmpresa, p.IdProyecto
FROM Empresas e, Proyectos p
WHERE e.Nombre = 'Grupo Digital de Guatemala'
  AND p.Nombre IN (
    'RRHH',
    'Promerica Guatemala',
    'Promerica El Salvador',
    'BAM',
    'Ippon',
    'Administrativo',
    'Easy Go',
    'TOM',
    'TPP Comercial',
    'Prometheus',
    'Let''s Advertise'
  );

-- Easy Go S.A.
INSERT INTO EmpresasProyecto (FK_IdEmpresa, FK_IdProyecto)
SELECT e.IdEmpresa, p.IdProyecto
FROM Empresas e, Proyectos p
WHERE e.Nombre = 'Easy Go S.A.'
  AND p.Nombre IN (
    'Easy Go'
  );

-- Oasis Digital SC
INSERT INTO EmpresasProyecto (FK_IdEmpresa, FK_IdProyecto)
SELECT e.IdEmpresa, p.IdProyecto
FROM Empresas e, Proyectos p
WHERE e.Nombre = 'Oasis Digital SC'
  AND p.Nombre IN (
    'Prometheus',
	'TOM');
GO

SELECT 
    ep.IdEmpresaProyecto,
    e.Nombre AS Empresa,
    p.Nombre AS Proyecto
FROM EmpresasProyecto ep
INNER JOIN Empresas e ON e.IdEmpresa = ep.FK_IdEmpresa
INNER JOIN Proyectos p ON p.IdProyecto = ep.FK_IdProyecto
ORDER BY e.Nombre,p.Nombre;
GO

-- Listar todos los proyectos (Hubs) por empresa para RRHH
CREATE PROCEDURE sp_ListarProyectosPorEmpresa
    @IdEmpresa INT
AS
BEGIN
    SET NOCOUNT ON;
    
    SELECT 
        p.IdProyecto,
        p.Nombre,
        p.Descripcion,
        p.FechaInicio,
        p.FK_IdEstado
    FROM 
        Proyectos p 
    INNER JOIN 
        EmpresasProyecto ep ON p.IdProyecto = ep.FK_IdProyecto 
    WHERE 
        ep.FK_IdEmpresa = @IdEmpresa;
    
    RETURN 0;
END;
GO