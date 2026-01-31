using OoplesFinance.StockIndicators.Builder.Enterprise.SSO;

namespace OoplesFinance.StockIndicators.Builder.Enterprise.Security;

/// <summary>
/// Role-Based Access Control (RBAC) manager.
/// Manages roles, permissions, and authorization policies.
/// </summary>
public sealed class RBACManager
{
    private readonly IRBACStorage _storage;
    private readonly ISecurityAuditLogger _auditLogger;
    private readonly RBACOptions _options;
    private readonly Dictionary<string, Role> _roleCache = [];
    private readonly SemaphoreSlim _cacheLock = new(1, 1);
    private DateTime _cacheLastRefreshed;

    /// <summary>
    /// Initializes a new instance of the RBACManager.
    /// </summary>
    public RBACManager(
        IRBACStorage storage,
        ISecurityAuditLogger auditLogger,
        RBACOptions? options = null)
    {
        _storage = storage ?? throw new ArgumentNullException(nameof(storage));
        _auditLogger = auditLogger ?? throw new ArgumentNullException(nameof(auditLogger));
        _options = options ?? new RBACOptions();
    }

    /// <summary>
    /// Checks if a user has a specific permission.
    /// </summary>
    public async Task<AuthorizationResult> AuthorizeAsync(
        string userId,
        string tenantId,
        string permission,
        string? resourceType = null,
        string? resourceId = null,
        CancellationToken ct = default)
    {
        // Get user's roles
        var userRoles = await _storage.GetUserRolesAsync(userId, tenantId, ct);

        // Check if any role grants the permission
        foreach (var userRole in userRoles)
        {
            var role = await GetRoleAsync(userRole.RoleId, tenantId, ct);
            if (role == null) continue;

            // Check direct permissions
            if (HasPermission(role, permission, resourceType, resourceId))
            {
                return AuthorizationResult.Allowed(role.Name, permission);
            }

            // Check inherited roles
            foreach (var inheritedRoleId in role.InheritedRoles)
            {
                var inheritedRole = await GetRoleAsync(inheritedRoleId, tenantId, ct);
                if (inheritedRole != null && HasPermission(inheritedRole, permission, resourceType, resourceId))
                {
                    return AuthorizationResult.Allowed(inheritedRole.Name, permission);
                }
            }
        }

        // Check resource-specific permissions
        if (resourceType is { Length: > 0 } rt && resourceId is { Length: > 0 } ri)
        {
            var resourcePermissions = await _storage.GetResourcePermissionsAsync(
                userId, tenantId, rt, ri, ct);

            if (resourcePermissions.Any(p => p.Permission == permission || p.Permission == "*"))
            {
                return AuthorizationResult.Allowed("resource", permission);
            }
        }

        await _auditLogger.LogAsync(new SecurityAuditEvent
        {
            EventType = SecurityEventType.PermissionDenied,
            UserId = userId,
            TenantId = tenantId,
            ResourceType = resourceType,
            ResourceId = resourceId,
            Action = permission,
            Success = false
        });

        return AuthorizationResult.Denied(permission);
    }

    /// <summary>
    /// Checks multiple permissions at once.
    /// </summary>
    public async Task<Dictionary<string, AuthorizationResult>> AuthorizeManyAsync(
        string userId,
        string tenantId,
        IEnumerable<string> permissions,
        string? resourceType = null,
        string? resourceId = null,
        CancellationToken ct = default)
    {
        var results = new Dictionary<string, AuthorizationResult>();

        foreach (var permission in permissions)
        {
            results[permission] = await AuthorizeAsync(userId, tenantId, permission, resourceType, resourceId, ct);
        }

        return results;
    }

    /// <summary>
    /// Creates a new role.
    /// </summary>
    public async Task<Role> CreateRoleAsync(
        string tenantId,
        string name,
        string description,
        IEnumerable<Permission> permissions,
        IEnumerable<string>? inheritedRoles = null,
        string? createdBy = null,
        CancellationToken ct = default)
    {
        var role = new Role
        {
            RoleId = Guid.NewGuid().ToString(),
            TenantId = tenantId,
            Name = name,
            Description = description,
            Permissions = permissions.ToList(),
            InheritedRoles = inheritedRoles?.ToList() ?? [],
            CreatedAt = DateTime.UtcNow,
            CreatedBy = createdBy,
            IsSystem = false
        };

        await _storage.SaveRoleAsync(role, ct);
        InvalidateCache(tenantId);

        return role;
    }

    /// <summary>
    /// Updates an existing role.
    /// </summary>
    public async Task<Role> UpdateRoleAsync(
        string roleId,
        string tenantId,
        string? name = null,
        string? description = null,
        IEnumerable<Permission>? permissions = null,
        IEnumerable<string>? inheritedRoles = null,
        string? updatedBy = null,
        CancellationToken ct = default)
    {
        var role = await _storage.GetRoleAsync(roleId, tenantId, ct);
        if (role == null)
        {
            throw new InvalidOperationException($"Role {roleId} not found");
        }

        if (role.IsSystem && !_options.AllowSystemRoleModification)
        {
            throw new InvalidOperationException("Cannot modify system role");
        }

        if (name != null) role.Name = name;
        if (description != null) role.Description = description;
        if (permissions != null) role.Permissions = permissions.ToList();
        if (inheritedRoles != null) role.InheritedRoles = inheritedRoles.ToList();
        role.UpdatedAt = DateTime.UtcNow;
        role.UpdatedBy = updatedBy;

        await _storage.SaveRoleAsync(role, ct);
        InvalidateCache(tenantId);

        return role;
    }

    /// <summary>
    /// Deletes a role.
    /// </summary>
    public async Task DeleteRoleAsync(
        string roleId,
        string tenantId,
        CancellationToken ct = default)
    {
        var role = await _storage.GetRoleAsync(roleId, tenantId, ct);
        if (role == null) return;

        if (role.IsSystem && !_options.AllowSystemRoleModification)
        {
            throw new InvalidOperationException("Cannot delete system role");
        }

        await _storage.DeleteRoleAsync(roleId, tenantId, ct);
        InvalidateCache(tenantId);
    }

    /// <summary>
    /// Gets all roles for a tenant.
    /// </summary>
    public async Task<IReadOnlyList<Role>> GetRolesAsync(
        string tenantId,
        CancellationToken ct = default)
    {
        return await _storage.GetRolesAsync(tenantId, ct);
    }

    /// <summary>
    /// Assigns a role to a user.
    /// </summary>
    public async Task AssignRoleAsync(
        string userId,
        string tenantId,
        string roleId,
        string? assignedBy = null,
        DateTime? expiresAt = null,
        string? scope = null,
        CancellationToken ct = default)
    {
        var role = await _storage.GetRoleAsync(roleId, tenantId, ct);
        if (role == null)
        {
            throw new InvalidOperationException($"Role {roleId} not found");
        }

        var assignment = new UserRoleAssignment
        {
            UserId = userId,
            TenantId = tenantId,
            RoleId = roleId,
            AssignedAt = DateTime.UtcNow,
            AssignedBy = assignedBy,
            ExpiresAt = expiresAt,
            Scope = scope
        };

        await _storage.AssignRoleAsync(assignment, ct);

        await _auditLogger.LogAsync(new SecurityAuditEvent
        {
            EventType = SecurityEventType.SessionCreated, // Using as role assignment event
            UserId = userId,
            TenantId = tenantId,
            Action = "RoleAssigned",
            ResourceType = "Role",
            ResourceId = roleId,
            Success = true,
            Metadata = new Dictionary<string, string>
            {
                { "roleName", role.Name },
                { "assignedBy", assignedBy ?? "system" }
            }
        });
    }

    /// <summary>
    /// Removes a role from a user.
    /// </summary>
    public async Task RemoveRoleAsync(
        string userId,
        string tenantId,
        string roleId,
        string? removedBy = null,
        CancellationToken ct = default)
    {
        await _storage.RemoveRoleAsync(userId, tenantId, roleId, ct);

        await _auditLogger.LogAsync(new SecurityAuditEvent
        {
            EventType = SecurityEventType.SessionRevoked, // Using as role removal event
            UserId = userId,
            TenantId = tenantId,
            Action = "RoleRemoved",
            ResourceType = "Role",
            ResourceId = roleId,
            Success = true,
            Metadata = new Dictionary<string, string>
            {
                { "removedBy", removedBy ?? "system" }
            }
        });
    }

    /// <summary>
    /// Gets all roles assigned to a user.
    /// </summary>
    public async Task<IReadOnlyList<UserRoleAssignment>> GetUserRolesAsync(
        string userId,
        string tenantId,
        CancellationToken ct = default)
    {
        return await _storage.GetUserRolesAsync(userId, tenantId, ct);
    }

    /// <summary>
    /// Gets all users with a specific role.
    /// </summary>
    public async Task<IReadOnlyList<string>> GetUsersInRoleAsync(
        string roleId,
        string tenantId,
        CancellationToken ct = default)
    {
        return await _storage.GetUsersInRoleAsync(roleId, tenantId, ct);
    }

    /// <summary>
    /// Grants a permission directly to a user on a specific resource.
    /// </summary>
    public async Task GrantResourcePermissionAsync(
        string userId,
        string tenantId,
        string resourceType,
        string resourceId,
        string permission,
        string? grantedBy = null,
        DateTime? expiresAt = null,
        CancellationToken ct = default)
    {
        var resourcePermission = new ResourcePermission
        {
            UserId = userId,
            TenantId = tenantId,
            ResourceType = resourceType,
            ResourceId = resourceId,
            Permission = permission,
            GrantedAt = DateTime.UtcNow,
            GrantedBy = grantedBy,
            ExpiresAt = expiresAt
        };

        await _storage.GrantResourcePermissionAsync(resourcePermission, ct);
    }

    /// <summary>
    /// Revokes a permission from a user on a specific resource.
    /// </summary>
    public async Task RevokeResourcePermissionAsync(
        string userId,
        string tenantId,
        string resourceType,
        string resourceId,
        string permission,
        CancellationToken ct = default)
    {
        await _storage.RevokeResourcePermissionAsync(userId, tenantId, resourceType, resourceId, permission, ct);
    }

    /// <summary>
    /// Gets all effective permissions for a user.
    /// </summary>
    public async Task<IReadOnlyList<EffectivePermission>> GetEffectivePermissionsAsync(
        string userId,
        string tenantId,
        CancellationToken ct = default)
    {
        var effectivePermissions = new Dictionary<string, EffectivePermission>();
        var userRoles = await _storage.GetUserRolesAsync(userId, tenantId, ct);

        foreach (var userRole in userRoles)
        {
            var role = await GetRoleAsync(userRole.RoleId, tenantId, ct);
            if (role == null) continue;

            AddPermissionsFromRole(effectivePermissions, role, userRole);

            // Add inherited permissions
            foreach (var inheritedRoleId in role.InheritedRoles)
            {
                var inheritedRole = await GetRoleAsync(inheritedRoleId, tenantId, ct);
                if (inheritedRole != null)
                {
                    AddPermissionsFromRole(effectivePermissions, inheritedRole, userRole, inherited: true);
                }
            }
        }

        return effectivePermissions.Values.ToList();
    }

    /// <summary>
    /// Creates default system roles for a new tenant.
    /// </summary>
    public async Task CreateDefaultRolesAsync(string tenantId, CancellationToken ct = default)
    {
        var defaultRoles = new[]
        {
            new Role
            {
                RoleId = "admin",
                TenantId = tenantId,
                Name = "Administrator",
                Description = "Full system access",
                IsSystem = true,
                Permissions =
                [
                    new Permission { Name = "*", ResourceType = "*", Actions = ["*"] }
                ]
            },
            new Role
            {
                RoleId = "trader",
                TenantId = tenantId,
                Name = "Trader",
                Description = "Can execute trades and manage orders",
                IsSystem = true,
                Permissions =
                [
                    new Permission { Name = "trade:execute", ResourceType = "Order", Actions = ["create", "cancel"] },
                    new Permission { Name = "position:view", ResourceType = "Position", Actions = ["read"] },
                    new Permission { Name = "account:view", ResourceType = "Account", Actions = ["read"] },
                    new Permission { Name = "strategy:run", ResourceType = "Strategy", Actions = ["execute"] }
                ]
            },
            new Role
            {
                RoleId = "analyst",
                TenantId = tenantId,
                Name = "Analyst",
                Description = "Can view data and run analysis",
                IsSystem = true,
                Permissions =
                [
                    new Permission { Name = "data:read", ResourceType = "MarketData", Actions = ["read"] },
                    new Permission { Name = "analysis:run", ResourceType = "Analysis", Actions = ["execute", "read"] },
                    new Permission { Name = "report:view", ResourceType = "Report", Actions = ["read"] }
                ]
            },
            new Role
            {
                RoleId = "viewer",
                TenantId = tenantId,
                Name = "Viewer",
                Description = "Read-only access",
                IsSystem = true,
                Permissions =
                [
                    new Permission { Name = "position:view", ResourceType = "Position", Actions = ["read"] },
                    new Permission { Name = "account:view", ResourceType = "Account", Actions = ["read"] },
                    new Permission { Name = "report:view", ResourceType = "Report", Actions = ["read"] }
                ]
            }
        };

        foreach (var role in defaultRoles)
        {
            role.CreatedAt = DateTime.UtcNow;
            await _storage.SaveRoleAsync(role, ct);
        }
    }

    private async Task<Role?> GetRoleAsync(string roleId, string tenantId, CancellationToken ct)
    {
        var cacheKey = $"{tenantId}:{roleId}";

        await _cacheLock.WaitAsync(ct);
        try
        {
            // Check if cache needs refresh
            if (DateTime.UtcNow - _cacheLastRefreshed > _options.CacheExpiration)
            {
                _roleCache.Clear();
                _cacheLastRefreshed = DateTime.UtcNow;
            }

            if (_roleCache.TryGetValue(cacheKey, out var cachedRole))
            {
                return cachedRole;
            }

            var role = await _storage.GetRoleAsync(roleId, tenantId, ct);
            if (role != null)
            {
                _roleCache[cacheKey] = role;
                _cacheLastRefreshed = DateTime.UtcNow;
            }

            return role;
        }
        finally
        {
            _cacheLock.Release();
        }
    }

    private void InvalidateCache(string tenantId)
    {
        _cacheLock.Wait();
        try
        {
            var keysToRemove = _roleCache.Keys.Where(k => k.StartsWith(tenantId + ":")).ToList();
            foreach (var key in keysToRemove)
            {
                _roleCache.Remove(key);
            }
        }
        finally
        {
            _cacheLock.Release();
        }
    }

    private static bool HasPermission(Role role, string permission, string? resourceType, string? resourceId)
    {
        foreach (var rolePermission in role.Permissions)
        {
            // Check wildcard permission
            if (rolePermission.Name == "*")
            {
                return true;
            }

            // Check exact permission match
            if (rolePermission.Name.Equals(permission, StringComparison.OrdinalIgnoreCase))
            {
                // Check resource type if specified
                if (!string.IsNullOrEmpty(resourceType) &&
                    !string.IsNullOrEmpty(rolePermission.ResourceType) &&
                    rolePermission.ResourceType != "*" &&
                    !rolePermission.ResourceType.Equals(resourceType, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                // Check resource ID if constraint exists
                if (rolePermission.ResourceConstraints.Count > 0 && !string.IsNullOrEmpty(resourceId))
                {
                    var hasConstraint = rolePermission.ResourceConstraints.Any(c =>
                        c.Equals(resourceId, StringComparison.OrdinalIgnoreCase) || c == "*");

                    if (!hasConstraint)
                    {
                        continue;
                    }
                }

                return true;
            }

            // Check wildcard patterns (e.g., "trade:*" matches "trade:execute")
            if (rolePermission.Name.EndsWith(":*"))
            {
                var prefix = rolePermission.Name.Substring(0, rolePermission.Name.Length - 1);
                if (permission.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static void AddPermissionsFromRole(
        Dictionary<string, EffectivePermission> permissions,
        Role role,
        UserRoleAssignment assignment,
        bool inherited = false)
    {
        foreach (var permission in role.Permissions)
        {
            var key = $"{permission.Name}:{permission.ResourceType}";

            if (!permissions.TryGetValue(key, out var existing))
            {
                permissions[key] = new EffectivePermission
                {
                    Permission = permission.Name,
                    ResourceType = permission.ResourceType,
                    Actions = permission.Actions.ToList(),
                    GrantedThrough = role.Name,
                    IsInherited = inherited,
                    ExpiresAt = assignment.ExpiresAt,
                    Scope = assignment.Scope
                };
            }
            else
            {
                // Merge actions
                foreach (var action in permission.Actions)
                {
                    if (!existing.Actions.Contains(action))
                    {
                        existing.Actions.Add(action);
                    }
                }
            }
        }
    }
}

/// <summary>
/// RBAC options.
/// </summary>
public sealed class RBACOptions
{
    /// <summary>Cache expiration time.</summary>
    public TimeSpan CacheExpiration { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>Whether to allow modification of system roles.</summary>
    public bool AllowSystemRoleModification { get; set; } = false;
}

/// <summary>
/// Role definition.
/// </summary>
public sealed class Role
{
    public string RoleId { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public IReadOnlyList<Permission> Permissions { get; set; } = [];
    public IReadOnlyList<string> InheritedRoles { get; set; } = [];
    public bool IsSystem { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}

/// <summary>
/// Permission definition.
/// </summary>
public sealed class Permission
{
    public string Name { get; set; } = string.Empty;
    public string ResourceType { get; set; } = string.Empty;
    public IReadOnlyList<string> Actions { get; set; } = [];
    public IReadOnlyList<string> ResourceConstraints { get; set; } = [];
}

/// <summary>
/// User role assignment.
/// </summary>
public sealed class UserRoleAssignment
{
    public string UserId { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
    public string RoleId { get; set; } = string.Empty;
    public DateTime AssignedAt { get; set; }
    public string? AssignedBy { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public string? Scope { get; set; }
}

/// <summary>
/// Resource-specific permission.
/// </summary>
public sealed class ResourcePermission
{
    public string UserId { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
    public string ResourceType { get; set; } = string.Empty;
    public string ResourceId { get; set; } = string.Empty;
    public string Permission { get; set; } = string.Empty;
    public DateTime GrantedAt { get; set; }
    public string? GrantedBy { get; set; }
    public DateTime? ExpiresAt { get; set; }
}

/// <summary>
/// Authorization result.
/// </summary>
public sealed class AuthorizationResult
{
    public bool IsAllowed { get; set; }
    public string? GrantedBy { get; set; }
    public string? Permission { get; set; }
    public string? DenialReason { get; set; }

    public static AuthorizationResult Allowed(string grantedBy, string permission) =>
        new() { IsAllowed = true, GrantedBy = grantedBy, Permission = permission };

    public static AuthorizationResult Denied(string permission, string? reason = null) =>
        new() { IsAllowed = false, Permission = permission, DenialReason = reason ?? "Permission denied" };
}

/// <summary>
/// Effective permission for a user.
/// </summary>
public sealed class EffectivePermission
{
    public string Permission { get; set; } = string.Empty;
    public string ResourceType { get; set; } = string.Empty;
    public List<string> Actions { get; set; } = [];
    public string GrantedThrough { get; set; } = string.Empty;
    public bool IsInherited { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public string? Scope { get; set; }
}

/// <summary>
/// Interface for RBAC storage.
/// </summary>
public interface IRBACStorage
{
    Task SaveRoleAsync(Role role, CancellationToken ct = default);
    Task<Role?> GetRoleAsync(string roleId, string tenantId, CancellationToken ct = default);
    Task<IReadOnlyList<Role>> GetRolesAsync(string tenantId, CancellationToken ct = default);
    Task DeleteRoleAsync(string roleId, string tenantId, CancellationToken ct = default);

    Task AssignRoleAsync(UserRoleAssignment assignment, CancellationToken ct = default);
    Task RemoveRoleAsync(string userId, string tenantId, string roleId, CancellationToken ct = default);
    Task<IReadOnlyList<UserRoleAssignment>> GetUserRolesAsync(string userId, string tenantId, CancellationToken ct = default);
    Task<IReadOnlyList<string>> GetUsersInRoleAsync(string roleId, string tenantId, CancellationToken ct = default);

    Task GrantResourcePermissionAsync(ResourcePermission permission, CancellationToken ct = default);
    Task RevokeResourcePermissionAsync(string userId, string tenantId, string resourceType, string resourceId, string permission, CancellationToken ct = default);
    Task<IReadOnlyList<ResourcePermission>> GetResourcePermissionsAsync(string userId, string tenantId, string resourceType, string resourceId, CancellationToken ct = default);
}
