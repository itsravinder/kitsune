# KITSUNE – SQL Server E-Commerce Database

## Folder Structure

```
database/
└── sqlserver/
    ├── schema.sql   ← All CREATE scripts (Tables → Functions → Stored Procedures → Views)
    ├── seed.sql     ← All INSERT scripts (reference + test data)
    └── README.md    ← This file
```

## How to Apply

### Step 1 — Run schema first
```sql
sqlcmd -S localhost,1433 -U sa -P YourStrong@Passw0rd -d KitsuneDB -i schema.sql
```

### Step 2 — Run seed data
```sql
sqlcmd -S localhost,1433 -U sa -P YourStrong@Passw0rd -d KitsuneDB -i seed.sql
```

### Docker users
```bash
docker exec -i kitsune-sqlserver /opt/mssql-tools18/bin/sqlcmd \
  -S localhost -U sa -P "YourStrong@Passw0rd" -C \
  -d KitsuneDB < database/sqlserver/schema.sql

docker exec -i kitsune-sqlserver /opt/mssql-tools18/bin/sqlcmd \
  -S localhost -U sa -P "YourStrong@Passw0rd" -C \
  -d KitsuneDB < database/sqlserver/seed.sql
```

---

## Schema Overview

### Tables (15)
| Table | Description |
|-------|-------------|
| Categories | 3-level category hierarchy |
| Brands | Product brands |
| Suppliers | Product suppliers |
| Customers | 30 realistic Indian customers |
| CustomerAddresses | Shipping addresses |
| Products | 50 products across 8 categories |
| ProductImages | Product photos |
| Coupons | Discount coupons |
| Orders | 120 orders (365-day history) |
| OrderItems | Line items per order |
| Shipments | Carrier tracking |
| Payments | Payment transactions |
| ProductReviews | Customer reviews |
| Wishlist | Customer wishlists |
| InventoryLog | Stock movement audit |

### Functions (5)
| Function | Description |
|----------|-------------|
| `fn_GetCustomerTotalSpend` | Lifetime spend for a customer |
| `fn_GetProductRevenue` | Total revenue for a product |
| `fn_CalculateDiscount` | Discount amount calculator |
| `fn_GetOrderCount` | Order count for a customer |
| `fn_GetCategoryPath` | Full category breadcrumb path |

### Stored Procedures (9)
| Procedure | Description |
|-----------|-------------|
| `usp_GetCustomerOrders` | Filtered customer orders |
| `usp_GetTopCustomers` | Top N customers by spend |
| `usp_GetProductSalesSummary` | Product sales report |
| `usp_PlaceOrder` | Create a new order |
| `usp_UpdateOrderStatus` | Update order lifecycle |
| `usp_GetInventoryAlerts` | Low stock / out of stock |
| `usp_GetRevenueSummary` | Revenue by Day/Month/Year |
| `usp_SearchProducts` | Full product search |
| `usp_GetCustomerLifetimeValue` | CLV analysis |

### Views (5)
| View | Description |
|------|-------------|
| `vw_OrderSummary` | Orders with customer + item counts |
| `vw_ProductPerformance` | Sales + margin per product |
| `vw_CustomerSummary` | Order stats per customer |
| `vw_CategoryRevenue` | Revenue rolled up by category |
| `vw_DailySalesReport` | Daily revenue dashboard |

---

## NQL → SQL Test Queries

These natural language queries should generate correct SQL via Kitsune:

### Read Mode (SELECT)
```
Show top 10 customers by total spend in the last 30 days
List all products with stock below reorder level
Which category generated the most revenue this month?
Show orders placed today with their customer and total amount
Find all Platinum tier customers who haven't ordered in 60 days
What is the average order value per payment method?
Show the 5 best-rated products with more than 100 reviews
List all delivered orders above ₹10,000
Which brand has the highest average product rating?
Show daily revenue for the last 7 days
```

### Write Mode (ALTER/UPDATE/CREATE)
```
Add a new column 'Pincode' to the Customers table
Update the stock quantity for product SKU 'SAM-S23U-256' to 100
Create a stored procedure to get orders by city
Add an index on Orders table for PaymentMethod column
```

---

## Sample Joins for Testing

```sql
-- 4-table JOIN: customer → orders → items → products
SELECT c.FullName, p.ProductName, oi.Quantity, o.OrderStatus
FROM Customers c
JOIN Orders o      ON o.CustomerId = c.CustomerId
JOIN OrderItems oi ON oi.OrderId   = o.OrderId
JOIN Products p    ON p.ProductId  = oi.ProductId
WHERE o.OrderedAt >= DATEADD(DAY,-30,GETDATE())
ORDER BY o.OrderedAt DESC;

-- Aggregation: revenue by category
SELECT cat.CategoryName, SUM(oi.LineAmount) AS Revenue, COUNT(*) AS Orders
FROM OrderItems oi
JOIN Products p    ON p.ProductId   = oi.ProductId
JOIN Categories cat ON cat.CategoryId = p.CategoryId
JOIN Orders o       ON o.OrderId    = oi.OrderId
WHERE o.OrderStatus = 'Delivered'
GROUP BY cat.CategoryName
ORDER BY Revenue DESC;

-- Window function: rank customers by spend
SELECT FullName, TotalSpend,
       RANK() OVER (ORDER BY TotalSpend DESC) AS Rank
FROM vw_CustomerSummary
WHERE TotalOrders > 0;
```
