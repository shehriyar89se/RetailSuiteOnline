using RetailSuite.Shared;

namespace RetailSuite.Modules.Accounting.Entities;

/// <summary>
/// A simple day-to-day cash-book entry — a daily expense, an amount a customer
/// still owes, or an amount owed to a vendor. Deliberately separate from the
/// double-entry Chart of Accounts / JournalEntry system: this is a quick
/// informal log (free-text party name, no account posting), not formal
/// bookkeeping. Receivables/payables can be marked settled once collected/paid;
/// expenses have no settled state — they're already a completed outflow.
/// </summary>
public class DailyLedgerEntry : TenantEntity
{
    public DateTime EntryDate       { get; private set; }
    public LedgerEntryType Type     { get; private set; }

    /// <summary>Customer name for Receivable, vendor name for Payable. Null for Expense.</summary>
    public string? PartyName        { get; private set; }
    public string  Description      { get; private set; } = string.Empty;
    public decimal Amount           { get; private set; }

    /// <summary>Always true for Expense (nothing to settle). For Receivable/Payable,
    /// true once the money has actually been collected/paid.</summary>
    public bool     IsSettled       { get; private set; }
    public DateTime? SettledAt      { get; private set; }

    public Guid?    CreatedByUserId { get; private set; }

    private DailyLedgerEntry() { }

    public DailyLedgerEntry(
        Guid tenantId, DateTime entryDate, LedgerEntryType type,
        string? partyName, string description, decimal amount, Guid? createdByUserId)
    {
        if (amount <= 0)
            throw new ArgumentException("Amount must be greater than zero.", nameof(amount));
        if (string.IsNullOrWhiteSpace(description))
            throw new ArgumentException("Description is required.", nameof(description));
        if (type != LedgerEntryType.Expense && string.IsNullOrWhiteSpace(partyName))
            throw new ArgumentException("Party name is required for a receivable or payable.", nameof(partyName));

        TenantId        = tenantId;
        EntryDate       = entryDate.Date;
        Type            = type;
        PartyName       = string.IsNullOrWhiteSpace(partyName) ? null : partyName.Trim();
        Description     = description.Trim();
        Amount          = amount;
        CreatedByUserId = createdByUserId;

        // Expenses are a completed outflow the moment they're logged — nothing left to settle.
        IsSettled = type == LedgerEntryType.Expense;
        SettledAt = IsSettled ? DateTime.UtcNow : null;
    }

    public void MarkSettled()
    {
        if (Type == LedgerEntryType.Expense) return; // no-op — already "settled" by definition
        IsSettled = true;
        SettledAt = DateTime.UtcNow;
    }
}

public enum LedgerEntryType
{
    Expense    = 0,
    Receivable = 1,
    Payable    = 2
}
