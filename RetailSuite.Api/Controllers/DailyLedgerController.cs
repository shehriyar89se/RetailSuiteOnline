using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RetailSuite.Infrastructure;
using RetailSuite.Shared;
using RetailSuite.Modules.Accounting.Entities;

namespace RetailSuite.Api.Controllers;

/// <summary>
/// A simple day-to-day cash book: daily expenses, amounts owed by customers,
/// and amounts owed to vendors. Deliberately separate from the formal
/// double-entry Chart of Accounts / JournalEntry system — see
/// <see cref="DailyLedgerEntry"/> for why.
/// </summary>
[ApiController]
[Route("api/daily-ledger")]
[Authorize(Policy = "AdminOnly")]
public class DailyLedgerController : ControllerBase
{
    private readonly RetailDbContext _db;
    private readonly ICurrentUserContext _currentUser;

    public DailyLedgerController(RetailDbContext db, ICurrentUserContext currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    // ============================================================
    //  GET /api/daily-ledger?from=&to=&type=
    // ============================================================
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] string? type)
    {
        var query = _db.DailyLedgerEntries.AsQueryable();

        if (from.HasValue) query = query.Where(e => e.EntryDate >= from.Value.Date);
        if (to.HasValue)   query = query.Where(e => e.EntryDate <= to.Value.Date);

        if (!string.IsNullOrWhiteSpace(type) && Enum.TryParse<LedgerEntryType>(type, ignoreCase: true, out var parsedType))
            query = query.Where(e => e.Type == parsedType);

        var entries = await query
            .OrderByDescending(e => e.EntryDate)
            .ThenByDescending(e => e.CreatedAt)
            .Select(e => new
            {
                e.Id,
                e.EntryDate,
                Type = e.Type.ToString(),
                e.AccountId,
                AccountCode = _db.Accounts.Where(a => a.Id == e.AccountId).Select(a => a.Code).FirstOrDefault(),
                AccountName = _db.Accounts.Where(a => a.Id == e.AccountId).Select(a => a.Name).FirstOrDefault(),
                e.PartyName,
                e.Description,
                e.Amount,
                e.IsSettled,
                e.SettledAt
            })
            .ToListAsync();

        return Ok(ApiResponse<object>.Ok(entries));
    }

    // ============================================================
    //  POST /api/daily-ledger
    // ============================================================
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateLedgerEntryRequest request)
    {
        if (!Enum.TryParse<LedgerEntryType>(request.Type, ignoreCase: true, out var type))
            return BadRequest(ApiResponse<object>.Fail("Type must be Expense, Receivable or Payable."));

        if (string.IsNullOrWhiteSpace(request.Description))
            return BadRequest(ApiResponse<object>.Fail("Description is required."));

        if (request.Amount <= 0)
            return BadRequest(ApiResponse<object>.Fail("Amount must be greater than zero."));

        if (type != LedgerEntryType.Expense && string.IsNullOrWhiteSpace(request.PartyName))
            return BadRequest(ApiResponse<object>.Fail(
                type == LedgerEntryType.Receivable ? "Customer name is required." : "Vendor name is required."));

        var account = await _db.Accounts.FirstOrDefaultAsync(a => a.Id == request.AccountId && a.IsActive);
        if (account == null)
            return BadRequest(ApiResponse<object>.Fail("Select a valid account."));

        try
        {
            var entry = new DailyLedgerEntry(
                _currentUser.TenantId,
                request.EntryDate ?? DateTime.UtcNow,
                type,
                request.AccountId,
                request.PartyName,
                request.Description,
                request.Amount,
                _currentUser.UserId);

            _db.DailyLedgerEntries.Add(entry);
            await _db.SaveChangesAsync();

            return Ok(ApiResponse<object>.Ok(new
            {
                entry.Id,
                entry.EntryDate,
                Type = entry.Type.ToString(),
                entry.AccountId,
                AccountCode = account.Code,
                AccountName = account.Name,
                entry.PartyName,
                entry.Description,
                entry.Amount,
                entry.IsSettled
            }));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    // ============================================================
    //  PATCH /api/daily-ledger/{id}/settle
    // ============================================================
    [HttpPatch("{id:guid}/settle")]
    public async Task<IActionResult> Settle(Guid id)
    {
        var entry = await _db.DailyLedgerEntries.FirstOrDefaultAsync(e => e.Id == id);
        if (entry == null)
            return NotFound(ApiResponse<object>.Fail("Entry not found."));

        entry.MarkSettled();
        await _db.SaveChangesAsync();

        return Ok(ApiResponse<object>.Ok(new { entry.Id, entry.IsSettled, entry.SettledAt }));
    }

    // ============================================================
    //  DELETE /api/daily-ledger/{id}
    // ============================================================
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var entry = await _db.DailyLedgerEntries.FirstOrDefaultAsync(e => e.Id == id);
        if (entry == null)
            return NotFound(ApiResponse<object>.Fail("Entry not found."));

        entry.MarkAsDeleted();
        await _db.SaveChangesAsync();

        return Ok(ApiResponse<object>.Ok(new { entry.Id }));
    }
}

public class CreateLedgerEntryRequest
{
    public DateTime? EntryDate   { get; set; }
    public string    Type        { get; set; } = string.Empty;
    public Guid      AccountId   { get; set; }
    public string?   PartyName   { get; set; }
    public string    Description { get; set; } = string.Empty;
    public decimal   Amount      { get; set; }
}
