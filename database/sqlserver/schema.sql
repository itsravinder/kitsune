-- ============================================================
-- KITSUNE – SQL Server E-Commerce Schema
-- File: database/sqlserver/schema.sql
-- Order: Tables → Functions → Stored Procedures → Views
-- Compatible: SQL Server 2016+
-- Run: sqlcmd -S localhost -U sa -P YourStrong@Passw0rd -d KitsuneDB -i schema.sql
-- ============================================================

USE KitsuneDB;
GO

PRINT '=== KITSUNE E-Commerce Schema ===';
PRINT 'Starting schema creation...';
GO

-- ============================================================
-- SECTION 1: TABLES
-- ============================================================
PRINT 'Creating tables...';
GO

-- ── 1.1 Categories ───────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Categories')
BEGIN
    CREATE TABLE dbo.Categories (
        CategoryId     INT            IDENTITY(1,1) PRIMARY KEY,
        CategoryName   NVARCHAR(100)  NOT NULL,
        ParentId       INT            NULL REFERENCES dbo.Categories(CategoryId),
        Description    NVARCHAR(500)  NULL,
        ImageUrl       NVARCHAR(500)  NULL,
        IsActive       BIT            NOT NULL DEFAULT 1,
        SortOrder      INT            NOT NULL DEFAULT 0,
        CreatedAt      DATETIME2      NOT NULL DEFAULT SYSUTCDATETIME(),
        UpdatedAt      DATETIME2      NULL
    );
    CREATE INDEX IX_Categories_ParentId ON dbo.Categories (ParentId);
    PRINT '  ✓ Categories';
END
GO

-- ── 1.2 Brands ───────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Brands')
BEGIN
    CREATE TABLE dbo.Brands (
        BrandId        INT            IDENTITY(1,1) PRIMARY KEY,
        BrandName      NVARCHAR(100)  NOT NULL UNIQUE,
        Slug           NVARCHAR(100)  NOT NULL UNIQUE,
        Description    NVARCHAR(1000) NULL,
        LogoUrl        NVARCHAR(500)  NULL,
        Website        NVARCHAR(300)  NULL,
        Country        NVARCHAR(100)  NULL DEFAULT 'India',
        IsActive       BIT            NOT NULL DEFAULT 1,
        CreatedAt      DATETIME2      NOT NULL DEFAULT SYSUTCDATETIME()
    );
    PRINT '  ✓ Brands';
END
GO

-- ── 1.3 Suppliers ────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Suppliers')
BEGIN
    CREATE TABLE dbo.Suppliers (
        SupplierId     INT            IDENTITY(1,1) PRIMARY KEY,
        SupplierName   NVARCHAR(200)  NOT NULL,
        ContactName    NVARCHAR(100)  NULL,
        Email          NVARCHAR(256)  NULL,
        Phone          NVARCHAR(20)   NULL,
        Address        NVARCHAR(500)  NULL,
        City           NVARCHAR(100)  NULL,
        State          NVARCHAR(100)  NULL,
        Country        NVARCHAR(100)  NULL DEFAULT 'India',
        PinCode        NVARCHAR(20)   NULL,
        GST            NVARCHAR(20)   NULL,
        IsActive       BIT            NOT NULL DEFAULT 1,
        CreatedAt      DATETIME2      NOT NULL DEFAULT SYSUTCDATETIME()
    );
    PRINT '  ✓ Suppliers';
END
GO

-- ── 1.4 Customers ────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Customers')
BEGIN
    CREATE TABLE dbo.Customers (
        CustomerId     INT            IDENTITY(1,1) PRIMARY KEY,
        FullName       NVARCHAR(200)  NOT NULL,
        Email          NVARCHAR(256)  NOT NULL UNIQUE,
        Phone          NVARCHAR(20)   NULL,
        DateOfBirth    DATE           NULL,
        Gender         NVARCHAR(10)   NULL,
        City           NVARCHAR(100)  NULL,
        State          NVARCHAR(100)  NULL,
        Country        NVARCHAR(100)  NULL DEFAULT 'India',
        PinCode        NVARCHAR(20)   NULL,
        IsActive       BIT            NOT NULL DEFAULT 1,
        LoyaltyPoints  INT            NOT NULL DEFAULT 0,
        Tier           NVARCHAR(20)   NOT NULL DEFAULT 'Bronze', -- Bronze|Silver|Gold|Platinum
        CreatedAt      DATETIME2      NOT NULL DEFAULT SYSUTCDATETIME(),
        LastLoginAt    DATETIME2      NULL
    );
    CREATE INDEX IX_Customers_Email   ON dbo.Customers (Email);
    CREATE INDEX IX_Customers_City    ON dbo.Customers (City);
    CREATE INDEX IX_Customers_Tier    ON dbo.Customers (Tier);
    PRINT '  ✓ Customers';
END
GO

-- ── 1.5 CustomerAddresses ────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'CustomerAddresses')
BEGIN
    CREATE TABLE dbo.CustomerAddresses (
        AddressId      INT            IDENTITY(1,1) PRIMARY KEY,
        CustomerId     INT            NOT NULL REFERENCES dbo.Customers(CustomerId),
        AddressType    NVARCHAR(20)   NOT NULL DEFAULT 'Home', -- Home|Work|Other
        AddressLine1   NVARCHAR(300)  NOT NULL,
        AddressLine2   NVARCHAR(300)  NULL,
        City           NVARCHAR(100)  NOT NULL,
        State          NVARCHAR(100)  NOT NULL,
        Country        NVARCHAR(100)  NOT NULL DEFAULT 'India',
        PinCode        NVARCHAR(20)   NOT NULL,
        IsDefault      BIT            NOT NULL DEFAULT 0,
        CreatedAt      DATETIME2      NOT NULL DEFAULT SYSUTCDATETIME()
    );
    CREATE INDEX IX_CustomerAddresses_CustomerId ON dbo.CustomerAddresses (CustomerId);
    PRINT '  ✓ CustomerAddresses';
END
GO

-- ── 1.6 Products ─────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Products')
BEGIN
    CREATE TABLE dbo.Products (
        ProductId      INT            IDENTITY(1,1) PRIMARY KEY,
        ProductName    NVARCHAR(300)  NOT NULL,
        SKU            NVARCHAR(100)  NOT NULL UNIQUE,
        CategoryId     INT            NOT NULL REFERENCES dbo.Categories(CategoryId),
        BrandId        INT            NULL REFERENCES dbo.Brands(BrandId),
        SupplierId     INT            NULL REFERENCES dbo.Suppliers(SupplierId),
        Description    NVARCHAR(MAX)  NULL,
        CostPrice      DECIMAL(12,2)  NOT NULL DEFAULT 0,
        SellingPrice   DECIMAL(12,2)  NOT NULL,
        MRP            DECIMAL(12,2)  NOT NULL,
        DiscountPct    AS CAST(CASE WHEN MRP > 0 THEN ((MRP - SellingPrice) / MRP) * 100 ELSE 0 END AS DECIMAL(5,2)) PERSISTED,
        StockQty       INT            NOT NULL DEFAULT 0,
        ReorderLevel   INT            NOT NULL DEFAULT 10,
        Weight         DECIMAL(8,3)   NULL, -- kg
        Unit           NVARCHAR(20)   NULL DEFAULT 'piece',
        HSNCode        NVARCHAR(20)   NULL,
        GSTRate        DECIMAL(5,2)   NOT NULL DEFAULT 18.00,
        IsActive       BIT            NOT NULL DEFAULT 1,
        IsFeatured     BIT            NOT NULL DEFAULT 0,
        Rating         DECIMAL(3,2)   NULL DEFAULT 0,
        ReviewCount    INT            NOT NULL DEFAULT 0,
        CreatedAt      DATETIME2      NOT NULL DEFAULT SYSUTCDATETIME(),
        UpdatedAt      DATETIME2      NULL
    );
    CREATE INDEX IX_Products_CategoryId    ON dbo.Products (CategoryId);
    CREATE INDEX IX_Products_BrandId       ON dbo.Products (BrandId);
    CREATE INDEX IX_Products_SKU           ON dbo.Products (SKU);
    CREATE INDEX IX_Products_SellingPrice  ON dbo.Products (SellingPrice);
    CREATE INDEX IX_Products_IsActive      ON dbo.Products (IsActive);
    PRINT '  ✓ Products';
END
GO

-- ── 1.7 ProductImages ────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ProductImages')
BEGIN
    CREATE TABLE dbo.ProductImages (
        ImageId        INT            IDENTITY(1,1) PRIMARY KEY,
        ProductId      INT            NOT NULL REFERENCES dbo.Products(ProductId),
        ImageUrl       NVARCHAR(500)  NOT NULL,
        AltText        NVARCHAR(200)  NULL,
        IsPrimary      BIT            NOT NULL DEFAULT 0,
        SortOrder      INT            NOT NULL DEFAULT 0,
        CreatedAt      DATETIME2      NOT NULL DEFAULT SYSUTCDATETIME()
    );
    CREATE INDEX IX_ProductImages_ProductId ON dbo.ProductImages (ProductId);
    PRINT '  ✓ ProductImages';
END
GO

-- ── 1.8 Coupons ──────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Coupons')
BEGIN
    CREATE TABLE dbo.Coupons (
        CouponId       INT            IDENTITY(1,1) PRIMARY KEY,
        CouponCode     NVARCHAR(50)   NOT NULL UNIQUE,
        Description    NVARCHAR(300)  NULL,
        DiscountType   NVARCHAR(20)   NOT NULL DEFAULT 'Percent', -- Percent|Fixed
        DiscountValue  DECIMAL(10,2)  NOT NULL,
        MinOrderValue  DECIMAL(12,2)  NOT NULL DEFAULT 0,
        MaxDiscount    DECIMAL(12,2)  NULL,
        UsageLimit     INT            NULL,
        UsedCount      INT            NOT NULL DEFAULT 0,
        ValidFrom      DATETIME2      NOT NULL DEFAULT SYSUTCDATETIME(),
        ValidTo        DATETIME2      NOT NULL,
        IsActive       BIT            NOT NULL DEFAULT 1,
        CreatedAt      DATETIME2      NOT NULL DEFAULT SYSUTCDATETIME()
    );
    PRINT '  ✓ Coupons';
END
GO

-- ── 1.9 Orders ───────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Orders')
BEGIN
    CREATE TABLE dbo.Orders (
        OrderId        INT            IDENTITY(1,1) PRIMARY KEY,
        OrderNumber    NVARCHAR(30)   NOT NULL UNIQUE,
        CustomerId     INT            NOT NULL REFERENCES dbo.Customers(CustomerId),
        AddressId      INT            NULL REFERENCES dbo.CustomerAddresses(AddressId),
        CouponId       INT            NULL REFERENCES dbo.Coupons(CouponId),
        OrderStatus    NVARCHAR(30)   NOT NULL DEFAULT 'Pending',
            -- Pending|Confirmed|Processing|Shipped|Delivered|Cancelled|Returned|Refunded
        PaymentStatus  NVARCHAR(30)   NOT NULL DEFAULT 'Pending',
            -- Pending|Paid|Failed|Refunded
        PaymentMethod  NVARCHAR(50)   NULL,
            -- CreditCard|DebitCard|UPI|NetBanking|Wallet|COD
        SubTotal       DECIMAL(14,2)  NOT NULL DEFAULT 0,
        DiscountAmount DECIMAL(14,2)  NOT NULL DEFAULT 0,
        ShippingCharge DECIMAL(10,2)  NOT NULL DEFAULT 0,
        TaxAmount      DECIMAL(12,2)  NOT NULL DEFAULT 0,
        TotalAmount    DECIMAL(14,2)  NOT NULL DEFAULT 0,
        CouponDiscount DECIMAL(12,2)  NOT NULL DEFAULT 0,
        Notes          NVARCHAR(MAX)  NULL,
        OrderedAt      DATETIME2      NOT NULL DEFAULT SYSUTCDATETIME(),
        ConfirmedAt    DATETIME2      NULL,
        ShippedAt      DATETIME2      NULL,
        DeliveredAt    DATETIME2      NULL,
        CancelledAt    DATETIME2      NULL
    );
    CREATE INDEX IX_Orders_CustomerId    ON dbo.Orders (CustomerId);
    CREATE INDEX IX_Orders_OrderStatus   ON dbo.Orders (OrderStatus);
    CREATE INDEX IX_Orders_OrderedAt     ON dbo.Orders (OrderedAt DESC);
    CREATE INDEX IX_Orders_OrderNumber   ON dbo.Orders (OrderNumber);
    PRINT '  ✓ Orders';
END
GO

-- ── 1.10 OrderItems ──────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'OrderItems')
BEGIN
    CREATE TABLE dbo.OrderItems (
        OrderItemId    INT            IDENTITY(1,1) PRIMARY KEY,
        OrderId        INT            NOT NULL REFERENCES dbo.Orders(OrderId),
        ProductId      INT            NOT NULL REFERENCES dbo.Products(ProductId),
        Quantity       INT            NOT NULL DEFAULT 1,
        UnitPrice      DECIMAL(12,2)  NOT NULL,
        DiscountPct    DECIMAL(5,2)   NOT NULL DEFAULT 0,
        TaxPct         DECIMAL(5,2)   NOT NULL DEFAULT 18,
        LineAmount     AS (CAST(Quantity * UnitPrice * (1 - DiscountPct/100) AS DECIMAL(14,2))) PERSISTED,
        TaxAmount      AS (CAST(Quantity * UnitPrice * (1 - DiscountPct/100) * TaxPct/100 AS DECIMAL(12,2))) PERSISTED,
        ReturnStatus   NVARCHAR(20)   NULL -- NULL|ReturnRequested|Returned
    );
    CREATE INDEX IX_OrderItems_OrderId   ON dbo.OrderItems (OrderId);
    CREATE INDEX IX_OrderItems_ProductId ON dbo.OrderItems (ProductId);
    PRINT '  ✓ OrderItems';
END
GO

-- ── 1.11 Shipments ───────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Shipments')
BEGIN
    CREATE TABLE dbo.Shipments (
        ShipmentId     INT            IDENTITY(1,1) PRIMARY KEY,
        OrderId        INT            NOT NULL REFERENCES dbo.Orders(OrderId),
        Carrier        NVARCHAR(100)  NOT NULL DEFAULT 'Delhivery',
        TrackingNumber NVARCHAR(100)  NULL,
        EstDelivery    DATE           NULL,
        ShippedAt      DATETIME2      NULL,
        DeliveredAt    DATETIME2      NULL,
        Status         NVARCHAR(30)   NOT NULL DEFAULT 'Pending',
            -- Pending|Dispatched|InTransit|OutForDelivery|Delivered|Failed
        CreatedAt      DATETIME2      NOT NULL DEFAULT SYSUTCDATETIME()
    );
    CREATE INDEX IX_Shipments_OrderId ON dbo.Shipments (OrderId);
    PRINT '  ✓ Shipments';
END
GO

-- ── 1.12 Payments ────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Payments')
BEGIN
    CREATE TABLE dbo.Payments (
        PaymentId      INT            IDENTITY(1,1) PRIMARY KEY,
        OrderId        INT            NOT NULL REFERENCES dbo.Orders(OrderId),
        Amount         DECIMAL(14,2)  NOT NULL,
        Method         NVARCHAR(50)   NOT NULL,
        Status         NVARCHAR(30)   NOT NULL DEFAULT 'Pending',
        TransactionRef NVARCHAR(200)  NULL,
        Gateway        NVARCHAR(100)  NULL,
        PaidAt         DATETIME2      NULL,
        FailureReason  NVARCHAR(500)  NULL,
        CreatedAt      DATETIME2      NOT NULL DEFAULT SYSUTCDATETIME()
    );
    CREATE INDEX IX_Payments_OrderId ON dbo.Payments (OrderId);
    PRINT '  ✓ Payments';
END
GO

-- ── 1.13 ProductReviews ──────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ProductReviews')
BEGIN
    CREATE TABLE dbo.ProductReviews (
        ReviewId       INT            IDENTITY(1,1) PRIMARY KEY,
        ProductId      INT            NOT NULL REFERENCES dbo.Products(ProductId),
        CustomerId     INT            NOT NULL REFERENCES dbo.Customers(CustomerId),
        OrderId        INT            NULL REFERENCES dbo.Orders(OrderId),
        Rating         INT            NOT NULL CHECK (Rating BETWEEN 1 AND 5),
        Title          NVARCHAR(200)  NULL,
        ReviewText     NVARCHAR(MAX)  NULL,
        IsVerified     BIT            NOT NULL DEFAULT 0,
        IsApproved     BIT            NOT NULL DEFAULT 1,
        HelpfulCount   INT            NOT NULL DEFAULT 0,
        CreatedAt      DATETIME2      NOT NULL DEFAULT SYSUTCDATETIME()
    );
    CREATE INDEX IX_ProductReviews_ProductId  ON dbo.ProductReviews (ProductId);
    CREATE INDEX IX_ProductReviews_CustomerId ON dbo.ProductReviews (CustomerId);
    PRINT '  ✓ ProductReviews';
END
GO

-- ── 1.14 Wishlist ────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Wishlist')
BEGIN
    CREATE TABLE dbo.Wishlist (
        WishlistId     INT            IDENTITY(1,1) PRIMARY KEY,
        CustomerId     INT            NOT NULL REFERENCES dbo.Customers(CustomerId),
        ProductId      INT            NOT NULL REFERENCES dbo.Products(ProductId),
        AddedAt        DATETIME2      NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT UQ_Wishlist UNIQUE (CustomerId, ProductId)
    );
    CREATE INDEX IX_Wishlist_CustomerId ON dbo.Wishlist (CustomerId);
    PRINT '  ✓ Wishlist';
END
GO

-- ── 1.15 Inventory Log ───────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'InventoryLog')
BEGIN
    CREATE TABLE dbo.InventoryLog (
        LogId          INT            IDENTITY(1,1) PRIMARY KEY,
        ProductId      INT            NOT NULL REFERENCES dbo.Products(ProductId),
        ChangeType     NVARCHAR(30)   NOT NULL,
            -- StockIn|StockOut|Adjustment|Return|Damage
        QuantityBefore INT            NOT NULL,
        QuantityChange INT            NOT NULL,
        QuantityAfter  INT            NOT NULL,
        Reference      NVARCHAR(100)  NULL,
        Notes          NVARCHAR(500)  NULL,
        CreatedAt      DATETIME2      NOT NULL DEFAULT SYSUTCDATETIME(),
        CreatedBy      NVARCHAR(128)  NOT NULL DEFAULT 'system'
    );
    CREATE INDEX IX_InventoryLog_ProductId ON dbo.InventoryLog (ProductId);
    PRINT '  ✓ InventoryLog';
END
GO

PRINT 'All tables created.';
GO

-- ============================================================
-- SECTION 2: FUNCTIONS
-- ============================================================
PRINT 'Creating functions...';
GO

-- ── 2.1 fn_GetCustomerTotalSpend ─────────────────────────────
IF OBJECT_ID('dbo.fn_GetCustomerTotalSpend','FN') IS NOT NULL
    DROP FUNCTION dbo.fn_GetCustomerTotalSpend;
GO
CREATE FUNCTION dbo.fn_GetCustomerTotalSpend
(
    @CustomerId INT,
    @FromDate   DATETIME2 = NULL,
    @ToDate     DATETIME2 = NULL
)
RETURNS DECIMAL(14,2)
AS
BEGIN
    DECLARE @Total DECIMAL(14,2);
    SELECT @Total = ISNULL(SUM(TotalAmount), 0)
    FROM dbo.Orders
    WHERE CustomerId   = @CustomerId
      AND OrderStatus  NOT IN ('Cancelled','Returned','Refunded')
      AND (@FromDate IS NULL OR OrderedAt >= @FromDate)
      AND (@ToDate   IS NULL OR OrderedAt <= @ToDate);
    RETURN @Total;
END;
GO
PRINT '  ✓ fn_GetCustomerTotalSpend';
GO

-- ── 2.2 fn_GetProductRevenue ──────────────────────────────────
IF OBJECT_ID('dbo.fn_GetProductRevenue','FN') IS NOT NULL
    DROP FUNCTION dbo.fn_GetProductRevenue;
GO
CREATE FUNCTION dbo.fn_GetProductRevenue
(
    @ProductId INT,
    @FromDate  DATETIME2 = NULL,
    @ToDate    DATETIME2 = NULL
)
RETURNS DECIMAL(14,2)
AS
BEGIN
    DECLARE @Revenue DECIMAL(14,2);
    SELECT @Revenue = ISNULL(SUM(oi.LineAmount), 0)
    FROM dbo.OrderItems oi
    INNER JOIN dbo.Orders o ON o.OrderId = oi.OrderId
    WHERE oi.ProductId   = @ProductId
      AND o.OrderStatus  NOT IN ('Cancelled','Returned','Refunded')
      AND (@FromDate IS NULL OR o.OrderedAt >= @FromDate)
      AND (@ToDate   IS NULL OR o.OrderedAt <= @ToDate);
    RETURN @Revenue;
END;
GO
PRINT '  ✓ fn_GetProductRevenue';
GO

-- ── 2.3 fn_CalculateDiscount ─────────────────────────────────
IF OBJECT_ID('dbo.fn_CalculateDiscount','FN') IS NOT NULL
    DROP FUNCTION dbo.fn_CalculateDiscount;
GO
CREATE FUNCTION dbo.fn_CalculateDiscount
(
    @OriginalPrice DECIMAL(12,2),
    @DiscountPct   DECIMAL(5,2)
)
RETURNS DECIMAL(12,2)
AS
BEGIN
    RETURN ROUND(@OriginalPrice * @DiscountPct / 100, 2);
END;
GO
PRINT '  ✓ fn_CalculateDiscount';
GO

-- ── 2.4 fn_GetOrderCount ─────────────────────────────────────
IF OBJECT_ID('dbo.fn_GetOrderCount','FN') IS NOT NULL
    DROP FUNCTION dbo.fn_GetOrderCount;
GO
CREATE FUNCTION dbo.fn_GetOrderCount
(
    @CustomerId INT,
    @Status     NVARCHAR(30) = NULL
)
RETURNS INT
AS
BEGIN
    DECLARE @Count INT;
    SELECT @Count = COUNT(*)
    FROM dbo.Orders
    WHERE CustomerId = @CustomerId
      AND (@Status IS NULL OR OrderStatus = @Status);
    RETURN ISNULL(@Count, 0);
END;
GO
PRINT '  ✓ fn_GetOrderCount';
GO

-- ── 2.5 fn_GetCategoryPath ───────────────────────────────────
IF OBJECT_ID('dbo.fn_GetCategoryPath','FN') IS NOT NULL
    DROP FUNCTION dbo.fn_GetCategoryPath;
GO
CREATE FUNCTION dbo.fn_GetCategoryPath (@CategoryId INT)
RETURNS NVARCHAR(500)
AS
BEGIN
    DECLARE @Path NVARCHAR(500) = '';
    DECLARE @Current INT = @CategoryId;
    DECLARE @Name NVARCHAR(100);
    DECLARE @Parent INT;
    DECLARE @Depth INT = 0;

    WHILE @Current IS NOT NULL AND @Depth < 10
    BEGIN
        SELECT @Name = CategoryName, @Parent = ParentId
        FROM dbo.Categories WHERE CategoryId = @Current;
        IF @Path = '' SET @Path = @Name;
        ELSE SET @Path = @Name + ' > ' + @Path;
        SET @Current = @Parent;
        SET @Depth = @Depth + 1;
    END
    RETURN @Path;
END;
GO
PRINT '  ✓ fn_GetCategoryPath';
GO

PRINT 'All functions created.';
GO

-- ============================================================
-- SECTION 3: STORED PROCEDURES
-- ============================================================
PRINT 'Creating stored procedures...';
GO

-- ── 3.1 usp_GetCustomerOrders ────────────────────────────────
IF OBJECT_ID('dbo.usp_GetCustomerOrders','P') IS NOT NULL DROP PROCEDURE dbo.usp_GetCustomerOrders;
GO
CREATE PROCEDURE dbo.usp_GetCustomerOrders
    @CustomerId     INT        = NULL,
    @OrderStatus    NVARCHAR(30) = NULL,
    @DateFrom       DATETIME2  = NULL,
    @DateTo         DATETIME2  = NULL,
    @TopN           INT        = 100,
    @PaymentMethod  NVARCHAR(50) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (@TopN)
        o.OrderId,
        o.OrderNumber,
        c.FullName,
        c.Email,
        o.OrderStatus,
        o.PaymentStatus,
        o.PaymentMethod,
        o.SubTotal,
        o.DiscountAmount,
        o.ShippingCharge,
        o.TaxAmount,
        o.TotalAmount,
        o.OrderedAt,
        o.DeliveredAt,
        COUNT(oi.OrderItemId)           AS ItemCount,
        SUM(oi.Quantity)                AS TotalQty
    FROM dbo.Orders o
    INNER JOIN dbo.Customers c ON c.CustomerId = o.CustomerId
    LEFT  JOIN dbo.OrderItems oi ON oi.OrderId = o.OrderId
    WHERE (@CustomerId    IS NULL OR o.CustomerId    = @CustomerId)
      AND (@OrderStatus   IS NULL OR o.OrderStatus   = @OrderStatus)
      AND (@PaymentMethod IS NULL OR o.PaymentMethod = @PaymentMethod)
      AND (@DateFrom      IS NULL OR o.OrderedAt    >= @DateFrom)
      AND (@DateTo        IS NULL OR o.OrderedAt    <= @DateTo)
    GROUP BY o.OrderId, o.OrderNumber, c.FullName, c.Email,
             o.OrderStatus, o.PaymentStatus, o.PaymentMethod,
             o.SubTotal, o.DiscountAmount, o.ShippingCharge,
             o.TaxAmount, o.TotalAmount, o.OrderedAt, o.DeliveredAt
    ORDER BY o.OrderedAt DESC;
END;
GO
PRINT '  ✓ usp_GetCustomerOrders';
GO

-- ── 3.2 usp_GetTopCustomers ──────────────────────────────────
IF OBJECT_ID('dbo.usp_GetTopCustomers','P') IS NOT NULL DROP PROCEDURE dbo.usp_GetTopCustomers;
GO
CREATE PROCEDURE dbo.usp_GetTopCustomers
    @TopN       INT       = 10,
    @DaysBack   INT       = 365,
    @MinOrders  INT       = 1,
    @Tier       NVARCHAR(20) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (@TopN)
        c.CustomerId,
        c.FullName,
        c.Email,
        c.Phone,
        c.City,
        c.Tier,
        c.LoyaltyPoints,
        COUNT(DISTINCT o.OrderId)      AS TotalOrders,
        SUM(o.TotalAmount)             AS TotalSpend,
        AVG(o.TotalAmount)             AS AvgOrderValue,
        MAX(o.OrderedAt)               AS LastOrderDate,
        dbo.fn_GetCustomerTotalSpend(c.CustomerId, NULL, NULL) AS LifetimeValue
    FROM dbo.Customers c
    INNER JOIN dbo.Orders o ON o.CustomerId = c.CustomerId
    WHERE o.OrderStatus NOT IN ('Cancelled','Refunded')
      AND o.OrderedAt   >= DATEADD(DAY, -@DaysBack, SYSUTCDATETIME())
      AND c.IsActive     = 1
      AND (@Tier IS NULL OR c.Tier = @Tier)
    GROUP BY c.CustomerId, c.FullName, c.Email, c.Phone, c.City, c.Tier, c.LoyaltyPoints
    HAVING COUNT(DISTINCT o.OrderId) >= @MinOrders
    ORDER BY TotalSpend DESC;
END;
GO
PRINT '  ✓ usp_GetTopCustomers';
GO

-- ── 3.3 usp_GetProductSalesSummary ──────────────────────────
IF OBJECT_ID('dbo.usp_GetProductSalesSummary','P') IS NOT NULL DROP PROCEDURE dbo.usp_GetProductSalesSummary;
GO
CREATE PROCEDURE dbo.usp_GetProductSalesSummary
    @CategoryId INT       = NULL,
    @BrandId    INT       = NULL,
    @DateFrom   DATETIME2 = NULL,
    @DateTo     DATETIME2 = NULL,
    @MinRating  DECIMAL(3,2) = NULL,
    @TopN       INT       = 50
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (@TopN)
        p.ProductId,
        p.ProductName,
        p.SKU,
        cat.CategoryName,
        b.BrandName,
        p.SellingPrice,
        p.StockQty,
        p.Rating,
        p.ReviewCount,
        ISNULL(SUM(oi.Quantity), 0)     AS UnitsSold,
        ISNULL(SUM(oi.LineAmount), 0)   AS Revenue,
        ISNULL(AVG(oi.UnitPrice), 0)    AS AvgSellingPrice,
        COUNT(DISTINCT o.OrderId)       AS OrderCount,
        dbo.fn_GetProductRevenue(p.ProductId, @DateFrom, @DateTo) AS TotalRevenue
    FROM dbo.Products p
    INNER JOIN dbo.Categories cat ON cat.CategoryId = p.CategoryId
    LEFT  JOIN dbo.Brands b       ON b.BrandId      = p.BrandId
    LEFT  JOIN dbo.OrderItems oi  ON oi.ProductId   = p.ProductId
    LEFT  JOIN dbo.Orders o       ON o.OrderId       = oi.OrderId
                                  AND o.OrderStatus  NOT IN ('Cancelled','Returned','Refunded')
                                  AND (@DateFrom IS NULL OR o.OrderedAt >= @DateFrom)
                                  AND (@DateTo   IS NULL OR o.OrderedAt <= @DateTo)
    WHERE p.IsActive     = 1
      AND (@CategoryId IS NULL OR p.CategoryId = @CategoryId)
      AND (@BrandId    IS NULL OR p.BrandId    = @BrandId)
      AND (@MinRating  IS NULL OR p.Rating     >= @MinRating)
    GROUP BY p.ProductId, p.ProductName, p.SKU, cat.CategoryName,
             b.BrandName, p.SellingPrice, p.StockQty, p.Rating, p.ReviewCount
    ORDER BY Revenue DESC;
END;
GO
PRINT '  ✓ usp_GetProductSalesSummary';
GO

-- ── 3.4 usp_PlaceOrder ───────────────────────────────────────
IF OBJECT_ID('dbo.usp_PlaceOrder','P') IS NOT NULL DROP PROCEDURE dbo.usp_PlaceOrder;
GO
CREATE PROCEDURE dbo.usp_PlaceOrder
    @CustomerId    INT,
    @AddressId     INT       = NULL,
    @CouponCode    NVARCHAR(50) = NULL,
    @PaymentMethod NVARCHAR(50) = 'COD',
    @Notes         NVARCHAR(MAX) = NULL,
    @OrderId       INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRY
        BEGIN TRANSACTION;

        DECLARE @CouponId INT = NULL, @CouponDiscount DECIMAL(14,2) = 0;

        -- Validate coupon
        IF @CouponCode IS NOT NULL
        BEGIN
            SELECT @CouponId = CouponId
            FROM dbo.Coupons
            WHERE CouponCode = @CouponCode
              AND IsActive    = 1
              AND ValidFrom  <= SYSUTCDATETIME()
              AND ValidTo    >= SYSUTCDATETIME()
              AND (UsageLimit IS NULL OR UsedCount < UsageLimit);

            IF @CouponId IS NOT NULL
                UPDATE dbo.Coupons SET UsedCount = UsedCount + 1 WHERE CouponId = @CouponId;
        END

        -- Generate unique order number using timestamp + customer id
        DECLARE @OrderNumber NVARCHAR(30);
        SET @OrderNumber = 'ORD-' + FORMAT(SYSUTCDATETIME(), 'yyyyMMddHHmmss') + '-' + CAST(@CustomerId AS NVARCHAR(10));

        INSERT INTO dbo.Orders
            (OrderNumber, CustomerId, AddressId, CouponId, OrderStatus,
             PaymentStatus, PaymentMethod, SubTotal, TotalAmount, Notes)
        VALUES
            (@OrderNumber, @CustomerId, @AddressId, @CouponId, 'Pending',
             'Pending', @PaymentMethod, 0, 0, @Notes);

        SET @OrderId = SCOPE_IDENTITY();
        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END;
GO
PRINT '  ✓ usp_PlaceOrder';
GO

-- ── 3.5 usp_UpdateOrderStatus ────────────────────────────────
IF OBJECT_ID('dbo.usp_UpdateOrderStatus','P') IS NOT NULL DROP PROCEDURE dbo.usp_UpdateOrderStatus;
GO
CREATE PROCEDURE dbo.usp_UpdateOrderStatus
    @OrderId      INT,
    @NewStatus    NVARCHAR(30),
    @Notes        NVARCHAR(500) = NULL,
    @UpdatedBy    NVARCHAR(128) = 'system'
AS
BEGIN
    SET NOCOUNT ON;
    IF NOT EXISTS (SELECT 1 FROM dbo.Orders WHERE OrderId = @OrderId)
    BEGIN
        RAISERROR('Order %d not found.', 16, 1, @OrderId);
        RETURN;
    END

    UPDATE dbo.Orders
    SET OrderStatus  = @NewStatus,
        ConfirmedAt  = CASE WHEN @NewStatus = 'Confirmed'  THEN SYSUTCDATETIME() ELSE ConfirmedAt  END,
        ShippedAt    = CASE WHEN @NewStatus = 'Shipped'    THEN SYSUTCDATETIME() ELSE ShippedAt    END,
        DeliveredAt  = CASE WHEN @NewStatus = 'Delivered'  THEN SYSUTCDATETIME() ELSE DeliveredAt  END,
        CancelledAt  = CASE WHEN @NewStatus = 'Cancelled'  THEN SYSUTCDATETIME() ELSE CancelledAt  END
    WHERE OrderId = @OrderId;

    SELECT @@ROWCOUNT AS RowsAffected, @OrderId AS OrderId, @NewStatus AS NewStatus;
END;
GO
PRINT '  ✓ usp_UpdateOrderStatus';
GO

-- ── 3.6 usp_GetInventoryAlerts ───────────────────────────────
IF OBJECT_ID('dbo.usp_GetInventoryAlerts','P') IS NOT NULL DROP PROCEDURE dbo.usp_GetInventoryAlerts;
GO
CREATE PROCEDURE dbo.usp_GetInventoryAlerts
    @AlertType  NVARCHAR(20) = 'All' -- All|LowStock|OutOfStock
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        p.ProductId,
        p.ProductName,
        p.SKU,
        cat.CategoryName,
        b.BrandName,
        p.StockQty,
        p.ReorderLevel,
        p.SellingPrice,
        CASE
            WHEN p.StockQty = 0               THEN 'OutOfStock'
            WHEN p.StockQty <= p.ReorderLevel  THEN 'LowStock'
            ELSE 'OK'
        END AS StockStatus
    FROM dbo.Products p
    INNER JOIN dbo.Categories cat ON cat.CategoryId = p.CategoryId
    LEFT  JOIN dbo.Brands b       ON b.BrandId      = p.BrandId
    WHERE p.IsActive = 1
      AND (
            @AlertType = 'All'
         OR (@AlertType = 'OutOfStock' AND p.StockQty = 0)
         OR (@AlertType = 'LowStock'   AND p.StockQty > 0 AND p.StockQty <= p.ReorderLevel)
          )
    ORDER BY p.StockQty ASC;
END;
GO
PRINT '  ✓ usp_GetInventoryAlerts';
GO

-- ── 3.7 usp_GetRevenueSummary ────────────────────────────────
IF OBJECT_ID('dbo.usp_GetRevenueSummary','P') IS NOT NULL DROP PROCEDURE dbo.usp_GetRevenueSummary;
GO
CREATE PROCEDURE dbo.usp_GetRevenueSummary
    @GroupBy  NVARCHAR(20) = 'Month', -- Day|Week|Month|Quarter|Year
    @DateFrom DATETIME2 = NULL,
    @DateTo   DATETIME2 = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET @DateFrom = ISNULL(@DateFrom, DATEADD(YEAR, -1, SYSUTCDATETIME()));
    SET @DateTo   = ISNULL(@DateTo,   SYSUTCDATETIME());

    SELECT
        CASE @GroupBy
            WHEN 'Day'     THEN CONVERT(NVARCHAR(10), o.OrderedAt, 23)
            WHEN 'Week'    THEN 'W' + CAST(DATEPART(WEEK, o.OrderedAt) AS NVARCHAR) + '-' + CAST(YEAR(o.OrderedAt) AS NVARCHAR)
            WHEN 'Month'   THEN FORMAT(o.OrderedAt, 'MMM yyyy')
            WHEN 'Quarter' THEN 'Q' + CAST(DATEPART(QUARTER, o.OrderedAt) AS NVARCHAR) + '-' + CAST(YEAR(o.OrderedAt) AS NVARCHAR)
            WHEN 'Year'    THEN CAST(YEAR(o.OrderedAt) AS NVARCHAR)
        END                             AS Period,
        COUNT(DISTINCT o.OrderId)       AS TotalOrders,
        COUNT(DISTINCT o.CustomerId)    AS UniqueCustomers,
        SUM(o.TotalAmount)              AS Revenue,
        AVG(o.TotalAmount)              AS AvgOrderValue,
        SUM(o.DiscountAmount + o.CouponDiscount) AS TotalDiscount,
        SUM(o.TaxAmount)                AS TotalTax,
        SUM(oi.Quantity)                AS UnitsSold
    FROM dbo.Orders o
    LEFT JOIN dbo.OrderItems oi ON oi.OrderId = o.OrderId
    WHERE o.OrderStatus NOT IN ('Cancelled','Refunded')
      AND o.OrderedAt BETWEEN @DateFrom AND @DateTo
    GROUP BY
        CASE @GroupBy
            WHEN 'Day'     THEN CONVERT(NVARCHAR(10), o.OrderedAt, 23)
            WHEN 'Week'    THEN 'W' + CAST(DATEPART(WEEK, o.OrderedAt) AS NVARCHAR) + '-' + CAST(YEAR(o.OrderedAt) AS NVARCHAR)
            WHEN 'Month'   THEN FORMAT(o.OrderedAt, 'MMM yyyy')
            WHEN 'Quarter' THEN 'Q' + CAST(DATEPART(QUARTER, o.OrderedAt) AS NVARCHAR) + '-' + CAST(YEAR(o.OrderedAt) AS NVARCHAR)
            WHEN 'Year'    THEN CAST(YEAR(o.OrderedAt) AS NVARCHAR)
        END
    ORDER BY MIN(o.OrderedAt);
END;
GO
PRINT '  ✓ usp_GetRevenueSummary';
GO

-- ── 3.8 usp_SearchProducts ───────────────────────────────────
IF OBJECT_ID('dbo.usp_SearchProducts','P') IS NOT NULL DROP PROCEDURE dbo.usp_SearchProducts;
GO
CREATE PROCEDURE dbo.usp_SearchProducts
    @Keyword    NVARCHAR(200) = NULL,
    @CategoryId INT           = NULL,
    @BrandId    INT           = NULL,
    @MinPrice   DECIMAL(12,2) = NULL,
    @MaxPrice   DECIMAL(12,2) = NULL,
    @InStock    BIT           = NULL,
    @MinRating  DECIMAL(3,2)  = NULL,
    @SortBy     NVARCHAR(30)  = 'Relevance', -- Relevance|PriceLow|PriceHigh|Rating|Newest
    @PageNum    INT           = 1,
    @PageSize   INT           = 20
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @Offset INT = (@PageNum - 1) * @PageSize;

    SELECT
        p.ProductId,
        p.ProductName,
        p.SKU,
        cat.CategoryName,
        b.BrandName,
        p.SellingPrice,
        p.MRP,
        p.DiscountPct,
        p.StockQty,
        p.Rating,
        p.ReviewCount,
        p.IsFeatured
    FROM dbo.Products p
    INNER JOIN dbo.Categories cat ON cat.CategoryId = p.CategoryId
    LEFT  JOIN dbo.Brands b       ON b.BrandId      = p.BrandId
    WHERE p.IsActive     = 1
      AND (@CategoryId IS NULL OR p.CategoryId = @CategoryId)
      AND (@BrandId    IS NULL OR p.BrandId    = @BrandId)
      AND (@MinPrice   IS NULL OR p.SellingPrice >= @MinPrice)
      AND (@MaxPrice   IS NULL OR p.SellingPrice <= @MaxPrice)
      AND (@InStock    IS NULL OR (CASE WHEN p.StockQty > 0 THEN 1 ELSE 0 END) = @InStock)
      AND (@MinRating  IS NULL OR p.Rating >= @MinRating)
      AND (@Keyword    IS NULL OR p.ProductName LIKE '%' + @Keyword + '%'
                               OR p.SKU LIKE '%' + @Keyword + '%')
    ORDER BY
        CASE WHEN @SortBy = 'PriceLow'  THEN p.SellingPrice  END ASC,
        CASE WHEN @SortBy = 'PriceHigh' THEN p.SellingPrice  END DESC,
        CASE WHEN @SortBy = 'Rating'    THEN p.Rating        END DESC,
        CASE WHEN @SortBy = 'Newest'    THEN p.CreatedAt     END DESC,
        p.IsFeatured DESC,
        p.ReviewCount DESC
    OFFSET @Offset ROWS
    FETCH NEXT @PageSize ROWS ONLY;
END;
GO
PRINT '  ✓ usp_SearchProducts';
GO

-- ── 3.9 usp_GetCustomerLifetimeValue ─────────────────────────
IF OBJECT_ID('dbo.usp_GetCustomerLifetimeValue','P') IS NOT NULL DROP PROCEDURE dbo.usp_GetCustomerLifetimeValue;
GO
CREATE PROCEDURE dbo.usp_GetCustomerLifetimeValue
    @CustomerId INT = NULL,
    @Tier       NVARCHAR(20) = NULL,
    @TopN       INT = 50
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (@TopN)
        c.CustomerId,
        c.FullName,
        c.Email,
        c.Tier,
        c.LoyaltyPoints,
        c.CreatedAt                             AS CustomerSince,
        COUNT(DISTINCT o.OrderId)               AS TotalOrders,
        ISNULL(SUM(o.TotalAmount),0)            AS LifetimeSpend,
        ISNULL(AVG(o.TotalAmount),0)            AS AvgOrderValue,
        ISNULL(MAX(o.TotalAmount),0)            AS MaxOrderValue,
        MAX(o.OrderedAt)                        AS LastOrderDate,
        DATEDIFF(DAY, MAX(o.OrderedAt), SYSUTCDATETIME()) AS DaysSinceLastOrder,
        COUNT(DISTINCT CASE WHEN o.OrderStatus = 'Cancelled' THEN o.OrderId END) AS CancelledOrders,
        COUNT(DISTINCT r.ReviewId)              AS ReviewsGiven
    FROM dbo.Customers c
    LEFT JOIN dbo.Orders o ON o.CustomerId = c.CustomerId
    LEFT JOIN dbo.ProductReviews r ON r.CustomerId = c.CustomerId
    WHERE c.IsActive = 1
      AND (@CustomerId IS NULL OR c.CustomerId = @CustomerId)
      AND (@Tier       IS NULL OR c.Tier       = @Tier)
    GROUP BY c.CustomerId, c.FullName, c.Email, c.Tier, c.LoyaltyPoints, c.CreatedAt
    ORDER BY LifetimeSpend DESC;
END;
GO
PRINT '  ✓ usp_GetCustomerLifetimeValue';
GO

PRINT 'All stored procedures created.';
GO

-- ============================================================
-- SECTION 4: VIEWS
-- ============================================================
PRINT 'Creating views...';
GO

-- ── 4.1 vw_OrderSummary ──────────────────────────────────────
IF OBJECT_ID('dbo.vw_OrderSummary','V') IS NOT NULL DROP VIEW dbo.vw_OrderSummary;
GO
CREATE VIEW dbo.vw_OrderSummary AS
SELECT
    o.OrderId,
    o.OrderNumber,
    c.CustomerId,
    c.FullName          AS CustomerName,
    c.Email,
    c.Tier              AS CustomerTier,
    o.OrderStatus,
    o.PaymentStatus,
    o.PaymentMethod,
    o.SubTotal,
    o.DiscountAmount,
    o.CouponDiscount,
    o.ShippingCharge,
    o.TaxAmount,
    o.TotalAmount,
    o.OrderedAt,
    o.DeliveredAt,
    DATEDIFF(DAY, o.OrderedAt, ISNULL(o.DeliveredAt, SYSUTCDATETIME())) AS FulfillmentDays,
    COUNT(oi.OrderItemId) AS ItemCount,
    SUM(oi.Quantity)      AS TotalQuantity
FROM dbo.Orders o
INNER JOIN dbo.Customers c  ON c.CustomerId = o.CustomerId
LEFT  JOIN dbo.OrderItems oi ON oi.OrderId  = o.OrderId
GROUP BY o.OrderId, o.OrderNumber, c.CustomerId, c.FullName, c.Email, c.Tier,
         o.OrderStatus, o.PaymentStatus, o.PaymentMethod, o.SubTotal,
         o.DiscountAmount, o.CouponDiscount, o.ShippingCharge, o.TaxAmount,
         o.TotalAmount, o.OrderedAt, o.DeliveredAt;
GO
PRINT '  ✓ vw_OrderSummary';
GO

-- ── 4.2 vw_ProductPerformance ────────────────────────────────
IF OBJECT_ID('dbo.vw_ProductPerformance','V') IS NOT NULL DROP VIEW dbo.vw_ProductPerformance;
GO
CREATE VIEW dbo.vw_ProductPerformance AS
SELECT
    p.ProductId,
    p.ProductName,
    p.SKU,
    cat.CategoryName,
    b.BrandName,
    p.SellingPrice,
    p.CostPrice,
    p.SellingPrice - p.CostPrice               AS GrossMargin,
    CASE WHEN p.SellingPrice > 0
         THEN CAST((p.SellingPrice - p.CostPrice) / p.SellingPrice * 100 AS DECIMAL(5,2))
         ELSE 0 END                             AS MarginPct,
    p.StockQty,
    p.Rating,
    p.ReviewCount,
    ISNULL(SUM(oi.Quantity), 0)                AS UnitsSold,
    ISNULL(SUM(oi.LineAmount), 0)              AS TotalRevenue,
    ISNULL(AVG(oi.UnitPrice), 0)              AS AvgSellingPrice,
    COUNT(DISTINCT o.OrderId)                   AS OrderCount,
    COUNT(DISTINCT o.CustomerId)               AS UniqueCustomers
FROM dbo.Products p
INNER JOIN dbo.Categories cat ON cat.CategoryId = p.CategoryId
LEFT  JOIN dbo.Brands b       ON b.BrandId      = p.BrandId
LEFT  JOIN dbo.OrderItems oi  ON oi.ProductId   = p.ProductId
LEFT  JOIN dbo.Orders o       ON o.OrderId       = oi.OrderId
                              AND o.OrderStatus  NOT IN ('Cancelled','Returned','Refunded')
GROUP BY p.ProductId, p.ProductName, p.SKU, cat.CategoryName, b.BrandName,
         p.SellingPrice, p.CostPrice, p.StockQty, p.Rating, p.ReviewCount;
GO
PRINT '  ✓ vw_ProductPerformance';
GO

-- ── 4.3 vw_CustomerSummary ───────────────────────────────────
IF OBJECT_ID('dbo.vw_CustomerSummary','V') IS NOT NULL DROP VIEW dbo.vw_CustomerSummary;
GO
CREATE VIEW dbo.vw_CustomerSummary AS
SELECT
    c.CustomerId,
    c.FullName,
    c.Email,
    c.Phone,
    c.City,
    c.State,
    c.Tier,
    c.LoyaltyPoints,
    c.CreatedAt                                 AS MemberSince,
    COUNT(DISTINCT o.OrderId)                   AS TotalOrders,
    ISNULL(SUM(CASE WHEN o.OrderStatus NOT IN ('Cancelled','Refunded') THEN o.TotalAmount END), 0) AS TotalSpend,
    ISNULL(AVG(CASE WHEN o.OrderStatus NOT IN ('Cancelled','Refunded') THEN o.TotalAmount END), 0) AS AvgOrderValue,
    MAX(o.OrderedAt)                            AS LastOrderDate,
    COUNT(DISTINCT CASE WHEN o.OrderStatus = 'Cancelled' THEN o.OrderId END)  AS CancelledOrders,
    COUNT(DISTINCT CASE WHEN o.OrderStatus = 'Delivered' THEN o.OrderId END)  AS DeliveredOrders,
    ISNULL(AVG(CAST(r.Rating AS FLOAT)), 0)    AS AvgRatingGiven
FROM dbo.Customers c
LEFT JOIN dbo.Orders o         ON o.CustomerId  = c.CustomerId
LEFT JOIN dbo.ProductReviews r ON r.CustomerId  = c.CustomerId AND r.IsApproved = 1
GROUP BY c.CustomerId, c.FullName, c.Email, c.Phone, c.City, c.State,
         c.Tier, c.LoyaltyPoints, c.CreatedAt;
GO
PRINT '  ✓ vw_CustomerSummary';
GO

-- ── 4.4 vw_CategoryRevenue ───────────────────────────────────
IF OBJECT_ID('dbo.vw_CategoryRevenue','V') IS NOT NULL DROP VIEW dbo.vw_CategoryRevenue;
GO
CREATE VIEW dbo.vw_CategoryRevenue AS
SELECT
    cat.CategoryId,
    cat.CategoryName,
    dbo.fn_GetCategoryPath(cat.CategoryId)     AS CategoryPath,
    COUNT(DISTINCT p.ProductId)                AS ProductCount,
    ISNULL(SUM(oi.Quantity), 0)               AS UnitsSold,
    ISNULL(SUM(oi.LineAmount), 0)             AS Revenue,
    COUNT(DISTINCT o.OrderId)                  AS OrderCount,
    COUNT(DISTINCT o.CustomerId)              AS UniqueCustomers,
    ISNULL(AVG(p.Rating), 0)                  AS AvgRating
FROM dbo.Categories cat
LEFT JOIN dbo.Products p      ON p.CategoryId  = cat.CategoryId AND p.IsActive = 1
LEFT JOIN dbo.OrderItems oi   ON oi.ProductId  = p.ProductId
LEFT JOIN dbo.Orders o        ON o.OrderId     = oi.OrderId
                             AND o.OrderStatus NOT IN ('Cancelled','Returned','Refunded')
GROUP BY cat.CategoryId, cat.CategoryName;
GO
PRINT '  ✓ vw_CategoryRevenue';
GO

-- ── 4.5 vw_DailySalesReport ──────────────────────────────────
IF OBJECT_ID('dbo.vw_DailySalesReport','V') IS NOT NULL DROP VIEW dbo.vw_DailySalesReport;
GO
CREATE VIEW dbo.vw_DailySalesReport AS
SELECT
    CAST(o.OrderedAt AS DATE)              AS SaleDate,
    COUNT(DISTINCT o.OrderId)              AS TotalOrders,
    COUNT(DISTINCT o.CustomerId)           AS UniqueCustomers,
    SUM(o.TotalAmount)                     AS Revenue,
    AVG(o.TotalAmount)                     AS AvgOrderValue,
    SUM(o.DiscountAmount + o.CouponDiscount) AS TotalDiscounts,
    SUM(o.TaxAmount)                       AS TaxCollected,
    SUM(CASE WHEN o.OrderStatus = 'Delivered'  THEN o.TotalAmount ELSE 0 END) AS DeliveredRevenue,
    SUM(CASE WHEN o.OrderStatus = 'Cancelled'  THEN 1 ELSE 0 END) AS CancelledCount,
    SUM(oi.Quantity)                       AS UnitsSold
FROM dbo.Orders o
LEFT JOIN dbo.OrderItems oi ON oi.OrderId = o.OrderId
GROUP BY CAST(o.OrderedAt AS DATE);
GO
PRINT '  ✓ vw_DailySalesReport';
GO

PRINT '';
PRINT '=== Schema creation COMPLETE ===';
PRINT 'Tables: 15';
PRINT 'Functions: 5';
PRINT 'Stored Procedures: 9';
PRINT 'Views: 5';
GO
