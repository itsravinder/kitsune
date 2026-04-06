-- ============================================================
-- KITSUNE – Seed Data & Test Objects
-- Run this on your KitsuneDB database to create sample data
-- Usage: sqlcmd -S localhost -U sa -P YourStrong@Passw0rd -d KitsuneDB -i scripts/seed-data.sql
-- ============================================================

USE KitsuneDB;
GO

-- ── Sample Tables ─────────────────────────────────────────────

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Customers')
BEGIN
    CREATE TABLE dbo.Customers (
        CustomerId   INT IDENTITY(1,1) PRIMARY KEY,
        FullName     NVARCHAR(200) NOT NULL,
        Email        NVARCHAR(256) NOT NULL UNIQUE,
        Phone        NVARCHAR(20)  NULL,
        City         NVARCHAR(100) NULL,
        Country      NVARCHAR(100) NULL DEFAULT 'India',
        CreatedAt    DATETIME2     NOT NULL DEFAULT SYSUTCDATETIME(),
        IsActive     BIT           NOT NULL DEFAULT 1
    );
    CREATE INDEX IX_Customers_Email   ON dbo.Customers (Email);
    CREATE INDEX IX_Customers_Country ON dbo.Customers (Country);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Products')
BEGIN
    CREATE TABLE dbo.Products (
        ProductId    INT IDENTITY(1,1) PRIMARY KEY,
        ProductName  NVARCHAR(200) NOT NULL,
        Category     NVARCHAR(100) NOT NULL,
        Price        DECIMAL(10,2) NOT NULL,
        Stock        INT           NOT NULL DEFAULT 0,
        IsActive     BIT           NOT NULL DEFAULT 1,
        CreatedAt    DATETIME2     NOT NULL DEFAULT SYSUTCDATETIME()
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Orders')
BEGIN
    CREATE TABLE dbo.Orders (
        OrderId      INT IDENTITY(1,1) PRIMARY KEY,
        CustomerId   INT           NOT NULL REFERENCES dbo.Customers(CustomerId),
        OrderDate    DATETIME2     NOT NULL DEFAULT SYSUTCDATETIME(),
        Status       NVARCHAR(50)  NOT NULL DEFAULT 'Pending',
        OrderTotal   DECIMAL(12,2) NOT NULL DEFAULT 0,
        ShippedAt    DATETIME2     NULL,
        Notes        NVARCHAR(MAX) NULL
    );
    CREATE INDEX IX_Orders_CustomerId ON dbo.Orders (CustomerId);
    CREATE INDEX IX_Orders_OrderDate  ON dbo.Orders (OrderDate DESC);
    CREATE INDEX IX_Orders_Status     ON dbo.Orders (Status);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'OrderItems')
BEGIN
    CREATE TABLE dbo.OrderItems (
        ItemId       INT IDENTITY(1,1) PRIMARY KEY,
        OrderId      INT           NOT NULL REFERENCES dbo.Orders(OrderId),
        ProductId    INT           NOT NULL REFERENCES dbo.Products(ProductId),
        Quantity     INT           NOT NULL DEFAULT 1,
        UnitPrice    DECIMAL(10,2) NOT NULL,
        LineTotal    AS (Quantity * UnitPrice) PERSISTED
    );
    CREATE INDEX IX_OrderItems_OrderId   ON dbo.OrderItems (OrderId);
    CREATE INDEX IX_OrderItems_ProductId ON dbo.OrderItems (ProductId);
END
GO

-- ── Seed Customers ────────────────────────────────────────────

IF NOT EXISTS (SELECT 1 FROM dbo.Customers)
BEGIN
    INSERT INTO dbo.Customers (FullName, Email, Phone, City, Country) VALUES
    ('Priya Sharma',     'priya.sharma@email.com',   '+91-9876543210', 'Mumbai',    'India'),
    ('Rahul Mehta',      'rahul.mehta@corp.in',      '+91-9876543211', 'Pune',      'India'),
    ('Ananya Iyer',      'ananya.iyer@startup.io',   '+91-9876543212', 'Bangalore', 'India'),
    ('Dev Patel',        'dev.patel@techco.com',     '+91-9876543213', 'Ahmedabad', 'India'),
    ('Sneha Krishnan',   'sneha.k@mail.com',         '+91-9876543214', 'Chennai',   'India'),
    ('Arjun Singh',      'arjun.singh@biz.in',       '+91-9876543215', 'Delhi',     'India'),
    ('Meera Nair',       'meera.nair@web.com',       '+91-9876543216', 'Kochi',     'India'),
    ('Vikram Reddy',     'vikram.r@enterprise.com',  '+91-9876543217', 'Hyderabad', 'India'),
    ('Kavya Joshi',      'kavya.j@cloud.io',         '+91-9876543218', 'Jaipur',    'India'),
    ('Rohan Gupta',      'rohan.gupta@tech.com',     '+91-9876543219', 'Kolkata',   'India');
    PRINT 'Customers seeded: 10 rows';
END
GO

-- ── Seed Products ─────────────────────────────────────────────

IF NOT EXISTS (SELECT 1 FROM dbo.Products)
BEGIN
    INSERT INTO dbo.Products (ProductName, Category, Price, Stock) VALUES
    ('SQL Server Enterprise',     'Software',     89999.00, 100),
    ('MongoDB Atlas M10',         'Cloud',        4999.00,  500),
    ('Visual Studio Pro',         'Software',     12999.00, 200),
    ('Azure SQL Database S3',     'Cloud',        7999.00,  1000),
    ('JetBrains DataGrip',        'Software',     5999.00,  300),
    ('KITSUNE Pro License',       'Software',     19999.00, 50),
    ('Database Training Course',  'Education',    2999.00,  999),
    ('Cloud Storage 1TB',         'Cloud',        1499.00,  999),
    ('Redis Enterprise',          'Software',     14999.00, 75),
    ('DevOps Toolkit Bundle',     'Software',     24999.00, 25);
    PRINT 'Products seeded: 10 rows';
END
GO

-- ── Seed Orders ───────────────────────────────────────────────

IF NOT EXISTS (SELECT 1 FROM dbo.Orders)
BEGIN
    DECLARE @i INT = 1;
    WHILE @i <= 50
    BEGIN
        DECLARE @custId INT = ((@i - 1) % 10) + 1;
        DECLARE @days   INT = -(@i * 3);
        DECLARE @status NVARCHAR(50) = CASE
            WHEN @i % 5 = 0 THEN 'Cancelled'
            WHEN @i % 4 = 0 THEN 'Shipped'
            WHEN @i % 3 = 0 THEN 'Processing'
            ELSE 'Completed'
        END;
        DECLARE @total  DECIMAL(12,2) = ROUND(RAND() * 50000 + 1000, 2);

        INSERT INTO dbo.Orders (CustomerId, OrderDate, Status, OrderTotal, ShippedAt)
        VALUES (
            @custId,
            DATEADD(DAY, @days, SYSUTCDATETIME()),
            @status,
            @total,
            CASE WHEN @status IN ('Shipped','Completed') THEN DATEADD(DAY, @days + 2, SYSUTCDATETIME()) ELSE NULL END
        );
        SET @i = @i + 1;
    END;
    PRINT 'Orders seeded: 50 rows';
END
GO

-- ── Sample View ───────────────────────────────────────────────

IF NOT EXISTS (SELECT 1 FROM sys.views WHERE name = 'vw_CustomerOrderSummary')
BEGIN
    EXEC('
    CREATE VIEW dbo.vw_CustomerOrderSummary AS
    SELECT
        c.CustomerId,
        c.FullName,
        c.Email,
        c.City,
        COUNT(o.OrderId)       AS TotalOrders,
        SUM(o.OrderTotal)      AS TotalSpend,
        MAX(o.OrderDate)       AS LastOrderDate,
        AVG(o.OrderTotal)      AS AvgOrderValue
    FROM dbo.Customers c
    LEFT JOIN dbo.Orders o ON o.CustomerId = c.CustomerId
    GROUP BY c.CustomerId, c.FullName, c.Email, c.City;
    ');
    PRINT 'View vw_CustomerOrderSummary created';
END
GO

-- ── Sample Stored Procedures (for testing KITSUNE validation) ─

IF NOT EXISTS (SELECT 1 FROM sys.procedures WHERE name = 'usp_GetCustomerOrders')
BEGIN
    EXEC('
    CREATE PROCEDURE dbo.usp_GetCustomerOrders
        @CustomerId  INT = NULL,
        @DateFrom    DATETIME2 = NULL,
        @DateTo      DATETIME2 = NULL,
        @StatusFilter NVARCHAR(50) = NULL
    AS
    BEGIN
        SET NOCOUNT ON;
        SELECT
            o.OrderId,
            o.OrderDate,
            o.Status,
            o.OrderTotal,
            o.ShippedAt,
            c.FullName,
            c.Email
        FROM dbo.Orders o
        INNER JOIN dbo.Customers c ON c.CustomerId = o.CustomerId
        WHERE
            (@CustomerId   IS NULL OR o.CustomerId = @CustomerId)
            AND (@DateFrom IS NULL OR o.OrderDate >= @DateFrom)
            AND (@DateTo   IS NULL OR o.OrderDate <= @DateTo)
            AND (@StatusFilter IS NULL OR o.Status = @StatusFilter)
        ORDER BY o.OrderDate DESC;
    END
    ');
    PRINT 'Procedure usp_GetCustomerOrders created';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.procedures WHERE name = 'usp_GetTopCustomers')
BEGIN
    EXEC('
    CREATE PROCEDURE dbo.usp_GetTopCustomers
        @TopN        INT = 10,
        @DaysBack    INT = 30
    AS
    BEGIN
        SET NOCOUNT ON;
        SELECT TOP (@TopN)
            c.CustomerId,
            c.FullName,
            c.Email,
            COUNT(o.OrderId)   AS OrderCount,
            SUM(o.OrderTotal)  AS TotalSpend
        FROM dbo.Customers c
        INNER JOIN dbo.Orders o ON o.CustomerId = c.CustomerId
        WHERE o.OrderDate >= DATEADD(DAY, -@DaysBack, SYSUTCDATETIME())
        GROUP BY c.CustomerId, c.FullName, c.Email
        ORDER BY TotalSpend DESC;
    END
    ');
    PRINT 'Procedure usp_GetTopCustomers created';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.procedures WHERE name = 'usp_UpdateOrderStatus')
BEGIN
    EXEC('
    CREATE PROCEDURE dbo.usp_UpdateOrderStatus
        @OrderId  INT,
        @Status   NVARCHAR(50),
        @ShipDate DATETIME2 = NULL
    AS
    BEGIN
        SET NOCOUNT ON;
        UPDATE dbo.Orders
        SET    Status    = @Status,
               ShippedAt = ISNULL(@ShipDate, ShippedAt)
        WHERE  OrderId   = @OrderId;
        SELECT @@ROWCOUNT AS RowsAffected;
    END
    ');
    PRINT 'Procedure usp_UpdateOrderStatus created';
END
GO

-- ── Verify seed ───────────────────────────────────────────────

SELECT
    'Customers' AS [Table], COUNT(*) AS [Rows] FROM dbo.Customers
UNION ALL SELECT 'Products', COUNT(*) FROM dbo.Products
UNION ALL SELECT 'Orders',   COUNT(*) FROM dbo.Orders;

PRINT '✓ KITSUNE seed data complete. Ready for testing.';
GO
