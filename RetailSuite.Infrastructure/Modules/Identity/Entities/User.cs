using RetailSuite.Infrastructure.Modules.Customer.Model;
using RetailSuite.Shared;

namespace RetailSuite.Infrastructure.Modules.Identity.Entities
{
    public class User : TenantEntity
    {
        public string Email { get; private set; } = string.Empty;
        public string PasswordHash { get; private set; } = string.Empty;
        public UserRole Role { get; private set; }

        /// <summary>Display name shown in user-management screens — "Ali Khan". Optional.</summary>
        public string? FullName { get; private set; }

        /// <summary>True if the user can sign in. Admin can disable users without deleting them.</summary>
        public bool IsActive { get; private set; } = true;

        /// <summary>
        /// True when the password was set by an admin (not the user themselves).
        /// First-login forces a password change so the temp password from the admin
        /// can't be reused indefinitely.
        /// </summary>
        public bool MustChangePassword { get; private set; }

        /// <summary>True once the user has clicked the verification link emailed at signup.</summary>
        public bool IsEmailVerified { get; private set; }

        /// <summary>UTC timestamp the user verified their email; null while pending.</summary>
        public DateTime? EmailVerifiedAt { get; private set; }

        private User() { }

        public User(Guid tenantId, string email, string passwordHash, UserRole role)
        {
            TenantId        = tenantId;
            Email           = email;
            PasswordHash    = passwordHash;
            Role            = role;
            IsEmailVerified = false;
        }

        /// <summary>Mark the user's email as verified. Idempotent.</summary>
        public void MarkEmailVerified()
        {
            if (IsEmailVerified) return;
            IsEmailVerified = true;
            EmailVerifiedAt = DateTime.UtcNow;
        }

        // ---- Mutators used by user-management endpoints --------------------

        public void SetFullName(string? fullName) => FullName = string.IsNullOrWhiteSpace(fullName) ? null : fullName.Trim();

        /// <summary>Change the login email. Resets verification — a changed address hasn't
        /// been proven yet, even if the old one was. Uniqueness is the caller's responsibility.</summary>
        public void SetEmail(string newEmail)
        {
            if (string.IsNullOrWhiteSpace(newEmail))
                throw new ArgumentException("Email is required.", nameof(newEmail));
            Email           = newEmail.Trim().ToLowerInvariant();
            IsEmailVerified = false;
            EmailVerifiedAt = null;
        }
        public void SetRole(UserRole role) => Role = role;
        public void Activate()   => IsActive = true;
        public void Deactivate() => IsActive = false;

        /// <summary>Mark that the user is using a temp password and must change it on next login.</summary>
        public void RequirePasswordChange() => MustChangePassword = true;

        /// <summary>Replace the password hash. Clears the must-change-password flag.</summary>
        public void SetPassword(string newPasswordHash)
        {
            if (string.IsNullOrWhiteSpace(newPasswordHash))
                throw new ArgumentException("PasswordHash is required.", nameof(newPasswordHash));
            PasswordHash       = newPasswordHash;
            MustChangePassword = false;
        }
    }
}
