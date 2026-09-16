using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RetailSuite.Infrastructure;
using RetailSuite.Infrastructure.Modules.Customer.Model;
using RetailSuite.Infrastructure.Seeders;
using RetailSuite.Infrastructure.Modules.Identity.Entities;
using RetailSuite.Infrastructure.Modules.Tenant.Entities;
using RetailSuite.Infrastructure.Modules.Subscriptions.Entities;
using RetailSuite.Infrastructure.Modules.Subscriptions.Services;
using RetailSuite.Shared;

namespace RetailSuite.Api.Controllers;

[ApiController]
[Route("api/tenants")]
[Authorize]
public class TenantsController : ControllerBase
{
    private readonly RetailDbContext             _db;
    private readonly ITenantContext              _tenantContext;
    private readonly ICurrentUserContext         _currentUser;
    private readonly ISubscriptionBillingService _billing;
    private readonly ISubscriptionService        _subs;

    public TenantsController(
        RetailDbContext db,
        ITenantContext tenantContext,
        ICurrentUserContext currentUser,
        ISubscriptionBillingService billing,
        ISubscriptionService subs)
    {
        _db            = db;
        _tenantContext = tenantContext;
        _currentUser   = currentUser;
        _billing       = billing;
        _subs          = subs;
    }

    private async Task LogAuditAsync(Guid tenantId, string action, string details)
    {
        var performerEmail = await _db.Users
            .IgnoreQueryFilters()
            .Where(u => u.Id == _currentUser.UserId)
            .Select(u => u.Email)
            .FirstOrDefaultAsync() ?? "unknown";

        _db.TenantAuditLogs.Add(new TenantAuditLog(tenantId, _currentUser.UserId, performerEmail, action, details));
        await _db.SaveChangesAsync();
    }

    // ---------------------------------------------------------------
    // GET /api/tenants/me  — any authenticated user
    // ---------------------------------------------------------------
    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        var tenantId = _tenantContext.TenantId;

        if (!tenantId.HasValue)
            return Unauthorized(ApiResponse<object>.Fail("Tenant context not available."));

        var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId.Value);

        if (tenant == null)
            return NotFound(ApiResponse<object>.Fail("Tenant not found."));

        return Ok(ApiResponse<object>.Ok(new
        {
            tenant.Id,
            tenant.Name,
            tenant.Subdomain,
            tenant.Status,
            tenant.CreatedAt
        }));
    }

    // ---------------------------------------------------------------
    // GET /api/tenants  — SuperAdmin: list all tenants with user count
    // ---------------------------------------------------------------
    [HttpGet]
    [Authorize(Policy = "SuperAdminOnly")]
    public async Task<IActionResult> GetAll([FromQuery] bool includeArchived = false)
    {
        var query = _db.Tenants.AsQueryable();
        if (!includeArchived)
            query = query.Where(t => !t.IsArchived);

        var tenants = await query
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();

        // Count users/products/orders per tenant in bulk (ignore global filter —
        // super admin sees all) rather than per-row, to avoid N+1 queries here.
        var userCounts = await _db.Users
            .IgnoreQueryFilters()
            .Where(u => u.TenantId != Guid.Empty && !u.IsDeleted)
            .GroupBy(u => u.TenantId)
            .Select(g => new { TenantId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.TenantId, x => x.Count);

        var productCounts = await _db.Products
            .IgnoreQueryFilters()
            .Where(p => !p.IsDeleted)
            .GroupBy(p => p.TenantId)
            .Select(g => new { TenantId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.TenantId, x => x.Count);

        var orderCountSince = DateTime.UtcNow.AddDays(-30);
        var orderCounts = await _db.Orders
            .IgnoreQueryFilters()
            .Where(o => !o.IsDeleted && o.CreatedAt >= orderCountSince)
            .GroupBy(o => o.TenantId)
            .Select(g => new { TenantId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.TenantId, x => x.Count);

        var result = tenants.Select(t => new
        {
            t.Id,
            t.Name,
            t.Subdomain,
            t.Status,
            t.BillingEmail,
            t.CountryCode,
            t.IsArchived,
            t.ArchivedAt,
            t.CreatedAt,
            UserCount    = userCounts.TryGetValue(t.Id, out var uc) ? uc : 0,
            ProductCount = productCounts.TryGetValue(t.Id, out var pc) ? pc : 0,
            OrderCount30d = orderCounts.TryGetValue(t.Id, out var oc) ? oc : 0
        });

        return Ok(ApiResponse<object>.Ok(result));
    }

    // ---------------------------------------------------------------
    // POST /api/tenants  — SuperAdmin: create a new tenant + admin user
    // ---------------------------------------------------------------
    [HttpPost]
    [Authorize(Policy = "SuperAdminOnly")]
    public async Task<IActionResult> Create([FromBody] CreateTenantRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.TenantName) ||
            string.IsNullOrWhiteSpace(request.Subdomain)  ||
            string.IsNullOrWhiteSpace(request.AdminEmail))
            return BadRequest(ApiResponse<object>.Fail("TenantName, Subdomain and AdminEmail are required."));

        if (RetailSuite.Api.MultiTenancy.ReservedSubdomains.IsReserved(request.Subdomain))
            return BadRequest(ApiResponse<object>.Fail("This subdomain is reserved."));

        if (await _db.Tenants.AnyAsync(t => t.Subdomain == request.Subdomain))
            return Conflict(ApiResponse<object>.Fail("Subdomain is already taken."));

        var normalizedEmail = request.AdminEmail.Trim().ToLowerInvariant();
        var existingUser = await _db.Users
            .IgnoreQueryFilters()
            .Where(u => u.Email == normalizedEmail && !u.IsDeleted)
            .Select(u => new { u.TenantId })
            .FirstOrDefaultAsync();

        if (existingUser != null)
        {
            var existingTenantName = await _db.Tenants
                .IgnoreQueryFilters()
                .Where(t => t.Id == existingUser.TenantId)
                .Select(t => t.Name)
                .FirstOrDefaultAsync();
            return Conflict(ApiResponse<object>.Fail(
                existingTenantName != null
                    ? $"This email is already used by an existing tenant ({existingTenantName}). Use a different admin email."
                    : "This email is already in use. Use a different admin email."));
        }

        var strategy = _db.Database.CreateExecutionStrategy();
        var result = await strategy.ExecuteAsync(async () =>
        {
            using var tx = await _db.Database.BeginTransactionAsync();
            try
            {
                // 1. Create Tenant
                var tenant = new Tenant(request.TenantName.Trim(), request.Subdomain.Trim().ToLowerInvariant());
                _db.Tenants.Add(tenant);
                await _db.SaveChangesAsync();

                // 2. Generate temp password and create Admin user
                var tempPassword = GenerateTempPassword();
                var hash         = BCrypt.Net.BCrypt.HashPassword(tempPassword);
                var adminUser    = new User(tenant.Id, request.AdminEmail.Trim().ToLowerInvariant(), hash, UserRole.Admin);
                _db.Users.Add(adminUser);
                await _db.SaveChangesAsync();

                // 3. Seed per-tenant defaults (Chart of Accounts, shipping methods,
                //    empty tax settings, default Main Branch location).
                await TenantDefaultsSeeder.SeedAsync(_db, tenant.Id);

                await tx.CommitAsync();

                return Ok(ApiResponse<object>.Ok(new
                {
                    TenantId     = tenant.Id,
                    TenantName   = tenant.Name,
                    Subdomain    = tenant.Subdomain,
                    AdminEmail   = adminUser.Email,
                    TempPassword = tempPassword
                }));
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        });

        return result;
    }

    // ---------------------------------------------------------------
    // PATCH /api/tenants/{id}/status  — SuperAdmin: set tenant lifecycle status
    // ---------------------------------------------------------------
    [HttpPatch("{id:guid}/status")]
    [Authorize(Policy = "SuperAdminOnly")]
    public async Task<IActionResult> SetStatus(Guid id, [FromBody] SetTenantStatusRequest request)
    {
        var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.Id == id);
        if (tenant == null)
            return NotFound(ApiResponse<object>.Fail("Tenant not found."));

        if (!TenantStatus.ManuallyAssignable.Contains(request.Status))
            return BadRequest(ApiResponse<object>.Fail(
                $"Status must be one of: {string.Join(", ", TenantStatus.ManuallyAssignable)}."));

        var previousStatus = tenant.Status;
        tenant.SetStatus(request.Status);
        await _db.SaveChangesAsync();

        if (previousStatus != request.Status)
            await LogAuditAsync(tenant.Id, "StatusChanged", $"{previousStatus} -> {request.Status}");

        return Ok(ApiResponse<object>.Ok(new { tenant.Id, tenant.Status }));
    }

    // ---------------------------------------------------------------
    // PATCH /api/tenants/{id}  — SuperAdmin: edit tenant details
    // ---------------------------------------------------------------
    [HttpPatch("{id:guid}")]
    [Authorize(Policy = "SuperAdminOnly")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateTenantRequest request)
    {
        var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.Id == id);
        if (tenant == null)
            return NotFound(ApiResponse<object>.Fail("Tenant not found."));

        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Subdomain))
            return BadRequest(ApiResponse<object>.Fail("Name and Subdomain are required."));

        var subdomain = request.Subdomain.Trim().ToLowerInvariant();

        if (RetailSuite.Api.MultiTenancy.ReservedSubdomains.IsReserved(subdomain))
            return BadRequest(ApiResponse<object>.Fail("This subdomain is reserved."));

        if (await _db.Tenants.AnyAsync(t => t.Id != id && t.Subdomain == subdomain))
            return Conflict(ApiResponse<object>.Fail("Subdomain is already taken."));

        var changes = new List<string>();
        if (tenant.Name != request.Name.Trim()) changes.Add($"Name: '{tenant.Name}' -> '{request.Name.Trim()}'");
        if (tenant.Subdomain != subdomain) changes.Add($"Subdomain: '{tenant.Subdomain}' -> '{subdomain}'");
        var newBillingEmail = string.IsNullOrWhiteSpace(request.BillingEmail) ? null : request.BillingEmail.Trim();
        if (tenant.BillingEmail != newBillingEmail) changes.Add($"BillingEmail: '{tenant.BillingEmail}' -> '{newBillingEmail}'");
        var newCountryCode = string.IsNullOrWhiteSpace(request.CountryCode) ? "PK" : request.CountryCode.ToUpperInvariant();
        if (tenant.CountryCode != newCountryCode) changes.Add($"CountryCode: '{tenant.CountryCode}' -> '{newCountryCode}'");

        tenant.Update(request.Name.Trim(), subdomain);
        tenant.SetBillingEmail(newBillingEmail);
        tenant.SetCountryCode(newCountryCode);

        await _db.SaveChangesAsync();

        if (changes.Any())
            await LogAuditAsync(tenant.Id, "Edited", string.Join("; ", changes));

        return Ok(ApiResponse<object>.Ok(new
        {
            tenant.Id,
            tenant.Name,
            tenant.Subdomain,
            tenant.BillingEmail,
            tenant.CountryCode
        }));
    }

    // ---------------------------------------------------------------
    // POST /api/tenants/{id}/archive  — SuperAdmin: hide from the default list
    // (fully reversible, no data is removed)
    // ---------------------------------------------------------------
    [HttpPost("{id:guid}/archive")]
    [Authorize(Policy = "SuperAdminOnly")]
    public async Task<IActionResult> Archive(Guid id)
    {
        var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.Id == id);
        if (tenant == null)
            return NotFound(ApiResponse<object>.Fail("Tenant not found."));

        if (tenant.IsArchived)
            return Ok(ApiResponse<object>.Ok(new { tenant.Id, tenant.IsArchived }));

        tenant.Archive();
        await _db.SaveChangesAsync();
        await LogAuditAsync(tenant.Id, "Archived", $"Archived tenant '{tenant.Name}'");

        return Ok(ApiResponse<object>.Ok(new { tenant.Id, tenant.IsArchived, tenant.ArchivedAt }));
    }

    // ---------------------------------------------------------------
    // POST /api/tenants/{id}/unarchive  — SuperAdmin: restore to the default list
    // ---------------------------------------------------------------
    [HttpPost("{id:guid}/unarchive")]
    [Authorize(Policy = "SuperAdminOnly")]
    public async Task<IActionResult> Unarchive(Guid id)
    {
        var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.Id == id);
        if (tenant == null)
            return NotFound(ApiResponse<object>.Fail("Tenant not found."));

        if (!tenant.IsArchived)
            return Ok(ApiResponse<object>.Ok(new { tenant.Id, tenant.IsArchived }));

        tenant.Unarchive();
        await _db.SaveChangesAsync();
        await LogAuditAsync(tenant.Id, "Unarchived", $"Unarchived tenant '{tenant.Name}'");

        return Ok(ApiResponse<object>.Ok(new { tenant.Id, tenant.IsArchived }));
    }

    // ---------------------------------------------------------------
    // POST /api/tenants/{id}/invoices  — SuperAdmin: create an ad-hoc
    // invoice for the tenant (e.g. to record an out-of-band payment
    // arrangement). Reuses the existing (previously unwired)
    // GenerateProrationInvoiceAsync — no new billing logic, just an
    // entry point for it.
    // ---------------------------------------------------------------
    [HttpPost("{id:guid}/invoices")]
    [Authorize(Policy = "SuperAdminOnly")]
    public async Task<IActionResult> CreateInvoice(Guid id, [FromBody] CreateInvoiceRequest request)
    {
        var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.Id == id);
        if (tenant == null)
            return NotFound(ApiResponse<object>.Fail("Tenant not found."));

        if (request.Amount <= 0)
            return BadRequest(ApiResponse<object>.Fail("Amount must be greater than zero."));

        if (string.IsNullOrWhiteSpace(request.Reason))
            return BadRequest(ApiResponse<object>.Fail("Reason is required."));

        if (request.Reason.Trim().Length > 250)
            return BadRequest(ApiResponse<object>.Fail("Reason must be 250 characters or fewer."));

        var subscription = await _db.TenantSubscriptions
            .IgnoreQueryFilters()
            .Where(s => s.TenantId == id && !s.IsDeleted)
            .OrderByDescending(s => s.CreatedAt)
            .FirstOrDefaultAsync();

        var planCode = string.IsNullOrWhiteSpace(request.PlanCode)
            ? (subscription?.PlanCode ?? "MANUAL")
            : request.PlanCode.Trim().ToUpperInvariant();

        var currency = string.IsNullOrWhiteSpace(request.Currency)
            ? (subscription?.Currency ?? "PKR")
            : request.Currency.Trim().ToUpperInvariant();

        var invoice = await _billing.GenerateProrationInvoiceAsync(
            id,
            subscription?.Id ?? Guid.Empty,
            request.Amount,
            planCode,
            currency,
            request.Reason.Trim());

        await LogAuditAsync(id, "InvoiceCreated",
            $"Created invoice {invoice.InvoiceNumber} for {currency} {request.Amount:N2} — {request.Reason.Trim()}");

        return Ok(ApiResponse<object>.Ok(new
        {
            invoice.Id,
            invoice.InvoiceNumber,
            invoice.Total,
            invoice.Currency,
            invoice.DueDate
        }));
    }

    // ---------------------------------------------------------------
    // POST /api/tenants/{id}/subscription  — SuperAdmin: assign a plan to a
    // tenant that has none yet, or change an existing one. Once a paid plan
    // + billing cycle is set, the existing renewal background job takes over
    // and auto-generates an invoice every cycle — no separate scheduling
    // needed here, this just connects a tenant to that already-running engine.
    // ---------------------------------------------------------------
    [HttpPost("{id:guid}/subscription")]
    [Authorize(Policy = "SuperAdminOnly")]
    public async Task<IActionResult> AssignOrChangePlan(Guid id, [FromBody] AssignPlanRequest request)
    {
        var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.Id == id);
        if (tenant == null)
            return NotFound(ApiResponse<object>.Fail("Tenant not found."));

        if (string.IsNullOrWhiteSpace(request.PlanCode))
            return BadRequest(ApiResponse<object>.Fail("PlanCode is required."));

        if (!Enum.TryParse<BillingCycle>(request.BillingCycle, ignoreCase: true, out var cycle))
            return BadRequest(ApiResponse<object>.Fail("BillingCycle must be 'Monthly' or 'Yearly'."));

        var existing = await _subs.GetActiveAsync(id);

        if (existing == null)
        {
            var created = await _subs.CreateInitialSubscriptionAsync(id, request.PlanCode, cycle);
            await LogAuditAsync(id, "PlanAssigned", $"Assigned plan {created.PlanCode} ({cycle})");

            return Ok(ApiResponse<object>.Ok(new
            {
                created.Id,
                created.PlanCode,
                Status       = created.Status.ToString(),
                BillingCycle = created.BillingCycle.ToString(),
                created.NextBillingAt
            }));
        }

        var result = await _subs.ChangePlanAsync(id, request.PlanCode, cycle);
        await LogAuditAsync(id, "PlanChanged",
            $"{result.FromPlanCode} -> {result.ToPlanCode} ({cycle}), NetDue={result.NetDue:N2}");

        return Ok(ApiResponse<object>.Ok(result));
    }

    // ---------------------------------------------------------------
    // GET /api/tenants/{id}/invoices/{invoiceId}  — SuperAdmin: full invoice
    // detail + its payment history. The tenant-facing equivalent
    // (GET /api/billing/invoices/{id}) is scoped to the caller's own tenant,
    // which doesn't work for a SuperAdmin looking at an arbitrary tenant.
    // ---------------------------------------------------------------
    [HttpGet("{id:guid}/invoices/{invoiceId:guid}")]
    [Authorize(Policy = "SuperAdminOnly")]
    public async Task<IActionResult> GetInvoiceDetail(Guid id, Guid invoiceId)
    {
        var invoice = await _db.SubscriptionInvoices
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(i => i.Id == invoiceId && i.TenantId == id);
        if (invoice == null)
            return NotFound(ApiResponse<object>.Fail("Invoice not found."));

        var payments = await _db.SubscriptionPayments
            .IgnoreQueryFilters()
            .Where(p => p.InvoiceId == invoiceId)
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => new
            {
                p.Id,
                p.Amount,
                p.Currency,
                p.PaymentMethod,
                p.Provider,
                p.ProviderTxnRef,
                Status = p.Status.ToString(),
                p.FailureReason,
                p.PaidAt,
                p.CreatedAt
            })
            .ToListAsync();

        return Ok(ApiResponse<object>.Ok(new
        {
            Invoice = new
            {
                invoice.Id,
                invoice.InvoiceNumber,
                invoice.PlanCode,
                invoice.PeriodStart,
                invoice.PeriodEnd,
                invoice.Subtotal,
                invoice.TaxAmount,
                invoice.Total,
                invoice.AmountPaid,
                invoice.AmountDue,
                invoice.Currency,
                Status = invoice.Status.ToString(),
                invoice.DueDate,
                invoice.PaidAt,
                invoice.Reason,
                invoice.CreatedAt
            },
            Payments = payments
        }));
    }

    // ---------------------------------------------------------------
    // GET /api/tenants/{id}  — SuperAdmin: full tenant detail (subscription,
    // usage vs plan limits, recent invoices). User list is fetched separately
    // via GET /api/tenants/{id}/users below.
    // ---------------------------------------------------------------
    [HttpGet("{id:guid}")]
    [Authorize(Policy = "SuperAdminOnly")]
    public async Task<IActionResult> GetDetail(Guid id)
    {
        var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.Id == id);
        if (tenant == null)
            return NotFound(ApiResponse<object>.Fail("Tenant not found."));

        var subscription = await _db.TenantSubscriptions
            .IgnoreQueryFilters()
            .Where(s => s.TenantId == id && !s.IsDeleted)
            .OrderByDescending(s => s.CreatedAt)
            .FirstOrDefaultAsync();

        object? subscriptionDto  = null;
        SubscriptionPlan? plan   = null;

        if (subscription != null)
        {
            plan = await _db.SubscriptionPlans.FirstOrDefaultAsync(p => p.Id == subscription.PlanId);
            subscriptionDto = new
            {
                subscription.Id,
                subscription.PlanCode,
                PlanName     = plan?.Name,
                Status       = subscription.Status.ToString(),
                BillingCycle = subscription.BillingCycle.ToString(),
                subscription.StartDate,
                subscription.EndDate,
                subscription.TrialEndsAt,
                subscription.NextBillingAt,
                subscription.CancelAtPeriodEnd,
                subscription.AutoRenew,
                subscription.LastPrice,
                subscription.Currency,
                subscription.PaymentMethodType,
                subscription.CardBrand,
                subscription.CardLast4
            };
        }

        // Usage counts computed directly rather than via IEntitlementService:
        // that service only returns CurrentCount/Limit when a limit is actually
        // exceeded (it's an allow/deny gate, not a reporting API), so it can't
        // be reused as-is for an always-show-the-numbers usage view.
        var userCount = await _db.Users
            .IgnoreQueryFilters()
            .CountAsync(u => u.TenantId == id && !u.IsDeleted);

        var productCount = await _db.Products
            .IgnoreQueryFilters()
            .CountAsync(p => p.TenantId == id && !p.IsDeleted);

        var orderCountSince = DateTime.UtcNow.AddDays(-30);
        var orderCount = await _db.Orders
            .IgnoreQueryFilters()
            .CountAsync(o => o.TenantId == id && !o.IsDeleted && o.CreatedAt >= orderCountSince);

        var invoices = await _db.SubscriptionInvoices
            .IgnoreQueryFilters()
            .Where(i => i.TenantId == id && !i.IsDeleted)
            .OrderByDescending(i => i.CreatedAt)
            .Take(20)
            .Select(i => new
            {
                i.Id,
                i.InvoiceNumber,
                i.PlanCode,
                i.PeriodStart,
                i.PeriodEnd,
                i.Total,
                i.Currency,
                Status = i.Status.ToString(),
                i.DueDate,
                i.PaidAt,
                i.AmountDue
            })
            .ToListAsync();

        var auditLog = await _db.TenantAuditLogs
            .Where(a => a.TenantId == id)
            .OrderByDescending(a => a.CreatedAt)
            .Take(50)
            .Select(a => new
            {
                a.Action,
                a.Details,
                a.PerformedByEmail,
                a.CreatedAt
            })
            .ToListAsync();

        return Ok(ApiResponse<object>.Ok(new
        {
            Tenant = new
            {
                tenant.Id,
                tenant.Name,
                tenant.Subdomain,
                tenant.Status,
                tenant.BillingEmail,
                tenant.CountryCode,
                tenant.IsArchived,
                tenant.ArchivedAt,
                tenant.CreatedAt
            },
            Subscription = subscriptionDto,
            Usage = new
            {
                Users    = new { Current = userCount,    Limit = plan?.MaxUsers },
                Products = new { Current = productCount, Limit = plan?.MaxProducts },
                Orders   = new { Current = orderCount,   Limit = plan?.MaxOrdersPerMonth, WindowDays = 30 }
            },
            Invoices = invoices,
            AuditLog = auditLog
        }));
    }

    // ---------------------------------------------------------------
    // GET /api/tenants/{id}/users  — SuperAdmin: list users of a tenant
    // ---------------------------------------------------------------
    [HttpGet("{id:guid}/users")]
    [Authorize(Policy = "SuperAdminOnly")]
    public async Task<IActionResult> GetUsers(Guid id)
    {
        var tenantExists = await _db.Tenants.AnyAsync(t => t.Id == id);
        if (!tenantExists)
            return NotFound(ApiResponse<object>.Fail("Tenant not found."));

        var users = await _db.Users
            .IgnoreQueryFilters()
            .Where(u => u.TenantId == id && !u.IsDeleted)
            .OrderBy(u => u.Email)
            .Select(u => new { u.Id, u.Email, Role = u.Role.ToString(), u.CreatedAt })
            .ToListAsync();

        return Ok(ApiResponse<object>.Ok(users));
    }

    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------
    private static string GenerateTempPassword()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghjkmnpqrstuvwxyz23456789!@#$";
        var rng    = new Random(Guid.NewGuid().GetHashCode());
        var result = new char[12];
        for (int i = 0; i < result.Length; i++)
            result[i] = chars[rng.Next(chars.Length)];
        return new string(result);
    }
}

// ---------------------------------------------------------------
// Request DTOs
// ---------------------------------------------------------------
public class CreateTenantRequest
{
    public string TenantName  { get; set; } = string.Empty;
    public string Subdomain   { get; set; } = string.Empty;
    public string AdminEmail  { get; set; } = string.Empty;
}

public class SetTenantStatusRequest
{
    public string Status { get; set; } = "Active";
}

public class UpdateTenantRequest
{
    public string  Name         { get; set; } = string.Empty;
    public string  Subdomain    { get; set; } = string.Empty;
    public string? BillingEmail { get; set; }
    public string? CountryCode  { get; set; }
}

public class CreateInvoiceRequest
{
    public decimal Amount   { get; set; }
    public string? Currency { get; set; }
    public string? PlanCode { get; set; }
    public string  Reason   { get; set; } = string.Empty;
}

public class AssignPlanRequest
{
    public string PlanCode     { get; set; } = string.Empty;
    public string BillingCycle { get; set; } = "Monthly";
}
