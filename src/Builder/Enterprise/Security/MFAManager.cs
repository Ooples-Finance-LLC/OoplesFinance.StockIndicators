using System.Security.Cryptography;
using System.Text;
using OoplesFinance.StockIndicators.Builder.Enterprise.SSO;

namespace OoplesFinance.StockIndicators.Builder.Enterprise.Security;

/// <summary>
/// Multi-Factor Authentication (MFA) manager.
/// Supports TOTP, SMS, Email, and backup codes.
/// </summary>
public sealed class MFAManager
{
    private readonly IMfaStorage _storage;
    private readonly ISecurityAuditLogger _auditLogger;
    private readonly ISmsProvider? _smsProvider;
    private readonly IEmailProvider? _emailProvider;
    private readonly MfaOptions _options;

    /// <summary>
    /// Initializes a new instance of the MFAManager.
    /// </summary>
    public MFAManager(
        IMfaStorage storage,
        ISecurityAuditLogger auditLogger,
        MfaOptions? options = null,
        ISmsProvider? smsProvider = null,
        IEmailProvider? emailProvider = null)
    {
        _storage = storage ?? throw new ArgumentNullException(nameof(storage));
        _auditLogger = auditLogger ?? throw new ArgumentNullException(nameof(auditLogger));
        _options = options ?? new MfaOptions();
        _smsProvider = smsProvider;
        _emailProvider = emailProvider;
    }

    /// <summary>
    /// Enrolls a user in TOTP-based MFA.
    /// </summary>
    public async Task<TotpEnrollmentResult> EnrollTotpAsync(
        string userId,
        string tenantId,
        CancellationToken ct = default)
    {
        // Generate secret key
        var secret = GenerateSecret();
        var secretBase32 = Base32Encode(secret);

        // Get user info for QR code
        var enrollment = new MfaEnrollment
        {
            UserId = userId,
            TenantId = tenantId,
            Method = MfaMethod.TOTP,
            Secret = secretBase32,
            CreatedAt = DateTime.UtcNow,
            IsVerified = false
        };

        await _storage.SaveEnrollmentAsync(enrollment, ct);

        // Generate QR code URI (otpauth format)
        var issuer = Uri.EscapeDataString(_options.Issuer);
        var label = Uri.EscapeDataString(userId);
        var otpauthUri = $"otpauth://totp/{issuer}:{label}?secret={secretBase32}&issuer={issuer}&algorithm=SHA1&digits=6&period=30";

        await _auditLogger.LogAsync(new SecurityAuditEvent
        {
            EventType = SecurityEventType.MfaEnabled,
            TenantId = tenantId,
            UserId = userId,
            Success = true,
            Metadata = new Dictionary<string, string> { { "method", "TOTP" } }
        });

        return new TotpEnrollmentResult
        {
            Secret = secretBase32,
            QrCodeUri = otpauthUri,
            BackupCodes = await GenerateBackupCodesAsync(userId, tenantId, ct)
        };
    }

    /// <summary>
    /// Verifies TOTP enrollment with the initial code.
    /// </summary>
    public async Task<bool> VerifyTotpEnrollmentAsync(
        string userId,
        string tenantId,
        string code,
        CancellationToken ct = default)
    {
        var enrollment = await _storage.GetEnrollmentAsync(userId, MfaMethod.TOTP, ct);
        if (enrollment == null || enrollment.IsVerified)
        {
            return false;
        }

        if (!ValidateTotp(enrollment.Secret, code))
        {
            return false;
        }

        enrollment.IsVerified = true;
        enrollment.VerifiedAt = DateTime.UtcNow;
        await _storage.SaveEnrollmentAsync(enrollment, ct);

        return true;
    }

    /// <summary>
    /// Enrolls a user in SMS-based MFA.
    /// </summary>
    public async Task<SmsEnrollmentResult> EnrollSmsAsync(
        string userId,
        string tenantId,
        string phoneNumber,
        CancellationToken ct = default)
    {
        if (_smsProvider == null)
        {
            throw new InvalidOperationException("SMS provider not configured");
        }

        // Normalize phone number
        var normalizedPhone = NormalizePhoneNumber(phoneNumber);

        var enrollment = new MfaEnrollment
        {
            UserId = userId,
            TenantId = tenantId,
            Method = MfaMethod.SMS,
            PhoneNumber = normalizedPhone,
            CreatedAt = DateTime.UtcNow,
            IsVerified = false
        };

        await _storage.SaveEnrollmentAsync(enrollment, ct);

        // Send verification code
        var verificationCode = GenerateNumericCode(6);
        await _storage.SaveVerificationCodeAsync(userId, MfaMethod.SMS, verificationCode,
            DateTime.UtcNow.Add(_options.CodeExpiration), ct);

        await _smsProvider.SendAsync(normalizedPhone,
            $"Your verification code is: {verificationCode}. It expires in {_options.CodeExpiration.TotalMinutes} minutes.",
            ct);

        return new SmsEnrollmentResult
        {
            PhoneNumberMasked = MaskPhoneNumber(normalizedPhone)
        };
    }

    /// <summary>
    /// Enrolls a user in Email-based MFA.
    /// </summary>
    public async Task<EmailEnrollmentResult> EnrollEmailAsync(
        string userId,
        string tenantId,
        string email,
        CancellationToken ct = default)
    {
        if (_emailProvider == null)
        {
            throw new InvalidOperationException("Email provider not configured");
        }

        var enrollment = new MfaEnrollment
        {
            UserId = userId,
            TenantId = tenantId,
            Method = MfaMethod.Email,
            Email = email.ToLowerInvariant(),
            CreatedAt = DateTime.UtcNow,
            IsVerified = false
        };

        await _storage.SaveEnrollmentAsync(enrollment, ct);

        // Send verification code
        var verificationCode = GenerateNumericCode(6);
        await _storage.SaveVerificationCodeAsync(userId, MfaMethod.Email, verificationCode,
            DateTime.UtcNow.Add(_options.CodeExpiration), ct);

        await _emailProvider.SendAsync(email,
            "Your Verification Code",
            $"Your verification code is: {verificationCode}. It expires in {_options.CodeExpiration.TotalMinutes} minutes.",
            ct);

        return new EmailEnrollmentResult
        {
            EmailMasked = MaskEmail(email)
        };
    }

    /// <summary>
    /// Initiates an MFA challenge for a user.
    /// </summary>
    public async Task<MfaChallenge> CreateChallengeAsync(
        string userId,
        string tenantId,
        MfaMethod? preferredMethod = null,
        CancellationToken ct = default)
    {
        var enrollments = await _storage.GetEnrollmentsAsync(userId, ct);
        var verifiedEnrollments = enrollments.Where(e => e.IsVerified).ToList();

        if (verifiedEnrollments.Count == 0)
        {
            throw new InvalidOperationException("User has no verified MFA methods");
        }

        // Select method
        var enrollment = preferredMethod.HasValue
            ? verifiedEnrollments.FirstOrDefault(e => e.Method == preferredMethod.Value)
            : verifiedEnrollments.First();

        if (enrollment == null)
        {
            enrollment = verifiedEnrollments.First();
        }

        var challengeId = Guid.NewGuid().ToString();
        var challenge = new MfaChallengeRecord
        {
            ChallengeId = challengeId,
            UserId = userId,
            TenantId = tenantId,
            Method = enrollment.Method,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.Add(_options.ChallengeExpiration)
        };

        await _storage.SaveChallengeAsync(challenge, ct);

        // For SMS and Email, send the code
        if (enrollment.Method == MfaMethod.SMS && _smsProvider != null)
        {
            var code = GenerateNumericCode(6);
            await _storage.SaveVerificationCodeAsync(userId, MfaMethod.SMS, code, challenge.ExpiresAt, ct);
            await _smsProvider.SendAsync(enrollment.PhoneNumber!,
                $"Your verification code is: {code}", ct);
        }
        else if (enrollment.Method == MfaMethod.Email && _emailProvider != null)
        {
            var code = GenerateNumericCode(6);
            await _storage.SaveVerificationCodeAsync(userId, MfaMethod.Email, code, challenge.ExpiresAt, ct);
            await _emailProvider.SendAsync(enrollment.Email!,
                "Your Verification Code",
                $"Your verification code is: {code}", ct);
        }

        return new MfaChallenge
        {
            ChallengeId = challengeId,
            Method = enrollment.Method,
            Hint = GetMethodHint(enrollment),
            ExpiresAt = challenge.ExpiresAt
        };
    }

    /// <summary>
    /// Verifies an MFA challenge.
    /// </summary>
    public async Task<MfaVerificationResult> VerifyChallengeAsync(
        string challengeId,
        string code,
        string? ipAddress = null,
        CancellationToken ct = default)
    {
        var challenge = await _storage.GetChallengeAsync(challengeId, ct);
        if (challenge == null)
        {
            return new MfaVerificationResult { Success = false, Error = "Challenge not found" };
        }

        if (challenge.ExpiresAt < DateTime.UtcNow)
        {
            return new MfaVerificationResult { Success = false, Error = "Challenge expired" };
        }

        if (challenge.IsCompleted)
        {
            return new MfaVerificationResult { Success = false, Error = "Challenge already completed" };
        }

        // Verify code based on method
        bool isValid;

        switch (challenge.Method)
        {
            case MfaMethod.TOTP:
                var enrollment = await _storage.GetEnrollmentAsync(challenge.UserId, MfaMethod.TOTP, ct);
                if (enrollment == null)
                {
                    return new MfaVerificationResult { Success = false, Error = "TOTP not enrolled" };
                }
                isValid = ValidateTotp(enrollment.Secret, code);
                break;

            case MfaMethod.SMS:
            case MfaMethod.Email:
                var storedCode = await _storage.GetVerificationCodeAsync(challenge.UserId, challenge.Method, ct);
                isValid = storedCode != null && storedCode.Code == code && storedCode.ExpiresAt > DateTime.UtcNow;
                if (isValid)
                {
                    await _storage.DeleteVerificationCodeAsync(challenge.UserId, challenge.Method, ct);
                }
                break;

            case MfaMethod.Backup:
                isValid = await VerifyBackupCodeAsync(challenge.UserId, code, ct);
                break;

            default:
                return new MfaVerificationResult { Success = false, Error = "Unsupported MFA method" };
        }

        // Update challenge status
        challenge.IsCompleted = true;
        challenge.CompletedAt = DateTime.UtcNow;
        challenge.Success = isValid;
        await _storage.SaveChallengeAsync(challenge, ct);

        // Log audit event
        await _auditLogger.LogAsync(new SecurityAuditEvent
        {
            EventType = isValid ? SecurityEventType.MfaSuccess : SecurityEventType.MfaFailure,
            TenantId = challenge.TenantId,
            UserId = challenge.UserId,
            IpAddress = ipAddress ?? "unknown",
            Success = isValid,
            Metadata = new Dictionary<string, string> { { "method", challenge.Method.ToString() } }
        });

        return new MfaVerificationResult
        {
            Success = isValid,
            Error = isValid ? null : "Invalid code"
        };
    }

    /// <summary>
    /// Generates new backup codes for a user.
    /// </summary>
    public async Task<IReadOnlyList<string>> GenerateBackupCodesAsync(
        string userId,
        string tenantId,
        CancellationToken ct = default)
    {
        var codes = new List<string>();

        for (int i = 0; i < _options.BackupCodeCount; i++)
        {
            codes.Add(GenerateBackupCode());
        }

        // Hash and store the codes
        var hashedCodes = codes.Select(c => HashCode(c)).ToList();
        await _storage.SaveBackupCodesAsync(userId, tenantId, hashedCodes, ct);

        return codes;
    }

    /// <summary>
    /// Disables MFA for a user.
    /// </summary>
    public async Task DisableMfaAsync(
        string userId,
        string tenantId,
        MfaMethod? method = null,
        CancellationToken ct = default)
    {
        if (method.HasValue)
        {
            await _storage.DeleteEnrollmentAsync(userId, method.Value, ct);
        }
        else
        {
            await _storage.DeleteAllEnrollmentsAsync(userId, ct);
        }

        await _auditLogger.LogAsync(new SecurityAuditEvent
        {
            EventType = SecurityEventType.MfaDisabled,
            TenantId = tenantId,
            UserId = userId,
            Success = true,
            Metadata = new Dictionary<string, string> { { "method", method?.ToString() ?? "all" } }
        });
    }

    /// <summary>
    /// Gets the enrolled MFA methods for a user.
    /// </summary>
    public async Task<IReadOnlyList<MfaMethodInfo>> GetEnrolledMethodsAsync(
        string userId,
        CancellationToken ct = default)
    {
        var enrollments = await _storage.GetEnrollmentsAsync(userId, ct);

        return enrollments
            .Where(e => e.IsVerified)
            .Select(e => new MfaMethodInfo
            {
                Method = e.Method,
                Hint = GetMethodHint(e),
                EnrolledAt = e.VerifiedAt ?? e.CreatedAt
            })
            .ToList();
    }

    private bool ValidateTotp(string secret, string code)
    {
        var secretBytes = Base32Decode(secret);
        var currentTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var timeStep = currentTime / 30;

        // Check current and adjacent time steps (for clock skew)
        for (int i = -_options.TotpClockSkewSteps; i <= _options.TotpClockSkewSteps; i++)
        {
            var expectedCode = GenerateTotp(secretBytes, timeStep + i);
            if (code == expectedCode)
            {
                return true;
            }
        }

        return false;
    }

    private static string GenerateTotp(byte[] secret, long counter)
    {
        var counterBytes = BitConverter.GetBytes(counter);
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(counterBytes);
        }

        using var hmac = new HMACSHA1(secret);
        var hash = hmac.ComputeHash(counterBytes);

        var offset = hash[hash.Length - 1] & 0x0F;
        var binaryCode = ((hash[offset] & 0x7F) << 24) |
                        ((hash[offset + 1] & 0xFF) << 16) |
                        ((hash[offset + 2] & 0xFF) << 8) |
                        (hash[offset + 3] & 0xFF);

        var otp = binaryCode % 1_000_000;
        return otp.ToString("D6");
    }

    private async Task<bool> VerifyBackupCodeAsync(string userId, string code, CancellationToken ct)
    {
        var hashedCode = HashCode(code);
        var usedCode = await _storage.UseBackupCodeAsync(userId, hashedCode, ct);
        return usedCode;
    }

    private static byte[] GenerateSecret()
    {
        var secret = new byte[20]; // 160 bits for SHA1
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(secret);
        return secret;
    }

    private static string GenerateNumericCode(int length)
    {
        var bytes = new byte[4];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        var number = Math.Abs(BitConverter.ToInt32(bytes, 0));
        return (number % (int)Math.Pow(10, length)).ToString($"D{length}");
    }

    private static string GenerateBackupCode()
    {
        var bytes = new byte[5];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);

        var code = new StringBuilder();
        foreach (var b in bytes)
        {
            code.Append((b % 36).ToString("x")); // 0-9a-z
        }

        // Format as XXXXX-XXXXX
        return $"{code.ToString().Substring(0, 5)}-{code.ToString().Substring(5)}".ToUpperInvariant();
    }

    private static string HashCode(string code)
    {
        var normalizedCode = code.Replace("-", "").ToUpperInvariant();
        var bytes = Encoding.UTF8.GetBytes(normalizedCode);
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToBase64String(hash);
    }

    private static string Base32Encode(byte[] data)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var result = new StringBuilder();

        int buffer = data[0];
        int bitsLeft = 8;
        int index = 1;

        while (bitsLeft > 0 || index < data.Length)
        {
            if (bitsLeft < 5)
            {
                if (index < data.Length)
                {
                    buffer <<= 8;
                    buffer |= data[index++];
                    bitsLeft += 8;
                }
                else
                {
                    int pad = 5 - bitsLeft;
                    buffer <<= pad;
                    bitsLeft += pad;
                }
            }

            bitsLeft -= 5;
            result.Append(alphabet[(buffer >> bitsLeft) & 0x1F]);
        }

        return result.ToString();
    }

    private static byte[] Base32Decode(string encoded)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        encoded = encoded.TrimEnd('=').ToUpperInvariant();

        var result = new List<byte>();
        int buffer = 0;
        int bitsLeft = 0;

        foreach (char c in encoded)
        {
            var value = alphabet.IndexOf(c);
            if (value < 0) continue;

            buffer <<= 5;
            buffer |= value;
            bitsLeft += 5;

            if (bitsLeft >= 8)
            {
                bitsLeft -= 8;
                result.Add((byte)(buffer >> bitsLeft));
            }
        }

        return result.ToArray();
    }

    private static string NormalizePhoneNumber(string phoneNumber)
    {
        // Remove all non-digit characters except leading +
        var normalized = new StringBuilder();
        var hasPlus = phoneNumber.StartsWith("+");

        if (hasPlus)
        {
            normalized.Append('+');
        }

        foreach (var c in phoneNumber)
        {
            if (char.IsDigit(c))
            {
                normalized.Append(c);
            }
        }

        return normalized.ToString();
    }

    private static string MaskPhoneNumber(string phoneNumber)
    {
        if (phoneNumber.Length <= 4)
        {
            return "****";
        }

        return new string('*', phoneNumber.Length - 4) + phoneNumber.Substring(phoneNumber.Length - 4);
    }

    private static string MaskEmail(string email)
    {
        var atIndex = email.IndexOf('@');
        if (atIndex <= 2)
        {
            return "***@" + email.Substring(atIndex + 1);
        }

        return email.Substring(0, 2) + new string('*', atIndex - 2) + email.Substring(atIndex);
    }

    private static string GetMethodHint(MfaEnrollment enrollment)
    {
        return enrollment.Method switch
        {
            MfaMethod.TOTP => "Authenticator app",
            MfaMethod.SMS => MaskPhoneNumber(enrollment.PhoneNumber ?? ""),
            MfaMethod.Email => MaskEmail(enrollment.Email ?? ""),
            MfaMethod.Backup => "Backup code",
            _ => enrollment.Method.ToString()
        };
    }
}

/// <summary>
/// MFA options.
/// </summary>
public sealed class MfaOptions
{
    /// <summary>Issuer name for TOTP.</summary>
    public string Issuer { get; set; } = "OoplesFinance";

    /// <summary>Number of backup codes to generate.</summary>
    public int BackupCodeCount { get; set; } = 10;

    /// <summary>Challenge expiration time.</summary>
    public TimeSpan ChallengeExpiration { get; set; } = TimeSpan.FromMinutes(10);

    /// <summary>Verification code expiration time.</summary>
    public TimeSpan CodeExpiration { get; set; } = TimeSpan.FromMinutes(10);

    /// <summary>Number of TOTP clock skew steps to allow.</summary>
    public int TotpClockSkewSteps { get; set; } = 1;
}

/// <summary>
/// TOTP enrollment result.
/// </summary>
public sealed class TotpEnrollmentResult
{
    public string Secret { get; set; } = string.Empty;
    public string QrCodeUri { get; set; } = string.Empty;
    public IReadOnlyList<string> BackupCodes { get; set; } = [];
}

/// <summary>
/// SMS enrollment result.
/// </summary>
public sealed class SmsEnrollmentResult
{
    public string PhoneNumberMasked { get; set; } = string.Empty;
}

/// <summary>
/// Email enrollment result.
/// </summary>
public sealed class EmailEnrollmentResult
{
    public string EmailMasked { get; set; } = string.Empty;
}

/// <summary>
/// MFA verification result.
/// </summary>
public sealed class MfaVerificationResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
}

/// <summary>
/// MFA method information.
/// </summary>
public sealed class MfaMethodInfo
{
    public MfaMethod Method { get; set; }
    public string Hint { get; set; } = string.Empty;
    public DateTime EnrolledAt { get; set; }
}

/// <summary>
/// MFA enrollment record.
/// </summary>
public sealed class MfaEnrollment
{
    public string UserId { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
    public MfaMethod Method { get; set; }
    public string Secret { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string? Email { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? VerifiedAt { get; set; }
    public bool IsVerified { get; set; }
}

/// <summary>
/// MFA challenge record.
/// </summary>
public sealed class MfaChallengeRecord
{
    public string ChallengeId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
    public MfaMethod Method { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public bool IsCompleted { get; set; }
    public DateTime? CompletedAt { get; set; }
    public bool Success { get; set; }
}

/// <summary>
/// Verification code record.
/// </summary>
public sealed class VerificationCode
{
    public string Code { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
}

/// <summary>
/// Interface for MFA storage.
/// </summary>
public interface IMfaStorage
{
    Task SaveEnrollmentAsync(MfaEnrollment enrollment, CancellationToken ct = default);
    Task<MfaEnrollment?> GetEnrollmentAsync(string userId, MfaMethod method, CancellationToken ct = default);
    Task<IReadOnlyList<MfaEnrollment>> GetEnrollmentsAsync(string userId, CancellationToken ct = default);
    Task DeleteEnrollmentAsync(string userId, MfaMethod method, CancellationToken ct = default);
    Task DeleteAllEnrollmentsAsync(string userId, CancellationToken ct = default);

    Task SaveChallengeAsync(MfaChallengeRecord challenge, CancellationToken ct = default);
    Task<MfaChallengeRecord?> GetChallengeAsync(string challengeId, CancellationToken ct = default);

    Task SaveVerificationCodeAsync(string userId, MfaMethod method, string code, DateTime expiresAt, CancellationToken ct = default);
    Task<VerificationCode?> GetVerificationCodeAsync(string userId, MfaMethod method, CancellationToken ct = default);
    Task DeleteVerificationCodeAsync(string userId, MfaMethod method, CancellationToken ct = default);

    Task SaveBackupCodesAsync(string userId, string tenantId, IReadOnlyList<string> hashedCodes, CancellationToken ct = default);
    Task<bool> UseBackupCodeAsync(string userId, string hashedCode, CancellationToken ct = default);
}

/// <summary>
/// Interface for SMS provider.
/// </summary>
public interface ISmsProvider
{
    Task SendAsync(string phoneNumber, string message, CancellationToken ct = default);
}

/// <summary>
/// Interface for email provider.
/// </summary>
public interface IEmailProvider
{
    Task SendAsync(string email, string subject, string body, CancellationToken ct = default);
}
