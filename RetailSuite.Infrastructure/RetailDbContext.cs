using Microsoft.EntityFrameworkCore;
using RetailSuite.Infrastructure.Email;
using RetailSuite.Infrastructure.Modules.Customer.Entities;
using RetailSuite.Infrastructure.Modules.Identity.Entities;
using RetailSuite.Infrastructure.Modules.Inventory.Entities;
using RetailSuite.Infrastructure.Modules.Receiving.Entities;
using RetailSuite.Infrastructure.Modules.Shipping.Entities;
using RetailSuite.Infrastructure.Modules.Subscriptions.Entities;
using RetailSuite.Infrastructure.Modules.SupplierReturns.Entities;
using RetailSuite.Infrastructure.Modules.Suppliers.Entities;
using RetailSuite.Infrastructure.Modules.Locations.Entities;
using RetailSuite.Infrastructure.Modules.Payments.Entities;
using RetailSuite.Infrastructure.Modules.Tax.Entities;
using RetailSuite.Infrastructure.Modules.Transfers.Entities;
using RetailSuite.Infrastructure.Modules.Wallet.Entities;
using RetailSuite.Infrastructure.Modules.Tenant.Entities;
using RetailSuite.Infrastructure.Payments;
using RetailSuite.Modules.Accounting.Entities;
using RetailSuite.Modules.Catalog.Entities;
using RetailSuite.Modules.Orders.Entities;
using RetailSuite.Shared;

namespace RetailSuite.Infrastructure;

public class RetailDbContext : DbContext
{
    private readonly ITenantContext _tenantContext;

    public RetailDbContext(
        DbContextOptions<RetailDbContext> options,
        ITenantContext tenantContext)
        : base(options)
    {
        _tenantContext = tenantContext;
    }

    private Guid? CurrentTenantId => _tenantContext.TenantId;

    // -----------------------------
    // Catalog
    // -----------------------------
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductImage> ProductImages => Set<ProductImage>();
    public DbSet<ProductVariant> ProductVariants => Set<ProductVariant>();
    public DbSet<Brand> Brands => Set<Brand>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<ProductCategory> ProductCategories => Set<ProductCategory>();
    public DbSet<ProductAttribute> ProductAttributes => Set<ProductAttribute>();
    public DbSet<ProductAttributeValue> ProductAttributeValues => Set<ProductAttributeValue>();
    public DbSet<VariantAttributeValue> VariantAttributeValues => Set<VariantAttributeValue>();

    // -----------------------------
    // Inventory
    // -----------------------------
    public DbSet<InventoryItem> InventoryItems => Set<InventoryItem>();
    public DbSet<InventoryTransaction> InventoryTransactions => Set<InventoryTransaction>();

    // -----------------------------
    // Orders
    // -----------------------------
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<HeldSale> HeldSales => Set<HeldSale>();

    // -----------------------------
    // Shipping methods (storefront)
    // -----------------------------
    public DbSet<ShippingMethod> ShippingMethods => Set<ShippingMethod>();
    public DbSet<Customer> Customers => Set<Customer>();
    // -----------------------------
    // Accounting
    // -----------------------------
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<JournalEntry> JournalEntries => Set<JournalEntry>();
    public DbSet<JournalEntryLine> JournalEntryLines => Set<JournalEntryLine>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<DailyLedgerEntry> DailyLedgerEntries => Set<DailyLedgerEntry>();
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<User> Users => Set<User>();
    public DbSet<UserPermission> UserPermissions => Set<UserPermission>();
    public DbSet<TenantVerificationToken> TenantVerificationTokens => Set<TenantVerificationToken>();
    public DbSet<TenantAuditLog> TenantAuditLogs => Set<TenantAuditLog>();

    // -----------------------------
    // Subscriptions
    // -----------------------------
    public DbSet<SubscriptionPlan> SubscriptionPlans => Set<SubscriptionPlan>();
    public DbSet<TenantSubscription> TenantSubscriptions => Set<TenantSubscription>();
    public DbSet<SubscriptionInvoice> SubscriptionInvoices => Set<SubscriptionInvoice>();
    public DbSet<SubscriptionPayment> SubscriptionPayments => Set<SubscriptionPayment>();

    // -----------------------------
    // Webhook ingestion (idempotency + audit)
    // -----------------------------
    public DbSet<WebhookEvent> WebhookEvents => Set<WebhookEvent>();

    // -----------------------------
    // Suppliers + Receiving (purchase orders)
    // -----------------------------
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<ReceivingOrder> ReceivingOrders => Set<ReceivingOrder>();
    public DbSet<ReceivingOrderItem> ReceivingOrderItems => Set<ReceivingOrderItem>();

    // -----------------------------
    // Supplier returns + credit notes
    // -----------------------------
    public DbSet<SupplierReturn>             SupplierReturns             => Set<SupplierReturn>();
    public DbSet<SupplierReturnItem>         SupplierReturnItems         => Set<SupplierReturnItem>();
    public DbSet<SupplierCreditNote>         SupplierCreditNotes         => Set<SupplierCreditNote>();
    public DbSet<SupplierCreditApplication>  SupplierCreditApplications  => Set<SupplierCreditApplication>();

    // -----------------------------
    // Tax / FBR settings
    // -----------------------------
    public DbSet<TaxSettings> TaxSettings => Set<TaxSettings>();

    // -----------------------------
    // Wallet (customer OTP login + ledger)
    // -----------------------------
    public DbSet<CustomerOtpToken> CustomerOtpTokens => Set<CustomerOtpToken>();

    // -----------------------------
    // Locations (branches / shops)
    // -----------------------------
    public DbSet<Location> Locations => Set<Location>();

    // -----------------------------
    // Inter-location stock transfers
    // -----------------------------
    public DbSet<InventoryTransfer>     InventoryTransfers     => Set<InventoryTransfer>();
    public DbSet<InventoryTransferItem> InventoryTransferItems => Set<InventoryTransferItem>();

    // -----------------------------
    // Order payment intents (storefront gateway flow)
    // -----------------------------
    public DbSet<OrderPaymentIntent> OrderPaymentIntents => Set<OrderPaymentIntent>();

    // -----------------------------
    // Customer extensions: addresses, store credit, loyalty
    // -----------------------------
    public DbSet<CustomerAddress> CustomerAddresses => Set<CustomerAddress>();
    public DbSet<StoreCreditTransaction> StoreCreditTransactions => Set<StoreCreditTransaction>();
    public DbSet<LoyaltyTransaction> LoyaltyTransactions => Set<LoyaltyTransaction>();
    public DbSet<LoyaltySettings> LoyaltySettings => Set<LoyaltySettings>();

    // -----------------------------
    // Notifications
    // -----------------------------
    public DbSet<EmailNotification> EmailNotifications => Set<EmailNotification>();
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(b =>
        {
            b.ToTable("Users");
            b.HasKey(x => x.Id);
            b.HasIndex(x => x.Email);
            b.Property(x => x.Email).IsRequired();
            b.Property(x => x.PasswordHash).IsRequired();
            b.Property(x => x.FullName).HasMaxLength(150);
            b.Property(x => x.IsActive).HasDefaultValue(true);
            b.Property(x => x.MustChangePassword).HasDefaultValue(false);
            b.Property(x => x.IsEmailVerified).HasDefaultValue(false);
        });

        modelBuilder.Entity<UserPermission>(b =>
        {
            b.ToTable("UserPermissions");
            b.HasKey(x => x.Id);
            b.Property(x => x.Code).IsRequired().HasMaxLength(50);
            b.HasIndex(x => new { x.UserId, x.Code }).IsUnique();

            b.HasOne<User>()
                .WithMany()
                .HasForeignKey(p => p.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<User>()
            .HasQueryFilter(u =>
                CurrentTenantId == null || u.TenantId == CurrentTenantId);

        modelBuilder.Entity<Tenant>(b =>
        {
            b.ToTable("Tenants");
            b.HasKey(x => x.Id);
            b.Property(x => x.Name).IsRequired().HasMaxLength(200);
            b.Property(x => x.Subdomain).IsRequired().HasMaxLength(100);
            b.HasIndex(x => x.Subdomain).IsUnique();
            b.Property(x => x.BillingEmail).HasMaxLength(250);
            b.Property(x => x.CountryCode).HasMaxLength(2).HasDefaultValue("PK");
        });

        modelBuilder.Entity<TenantAuditLog>(b =>
        {
            b.ToTable("TenantAuditLogs");
            b.HasKey(x => x.Id);
            b.Property(x => x.PerformedByEmail).IsRequired().HasMaxLength(250);
            b.Property(x => x.Action).IsRequired().HasMaxLength(50);
            b.Property(x => x.Details).IsRequired().HasMaxLength(1000);
            b.HasIndex(x => x.TenantId);
        });

        modelBuilder.Entity<TenantVerificationToken>(b =>
        {
            b.ToTable("TenantVerificationTokens");
            b.HasKey(t => t.Id);

            b.Property(t => t.TokenHash).IsRequired().HasMaxLength(128);
            b.Property(t => t.Purpose).IsRequired();
            b.Property(t => t.ExpiresAt).IsRequired();

            b.HasIndex(t => t.TokenHash).IsUnique();
            b.HasIndex(t => new { t.TenantId, t.UserId, t.Purpose });
            b.HasIndex(t => t.ExpiresAt);
        });

        // =====================================================
        // CATALOG CONFIGURATION
        // =====================================================

        modelBuilder.Entity<Product>(b =>
        {
            b.ToTable("Products");
            b.HasKey(p => p.Id);

            b.Property(p => p.Name)
                .IsRequired()
                .HasMaxLength(200);

            // HTML descriptions can be large — use MAX. Migration must drop the old
            // 2000-char limit on existing rows; data is preserved.
            b.Property(p => p.Description)
                .HasColumnType("nvarchar(max)");

            b.Property(p => p.ShortDescription)
                .HasMaxLength(500);

            b.Property(p => p.Slug)
                .IsRequired()
                .HasMaxLength(160);
            // One product per slug per tenant.
            b.HasIndex(p => new { p.TenantId, p.Slug })
                .IsUnique();

            b.Property(p => p.UnitOfMeasure)
                .IsRequired()
                .HasMaxLength(20)
                .HasDefaultValue("PCS");

            b.Property(p => p.Specs)
                .HasColumnType("nvarchar(max)");

            b.Property(p => p.Tags)
                .HasMaxLength(500);

            b.Property(p => p.ImageUrl)
                .HasMaxLength(500);

            // Brand FK (optional) — no cascade so dropping a brand doesn't kill its products.
            b.HasOne<Brand>()
                .WithMany()
                .HasForeignKey(p => p.BrandId)
                .OnDelete(DeleteBehavior.SetNull);
            b.HasIndex(p => p.BrandId);

            b.HasMany(p => p.Variants)
                .WithOne()
                .HasForeignKey(v => v.ProductId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Brand>(b =>
        {
            b.ToTable("Brands");
            b.HasKey(x => x.Id);

            b.Property(x => x.Name).IsRequired().HasMaxLength(120);
            b.Property(x => x.Slug).IsRequired().HasMaxLength(120);
            b.Property(x => x.Description).HasMaxLength(1000);
            b.Property(x => x.LogoUrl).HasMaxLength(500);

            // Unique slug per tenant — the storefront uses it for /shop/brand/{slug}.
            b.HasIndex(x => new { x.TenantId, x.Slug }).IsUnique();
        });

        modelBuilder.Entity<ProductImage>(b =>
        {
            b.ToTable("ProductImages");
            b.HasKey(i => i.Id);

            b.Property(i => i.RelativePath).IsRequired().HasMaxLength(500);
            b.Property(i => i.MimeType).IsRequired().HasMaxLength(80);

            b.HasOne<Product>()
                .WithMany()
                .HasForeignKey(i => i.ProductId)
                .OnDelete(DeleteBehavior.Cascade);

            b.HasIndex(i => new { i.TenantId, i.ProductId, i.SortOrder });
            b.HasIndex(i => new { i.ProductId, i.IsPrimary });
        });

        modelBuilder.Entity<ProductVariant>(b =>
        {
            b.ToTable("ProductVariants");
            b.HasKey(v => v.Id);

            b.Property(v => v.SKU)
                .IsRequired()
                .HasMaxLength(100);

            b.HasIndex(v => new { v.TenantId, v.SKU })
                .IsUnique();

            b.Property(v => v.Price)
                .HasColumnType("decimal(18,2)");

            b.HasMany(v => v.AttributeValues)
                .WithOne(vav => vav.ProductVariant)
                .HasForeignKey(vav => vav.ProductVariantId);

            modelBuilder.Entity<ProductVariant>()
                        .HasOne(v => v.Product)
                        .WithMany(p => p.Variants)
                        .HasForeignKey(v => v.ProductId)
                        .OnDelete(DeleteBehavior.Restrict);

            b.Property(v => v.CostPrice)
                .HasColumnType("decimal(18,2)");

            b.Property(v => v.TaxRate)
                .HasColumnType("decimal(5,4)")
                .HasDefaultValue(0m);

            // AverageCost is the rolling weighted-average cost of the variant's
            // inventory. Explicit precision so SQL Server doesn't silently
            // truncate high-value items (18 total digits, 4 decimal places —
            // matches InventoryItem.AverageCost's precision).
            b.Property(v => v.AverageCost)
                .HasColumnType("decimal(18,4)");
        });

        modelBuilder.Entity<Category>(b =>
        {
            b.ToTable("Categories");
            b.HasKey(c => c.Id);

            b.Property(c => c.Name)
                .IsRequired()
                .HasMaxLength(200);

            b.Property(c => c.Slug)
                .IsRequired()
                .HasMaxLength(200);

            // Slug unique within a tenant — drives /shop/c/{slug} URLs.
            b.HasIndex(c => new { c.TenantId, c.Slug }).IsUnique();

            b.HasOne<Category>()
                .WithMany()
                .HasForeignKey(c => c.ParentCategoryId)
                .OnDelete(DeleteBehavior.Restrict);

            // Index for fetching siblings of a parent quickly.
            b.HasIndex(c => new { c.TenantId, c.ParentCategoryId, c.SortOrder });
        });

        modelBuilder.Entity<ProductCategory>(b =>
        {
            b.ToTable("ProductCategories");
            b.HasKey(pc => new { pc.ProductId, pc.CategoryId });

            // Wire to Product.Categories (the reverse navigation) explicitly.
            // Without this, EF sees Product.Categories un-configured, creates a
            // second relationship for it, and stamps a shadow "ProductId1" FK
            // on ProductCategory. Same reasoning for Category.
            b.HasOne(pc => pc.Product)
                .WithMany(p => p.Categories)
                .HasForeignKey(pc => pc.ProductId);

            b.HasOne(pc => pc.Category)
                .WithMany()
                .HasForeignKey(pc => pc.CategoryId);

            // Mirror the tenant + soft-delete filter from the parent Product,
            // so this junction row is hidden whenever the parent Product is.
            // Fixes the 'required end of a relationship has a filter' warning.
            b.HasQueryFilter(pc =>
                (CurrentTenantId == null || pc.Product.TenantId == CurrentTenantId)
                && !pc.Product.IsDeleted);
        });

        modelBuilder.Entity<ProductAttribute>(b =>
        {
            b.ToTable("ProductAttributes");
            b.HasKey(a => a.Id);

            b.Property(a => a.Name)
                .IsRequired()
                .HasMaxLength(100);
        });

        modelBuilder.Entity<ProductAttributeValue>(b =>
        {
            b.ToTable("ProductAttributeValues");
            b.HasKey(av => av.Id);

            b.Property(av => av.Value)
                .IsRequired()
                .HasMaxLength(100);

            b.HasOne<ProductAttribute>()
                .WithMany()
                .HasForeignKey(av => av.AttributeId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<VariantAttributeValue>(b =>
        {
            b.ToTable("VariantAttributeValues");

            b.HasKey(v => new { v.ProductVariantId, v.ProductAttributeValueId });

            b.HasOne(v => v.ProductVariant)
                .WithMany(pv => pv.AttributeValues)
                .HasForeignKey(v => v.ProductVariantId);

            b.HasOne(v => v.ProductAttributeValue)
                .WithMany()
                .HasForeignKey(v => v.ProductAttributeValueId);

            // Mirror the tenant + soft-delete filter from the parent ProductVariant,
            // so this junction row is hidden whenever the parent variant is.
            // Fixes the 'required end of a relationship has a filter' warning
            // for ProductAttributeValue → VariantAttributeValue.
            b.HasQueryFilter(v =>
                (CurrentTenantId == null || v.ProductVariant.TenantId == CurrentTenantId)
                && !v.ProductVariant.IsDeleted);
        });

        // =====================================================
        // INVENTORY CONFIGURATION
        // =====================================================

        modelBuilder.Entity<InventoryItem>(b =>
        {
            b.ToTable("InventoryItems");
            b.HasKey(i => i.Id);

            // One row per (variant, location). The old unique on (tenant, variant)
            // is replaced with (tenant, variant, location) now that stock is per-location.
            b.HasIndex(i => new { i.TenantId, i.ProductVariantId, i.LocationId })
                .IsUnique();

            b.HasOne<Location>()
                .WithMany()
                .HasForeignKey(i => i.LocationId)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasMany(i => i.Transactions)
                .WithOne()
                .HasForeignKey(t => t.InventoryItemId)
                .OnDelete(DeleteBehavior.Cascade);

            b.Property(i => i.AverageCost)
                .HasColumnType("decimal(18,4)");

            b.Property(i => i.TotalStockValue)
                .HasColumnType("decimal(18,4)");
        });

        modelBuilder.Entity<InventoryTransaction>(b =>
        {
            b.ToTable("InventoryTransactions");
            b.HasKey(t => t.Id);

            b.Property(t => t.QuantityChange)
                .IsRequired();

            b.Property(t => t.TransactionType)
                .IsRequired();

            b.HasIndex(t => new { t.TenantId, t.ProductVariantId });
            b.HasIndex(t => t.CreatedAt);
            b.HasIndex(t => t.LocationId);

            b.HasOne<Location>()
                .WithMany()
                .HasForeignKey(t => t.LocationId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // =====================================================
        // ORDERS CONFIGURATION
        // =====================================================

        modelBuilder.Entity<Customer>(b =>
        {
            b.ToTable("Customers");
            b.HasKey(c => c.Id);

            b.Property(c => c.FirstName).IsRequired().HasMaxLength(150);
            b.Property(c => c.LastName).IsRequired().HasMaxLength(150);
            b.Property(c => c.Email).HasMaxLength(200);
            b.Property(c => c.Phone).HasMaxLength(50);
            b.Property(c => c.UserId).IsRequired();

            // Customer extensions.
            b.Property(c => c.Cnic).HasMaxLength(20);
            b.Property(c => c.Group).HasConversion<int>();
            b.Property(c => c.Notes).HasMaxLength(1000);

            b.HasIndex(c => new { c.TenantId, c.UserId }).IsUnique();
            b.HasIndex(c => new { c.TenantId, c.Phone });
            b.HasIndex(c => new { c.TenantId, c.Cnic });
            b.HasIndex(c => new { c.TenantId, c.Group });
        });

        modelBuilder.Entity<CustomerAddress>(b =>
        {
            b.ToTable("CustomerAddresses");
            b.HasKey(a => a.Id);

            b.Property(a => a.Label).IsRequired().HasMaxLength(50);
            b.Property(a => a.RecipientName).IsRequired().HasMaxLength(200);
            b.Property(a => a.Line1).IsRequired().HasMaxLength(250);
            b.Property(a => a.Line2).HasMaxLength(250);
            b.Property(a => a.City).IsRequired().HasMaxLength(100);
            b.Property(a => a.Province).HasMaxLength(100);
            b.Property(a => a.PostalCode).HasMaxLength(20);
            b.Property(a => a.Country).IsRequired().HasMaxLength(2);
            b.Property(a => a.Phone).HasMaxLength(50);
            b.Property(a => a.DeliveryInstructions).HasMaxLength(500);

            b.HasOne<Customer>()
                .WithMany()
                .HasForeignKey(a => a.CustomerId)
                .OnDelete(DeleteBehavior.Cascade);

            b.HasIndex(a => new { a.TenantId, a.CustomerId });
        });

        modelBuilder.Entity<StoreCreditTransaction>(b =>
        {
            b.ToTable("StoreCreditTransactions");
            b.HasKey(t => t.Id);

            b.Property(t => t.Amount).HasColumnType("decimal(18,2)");
            b.Property(t => t.Currency).IsRequired().HasMaxLength(3);
            b.Property(t => t.Reason).HasConversion<int>();
            b.Property(t => t.Note).HasMaxLength(500);

            b.HasOne<Customer>()
                .WithMany()
                .HasForeignKey(t => t.CustomerId)
                .OnDelete(DeleteBehavior.Cascade);

            b.HasIndex(t => new { t.TenantId, t.CustomerId, t.CreatedAt });
        });

        modelBuilder.Entity<LoyaltySettings>(b =>
        {
            b.ToTable("LoyaltySettings");
            b.HasKey(s => s.Id);

            b.Property(s => s.RupeesPerPoint).HasColumnType("decimal(18,2)");
            b.Property(s => s.PointValueRupees).HasColumnType("decimal(18,2)");
            b.Property(s => s.MaxRedemptionPercentOfOrder).HasColumnType("decimal(5,2)");

            // One settings row per tenant.
            b.HasIndex(s => s.TenantId).IsUnique();
        });

        modelBuilder.Entity<LoyaltyTransaction>(b =>
        {
            b.ToTable("LoyaltyTransactions");
            b.HasKey(t => t.Id);

            b.Property(t => t.Reason).HasConversion<int>();
            b.Property(t => t.RupeesValue).HasColumnType("decimal(18,2)");
            b.Property(t => t.Note).HasMaxLength(500);

            b.HasOne<Customer>()
                .WithMany()
                .HasForeignKey(t => t.CustomerId)
                .OnDelete(DeleteBehavior.Cascade);

            b.HasIndex(t => new { t.TenantId, t.CustomerId, t.CreatedAt });
            b.HasIndex(t => t.OrderId);
        });

        modelBuilder.Entity<Order>(b =>
        {
            b.ToTable("Orders");
            b.HasKey(o => o.Id);

            b.Property(o => o.OrderNumber)
                .IsRequired()
                .HasMaxLength(100);

            b.HasIndex(o => new { o.TenantId, o.OrderNumber })
                .IsUnique();

            b.Property(o => o.TotalAmount)
                .HasColumnType("decimal(18,2)");

            b.Property(o => o.TaxAmount)
                .HasColumnType("decimal(18,2)")
                .HasDefaultValue(0m);

            // Running total of what the customer has paid on this order
            // (POS = one lump; layaway = several payments). Explicit precision
            // so SQL Server doesn't silently truncate.
            b.Property(o => o.PaidAmount)
                .HasColumnType("decimal(18,2)")
                .HasDefaultValue(0m);

            // POS extensions (Sprint B).
            b.Property(o => o.OrderDiscountAmount).HasColumnType("decimal(18,2)").HasDefaultValue(0m);
            b.Property(o => o.StoreCreditRedeemed).HasColumnType("decimal(18,2)").HasDefaultValue(0m);
            b.Property(o => o.LoyaltyRedeemedRupees).HasColumnType("decimal(18,2)").HasDefaultValue(0m);
            b.Property(o => o.LoyaltyPointsRedeemed).HasDefaultValue(0);
            b.Property(o => o.CashierUserId);
            b.HasIndex(o => new { o.TenantId, o.CashierUserId, o.CreatedAt });

            // Online-store extensions (Sprint C).
            b.Property(o => o.Channel).IsRequired().HasMaxLength(20).HasDefaultValue("POS");
            b.Property(o => o.ShippingMethodCode).HasMaxLength(50);
            b.Property(o => o.ShippingAmount).HasColumnType("decimal(18,2)").HasDefaultValue(0m);
            b.Property(o => o.ShippingAddressJson);
            b.Property(o => o.GuestName).HasMaxLength(200);
            b.Property(o => o.GuestPhone).HasMaxLength(50);
            b.Property(o => o.GuestEmail).HasMaxLength(250);
            b.Property(o => o.PaymentMethodCode).HasMaxLength(50);
            b.Property(o => o.FulfillmentStatus).IsRequired().HasMaxLength(30).HasDefaultValue("Pending");

            b.HasIndex(o => new { o.TenantId, o.GuestPhone });
            b.HasIndex(o => new { o.TenantId, o.Channel, o.FulfillmentStatus });

            // Every Order has a Customer — walk-in sales use the tenant's
            // auto-seeded "Walk-in Customer" row instead of a null FK.
            b.HasOne(o => o.Customer)
                    .WithMany()
                    .HasForeignKey(o => o.CustomerId)
                    .OnDelete(DeleteBehavior.Restrict);

            b.HasMany(o => o.Payments)
                  .WithOne()
                  .HasForeignKey(p => p.OrderId)
                  .OnDelete(DeleteBehavior.Cascade);

            b.HasMany(o => o.Items)
                .WithOne()
                .HasForeignKey(i => i.OrderId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<OrderItem>(b =>
        {
            b.ToTable("OrderItems");
            b.HasKey(i => i.Id);

            b.Property(i => i.SKU)
                .IsRequired()
                .HasMaxLength(100);

            b.Property(i => i.UnitPrice)
                .HasColumnType("decimal(18,2)");

            b.Property(i => i.TaxRate)
                .HasColumnType("decimal(5,4)")
                .HasDefaultValue(0m);

            // Per-line discount (Sprint B).
            b.Property(i => i.LineDiscountAmount).HasColumnType("decimal(18,2)").HasDefaultValue(0m);
        });

        modelBuilder.Entity<HeldSale>(b =>
        {
            b.ToTable("HeldSales");
            b.HasKey(h => h.Id);
            b.Property(h => h.Label).IsRequired().HasMaxLength(150);
            b.Property(h => h.CustomerPhone).HasMaxLength(50);
            b.Property(h => h.CartJson).IsRequired();
            b.Property(h => h.OrderDiscountAmount).HasColumnType("decimal(18,2)");
            b.Property(h => h.Notes).HasMaxLength(500);
            b.HasIndex(h => new { h.TenantId, h.CashierUserId, h.CreatedAt });
        });

        modelBuilder.Entity<ShippingMethod>(b =>
        {
            b.ToTable("ShippingMethods");
            b.HasKey(s => s.Id);

            b.Property(s => s.Code).IsRequired().HasMaxLength(50);
            b.Property(s => s.Name).IsRequired().HasMaxLength(150);
            b.Property(s => s.Description).HasMaxLength(500);
            b.Property(s => s.BaseFee).HasColumnType("decimal(18,2)");
            b.Property(s => s.FreeOverAmount).HasColumnType("decimal(18,2)");
            b.Property(s => s.Eta).HasMaxLength(50);

            b.HasIndex(s => new { s.TenantId, s.Code }).IsUnique();
            b.HasIndex(s => new { s.TenantId, s.IsActive, s.SortOrder });
        });

        // =====================================================
        // ACCOUNTING CONFIGURATION
        // =====================================================

        modelBuilder.Entity<Account>(b =>
        {
            b.ToTable("Accounts");
            b.HasKey(a => a.Id);

            b.Property(a => a.Code).IsRequired().HasMaxLength(50);
            b.Property(a => a.Name).IsRequired().HasMaxLength(200);
            b.Property(a => a.IsActive).HasDefaultValue(true);

            b.HasIndex(a => new { a.TenantId, a.Code }).IsUnique();
        });

        modelBuilder.Entity<JournalEntry>(b =>
        {
            b.ToTable("JournalEntries");
            b.HasKey(j => j.Id);

            b.Property(j => j.Description)
                .IsRequired()
                .HasMaxLength(500);

            b.HasMany(j => j.Lines)
                .WithOne()
                .HasForeignKey(l => l.JournalEntryId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<JournalEntryLine>(b =>
        {
            b.ToTable("JournalEntryLines");
            b.HasKey(l => l.Id);

            b.Property(l => l.DebitAmount)
                .HasColumnType("decimal(18,2)");

            b.Property(l => l.CreditAmount)
                .HasColumnType("decimal(18,2)");
        });

        modelBuilder.Entity<Payment>(b =>
        {
            b.ToTable("Payments");
            b.HasKey(p => p.Id);
            b.Property(p => p.Amount)
                .HasColumnType("decimal(18,2)");
            b.Property(p => p.PaymentMethod)
                .IsRequired()
                .HasMaxLength(100);
            b.Property(p => p.TransactionReference)
                .HasMaxLength(200);
        });

        modelBuilder.Entity<DailyLedgerEntry>(b =>
        {
            b.ToTable("DailyLedgerEntries");
            b.HasKey(e => e.Id);
            b.Property(e => e.Amount).HasColumnType("decimal(18,2)");
            b.Property(e => e.Type).HasConversion<int>();
            b.Property(e => e.PartyName).HasMaxLength(200);
            b.Property(e => e.Description).IsRequired().HasMaxLength(500);
            b.HasIndex(e => new { e.TenantId, e.EntryDate });
        });

        // =====================================================
        // EMAIL NOTIFICATIONS CONFIGURATION
        // =====================================================

        modelBuilder.Entity<EmailNotification>(b =>
        {
            b.ToTable("EmailNotifications");
            b.HasKey(e => e.Id);

            b.Property(e => e.ToAddress).IsRequired().HasMaxLength(250);
            b.Property(e => e.Subject).IsRequired().HasMaxLength(300);
            b.Property(e => e.TemplateKey).IsRequired().HasMaxLength(100);
            b.Property(e => e.Body).IsRequired();
            b.Property(e => e.ErrorMessage).HasMaxLength(1000);
            b.Property(e => e.RelatedEntityType).HasMaxLength(100);
            b.Property(e => e.RelatedEntityId).HasMaxLength(100);

            b.HasIndex(e => new { e.TenantId, e.Status });
            b.HasIndex(e => e.CreatedAt);
        });

        // =====================================================
        // SUBSCRIPTIONS CONFIGURATION
        // =====================================================

        modelBuilder.Entity<SubscriptionPlan>(b =>
        {
            b.ToTable("SubscriptionPlans");
            b.HasKey(p => p.Id);

            b.Property(p => p.Code).IsRequired().HasMaxLength(50);
            b.HasIndex(p => p.Code).IsUnique();

            b.Property(p => p.Name).IsRequired().HasMaxLength(150);
            b.Property(p => p.Description).HasMaxLength(1000);
            b.Property(p => p.Currency).IsRequired().HasMaxLength(3);

            b.Property(p => p.MonthlyPrice).HasColumnType("decimal(18,2)");
            b.Property(p => p.YearlyPrice).HasColumnType("decimal(18,2)");

            b.HasIndex(p => p.IsActive);
        });

        modelBuilder.Entity<TenantSubscription>(b =>
        {
            b.ToTable("TenantSubscriptions");
            b.HasKey(s => s.Id);

            b.Property(s => s.PlanCode).IsRequired().HasMaxLength(50);
            b.Property(s => s.Currency).IsRequired().HasMaxLength(3);
            b.Property(s => s.LastPrice).HasColumnType("decimal(18,2)");
            b.Property(s => s.PaymentMethodType).HasMaxLength(20);
            b.Property(s => s.CardBrand).HasMaxLength(30);
            b.Property(s => s.CardLast4).HasMaxLength(4);
            b.Property(s => s.CardHolderName).HasMaxLength(150);
            b.Property(s => s.GatewayCustomerId).HasMaxLength(100);

            b.HasOne<SubscriptionPlan>()
                .WithMany()
                .HasForeignKey(s => s.PlanId)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasIndex(s => new { s.TenantId, s.Status });
            b.HasIndex(s => s.NextBillingAt);
            // One active subscription per tenant — enforce via filtered unique index.
            b.HasIndex(s => s.TenantId)
                .HasFilter("[Status] IN (0, 1, 2, 4)")  // Trialing|Active|PastDue|GracePeriod
                .IsUnique();
        });

        modelBuilder.Entity<SubscriptionInvoice>(b =>
        {
            b.ToTable("SubscriptionInvoices");
            b.HasKey(i => i.Id);

            b.Property(i => i.InvoiceNumber).IsRequired().HasMaxLength(50);
            b.Property(i => i.PlanCode).IsRequired().HasMaxLength(50);
            b.Property(i => i.Reason).HasMaxLength(250);
            b.Property(i => i.Currency).IsRequired().HasMaxLength(3);

            b.Property(i => i.Subtotal).HasColumnType("decimal(18,2)");
            b.Property(i => i.TaxAmount).HasColumnType("decimal(18,2)");
            b.Property(i => i.Total).HasColumnType("decimal(18,2)");
            b.Property(i => i.AmountPaid).HasColumnType("decimal(18,2)");

            b.HasIndex(i => new { i.TenantId, i.InvoiceNumber }).IsUnique();
            b.HasIndex(i => new { i.TenantId, i.Status });
            b.HasIndex(i => i.DueDate);
        });

        modelBuilder.Entity<SubscriptionPayment>(b =>
        {
            b.ToTable("SubscriptionPayments");
            b.HasKey(p => p.Id);

            b.Property(p => p.PaymentMethod).IsRequired().HasMaxLength(50);
            b.Property(p => p.Provider).IsRequired().HasMaxLength(50);
            b.Property(p => p.ProviderTxnRef).HasMaxLength(200);
            b.Property(p => p.Currency).IsRequired().HasMaxLength(3);
            b.Property(p => p.FailureReason).HasMaxLength(500);

            b.Property(p => p.Amount).HasColumnType("decimal(18,2)");

            b.HasOne<SubscriptionInvoice>()
                .WithMany()
                .HasForeignKey(p => p.InvoiceId)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasIndex(p => new { p.TenantId, p.InvoiceId });
            b.HasIndex(p => p.ProviderTxnRef);
        });

        modelBuilder.Entity<WebhookEvent>(b =>
        {
            b.ToTable("WebhookEvents");
            b.HasKey(w => w.Id);

            b.Property(w => w.Provider).IsRequired().HasMaxLength(50);
            b.Property(w => w.ExternalEventId).IsRequired().HasMaxLength(200);
            b.Property(w => w.EventType).HasMaxLength(100);
            b.Property(w => w.RawPayload).IsRequired();
            b.Property(w => w.ProcessingError).HasMaxLength(1000);

            // Idempotency: (Provider, ExternalEventId) is unique.
            b.HasIndex(w => new { w.Provider, w.ExternalEventId }).IsUnique();
            b.HasIndex(w => w.CreatedAt);
        });

        // =====================================================
        // SUPPLIERS + RECEIVING CONFIGURATION
        // =====================================================

        modelBuilder.Entity<Supplier>(b =>
        {
            b.ToTable("Suppliers");
            b.HasKey(s => s.Id);

            b.Property(s => s.Name).IsRequired().HasMaxLength(200);
            b.Property(s => s.ContactPerson).HasMaxLength(150);
            b.Property(s => s.Phone).HasMaxLength(50);
            b.Property(s => s.Email).HasMaxLength(200);
            b.Property(s => s.Address).HasMaxLength(500);
            b.Property(s => s.Notes).HasMaxLength(1000);

            b.HasIndex(s => new { s.TenantId, s.Name });
        });

        modelBuilder.Entity<ReceivingOrder>(b =>
        {
            b.ToTable("ReceivingOrders");
            b.HasKey(r => r.Id);

            b.Property(r => r.OrderNumber).IsRequired().HasMaxLength(50);
            b.Property(r => r.SupplierReference).HasMaxLength(100);
            b.Property(r => r.Notes).HasMaxLength(1000);
            b.Property(r => r.Currency).IsRequired().HasMaxLength(3);

            b.Property(r => r.ExpectedTotal).HasColumnType("decimal(18,2)");
            b.Property(r => r.ReceivedTotal).HasColumnType("decimal(18,2)");

            // OrderNumber must be unique within a tenant.
            b.HasIndex(r => new { r.TenantId, r.OrderNumber }).IsUnique();
            b.HasIndex(r => new { r.TenantId, r.Status });
            b.HasIndex(r => r.SupplierId);
            b.HasIndex(r => r.DestinationLocationId);

            b.HasOne<Supplier>()
                .WithMany()
                .HasForeignKey(r => r.SupplierId)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasOne<Location>()
                .WithMany()
                .HasForeignKey(r => r.DestinationLocationId)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasMany(r => r.Items)
                .WithOne()
                .HasForeignKey(i => i.ReceivingOrderId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ReceivingOrderItem>(b =>
        {
            b.ToTable("ReceivingOrderItems");
            b.HasKey(i => i.Id);

            b.Property(i => i.Sku).IsRequired().HasMaxLength(100);
            b.Property(i => i.Notes).HasMaxLength(500);
            b.Property(i => i.UnitCost).HasColumnType("decimal(18,4)");

            b.HasIndex(i => i.ReceivingOrderId);
            b.HasIndex(i => i.ProductVariantId);

            b.HasOne<ProductVariant>()
                .WithMany()
                .HasForeignKey(i => i.ProductVariantId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // =====================================================
        //  Supplier returns + credit notes
        // =====================================================
        modelBuilder.Entity<SupplierReturn>(b =>
        {
            b.ToTable("SupplierReturns");
            b.HasKey(r => r.Id);

            b.Property(r => r.ReturnNumber).IsRequired().HasMaxLength(50);
            b.Property(r => r.Notes).HasMaxLength(1000);
            b.Property(r => r.Currency).IsRequired().HasMaxLength(3);
            b.Property(r => r.TotalValue).HasColumnType("decimal(18,2)");
            b.Property(r => r.Status).HasConversion<int>();
            b.Property(r => r.Reason).HasConversion<int>();

            b.HasIndex(r => new { r.TenantId, r.ReturnNumber }).IsUnique();
            b.HasIndex(r => new { r.TenantId, r.Status });
            b.HasIndex(r => r.SupplierId);
            b.HasIndex(r => r.SourceReceivingOrderId);

            b.HasOne<Supplier>()
                .WithMany()
                .HasForeignKey(r => r.SupplierId)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasOne<ReceivingOrder>()
                .WithMany()
                .HasForeignKey(r => r.SourceReceivingOrderId)
                .OnDelete(DeleteBehavior.SetNull);

            b.HasMany(r => r.Items)
                .WithOne()
                .HasForeignKey(i => i.SupplierReturnId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SupplierReturnItem>(b =>
        {
            b.ToTable("SupplierReturnItems");
            b.HasKey(i => i.Id);

            b.Property(i => i.Sku).IsRequired().HasMaxLength(100);
            b.Property(i => i.Notes).HasMaxLength(500);
            b.Property(i => i.UnitCost).HasColumnType("decimal(18,4)");

            b.HasIndex(i => i.SupplierReturnId);
            b.HasIndex(i => i.ProductVariantId);

            b.HasOne<ProductVariant>()
                .WithMany()
                .HasForeignKey(i => i.ProductVariantId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<SupplierCreditNote>(b =>
        {
            b.ToTable("SupplierCreditNotes");
            b.HasKey(c => c.Id);

            b.Property(c => c.CreditNoteNumber).IsRequired().HasMaxLength(50);
            b.Property(c => c.Currency).IsRequired().HasMaxLength(3);
            b.Property(c => c.Notes).HasMaxLength(1000);
            b.Property(c => c.Amount).HasColumnType("decimal(18,2)");
            b.Property(c => c.AppliedAmount).HasColumnType("decimal(18,2)");

            b.HasIndex(c => new { c.TenantId, c.CreditNoteNumber }).IsUnique();
            b.HasIndex(c => c.SupplierId);
            b.HasIndex(c => c.SupplierReturnId).IsUnique();

            b.HasOne<Supplier>()
                .WithMany()
                .HasForeignKey(c => c.SupplierId)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasOne<SupplierReturn>()
                .WithMany()
                .HasForeignKey(c => c.SupplierReturnId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<InventoryTransfer>(b =>
        {
            b.ToTable("InventoryTransfers");
            b.HasKey(t => t.Id);
            b.Property(t => t.TransferNumber).IsRequired().HasMaxLength(50);
            b.Property(t => t.Notes).HasMaxLength(1000);
            b.Property(t => t.Currency).IsRequired().HasMaxLength(3);
            b.Property(t => t.TotalValue).HasColumnType("decimal(18,2)");
            b.Property(t => t.Status).HasConversion<int>();

            b.HasIndex(t => new { t.TenantId, t.TransferNumber }).IsUnique();
            b.HasIndex(t => new { t.TenantId, t.Status });
            b.HasIndex(t => t.SourceLocationId);
            b.HasIndex(t => t.DestinationLocationId);

            b.HasOne<Location>()
                .WithMany()
                .HasForeignKey(t => t.SourceLocationId)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasMany(t => t.Items)
                .WithOne()
                .HasForeignKey(i => i.InventoryTransferId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<OrderPaymentIntent>(b =>
        {
            b.ToTable("OrderPaymentIntents");
            b.HasKey(i => i.Id);
            b.Property(i => i.Provider).IsRequired().HasMaxLength(50);
            b.Property(i => i.ProviderTxnId).HasMaxLength(100);
            b.Property(i => i.Currency).IsRequired().HasMaxLength(3);
            b.Property(i => i.AmountDue).HasColumnType("decimal(18,2)");
            b.Property(i => i.QrPayload).HasMaxLength(2000);
            b.Property(i => i.FailureReason).HasMaxLength(500);
            b.Property(i => i.Status).HasConversion<int>();

            b.HasIndex(i => i.OrderId);
            b.HasIndex(i => new { i.Provider, i.ProviderTxnId });
            b.HasIndex(i => i.Status);

            b.HasOne<Order>()
                .WithMany()
                .HasForeignKey(i => i.OrderId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<InventoryTransferItem>(b =>
        {
            b.ToTable("InventoryTransferItems");
            b.HasKey(i => i.Id);
            b.Property(i => i.Sku).IsRequired().HasMaxLength(100);
            b.Property(i => i.Notes).HasMaxLength(500);
            b.Property(i => i.UnitCost).HasColumnType("decimal(18,4)");

            b.HasIndex(i => i.InventoryTransferId);
            b.HasIndex(i => i.ProductVariantId);

            b.HasOne<ProductVariant>()
                .WithMany()
                .HasForeignKey(i => i.ProductVariantId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Location>(b =>
        {
            b.ToTable("Locations");
            b.HasKey(l => l.Id);
            b.Property(l => l.Code).IsRequired().HasMaxLength(20);
            b.Property(l => l.Name).IsRequired().HasMaxLength(150);
            b.Property(l => l.Address).HasMaxLength(500);
            b.Property(l => l.Phone).HasMaxLength(50);
            b.Property(l => l.Notes).HasMaxLength(1000);

            b.HasIndex(l => new { l.TenantId, l.Code }).IsUnique();
            // At most one default per tenant.
            b.HasIndex(l => new { l.TenantId, l.IsDefault })
             .IsUnique()
             .HasFilter("[IsDefault] = 1");
        });

        modelBuilder.Entity<CustomerOtpToken>(b =>
        {
            b.ToTable("CustomerOtpTokens");
            b.HasKey(t => t.Id);
            b.Property(t => t.Phone).IsRequired().HasMaxLength(20);
            b.Property(t => t.CodeHash).IsRequired().HasMaxLength(128);

            b.HasIndex(t => new { t.TenantId, t.Phone, t.CreatedAt });
        });

        modelBuilder.Entity<TaxSettings>(b =>
        {
            b.ToTable("TaxSettings");
            b.HasKey(t => t.Id);
            b.Property(t => t.Ntn).HasMaxLength(20);
            b.Property(t => t.Strn).HasMaxLength(20);
            b.Property(t => t.BusinessNameAsRegistered).HasMaxLength(200);
            b.Property(t => t.RegisteredAddress).HasMaxLength(500);
            b.Property(t => t.InvoicePrefix).IsRequired().HasMaxLength(10);
            b.Property(t => t.DefaultTaxRate).HasColumnType("decimal(5,4)");
            b.Property(t => t.FbrPosId).HasMaxLength(50);
            b.Property(t => t.FbrStatus).HasMaxLength(50);

            // One TaxSettings per tenant.
            b.HasIndex(t => t.TenantId).IsUnique();
        });

        modelBuilder.Entity<Order>(b =>
        {
            b.Property(o => o.InvoiceNumber).HasMaxLength(50);
            b.Property(o => o.SellerNtnSnapshot).HasMaxLength(20);
            b.Property(o => o.SellerStrnSnapshot).HasMaxLength(20);
            b.Property(o => o.SellerBusinessNameSnapshot).HasMaxLength(200);
            b.Property(o => o.SellerAddressSnapshot).HasMaxLength(500);
            b.Property(o => o.FbrInvoiceNumber).HasMaxLength(100);

            // InvoiceNumber must be unique within a tenant when set.
            b.HasIndex(o => new { o.TenantId, o.InvoiceNumber })
             .IsUnique()
             .HasFilter("[InvoiceNumber] IS NOT NULL");
        });

        modelBuilder.Entity<SupplierCreditApplication>(b =>
        {
            b.ToTable("SupplierCreditApplications");
            b.HasKey(a => a.Id);

            b.Property(a => a.Amount).HasColumnType("decimal(18,2)");
            b.Property(a => a.Notes).HasMaxLength(500);

            b.HasIndex(a => a.CreditNoteId);
            b.HasIndex(a => a.ReceivingOrderId);
            b.HasIndex(a => a.SupplierId);

            b.HasOne<SupplierCreditNote>()
                .WithMany()
                .HasForeignKey(a => a.CreditNoteId)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasOne<ReceivingOrder>()
                .WithMany()
                .HasForeignKey(a => a.ReceivingOrderId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // =====================================================
        // GLOBAL TENANT FILTER
        // =====================================================

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(TenantEntity).IsAssignableFrom(entityType.ClrType))
            {
                var method = typeof(RetailDbContext)
                    .GetMethod(nameof(ApplyTenantFilter),
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                    ?.MakeGenericMethod(entityType.ClrType);

                method?.Invoke(this, new object[] { modelBuilder });
            }
        }

        modelBuilder.Ignore<TenantEntity>();

        foreach (var relationship in modelBuilder.Model.GetEntityTypes()
         .SelectMany(e => e.GetForeignKeys()))
        {
            relationship.DeleteBehavior = DeleteBehavior.Restrict;
        }
    }

    private void ApplyTenantFilter<TEntity>(
      ModelBuilder modelBuilder)
      where TEntity : TenantEntity
    {
        modelBuilder.Entity<TEntity>()
            .HasQueryFilter(e =>
                (CurrentTenantId == null || e.TenantId == CurrentTenantId)
                && !e.IsDeleted);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        foreach (var entry in ChangeTracker.Entries<TenantEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                if (_tenantContext.TenantId.HasValue && entry.Entity.TenantId == Guid.Empty)
                    entry.Entity.TenantId = _tenantContext.TenantId.Value;

                // Belt-and-braces: some entity constructors forget to set CreatedAt
                // (e.g. JournalEntry). Fill it in here so downstream date filters
                // don't miss the row. Only fills when still at the default value.
                if (entry.Entity.CreatedAt == default)
                    entry.Entity.CreatedAt = now;

                if (entry.Entity.Id == Guid.Empty)
                    entry.Entity.Id = Guid.NewGuid();
            }
        }

        return base.SaveChangesAsync(cancellationToken);
    }
}
