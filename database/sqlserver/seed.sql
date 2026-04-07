-- ============================================================
-- KITSUNE – SQL Server E-Commerce Seed Data
-- File: database/sqlserver/seed.sql
-- Supports: JOINs, Aggregations, NQL→SQL testing
-- Run AFTER schema.sql
-- Run: sqlcmd -S localhost -U sa -P YourStrong@Passw0rd -d KitsuneDB -i seed.sql
-- ============================================================

USE KitsuneDB;
GO

PRINT '=== KITSUNE Seed Data ===';
PRINT 'Inserting reference and test data...';
SET NOCOUNT ON;
GO

-- ============================================================
-- CATEGORIES (3-level hierarchy)
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM dbo.Categories)
BEGIN
    -- Level 1 – Root categories
    INSERT INTO dbo.Categories (CategoryName, ParentId, Description, SortOrder) VALUES
    ('Electronics',      NULL, 'Electronic gadgets and devices',        1),
    ('Fashion',          NULL, 'Clothing, footwear and accessories',    2),
    ('Home & Kitchen',   NULL, 'Home appliances and kitchen essentials',3),
    ('Books',            NULL, 'Books, e-books and educational material',4),
    ('Sports & Fitness', NULL, 'Sports equipment and fitness gear',     5),
    ('Beauty & Health',  NULL, 'Beauty products and health supplements',6),
    ('Grocery',          NULL, 'Daily grocery and FMCG products',       7),
    ('Toys & Games',     NULL, 'Toys, games and hobby items',           8);

    -- Level 2 – Sub-categories
    INSERT INTO dbo.Categories (CategoryName, ParentId, Description, SortOrder) VALUES
    ('Mobiles & Accessories', 1, 'Smartphones, tablets and accessories', 1),
    ('Laptops & Computers',   1, 'Laptops, desktops and peripherals',    2),
    ('Audio & Video',         1, 'Headphones, speakers, TVs',            3),
    ('Cameras',               1, 'DSLR, mirrorless and action cameras',  4),
    ('Men''s Clothing',       2, 'T-shirts, shirts, trousers, suits',    1),
    ('Women''s Clothing',     2, 'Kurtas, sarees, dresses, tops',        2),
    ('Footwear',              2, 'Shoes, sandals and sport shoes',       3),
    ('Kitchen Appliances',    3, 'Mixer, microwave, induction cooktop',  1),
    ('Furniture',             3, 'Sofas, beds, wardrobes, tables',       2),
    ('Technology Books',      4, 'Programming, data science, AI books',  1),
    ('Fitness Equipment',     5, 'Dumbbells, resistance bands, mats',    1),
    ('Skincare',              6, 'Moisturisers, serums, sunscreen',      1),
    ('Supplements',           6, 'Protein, vitamins, health drinks',     2);

    PRINT '  ✓ Categories (23 rows)';
END
GO

-- ============================================================
-- BRANDS
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM dbo.Brands)
BEGIN
    INSERT INTO dbo.Brands (BrandName, Slug, Description, Country) VALUES
    ('Samsung',       'samsung',       'Global electronics leader',            'South Korea'),
    ('Apple',         'apple',         'Premium consumer electronics',         'USA'),
    ('OnePlus',       'oneplus',       'Premium Android smartphones',          'China'),
    ('Xiaomi',        'xiaomi',        'Value-for-money electronics',          'China'),
    ('Sony',          'sony',          'Electronics and entertainment',        'Japan'),
    ('LG',            'lg',            'Home appliances and electronics',      'South Korea'),
    ('Dell',          'dell',          'Laptops and computing solutions',      'USA'),
    ('HP',            'hp',            'Computers and printers',              'USA'),
    ('Lenovo',        'lenovo',        'ThinkPad and IdeaPad laptops',        'China'),
    ('Nike',          'nike',          'Athletic footwear and apparel',       'USA'),
    ('Adidas',        'adidas',        'Sports clothing and shoes',           'Germany'),
    ('Puma',          'puma',          'Sportswear and lifestyle',            'Germany'),
    ('Allen Solly',   'allen-solly',   'Business and casual fashion',         'India'),
    ('Peter England', 'peter-england', 'Men''s formal and casual wear',       'India'),
    ('Bata',          'bata',          'Affordable quality footwear',         'India'),
    ('Prestige',      'prestige',      'Kitchen appliances and cookware',     'India'),
    ('Philips',       'philips',       'Home appliances and healthcare',      'Netherlands'),
    ('Lakme',         'lakme',         'Beauty and cosmetics brand',          'India'),
    ('Mamaearth',     'mamaearth',     'Natural personal care products',      'India'),
    ('Boat',          'boat',          'Budget audio and accessories',        'India'),
    ('JBL',           'jbl',           'Premium audio equipment',             'USA'),
    ('Nikon',         'nikon',         'Professional camera systems',         'Japan'),
    ('IKEA',          'ikea',          'Modern furniture and home decor',     'Sweden'),
    ('Penguin Books', 'penguin-books', 'Classic and contemporary literature', 'UK');

    PRINT '  ✓ Brands (24 rows)';
END
GO

-- ============================================================
-- SUPPLIERS
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM dbo.Suppliers)
BEGIN
    INSERT INTO dbo.Suppliers (SupplierName, ContactName, Email, Phone, City, State, GST) VALUES
    ('TechWorld Distributors',  'Rajesh Kumar',   'rajesh@techworld.in',   '+91-9811001100', 'Delhi',     'Delhi',         '07AABCT1234A1Z5'),
    ('Fashion Hub Wholesale',   'Priya Mehta',    'priya@fashionhub.in',   '+91-9922002200', 'Mumbai',    'Maharashtra',   '27AABCF5678B2Y4'),
    ('ElectroSource India',     'Sanjay Gupta',   'sanjay@electrosrc.in',  '+91-9733003300', 'Bengaluru', 'Karnataka',     '29AABCE9012C3X3'),
    ('KitchenPro Suppliers',    'Anita Singh',    'anita@kitchenpro.in',   '+91-9844004400', 'Pune',      'Maharashtra',   '27AABCK3456D4W2'),
    ('BooksBazaar Pvt Ltd',     'Vikram Sharma',  'vikram@booksbazaar.in', '+91-9955005500', 'Chennai',   'Tamil Nadu',    '33AABCB7890E5V1'),
    ('SportZone Wholesale',     'Kavita Reddy',   'kavita@sportzone.in',   '+91-9866006600', 'Hyderabad', 'Telangana',     '36AABCS2345F6U0'),
    ('BeautyLine India',        'Neha Joshi',     'neha@beautyline.in',    '+91-9977007700', 'Jaipur',    'Rajasthan',     '08AABCB6789G7T9'),
    ('GroceryFirst Pvt Ltd',    'Arun Nair',      'arun@groceryfirst.in',  '+91-9888008800', 'Kochi',     'Kerala',        '32AABCG1234H8S8');

    PRINT '  ✓ Suppliers (8 rows)';
END
GO

-- ============================================================
-- CUSTOMERS (30 realistic Indian customers)
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM dbo.Customers)
BEGIN
    INSERT INTO dbo.Customers (FullName, Email, Phone, DateOfBirth, Gender, City, State, PinCode, LoyaltyPoints, Tier, CreatedAt) VALUES
    ('Priya Sharma',        'priya.sharma@gmail.com',      '+91-9876543210', '1992-03-15', 'Female', 'Mumbai',     'Maharashtra', '400001', 4500,  'Platinum', DATEADD(DAY,-365, SYSUTCDATETIME())),
    ('Rahul Mehta',         'rahul.mehta@outlook.com',     '+91-9876543211', '1988-07-22', 'Male',   'Delhi',      'Delhi',       '110001', 3200,  'Gold',     DATEADD(DAY,-300, SYSUTCDATETIME())),
    ('Ananya Iyer',         'ananya.iyer@yahoo.com',       '+91-9876543212', '1995-11-08', 'Female', 'Bengaluru',  'Karnataka',   '560001', 1800,  'Silver',   DATEADD(DAY,-280, SYSUTCDATETIME())),
    ('Dev Patel',           'dev.patel@gmail.com',         '+91-9876543213', '1990-05-30', 'Male',   'Ahmedabad',  'Gujarat',     '380001', 900,   'Bronze',   DATEADD(DAY,-250, SYSUTCDATETIME())),
    ('Sneha Krishnan',      'sneha.krishnan@gmail.com',    '+91-9876543214', '1993-09-12', 'Female', 'Chennai',    'Tamil Nadu',  '600001', 2100,  'Silver',   DATEADD(DAY,-220, SYSUTCDATETIME())),
    ('Arjun Singh',         'arjun.singh@hotmail.com',     '+91-9876543215', '1985-12-01', 'Male',   'Pune',       'Maharashtra', '411001', 5200,  'Platinum', DATEADD(DAY,-400, SYSUTCDATETIME())),
    ('Meera Nair',          'meera.nair@gmail.com',        '+91-9876543216', '1997-02-28', 'Female', 'Kochi',      'Kerala',      '682001', 650,   'Bronze',   DATEADD(DAY,-180, SYSUTCDATETIME())),
    ('Vikram Reddy',        'vikram.reddy@gmail.com',      '+91-9876543217', '1991-08-14', 'Male',   'Hyderabad',  'Telangana',   '500001', 3800,  'Gold',     DATEADD(DAY,-350, SYSUTCDATETIME())),
    ('Kavya Joshi',         'kavya.joshi@gmail.com',       '+91-9876543218', '1994-06-20', 'Female', 'Jaipur',     'Rajasthan',   '302001', 1200,  'Silver',   DATEADD(DAY,-200, SYSUTCDATETIME())),
    ('Rohan Gupta',         'rohan.gupta@gmail.com',       '+91-9876543219', '1989-10-05', 'Male',   'Kolkata',    'West Bengal', '700001', 2700,  'Gold',     DATEADD(DAY,-320, SYSUTCDATETIME())),
    ('Divya Pillai',        'divya.pillai@gmail.com',      '+91-9876543220', '1996-04-17', 'Female', 'Trivandrum', 'Kerala',      '695001', 450,   'Bronze',   DATEADD(DAY,-90,  SYSUTCDATETIME())),
    ('Karthik Rajan',       'karthik.rajan@gmail.com',     '+91-9876543221', '1987-01-25', 'Male',   'Coimbatore', 'Tamil Nadu',  '641001', 6100,  'Platinum', DATEADD(DAY,-450, SYSUTCDATETIME())),
    ('Neha Agarwal',        'neha.agarwal@gmail.com',      '+91-9876543222', '1993-07-09', 'Female', 'Lucknow',    'Uttar Pradesh','226001',1500,  'Silver',   DATEADD(DAY,-170, SYSUTCDATETIME())),
    ('Siddharth Bansal',    'siddharth.bansal@gmail.com',  '+91-9876543223', '1992-03-30', 'Male',   'Chandigarh', 'Punjab',      '160001', 2300,  'Silver',   DATEADD(DAY,-240, SYSUTCDATETIME())),
    ('Pooja Verma',         'pooja.verma@gmail.com',       '+91-9876543224', '1998-11-15', 'Female', 'Bhopal',     'Madhya Pradesh','462001',300, 'Bronze',   DATEADD(DAY,-60,  SYSUTCDATETIME())),
    ('Amit Saxena',         'amit.saxena@gmail.com',       '+91-9876543225', '1986-05-22', 'Male',   'Nagpur',     'Maharashtra', '440001', 4100,  'Gold',     DATEADD(DAY,-380, SYSUTCDATETIME())),
    ('Ritika Kapoor',       'ritika.kapoor@gmail.com',     '+91-9876543226', '1995-08-07', 'Female', 'Surat',      'Gujarat',     '395001', 750,   'Bronze',   DATEADD(DAY,-120, SYSUTCDATETIME())),
    ('Manish Tiwari',       'manish.tiwari@gmail.com',     '+91-9876543227', '1990-02-14', 'Male',   'Varanasi',   'Uttar Pradesh','221001',1900,  'Silver',   DATEADD(DAY,-210, SYSUTCDATETIME())),
    ('Shalini Desai',       'shalini.desai@gmail.com',     '+91-9876543228', '1994-12-29', 'Female', 'Vadodara',   'Gujarat',     '390001', 3300,  'Gold',     DATEADD(DAY,-290, SYSUTCDATETIME())),
    ('Rajesh Pandey',       'rajesh.pandey@gmail.com',     '+91-9876543229', '1983-09-18', 'Male',   'Patna',      'Bihar',       '800001', 550,   'Bronze',   DATEADD(DAY,-140, SYSUTCDATETIME())),
    ('Sunita Rao',          'sunita.rao@gmail.com',        '+91-9876543230', '1991-04-03', 'Female', 'Visakhapatnam','Andhra Pradesh','530001',2800,'Gold',    DATEADD(DAY,-310, SYSUTCDATETIME())),
    ('Aditya Kumar',        'aditya.kumar@gmail.com',      '+91-9876543231', '1997-07-11', 'Male',   'Ranchi',     'Jharkhand',   '834001', 400,   'Bronze',   DATEADD(DAY,-75,  SYSUTCDATETIME())),
    ('Harpreet Kaur',       'harpreet.kaur@gmail.com',     '+91-9876543232', '1989-10-28', 'Female', 'Amritsar',   'Punjab',      '143001', 1600,  'Silver',   DATEADD(DAY,-190, SYSUTCDATETIME())),
    ('Deepak Mishra',       'deepak.mishra@gmail.com',     '+91-9876543233', '1984-01-16', 'Male',   'Indore',     'Madhya Pradesh','452001',4600, 'Platinum', DATEADD(DAY,-420, SYSUTCDATETIME())),
    ('Aarti Bose',          'aarti.bose@gmail.com',        '+91-9876543234', '1996-06-24', 'Female', 'Kolkata',    'West Bengal', '700002', 900,   'Bronze',   DATEADD(DAY,-155, SYSUTCDATETIME())),
    ('Suresh Nambiar',      'suresh.nambiar@gmail.com',    '+91-9876543235', '1988-03-07', 'Male',   'Mangaluru',  'Karnataka',   '575001', 2200,  'Silver',   DATEADD(DAY,-260, SYSUTCDATETIME())),
    ('Tanvi Shah',          'tanvi.shah@gmail.com',        '+91-9876543236', '1993-11-30', 'Female', 'Mumbai',     'Maharashtra', '400002', 3900,  'Gold',     DATEADD(DAY,-330, SYSUTCDATETIME())),
    ('Prakash Rathore',     'prakash.rathore@gmail.com',   '+91-9876543237', '1987-08-19', 'Male',   'Jodhpur',    'Rajasthan',   '342001', 1100,  'Silver',   DATEADD(DAY,-175, SYSUTCDATETIME())),
    ('Laxmi Subramaniam',   'laxmi.subram@gmail.com',      '+91-9876543238', '1992-05-13', 'Female', 'Madurai',    'Tamil Nadu',  '625001', 5500,  'Platinum', DATEADD(DAY,-410, SYSUTCDATETIME())),
    ('Nilesh Chaudhari',    'nilesh.chaudhari@gmail.com',  '+91-9876543239', '1991-12-04', 'Male',   'Nashik',     'Maharashtra', '422001', 700,   'Bronze',   DATEADD(DAY,-100, SYSUTCDATETIME()));

    PRINT '  ✓ Customers (30 rows)';
END
GO

-- ============================================================
-- CUSTOMER ADDRESSES
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM dbo.CustomerAddresses)
BEGIN
    INSERT INTO dbo.CustomerAddresses (CustomerId, AddressType, AddressLine1, City, State, PinCode, IsDefault) VALUES
    (1,  'Home', 'Flat 4B, Sunshine Apartments, Andheri West', 'Mumbai',    'Maharashtra',    '400001', 1),
    (1,  'Work', 'Office 12, Tech Park, BKC',                   'Mumbai',    'Maharashtra',    '400051', 0),
    (2,  'Home', '34, Rajouri Garden, Near Metro',               'Delhi',     'Delhi',          '110027', 1),
    (3,  'Home', '204, Indiranagar 1st Stage',                   'Bengaluru', 'Karnataka',      '560038', 1),
    (4,  'Home', 'B/5, Paldi Society, Ellisbridge',              'Ahmedabad', 'Gujarat',        '380006', 1),
    (5,  'Home', 'No.7, Anna Nagar East',                        'Chennai',   'Tamil Nadu',     '600102', 1),
    (6,  'Home', '22, Koregaon Park Road',                       'Pune',      'Maharashtra',    '411001', 1),
    (7,  'Home', 'TC 45/220, Vazhuthacaud',                      'Kochi',     'Kerala',         '695001', 1),
    (8,  'Home', 'Plot 45, Jubilee Hills, Road No. 36',          'Hyderabad', 'Telangana',      '500033', 1),
    (9,  'Home', 'C-12, Vaishali Nagar',                         'Jaipur',    'Rajasthan',      '302021', 1),
    (10, 'Home', '8B, Lake Town, Block A',                       'Kolkata',   'West Bengal',    '700089', 1),
    (11, 'Home', 'TC 15/1702, Nalanchira',                       'Trivandrum','Kerala',         '695015', 1),
    (12, 'Home', '35, Rathinapuri, Podanur Road',                'Coimbatore','Tamil Nadu',     '641023', 1),
    (13, 'Home', '7/1, Hazratganj',                              'Lucknow',   'Uttar Pradesh',  '226001', 1),
    (14, 'Home', '124, Sector 8-C',                              'Chandigarh','Punjab',         '160008', 1),
    (15, 'Home', 'E-5, Arera Colony',                            'Bhopal',    'Madhya Pradesh', '462016', 1),
    (16, 'Home', '12, Dharampeth Extension',                     'Nagpur',    'Maharashtra',    '440010', 1),
    (17, 'Home', '78, Ghod Dod Road, Near Athwa Lines',          'Surat',     'Gujarat',        '395001', 1),
    (18, 'Home', '67, Sigra, Near DLW',                          'Varanasi',  'Uttar Pradesh',  '221001', 1),
    (19, 'Home', '3, Alkapuri Society',                          'Vadodara',  'Gujarat',        '390007', 1),
    (20, 'Home', 'Near Gandhi Maidan, Patna City',               'Patna',     'Bihar',          '800007', 1),
    (21, 'Home', '44, MVP Colony, Sector 10',                    'Visakhapatnam','Andhra Pradesh','530017',1),
    (22, 'Home', 'Kanke Road, Near Gandhi Ashram',               'Ranchi',    'Jharkhand',      '834008', 1),
    (23, 'Home', '12, Golden Avenue, Near Railway Station',      'Amritsar',  'Punjab',         '143001', 1),
    (24, 'Home', '202, Sapna Sangeeta Road, Scheme 78',          'Indore',    'Madhya Pradesh', '452001', 1),
    (25, 'Home', '5A, Hindustan Park',                           'Kolkata',   'West Bengal',    '700029', 1),
    (26, 'Home', 'Bunts Hostel Road, Kodialbail',                'Mangaluru', 'Karnataka',      '575003', 1),
    (27, 'Home', '401, Nariman Point, near NCPA',                'Mumbai',    'Maharashtra',    '400021', 1),
    (28, 'Home', 'Near Umaid Bhawan Palace, Circuit House Area', 'Jodhpur',   'Rajasthan',      '342006', 1),
    (29, 'Home', '14, Anna Nagar, Bypass Road',                  'Madurai',   'Tamil Nadu',     '625020', 1),
    (30, 'Home', '23, College Road, Nashik West',                'Nashik',    'Maharashtra',    '422005', 1);

    PRINT '  ✓ CustomerAddresses (31 rows)';
END
GO

-- ============================================================
-- PRODUCTS (50 across all categories)
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM dbo.Products)
BEGIN
    -- Electronics – Mobiles (Category 9)
    INSERT INTO dbo.Products (ProductName, SKU, CategoryId, BrandId, SupplierId, CostPrice, SellingPrice, MRP, StockQty, ReorderLevel, Weight, Unit, GSTRate, Rating, ReviewCount, IsFeatured) VALUES
    ('Samsung Galaxy S23 Ultra 256GB',  'SAM-S23U-256',  9,  1, 1,  70000, 94999, 109999, 45, 5, 0.234, 'piece', 18, 4.50, 1250, 1),
    ('Apple iPhone 14 128GB',           'APL-IP14-128',  9,  2, 1,  65000, 79999,  89999, 30, 5, 0.172, 'piece', 18, 4.70, 2100, 1),
    ('OnePlus 11 5G 256GB',             'OPL-11-256',    9,  3, 1,  42000, 56999,  61999, 60, 8, 0.205, 'piece', 18, 4.30, 890,  0),
    ('Xiaomi Redmi Note 12 Pro 128GB',  'XMI-RN12P-128', 9,  4, 1,  18000, 24999,  28999,120, 15,0.187, 'piece', 18, 4.20, 3400, 0),
    ('Samsung Galaxy A54 128GB',        'SAM-A54-128',   9,  1, 1,  28000, 38999,  42999, 75, 10,0.202, 'piece', 18, 4.10, 760,  0),

    -- Electronics – Laptops (Category 10)
    ('Dell XPS 15 Core i7 16GB 512GB',  'DEL-XPS15-I7',  10, 7, 1,  85000, 129999, 139999, 20, 3, 1.860, 'piece', 18, 4.60, 540, 1),
    ('HP Pavilion 15 Core i5 8GB 256GB','HP-PAV15-I5',   10, 8, 1,  45000,  65999,  72999, 35, 5, 1.750, 'piece', 18, 4.20, 820, 0),
    ('Lenovo ThinkPad E14 Core i5',     'LEN-TP-E14-I5', 10, 9, 1,  52000,  74999,  82999, 25, 4, 1.620, 'piece', 18, 4.40, 410, 0),
    ('Apple MacBook Air M2 8GB 256GB',  'APL-MBA-M2',    10, 2, 1,  88000, 114999, 119900, 15, 2, 1.240, 'piece', 18, 4.80, 980, 1),

    -- Electronics – Audio (Category 11)
    ('Sony WH-1000XM5 Noise Cancelling','SON-WH1000XM5', 11, 5, 3,  18000,  27999,  29990, 50, 8, 0.250, 'piece', 18, 4.70, 1840, 1),
    ('JBL Flip 6 Portable Speaker',     'JBL-FLIP6',     11, 21,3,   5500,   8999,   9999, 80, 12,0.550, 'piece', 18, 4.50, 2200, 0),
    ('boAt Airdopes 141 TWS',           'BOAT-AD141',    11, 20,3,    700,   1499,   2499,200, 30,0.042, 'piece', 18, 4.00, 8900, 0),
    ('Sony Linkbuds S Wireless',        'SON-LBS',       11, 5, 3,  10000,  16999,  19990, 40, 6, 0.052, 'piece', 18, 4.30, 560, 0),

    -- Fashion – Men (Category 13)
    ('Allen Solly Men''s Formal Shirt',    'AS-MF-SHIRT-M', 13,13, 2, 500,  1299,  1599, 150, 20,0.200, 'piece', 5,  4.10, 320, 0),
    ('Peter England Men''s Chinos 32',     'PE-CHN-32',     13,14, 2, 700,  1899,  2299, 100, 15,0.450, 'piece', 5,  4.20, 190, 0),
    ('Nike Dri-FIT Running T-Shirt M',    'NK-DRFT-M',     13,10, 2, 800,  1999,  2499,  80, 12,0.180, 'piece', 5,  4.50, 580, 1),
    ('Adidas Tiro 23 Track Pants M',      'AD-TIRO23-M',   13,11, 2, 900,  2499,  2999,  60, 10,0.350, 'piece', 5,  4.30, 240, 0),

    -- Fashion – Women (Category 14)
    ('Women''s Printed Kurti Set M',      'KRT-PRT-M',     14, NULL,2,400,   899,  1299, 200, 25,0.350, 'piece', 5,  4.00, 650, 0),
    ('Levi''s Women''s Slim Fit Jeans 28', 'LEV-WJN-28',   14, NULL,2,900,  2199,  2799, 120, 15,0.480, 'piece', 5,  4.20, 430, 0),

    -- Fashion – Footwear (Category 15)
    ('Nike Air Max 270 Men UK-9',         'NK-AM270-9',    15,10, 2,4500,   8999, 11999,  55,  8,0.850, 'pair',  5,  4.60, 720, 1),
    ('Adidas Ultraboost 22 Men UK-9',     'AD-UB22-9',     15,11, 2,5000,  11999, 14999,  40,  6,0.720, 'pair',  5,  4.50, 380, 1),
    ('Bata Men''s Formal Shoes UK-9',     'BAT-FML-9',     15,15, 2,1200,   2499,  2999,  90, 12,0.950, 'pair',  5,  3.90, 510, 0),

    -- Home – Kitchen (Category 16)
    ('Prestige Iris 750W Mixer Grinder 3J','PRE-IRIS-750',  16,16, 4,2500,   4499,  5999, 100, 15,3.200, 'piece', 18, 4.30, 1100, 0),
    ('Philips 2300W Induction Cooktop',   'PHL-IC-2300',   16,17, 4,2800,   4999,  6499,  70, 10,2.100, 'piece', 18, 4.20, 890,  0),
    ('LG 30L Microwave Oven',             'LG-MWO-30L',    16, 6, 4,7500,  12999, 15999,  45,  6,14.00, 'piece', 18, 4.40, 560,  0),

    -- Home – Furniture (Category 17)
    ('IKEA KALLAX Shelf Unit 4x4',        'IKEA-KLX-4X4',  17,23, 4,8000,  14999, 17999,  20,  3,40.00, 'piece', 18, 4.10, 280, 0),
    ('IKEA POANG Armchair Birch',         'IKEA-PNG-ARM',   17,23, 4,6000,  12499, 14999,  15,  2,9.000, 'piece', 18, 4.30, 190, 0),

    -- Books – Tech (Category 18)
    ('Python for Data Analysis 3rd Ed',   'BK-PY-DA-3E',   18,24, 5,  500,   2999,  3999, 300, 30,0.800, 'piece',  0, 4.60, 1240, 1),
    ('Clean Code by Robert Martin',       'BK-CC-MART',     18,24, 5,  400,   2199,  2799, 250, 25,0.650, 'piece',  0, 4.70, 1850, 1),
    ('System Design Interview Vol 2',     'BK-SDI-V2',      18,24, 5,  550,   2799,  3499, 200, 20,0.720, 'piece',  0, 4.50, 980,  0),
    ('The Pragmatic Programmer 20th Ann', 'BK-PPG-20A',     18,24, 5,  480,   2499,  2999, 180, 20,0.600, 'piece',  0, 4.80, 2100, 1),

    -- Sports – Fitness (Category 19)
    ('Boldfit Gym Gloves L/XL',           'BF-GGL-LXL',    19, NULL,6,  300,    699,   999, 500, 50,0.120, 'pair',  18, 4.10, 2800, 0),
    ('Nivia Yoga Mat 6mm Non-Slip',       'NIV-YGM-6MM',   19, NULL,6,  600,   1299,  1799, 400, 40,0.900, 'piece', 18, 4.20, 1900, 0),
    ('Amazon Basics Rubber Dumbbell 5kg', 'AMZ-DMB-5KG',   19, NULL,6, 1200,   1999,  2499, 300, 30,5.000, 'piece', 18, 4.30, 1400, 0),
    ('Boldfit Resistance Band Set 5pc',   'BF-RBS-5PC',    19, NULL,6,  400,    849,  1299, 350, 35,0.300, 'set',   18, 4.00, 1100, 0),

    -- Beauty – Skincare (Category 20)
    ('Mamaearth Vitamin C Face Wash 100g','ME-VCFW-100',    20,19, 7,  150,    349,   499, 600, 50,0.120, 'piece', 18, 4.30, 4200, 1),
    ('Lakme 9to5 Mousse Foundation N',    'LAK-9T5-MF-N',  20,18, 7,  350,    699,   899, 400, 40,0.030, 'piece', 18, 4.00, 2800, 0),
    ('Neutrogena Sunscreen SPF 50 88ml',  'NEU-SPF50-88',  20, NULL,7,  450,    899,  1099, 350, 30,0.100, 'piece', 18, 4.40, 3100, 1),
    ('Mamaearth Ubtan Face Mask 100ml',   'ME-UFM-100',    20,19, 7,  120,    349,   499, 500, 40,0.110, 'piece', 18, 4.20, 2200, 0),

    -- Beauty – Supplements (Category 21)
    ('MuscleBlaze Whey Protein 1kg Choco','MB-WP-1KG-CH',  21, NULL,7, 1200,   1999,  2499, 200, 20,1.100, 'piece', 18, 4.40, 3800, 1),
    ('HealthKart HK Vitals Multivitamin', 'HK-VIT-MULTI',  21, NULL,7,  300,    599,   799, 400, 30,0.180, 'piece', 18, 4.10, 2100, 0),

    -- Grocery (Category 7)
    ('Tata Tea Premium 250g',             'TATA-TP-250',    7, NULL,8,   80,    179,   199, 1000,100,0.260,'piece',  5, 4.50, 6800, 0),
    ('Amul Butter 500g',                  'AMUL-BUT-500',   7, NULL,8,  200,    270,   290, 800, 80, 0.520,'piece',  5, 4.70, 5200, 0),
    ('Aashirvaad Atta 5kg',               'AASH-AT-5KG',    7, NULL,8,  220,    285,   310, 600, 60, 5.000,'piece',  5, 4.60, 7400, 0),
    ('Fortune Sunlite Refined Oil 2L',    'FORT-SRO-2L',    7, NULL,8,  220,    260,   290, 700, 70, 2.050,'piece',  5, 4.30, 4100, 0);

    PRINT '  ✓ Products (50 rows)';
END
GO

-- ============================================================
-- COUPONS
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM dbo.Coupons)
BEGIN
    INSERT INTO dbo.Coupons (CouponCode, Description, DiscountType, DiscountValue, MinOrderValue, MaxDiscount, UsageLimit, UsedCount, ValidFrom, ValidTo) VALUES
    ('WELCOME10',   'Welcome discount for new customers',  'Percent', 10, 299,   200,  NULL,  45,  DATEADD(DAY,-365,SYSUTCDATETIME()), DATEADD(DAY,30,SYSUTCDATETIME())),
    ('FLAT200',     'Flat Rs.200 off on orders above 999', 'Fixed',  200, 999,  NULL,  500,  123, DATEADD(DAY,-90, SYSUTCDATETIME()), DATEADD(DAY,60,SYSUTCDATETIME())),
    ('SAVE15',      '15% off on electronics',              'Percent', 15, 4999, 1500, 200,   78,  DATEADD(DAY,-60, SYSUTCDATETIME()), DATEADD(DAY,30,SYSUTCDATETIME())),
    ('FASHION20',   '20% off on fashion items',            'Percent', 20, 999,   500, 300,   156, DATEADD(DAY,-30, SYSUTCDATETIME()), DATEADD(DAY,90,SYSUTCDATETIME())),
    ('FESTIVE500',  'Festive season - Rs.500 off',         'Fixed',  500, 2999, NULL, 1000,  389, DATEADD(DAY,-20, SYSUTCDATETIME()), DATEADD(DAY,10,SYSUTCDATETIME())),
    ('PLATINUM25',  '25% off for Platinum members',        'Percent', 25, 999,  2000, NULL,  67,  DATEADD(DAY,-180,SYSUTCDATETIME()), DATEADD(DAY,180,SYSUTCDATETIME())),
    ('FREESHIP',    'Free shipping on all orders',         'Fixed',    0, 199,  NULL, NULL,  234, DATEADD(DAY,-365,SYSUTCDATETIME()), DATEADD(DAY,365,SYSUTCDATETIME())),
    ('BOOK50',      'Rs.50 off on books',                  'Fixed',   50, 199,  NULL, NULL,  89,  DATEADD(DAY,-60, SYSUTCDATETIME()), DATEADD(DAY,60,SYSUTCDATETIME()));

    PRINT '  ✓ Coupons (8 rows)';
END
GO

-- ============================================================
-- ORDERS + ORDER ITEMS (100 orders with realistic patterns)
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM dbo.Orders)
BEGIN
    -- Helper: generate orders across past 12 months
    DECLARE @i INT = 1;
    DECLARE @OrderNum INT = 1001;
    DECLARE @CustId INT, @ProdId INT, @AddrId INT, @CoupId INT;
    DECLARE @Qty INT, @Price DECIMAL(12,2), @Sub DECIMAL(14,2);
    DECLARE @Status NVARCHAR(30), @Pay NVARCHAR(30), @PayMethod NVARCHAR(50);
    DECLARE @DaysAgo INT;

    WHILE @i <= 120
    BEGIN
        -- Rotate customers (weighted towards active spenders)
        SET @CustId = CASE
            WHEN @i % 7 = 0 THEN 1  -- Priya (Platinum) – heavy buyer
            WHEN @i % 7 = 1 THEN 6  -- Arjun (Platinum)
            WHEN @i % 7 = 2 THEN 12 -- Karthik (Platinum)
            WHEN @i % 7 = 3 THEN 2  -- Rahul (Gold)
            WHEN @i % 7 = 4 THEN 8  -- Vikram (Gold)
            WHEN @i % 7 = 5 THEN ((@i % 20) + 3)
            ELSE ((@i % 30) + 1)
        END;
        IF @CustId > 30 SET @CustId = (@CustId % 30) + 1;

        -- Days ago (spread over 365 days)
        SET @DaysAgo = ((@i * 3) % 365) + 1;

        -- Address
        SELECT TOP 1 @AddrId = AddressId FROM dbo.CustomerAddresses WHERE CustomerId = @CustId AND IsDefault = 1;

        -- Coupon (30% of orders use coupons)
        SET @CoupId = CASE WHEN @i % 3 = 0 THEN (@i % 8) + 1 ELSE NULL END;

        -- Status based on age
        SET @Status = CASE
            WHEN @DaysAgo > 30  THEN CASE WHEN @i % 10 = 0 THEN 'Cancelled'
                                          WHEN @i % 15 = 0 THEN 'Returned'
                                          ELSE 'Delivered' END
            WHEN @DaysAgo > 10  THEN CASE WHEN @i % 8 = 0 THEN 'Cancelled' ELSE 'Delivered' END
            WHEN @DaysAgo > 5   THEN 'Shipped'
            WHEN @DaysAgo > 2   THEN 'Confirmed'
            ELSE 'Pending'
        END;

        -- Payment method
        SET @PayMethod = CASE @i % 5
            WHEN 0 THEN 'UPI'
            WHEN 1 THEN 'CreditCard'
            WHEN 2 THEN 'DebitCard'
            WHEN 3 THEN 'NetBanking'
            ELSE 'COD'
        END;
        SET @Pay = CASE WHEN @Status IN ('Pending') AND @PayMethod <> 'COD' THEN 'Pending' ELSE 'Paid' END;
        IF @Status = 'Cancelled' SET @Pay = CASE WHEN @i % 3 = 0 THEN 'Refunded' ELSE 'Pending' END;

        INSERT INTO dbo.Orders
            (OrderNumber, CustomerId, AddressId, CouponId, OrderStatus, PaymentStatus, PaymentMethod,
             SubTotal, DiscountAmount, ShippingCharge, TaxAmount, TotalAmount, CouponDiscount,
             OrderedAt, ConfirmedAt, ShippedAt, DeliveredAt, CancelledAt)
        VALUES (
            'ORD-2025-' + RIGHT('00000' + CAST(@OrderNum AS NVARCHAR), 5),
            @CustId, @AddrId, @CoupId, @Status, @Pay, @PayMethod,
            0, 0, CASE WHEN @i % 4 = 0 THEN 0 ELSE 49 END, 0, 0, 0,
            DATEADD(DAY, -@DaysAgo, SYSUTCDATETIME()),
            CASE WHEN @Status NOT IN ('Pending') THEN DATEADD(DAY, -@DaysAgo+1, SYSUTCDATETIME()) ELSE NULL END,
            CASE WHEN @Status IN ('Shipped','Delivered') THEN DATEADD(DAY, -@DaysAgo+3, SYSUTCDATETIME()) ELSE NULL END,
            CASE WHEN @Status = 'Delivered' THEN DATEADD(DAY, -@DaysAgo+7, SYSUTCDATETIME()) ELSE NULL END,
            CASE WHEN @Status = 'Cancelled' THEN DATEADD(DAY, -@DaysAgo+1, SYSUTCDATETIME()) ELSE NULL END
        );

        -- Insert 1-4 items per order
        DECLARE @ItemCount INT = (@i % 4) + 1;
        DECLARE @j INT = 1;
        DECLARE @OrderId INT = SCOPE_IDENTITY();
        DECLARE @TotalSub DECIMAL(14,2) = 0;
        DECLARE @TotalTax DECIMAL(12,2) = 0;

        WHILE @j <= @ItemCount
        BEGIN
            SET @ProdId = ((@i + @j * 7) % 50) + 1;
            SET @Qty = (@j % 3) + 1;
            SELECT @Price = SellingPrice FROM dbo.Products WHERE ProductId = @ProdId;
            SET @Sub = @Price * @Qty;
            SET @TotalSub = @TotalSub + @Sub;
            SET @TotalTax = @TotalTax + ROUND(@Sub * 0.18, 2);

            INSERT INTO dbo.OrderItems (OrderId, ProductId, Quantity, UnitPrice, DiscountPct, TaxPct)
            VALUES (@OrderId, @ProdId, @Qty, @Price, CASE WHEN @j % 4 = 0 THEN 5 ELSE 0 END, 18);
            SET @j = @j + 1;
        END

        -- Update order totals
        DECLARE @Disc DECIMAL(12,2) = CASE WHEN @CoupId IS NOT NULL THEN ROUND(@TotalSub * 0.10, 2) ELSE 0 END;
        DECLARE @Ship DECIMAL(10,2) = CASE WHEN @i % 4 = 0 THEN 0 ELSE 49 END;
        UPDATE dbo.Orders
        SET SubTotal       = @TotalSub,
            DiscountAmount = 0,
            CouponDiscount = @Disc,
            TaxAmount      = @TotalTax,
            ShippingCharge = @Ship,
            TotalAmount    = @TotalSub - @Disc + @Ship + @TotalTax
        WHERE OrderId = @OrderId;

        SET @i = @i + 1;
        SET @OrderNum = @OrderNum + 1;
    END

    PRINT '  ✓ Orders (120 rows) + OrderItems';
END
GO

-- ============================================================
-- PAYMENTS
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM dbo.Payments)
BEGIN
    INSERT INTO dbo.Payments (OrderId, Amount, Method, Status, TransactionRef, Gateway, PaidAt)
    SELECT
        o.OrderId,
        o.TotalAmount,
        o.PaymentMethod,
        o.PaymentStatus,
        'TXN' + RIGHT('000000' + CAST(o.OrderId AS NVARCHAR), 8) + 'IN',
        CASE o.PaymentMethod
            WHEN 'UPI'        THEN 'Razorpay'
            WHEN 'CreditCard' THEN 'Stripe'
            WHEN 'DebitCard'  THEN 'Razorpay'
            WHEN 'NetBanking' THEN 'CCAvenue'
            ELSE NULL
        END,
        CASE WHEN o.PaymentStatus = 'Paid' THEN DATEADD(HOUR, 1, o.OrderedAt) ELSE NULL END
    FROM dbo.Orders o
    WHERE o.PaymentMethod <> 'COD';

    PRINT '  ✓ Payments';
END
GO

-- ============================================================
-- SHIPMENTS
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM dbo.Shipments)
BEGIN
    INSERT INTO dbo.Shipments (OrderId, Carrier, TrackingNumber, EstDelivery, ShippedAt, DeliveredAt, Status)
    SELECT
        o.OrderId,
        CASE o.OrderId % 4
            WHEN 0 THEN 'Delhivery'
            WHEN 1 THEN 'BlueDart'
            WHEN 2 THEN 'Ekart'
            ELSE 'DTDC'
        END,
        'TRK' + RIGHT('00000000' + CAST(o.OrderId AS NVARCHAR), 10),
        CAST(DATEADD(DAY, 5, o.OrderedAt) AS DATE),
        o.ShippedAt,
        o.DeliveredAt,
        CASE o.OrderStatus
            WHEN 'Delivered' THEN 'Delivered'
            WHEN 'Shipped'   THEN 'InTransit'
            WHEN 'Cancelled' THEN 'Failed'
            ELSE 'Pending'
        END
    FROM dbo.Orders o
    WHERE o.OrderStatus IN ('Shipped','Delivered','Cancelled');

    PRINT '  ✓ Shipments';
END
GO

-- ============================================================
-- PRODUCT REVIEWS
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM dbo.ProductReviews)
BEGIN
    INSERT INTO dbo.ProductReviews (ProductId, CustomerId, OrderId, Rating, Title, ReviewText, IsVerified, IsApproved, HelpfulCount, CreatedAt) VALUES
    (1, 1, 1,  5, 'Absolutely love it!',       'Best phone I''ve ever used. Camera is outstanding, battery lasts all day.', 1, 1, 234, DATEADD(DAY,-50, SYSUTCDATETIME())),
    (1, 2, 8,  4, 'Great phone, minor issues',  'Excellent performance but heats slightly during gaming. Otherwise perfect.', 1, 1, 89,  DATEADD(DAY,-40, SYSUTCDATETIME())),
    (2, 6, 22, 5, 'Worth every rupee',           'iOS ecosystem is seamless. Face ID is super fast. Battery lasts great.', 1, 1, 312, DATEADD(DAY,-35, SYSUTCDATETIME())),
    (3, 3, 15, 4, 'Fast and smooth',             'Snapdragon 8 Gen 2 is blazing fast. Oxygen OS is clean and fast.',  1, 1, 67, DATEADD(DAY,-45, SYSUTCDATETIME())),
    (4, 5, 30, 4, 'Value for money king',        'MIUI has ads but performance is great for the price. Good camera too.',1, 1, 145, DATEADD(DAY,-25, SYSUTCDATETIME())),
    (6, 8, 11, 5, 'Best laptop for developers', 'OLED display is gorgeous. i7 handles everything thrown at it smoothly.', 1, 1, 178, DATEADD(DAY,-60, SYSUTCDATETIME())),
    (9, 1, 5,  5, 'MacBook is unbeatable',       'M2 chip performance is insane. Battery life of 15+ hours is real!',   1, 1, 421, DATEADD(DAY,-55, SYSUTCDATETIME())),
    (10,2, 18, 5, 'ANC is top class',            'WH-1000XM5 is the best ANC headphone I''ve used. Sony delivered!',   1, 1, 289, DATEADD(DAY,-42, SYSUTCDATETIME())),
    (11,7, 25, 4, 'Solid Bluetooth speaker',     'Rich sound, great bass. Waterproof as advertised. Slightly pricey.',  1, 1, 134, DATEADD(DAY,-38, SYSUTCDATETIME())),
    (12,4, 33, 4, 'Perfect budget earbuds',      'Sound quality is good. Connectivity is stable. Battery life is excellent.',1,1,567, DATEADD(DAY,-30, SYSUTCDATETIME())),
    (28,3, 40, 5, 'Bible for data scientists',   'Wes McKinney explains pandas brilliantly. Essential for anyone in data.',1,1,389, DATEADD(DAY,-90, SYSUTCDATETIME())),
    (29,9, 45, 5, 'Changed how I write code',    'Uncle Bob''s principles are timeless. Every developer should read this.', 1,1,512, DATEADD(DAY,-85, SYSUTCDATETIME())),
    (30,12,50, 5, 'Must-read for SDE interviews','Covers all system design patterns with clear diagrams. Highly recommended.',1,1,298,DATEADD(DAY,-70, SYSUTCDATETIME())),
    (35,5, 55, 4, 'Good face wash',              'Brightens skin noticeably. Mild formula, great for daily use.',          1,1,234, DATEADD(DAY,-20, SYSUTCDATETIME())),
    (40,10,60, 5, 'Best protein supplement',     'Mixes well, tastes great. Chocolate flavor is delicious. Results showing.',1,1,445,DATEADD(DAY,-35, SYSUTCDATETIME())),
    (23,8, 65, 4, 'Good mixer grinder',          'Sturdy build. All 3 jars are well-made. Motor is powerful enough.',      1,1,178, DATEADD(DAY,-50, SYSUTCDATETIME())),
    (19,6, 70, 5, 'Nike never disappoints',      'Air Max 270 is super comfortable for daily wear. Cushioning is great.',  1,1,312, DATEADD(DAY,-45, SYSUTCDATETIME())),
    (20,1, 75, 5, 'Best running shoe',           'Ultraboost energy return is phenomenal. Worth the premium price.',      1,1,267, DATEADD(DAY,-40, SYSUTCDATETIME()));

    PRINT '  ✓ ProductReviews (18 rows)';
END
GO

-- ============================================================
-- WISHLIST
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM dbo.Wishlist)
BEGIN
    INSERT INTO dbo.Wishlist (CustomerId, ProductId) VALUES
    (1, 2), (1, 6), (1, 9),
    (2, 1), (2, 10),(2, 28),
    (3, 9), (3, 19),(3, 20),
    (4, 6), (4, 12),
    (5, 2), (5, 35),(5, 40),
    (6, 1), (6, 9),
    (7, 35),(7, 36),
    (8, 6), (8, 10),
    (9, 28),(9, 29),(9, 30),
    (10,1), (10,2),
    (11,35),(11,36),(11,37),
    (12,6), (12,9),
    (13,28),(13,29),
    (14,19),(14,20),
    (15,35),(15,40),
    (16,1), (16,10);

    PRINT '  ✓ Wishlist (36 rows)';
END
GO

-- ============================================================
-- INVENTORY LOG
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM dbo.InventoryLog)
BEGIN
    INSERT INTO dbo.InventoryLog (ProductId, ChangeType, QuantityBefore, QuantityChange, QuantityAfter, Reference, Notes, CreatedBy) VALUES
    (1,  'StockIn',     0,   50,  50,  'PO-2025-001', 'Initial stock from Samsung distributor', 'system'),
    (2,  'StockIn',     0,   35,  35,  'PO-2025-002', 'Initial stock from Apple reseller',      'system'),
    (3,  'StockIn',     0,   65,  65,  'PO-2025-003', 'Initial OnePlus stock',                  'system'),
    (4,  'StockIn',     0,  130, 130,  'PO-2025-004', 'Xiaomi bulk order',                      'system'),
    (6,  'StockIn',     0,   25,  25,  'PO-2025-005', 'Dell XPS direct purchase',               'system'),
    (9,  'StockIn',     0,   20,  20,  'PO-2025-006', 'Apple MacBook batch',                    'system'),
    (28, 'StockIn',     0,  350, 350,  'PO-2025-007', 'Books bulk purchase',                    'system'),
    (29, 'StockIn',     0,  280, 280,  'PO-2025-008', 'Clean Code restock',                     'system'),
    (1,  'StockOut',   50,   -5,  45,  'ORD-2025-01002', 'Order fulfilment',                   'system'),
    (2,  'StockOut',   35,   -5,  30,  'ORD-2025-01008', 'Order fulfilment',                   'system'),
    (3,  'StockOut',   65,   -5,  60,  'ORD-2025-01015', 'Order fulfilment',                   'system'),
    (4,  'StockOut',  130,  -10, 120,  'ORD-2025-01020', 'Order fulfilment',                   'system'),
    (12, 'StockIn',   180,   50, 230,  'PO-2025-009', 'boAt restock due to high demand',        'system'),
    (35, 'StockIn',   550,  100, 650,  'PO-2025-010', 'Mamaearth promotional stock',            'system'),
    (28, 'StockOut',  350,  -50, 300,  'BULK-CORP-01', 'Corporate bulk order for training',     'admin'),
    (6,  'Adjustment', 25,   -5,  20,  'ADJ-2025-001', 'Damaged units removed from stock',     'warehouse'),
    (9,  'Return',     15,    1,  16,  'RET-ORD-01050','Customer return – within policy',       'system'),
    (40, 'StockIn',   180,   50, 230,  'PO-2025-011', 'MuscleBlaze protein restock',            'system');

    PRINT '  ✓ InventoryLog (18 rows)';
END
GO

-- ============================================================
-- UPDATE PRODUCT RATINGS (from reviews)
-- ============================================================
UPDATE p
SET p.Rating      = CAST(r.AvgRating AS DECIMAL(3,2)),
    p.ReviewCount  = r.RvwCount
FROM dbo.Products p
INNER JOIN (
    SELECT ProductId, AVG(CAST(Rating AS FLOAT)) AS AvgRating, COUNT(*) AS RvwCount
    FROM dbo.ProductReviews
    WHERE IsApproved = 1
    GROUP BY ProductId
) r ON r.ProductId = p.ProductId;
PRINT '  ✓ Product ratings updated from reviews';
GO

-- ============================================================
-- UPDATE CUSTOMER TIERS (based on lifetime spend)
-- ============================================================
UPDATE c
SET c.Tier = CASE
    WHEN cs.TotalSpend >= 100000 THEN 'Platinum'
    WHEN cs.TotalSpend >=  50000 THEN 'Gold'
    WHEN cs.TotalSpend >=  10000 THEN 'Silver'
    ELSE 'Bronze'
END
FROM dbo.Customers c
INNER JOIN (
    SELECT CustomerId, ISNULL(SUM(TotalAmount),0) AS TotalSpend
    FROM dbo.Orders
    WHERE OrderStatus NOT IN ('Cancelled','Refunded')
    GROUP BY CustomerId
) cs ON cs.CustomerId = c.CustomerId;
PRINT '  ✓ Customer tiers recalculated';
GO

-- ============================================================
-- VERIFY DATA (summary report)
-- ============================================================
PRINT '';
PRINT '=== Seed Data Summary ===';

SELECT 'Categories'       AS TableName, COUNT(*) AS RowCount FROM dbo.Categories    UNION ALL
SELECT 'Brands',                         COUNT(*)             FROM dbo.Brands         UNION ALL
SELECT 'Suppliers',                      COUNT(*)             FROM dbo.Suppliers      UNION ALL
SELECT 'Customers',                      COUNT(*)             FROM dbo.Customers      UNION ALL
SELECT 'CustomerAddresses',              COUNT(*)             FROM dbo.CustomerAddresses UNION ALL
SELECT 'Products',                       COUNT(*)             FROM dbo.Products        UNION ALL
SELECT 'Coupons',                        COUNT(*)             FROM dbo.Coupons         UNION ALL
SELECT 'Orders',                         COUNT(*)             FROM dbo.Orders          UNION ALL
SELECT 'OrderItems',                     COUNT(*)             FROM dbo.OrderItems      UNION ALL
SELECT 'Payments',                       COUNT(*)             FROM dbo.Payments        UNION ALL
SELECT 'Shipments',                      COUNT(*)             FROM dbo.Shipments       UNION ALL
SELECT 'ProductReviews',                 COUNT(*)             FROM dbo.ProductReviews  UNION ALL
SELECT 'Wishlist',                       COUNT(*)             FROM dbo.Wishlist        UNION ALL
SELECT 'InventoryLog',                   COUNT(*)             FROM dbo.InventoryLog;
GO

PRINT '';
PRINT '=== Quick Validation Queries ===';

-- Test 1: JOIN test
SELECT TOP 5 c.FullName, COUNT(o.OrderId) AS Orders, SUM(o.TotalAmount) AS Spend
FROM dbo.Customers c
INNER JOIN dbo.Orders o ON o.CustomerId = c.CustomerId
WHERE o.OrderStatus = 'Delivered'
GROUP BY c.FullName
ORDER BY Spend DESC;

-- Test 2: Multi-table JOIN
SELECT TOP 5 p.ProductName, b.BrandName, cat.CategoryName, SUM(oi.Quantity) AS Sold
FROM dbo.OrderItems oi
INNER JOIN dbo.Products p   ON p.ProductId   = oi.ProductId
INNER JOIN dbo.Brands b     ON b.BrandId     = p.BrandId
INNER JOIN dbo.Categories cat ON cat.CategoryId = p.CategoryId
INNER JOIN dbo.Orders o     ON o.OrderId     = oi.OrderId
WHERE o.OrderStatus NOT IN ('Cancelled','Refunded')
GROUP BY p.ProductName, b.BrandName, cat.CategoryName
ORDER BY Sold DESC;

PRINT '';
PRINT '=== Seed Data COMPLETE ===';
GO
