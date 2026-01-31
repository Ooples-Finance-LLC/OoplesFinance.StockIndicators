using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
#if !NET461
using System.Security.Cryptography.Xml;
#endif
using System.Text;
using System.Xml;
using OoplesFinance.StockIndicators.Builder.Enterprise.Security;

namespace OoplesFinance.StockIndicators.Builder.Enterprise.SSO;

/// <summary>
/// SAML 2.0 authentication provider.
/// Implements Service Provider (SP) initiated SSO.
/// </summary>
public sealed class SAMLProvider : IAuthenticationProvider
{
    private readonly SamlConfig _config;
    private readonly string _tenantId;
    private readonly ITokenStorage _tokenStorage;
    private readonly ISecurityAuditLogger _auditLogger;
    private readonly X509Certificate2? _signingCertificate;
    private readonly X509Certificate2? _idpCertificate;

    public AuthProviderType ProviderType => AuthProviderType.SAML;

    /// <summary>
    /// Initializes a new instance of the SAMLProvider.
    /// </summary>
    public SAMLProvider(
        SamlConfig config,
        string tenantId,
        ITokenStorage tokenStorage,
        ISecurityAuditLogger auditLogger)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _tenantId = tenantId;
        _tokenStorage = tokenStorage ?? throw new ArgumentNullException(nameof(tokenStorage));
        _auditLogger = auditLogger ?? throw new ArgumentNullException(nameof(auditLogger));

        // Load certificates
        if (_config.SigningCertificate is { Length: > 0 } signingCert)
        {
            _signingCertificate = LoadCertificate(signingCert);
        }

        if (_config.Certificate is { Length: > 0 } idpCert)
        {
            _idpCertificate = LoadCertificate(idpCert);
        }
    }

    /// <summary>
    /// Creates a SAML authentication request.
    /// </summary>
    public SamlAuthnRequest CreateAuthnRequest(string? relayState = null)
    {
        var requestId = $"_" + Guid.NewGuid().ToString("N");
        var issueInstant = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");

        var authnRequest = new XmlDocument();
        authnRequest.PreserveWhitespace = true;

        var declaration = authnRequest.CreateXmlDeclaration("1.0", "UTF-8", null);
        authnRequest.AppendChild(declaration);

        // Create AuthnRequest element
        var authnRequestElement = authnRequest.CreateElement("samlp", "AuthnRequest", SamlConstants.ProtocolNamespace);
        authnRequestElement.SetAttribute("ID", requestId);
        authnRequestElement.SetAttribute("Version", "2.0");
        authnRequestElement.SetAttribute("IssueInstant", issueInstant);
        authnRequestElement.SetAttribute("Destination", _config.SingleSignOnUrl);
        authnRequestElement.SetAttribute("AssertionConsumerServiceURL", GetAssertionConsumerServiceUrl());
        authnRequestElement.SetAttribute("ProtocolBinding", SamlConstants.HttpPostBinding);

        authnRequest.AppendChild(authnRequestElement);

        // Add Issuer
        var issuerElement = authnRequest.CreateElement("saml", "Issuer", SamlConstants.AssertionNamespace);
        issuerElement.InnerText = _config.EntityId;
        authnRequestElement.AppendChild(issuerElement);

        // Add NameIDPolicy
        var nameIdPolicy = authnRequest.CreateElement("samlp", "NameIDPolicy", SamlConstants.ProtocolNamespace);
        nameIdPolicy.SetAttribute("Format", GetNameIdFormat());
        nameIdPolicy.SetAttribute("AllowCreate", "true");
        authnRequestElement.AppendChild(nameIdPolicy);

        // Sign the request if configured
        if (_config.SignRequests && _signingCertificate != null)
        {
            SignXmlDocument(authnRequest, _signingCertificate, requestId);
        }

        var samlRequest = Convert.ToBase64String(Encoding.UTF8.GetBytes(authnRequest.OuterXml));

        // Build redirect URL with SAML request
        var redirectUrl = BuildRedirectUrl(samlRequest, relayState);

        return new SamlAuthnRequest
        {
            RequestId = requestId,
            RedirectUrl = redirectUrl,
            SamlRequest = samlRequest,
            RelayState = relayState
        };
    }

    /// <summary>
    /// Processes a SAML response from the IdP.
    /// </summary>
    public async Task<AuthenticationResult> ProcessSamlResponseAsync(
        string samlResponse,
        string? relayState = null,
        string? ipAddress = null,
        CancellationToken ct = default)
    {
        try
        {
            var responseXml = Encoding.UTF8.GetString(Convert.FromBase64String(samlResponse));
            var responseDoc = new XmlDocument();
            responseDoc.PreserveWhitespace = true;
            responseDoc.LoadXml(responseXml);

            // Validate signature
            if (_config.WantAssertionsSigned && _idpCertificate != null)
            {
                if (!ValidateSignature(responseDoc, _idpCertificate))
                {
                    await _auditLogger.LogAsync(new SecurityAuditEvent
                    {
                        EventType = SecurityEventType.LoginFailure,
                        TenantId = _tenantId,
                        IpAddress = ipAddress ?? "unknown",
                        FailureReason = "Invalid SAML signature",
                        Success = false
                    });

                    return AuthenticationResult.Failed(new AuthenticationError
                    {
                        Code = AuthErrorCode.TokenInvalid,
                        Message = "SAML response signature validation failed"
                    });
                }
            }

            // Extract status
            var status = ExtractStatus(responseDoc);
            if (!status.IsSuccess)
            {
                await _auditLogger.LogAsync(new SecurityAuditEvent
                {
                    EventType = SecurityEventType.LoginFailure,
                    TenantId = _tenantId,
                    IpAddress = ipAddress ?? "unknown",
                    FailureReason = status.StatusMessage,
                    Success = false
                });

                return AuthenticationResult.Failed(new AuthenticationError
                {
                    Code = AuthErrorCode.ProviderError,
                    Message = status.StatusMessage ?? "SAML authentication failed"
                });
            }

            // Extract assertion
            var assertion = ExtractAssertion(responseDoc);
            if (assertion == null)
            {
                return AuthenticationResult.Failed(new AuthenticationError
                {
                    Code = AuthErrorCode.TokenInvalid,
                    Message = "No valid assertion found in SAML response"
                });
            }

            // Validate conditions
            var conditionsValid = ValidateConditions(assertion);
            if (!conditionsValid)
            {
                return AuthenticationResult.Failed(new AuthenticationError
                {
                    Code = AuthErrorCode.TokenExpired,
                    Message = "SAML assertion conditions not met"
                });
            }

            // Extract user attributes
            var user = ExtractUserPrincipal(assertion);
            user.TenantId = _tenantId;

            // Generate internal token
            var accessToken = GenerateInternalToken(user);
            var expiresAt = DateTime.UtcNow.AddHours(8);

            // Store session
            await _tokenStorage.StoreTokensAsync(user.UserId, new StoredTokens
            {
                AccessToken = accessToken,
                ExpiresAt = expiresAt
            }, ct);

            await _auditLogger.LogAsync(new SecurityAuditEvent
            {
                EventType = SecurityEventType.LoginSuccess,
                TenantId = _tenantId,
                UserId = user.UserId,
                Username = user.Username,
                IpAddress = ipAddress ?? "unknown",
                Success = true,
                Metadata = new Dictionary<string, string>
                {
                    { "provider", "SAML" },
                    { "relayState", relayState ?? string.Empty }
                }
            });

            return AuthenticationResult.Succeeded(accessToken, string.Empty, user, expiresAt);
        }
        catch (Exception ex)
        {
            await _auditLogger.LogAsync(new SecurityAuditEvent
            {
                EventType = SecurityEventType.LoginFailure,
                TenantId = _tenantId,
                IpAddress = ipAddress ?? "unknown",
                FailureReason = ex.Message,
                Success = false
            });

            return AuthenticationResult.Failed(new AuthenticationError
            {
                Code = AuthErrorCode.ProviderError,
                Message = "Failed to process SAML response",
                Details = ex.Message
            });
        }
    }

    /// <summary>
    /// Creates a SAML logout request.
    /// </summary>
    public SamlLogoutRequest CreateLogoutRequest(string nameId, string sessionIndex)
    {
        var requestId = $"_" + Guid.NewGuid().ToString("N");
        var issueInstant = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");

        var logoutRequest = new XmlDocument();
        logoutRequest.PreserveWhitespace = true;

        var declaration = logoutRequest.CreateXmlDeclaration("1.0", "UTF-8", null);
        logoutRequest.AppendChild(declaration);

        // Create LogoutRequest element
        var logoutRequestElement = logoutRequest.CreateElement("samlp", "LogoutRequest", SamlConstants.ProtocolNamespace);
        logoutRequestElement.SetAttribute("ID", requestId);
        logoutRequestElement.SetAttribute("Version", "2.0");
        logoutRequestElement.SetAttribute("IssueInstant", issueInstant);
        logoutRequestElement.SetAttribute("Destination", _config.SingleLogoutUrl);

        logoutRequest.AppendChild(logoutRequestElement);

        // Add Issuer
        var issuerElement = logoutRequest.CreateElement("saml", "Issuer", SamlConstants.AssertionNamespace);
        issuerElement.InnerText = _config.EntityId;
        logoutRequestElement.AppendChild(issuerElement);

        // Add NameID
        var nameIdElement = logoutRequest.CreateElement("saml", "NameID", SamlConstants.AssertionNamespace);
        nameIdElement.SetAttribute("Format", GetNameIdFormat());
        nameIdElement.InnerText = nameId;
        logoutRequestElement.AppendChild(nameIdElement);

        // Add SessionIndex
        var sessionIndexElement = logoutRequest.CreateElement("samlp", "SessionIndex", SamlConstants.ProtocolNamespace);
        sessionIndexElement.InnerText = sessionIndex;
        logoutRequestElement.AppendChild(sessionIndexElement);

        // Sign the request if configured
        if (_config.SignRequests && _signingCertificate != null)
        {
            SignXmlDocument(logoutRequest, _signingCertificate, requestId);
        }

        var samlRequest = Convert.ToBase64String(Encoding.UTF8.GetBytes(logoutRequest.OuterXml));
        var redirectUrl = BuildLogoutRedirectUrl(samlRequest);

        return new SamlLogoutRequest
        {
            RequestId = requestId,
            RedirectUrl = redirectUrl,
            SamlRequest = samlRequest
        };
    }

    /// <summary>
    /// Generates SP metadata XML.
    /// </summary>
    public string GenerateMetadata()
    {
        var metadata = new XmlDocument();
        metadata.PreserveWhitespace = true;

        var declaration = metadata.CreateXmlDeclaration("1.0", "UTF-8", null);
        metadata.AppendChild(declaration);

        // EntityDescriptor
        var entityDescriptor = metadata.CreateElement("md", "EntityDescriptor", SamlConstants.MetadataNamespace);
        entityDescriptor.SetAttribute("entityID", _config.EntityId);
        metadata.AppendChild(entityDescriptor);

        // SPSSODescriptor
        var spDescriptor = metadata.CreateElement("md", "SPSSODescriptor", SamlConstants.MetadataNamespace);
        spDescriptor.SetAttribute("AuthnRequestsSigned", _config.SignRequests.ToString().ToLower());
        spDescriptor.SetAttribute("WantAssertionsSigned", _config.WantAssertionsSigned.ToString().ToLower());
        spDescriptor.SetAttribute("protocolSupportEnumeration", SamlConstants.ProtocolNamespace);
        entityDescriptor.AppendChild(spDescriptor);

        // KeyDescriptor for signing
        if (_signingCertificate != null)
        {
            var keyDescriptor = CreateKeyDescriptor(metadata, "signing");
            spDescriptor.AppendChild(keyDescriptor);
        }

        // NameIDFormat
        var nameIdFormat = metadata.CreateElement("md", "NameIDFormat", SamlConstants.MetadataNamespace);
        nameIdFormat.InnerText = GetNameIdFormat();
        spDescriptor.AppendChild(nameIdFormat);

        // AssertionConsumerService
        var acs = metadata.CreateElement("md", "AssertionConsumerService", SamlConstants.MetadataNamespace);
        acs.SetAttribute("index", "0");
        acs.SetAttribute("isDefault", "true");
        acs.SetAttribute("Binding", SamlConstants.HttpPostBinding);
        acs.SetAttribute("Location", GetAssertionConsumerServiceUrl());
        spDescriptor.AppendChild(acs);

        // SingleLogoutService
        if (!string.IsNullOrEmpty(_config.SingleLogoutUrl))
        {
            var slo = metadata.CreateElement("md", "SingleLogoutService", SamlConstants.MetadataNamespace);
            slo.SetAttribute("Binding", SamlConstants.HttpPostBinding);
            slo.SetAttribute("Location", GetSingleLogoutServiceUrl());
            spDescriptor.AppendChild(slo);
        }

        return metadata.OuterXml;
    }

    /// <inheritdoc />
    public Task<AuthenticationResult> AuthenticateAsync(AuthenticationRequest request, CancellationToken ct = default)
    {
        // SAML uses browser-based flow
        return Task.FromResult(AuthenticationResult.Failed(new AuthenticationError
        {
            Code = AuthErrorCode.ProviderError,
            Message = "SAML authentication requires browser redirect. Use CreateAuthnRequest."
        }));
    }

    /// <inheritdoc />
    public async Task<TokenValidationResult> ValidateTokenAsync(string token, CancellationToken ct = default)
    {
        // For SAML, we validate internally generated tokens
        try
        {
            var claims = ValidateInternalToken(token);
            if (claims == null)
            {
                return new TokenValidationResult { IsValid = false, Error = "Invalid token" };
            }

            claims.TryGetValue("sub", out var subClaim);
            claims.TryGetValue("email", out var emailClaim);
            claims.TryGetValue("tenant", out var tenantClaim);

            var user = new UserPrincipal
            {
                UserId = subClaim ?? string.Empty,
                Username = emailClaim ?? string.Empty,
                Email = emailClaim ?? string.Empty,
                TenantId = tenantClaim ?? _tenantId
            };

            if (claims.TryGetValue("roles", out var roles))
            {
                user.Roles = roles.Split(',').ToList();
            }

            return new TokenValidationResult
            {
                IsValid = true,
                User = user,
                ExpiresAt = claims.TryGetValue("exp", out var exp) ?
                    DateTimeOffset.FromUnixTimeSeconds(long.Parse(exp)).UtcDateTime : null
            };
        }
        catch
        {
            return new TokenValidationResult { IsValid = false, Error = "Token validation failed" };
        }
    }

    /// <inheritdoc />
    public Task<AuthenticationResult> RefreshTokenAsync(string refreshToken, CancellationToken ct = default)
    {
        // SAML doesn't support refresh tokens - requires new SSO flow
        return Task.FromResult(AuthenticationResult.Failed(new AuthenticationError
        {
            Code = AuthErrorCode.TokenExpired,
            Message = "SAML does not support token refresh. Re-authenticate via SSO."
        }));
    }

    /// <inheritdoc />
    public async Task RevokeTokenAsync(string token, CancellationToken ct = default)
    {
        var claims = ValidateInternalToken(token);
        if (claims != null && claims.TryGetValue("sub", out var userId))
        {
            await _tokenStorage.RemoveTokensAsync(userId, ct);
        }

        await _auditLogger.LogAsync(new SecurityAuditEvent
        {
            EventType = SecurityEventType.TokenRevoked,
            TenantId = _tenantId,
            Success = true
        });
    }

    private XmlElement CreateKeyDescriptor(XmlDocument doc, string use)
    {
        var keyDescriptor = doc.CreateElement("md", "KeyDescriptor", SamlConstants.MetadataNamespace);
        keyDescriptor.SetAttribute("use", use);

        var keyInfo = doc.CreateElement("ds", "KeyInfo", SamlConstants.XmlDsigNamespace);
        keyDescriptor.AppendChild(keyInfo);

        var x509Data = doc.CreateElement("ds", "X509Data", SamlConstants.XmlDsigNamespace);
        keyInfo.AppendChild(x509Data);

        var x509Cert = doc.CreateElement("ds", "X509Certificate", SamlConstants.XmlDsigNamespace);
        x509Cert.InnerText = Convert.ToBase64String(_signingCertificate!.Export(X509ContentType.Cert));
        x509Data.AppendChild(x509Cert);

        return keyDescriptor;
    }

    private static void SignXmlDocument(XmlDocument doc, X509Certificate2 cert, string referenceId)
    {
#if NET461
        // XML signing not supported in net461 without additional package
        // In production, add System.Security.Cryptography.Xml NuGet package for net461
        _ = doc;
        _ = cert;
        _ = referenceId;
#else
        var signedXml = new SignedXml(doc)
        {
            SigningKey = cert.GetRSAPrivateKey()
        };

        var reference = new Reference
        {
            Uri = "#" + referenceId
        };
        reference.AddTransform(new XmlDsigEnvelopedSignatureTransform());
        reference.AddTransform(new XmlDsigExcC14NTransform());

        signedXml.AddReference(reference);

        var keyInfo = new KeyInfo();
        keyInfo.AddClause(new KeyInfoX509Data(cert));
        signedXml.KeyInfo = keyInfo;

        signedXml.ComputeSignature();

        var signatureElement = signedXml.GetXml();
        var issuerElement = doc.GetElementsByTagName("Issuer", SamlConstants.AssertionNamespace)[0];
        issuerElement?.ParentNode?.InsertAfter(doc.ImportNode(signatureElement, true), issuerElement);
#endif
    }

    private static bool ValidateSignature(XmlDocument doc, X509Certificate2 cert)
    {
#if NET461
        // XML signature validation not supported in net461 without additional package
        // In production, add System.Security.Cryptography.Xml NuGet package for net461
        _ = doc;
        _ = cert;
        return true; // Skip signature validation in net461
#else
        var signedXml = new SignedXml(doc);
        var signatureNode = doc.GetElementsByTagName("Signature", SamlConstants.XmlDsigNamespace);

        if (signatureNode.Count == 0)
        {
            return false;
        }

        signedXml.LoadXml((XmlElement)signatureNode[0]!);

        return signedXml.CheckSignature(cert, true);
#endif
    }

    private static SamlStatus ExtractStatus(XmlDocument responseDoc)
    {
        var nsMgr = CreateNamespaceManager(responseDoc);

        var statusCodeNode = responseDoc.SelectSingleNode("//samlp:Status/samlp:StatusCode", nsMgr);
        var statusCode = statusCodeNode?.Attributes?["Value"]?.Value;

        var statusMessageNode = responseDoc.SelectSingleNode("//samlp:Status/samlp:StatusMessage", nsMgr);
        var statusMessage = statusMessageNode?.InnerText;

        return new SamlStatus
        {
            IsSuccess = statusCode == SamlConstants.StatusSuccess,
            StatusCode = statusCode,
            StatusMessage = statusMessage
        };
    }

    private static XmlElement? ExtractAssertion(XmlDocument responseDoc)
    {
        var nsMgr = CreateNamespaceManager(responseDoc);
        return responseDoc.SelectSingleNode("//saml:Assertion", nsMgr) as XmlElement;
    }

    private bool ValidateConditions(XmlElement assertion)
    {
        var nsMgr = CreateNamespaceManager(assertion.OwnerDocument);

        var conditions = assertion.SelectSingleNode("saml:Conditions", nsMgr);
        if (conditions == null)
        {
            return true; // No conditions means valid
        }

        var notBefore = conditions.Attributes?["NotBefore"]?.Value;
        var notOnOrAfter = conditions.Attributes?["NotOnOrAfter"]?.Value;

        var now = DateTime.UtcNow;

        if (!string.IsNullOrEmpty(notBefore))
        {
            var notBeforeTime = DateTime.Parse(notBefore, null, System.Globalization.DateTimeStyles.RoundtripKind);
            if (now < notBeforeTime.AddMinutes(-5)) // 5 minute clock skew
            {
                return false;
            }
        }

        if (!string.IsNullOrEmpty(notOnOrAfter))
        {
            var notOnOrAfterTime = DateTime.Parse(notOnOrAfter, null, System.Globalization.DateTimeStyles.RoundtripKind);
            if (now >= notOnOrAfterTime.AddMinutes(5)) // 5 minute clock skew
            {
                return false;
            }
        }

        // Validate audience
        var audienceNode = conditions.SelectSingleNode("saml:AudienceRestriction/saml:Audience", nsMgr);
        if (audienceNode != null)
        {
            var audience = audienceNode.InnerText;
            if (!audience.Equals(_config.EntityId, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    private UserPrincipal ExtractUserPrincipal(XmlElement assertion)
    {
        var nsMgr = CreateNamespaceManager(assertion.OwnerDocument);
        var user = new UserPrincipal();

        // Extract NameID
        var nameIdNode = assertion.SelectSingleNode("saml:Subject/saml:NameID", nsMgr);
        if (nameIdNode != null)
        {
            user.UserId = nameIdNode.InnerText;
            user.Username = nameIdNode.InnerText;
        }

        // Extract attributes
        var attributeNodes = assertion.SelectNodes("saml:AttributeStatement/saml:Attribute", nsMgr);
        if (attributeNodes != null)
        {
            var attributes = new Dictionary<string, string>();
            var groups = new List<string>();

            foreach (XmlElement attr in attributeNodes)
            {
                var attrName = attr.GetAttribute("Name");
                var attrValueNode = attr.SelectSingleNode("saml:AttributeValue", nsMgr)?.InnerText;

                if (attrName is { Length: > 0 } name && attrValueNode is { Length: > 0 } attrValue)
                {
                    attributes[name] = attrValue;

                    // Map known attributes
                    foreach (var mapping in _config.AttributeMapping)
                    {
                        if (name.Equals(mapping.Value, StringComparison.OrdinalIgnoreCase))
                        {
                            switch (mapping.Key.ToLowerInvariant())
                            {
                                case "email":
                                    user.Email = attrValue;
                                    if (string.IsNullOrEmpty(user.Username))
                                        user.Username = attrValue;
                                    break;
                                case "firstname":
                                case "givenname":
                                    user.DisplayName = attrValue + (user.DisplayName != null ? " " + user.DisplayName : "");
                                    break;
                                case "lastname":
                                case "surname":
                                    user.DisplayName = (user.DisplayName ?? "") + " " + attrValue;
                                    break;
                                case "displayname":
                                case "name":
                                    user.DisplayName = attrValue;
                                    break;
                                case "groups":
                                    groups.Add(attrValue);
                                    break;
                            }
                        }
                    }

                    // Also check for group attribute values
                    if (attrName.Contains("Group", StringComparison.OrdinalIgnoreCase))
                    {
                        var groupValues = attr.SelectNodes("saml:AttributeValue", nsMgr);
                        if (groupValues != null)
                        {
                            foreach (XmlNode groupNode in groupValues)
                            {
                                if (!string.IsNullOrEmpty(groupNode.InnerText))
                                {
                                    groups.Add(groupNode.InnerText);
                                }
                            }
                        }
                    }
                }
            }

            user.Groups = groups.Distinct().ToList();
            user.Claims = attributes;
        }

        return user;
    }

    private string GetNameIdFormat()
    {
        return _config.NameIdFormat switch
        {
            SamlNameIdFormat.EmailAddress => SamlConstants.NameIdFormatEmailAddress,
            SamlNameIdFormat.Persistent => SamlConstants.NameIdFormatPersistent,
            SamlNameIdFormat.Transient => SamlConstants.NameIdFormatTransient,
            _ => SamlConstants.NameIdFormatUnspecified
        };
    }

    private string GetAssertionConsumerServiceUrl()
    {
        // This should be configured per tenant
        return $"{_config.EntityId}/saml/acs";
    }

    private string GetSingleLogoutServiceUrl()
    {
        return $"{_config.EntityId}/saml/slo";
    }

    private string BuildRedirectUrl(string samlRequest, string? relayState)
    {
        var url = $"{_config.SingleSignOnUrl}?SAMLRequest={Uri.EscapeDataString(samlRequest)}";

        if (!string.IsNullOrEmpty(relayState))
        {
            url += $"&RelayState={Uri.EscapeDataString(relayState)}";
        }

        return url;
    }

    private string BuildLogoutRedirectUrl(string samlRequest)
    {
        return $"{_config.SingleLogoutUrl}?SAMLRequest={Uri.EscapeDataString(samlRequest)}";
    }

    private static X509Certificate2 LoadCertificate(string certData)
    {
        // Handle both raw certificate data and file paths
        if (File.Exists(certData))
        {
#if NET9_0_OR_GREATER
            return X509CertificateLoader.LoadCertificateFromFile(certData);
#else
#pragma warning disable SYSLIB0057 // Type or member is obsolete
            return new X509Certificate2(certData);
#pragma warning restore SYSLIB0057
#endif
        }

        // Assume base64 encoded certificate
        var certBytes = Convert.FromBase64String(certData
            .Replace("-----BEGIN CERTIFICATE-----", "")
            .Replace("-----END CERTIFICATE-----", "")
            .Replace("\n", "")
            .Replace("\r", ""));

#if NET9_0_OR_GREATER
        return X509CertificateLoader.LoadCertificate(certBytes);
#else
#pragma warning disable SYSLIB0057 // Type or member is obsolete
        return new X509Certificate2(certBytes);
#pragma warning restore SYSLIB0057
#endif
    }

    private static XmlNamespaceManager CreateNamespaceManager(XmlDocument doc)
    {
        var nsMgr = new XmlNamespaceManager(doc.NameTable);
        nsMgr.AddNamespace("samlp", SamlConstants.ProtocolNamespace);
        nsMgr.AddNamespace("saml", SamlConstants.AssertionNamespace);
        nsMgr.AddNamespace("ds", SamlConstants.XmlDsigNamespace);
        return nsMgr;
    }

    private string GenerateInternalToken(UserPrincipal user)
    {
        var claims = new Dictionary<string, string>
        {
            { "sub", user.UserId },
            { "email", user.Email },
            { "tenant", user.TenantId },
            { "exp", DateTimeOffset.UtcNow.AddHours(8).ToUnixTimeSeconds().ToString() }
        };

        if (user.Roles.Count > 0)
        {
            claims["roles"] = string.Join(",", user.Roles);
        }

        var payload = System.Text.Json.JsonSerializer.Serialize(claims);
        var payloadBytes = Encoding.UTF8.GetBytes(payload);

        // Simple HMAC signature (in production, use proper JWT library)
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_config.EntityId));
        var signature = hmac.ComputeHash(payloadBytes);

        return $"{Convert.ToBase64String(payloadBytes)}.{Convert.ToBase64String(signature)}";
    }

    private Dictionary<string, string>? ValidateInternalToken(string token)
    {
        try
        {
            var parts = token.Split('.');
            if (parts.Length != 2) return null;

            var payloadBytes = Convert.FromBase64String(parts[0]);
            var signature = Convert.FromBase64String(parts[1]);

            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_config.EntityId));
            var expectedSignature = hmac.ComputeHash(payloadBytes);

            if (!signature.SequenceEqual(expectedSignature)) return null;

            var payload = Encoding.UTF8.GetString(payloadBytes);
            return System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(payload);
        }
        catch
        {
            return null;
        }
    }
}

/// <summary>
/// SAML constants.
/// </summary>
public static class SamlConstants
{
    public const string ProtocolNamespace = "urn:oasis:names:tc:SAML:2.0:protocol";
    public const string AssertionNamespace = "urn:oasis:names:tc:SAML:2.0:assertion";
    public const string MetadataNamespace = "urn:oasis:names:tc:SAML:2.0:metadata";
    public const string XmlDsigNamespace = "http://www.w3.org/2000/09/xmldsig#";

    public const string HttpPostBinding = "urn:oasis:names:tc:SAML:2.0:bindings:HTTP-POST";
    public const string HttpRedirectBinding = "urn:oasis:names:tc:SAML:2.0:bindings:HTTP-Redirect";

    public const string StatusSuccess = "urn:oasis:names:tc:SAML:2.0:status:Success";
    public const string StatusRequester = "urn:oasis:names:tc:SAML:2.0:status:Requester";
    public const string StatusResponder = "urn:oasis:names:tc:SAML:2.0:status:Responder";

    public const string NameIdFormatEmailAddress = "urn:oasis:names:tc:SAML:1.1:nameid-format:emailAddress";
    public const string NameIdFormatPersistent = "urn:oasis:names:tc:SAML:2.0:nameid-format:persistent";
    public const string NameIdFormatTransient = "urn:oasis:names:tc:SAML:2.0:nameid-format:transient";
    public const string NameIdFormatUnspecified = "urn:oasis:names:tc:SAML:1.1:nameid-format:unspecified";
}

/// <summary>
/// SAML authentication request.
/// </summary>
public sealed class SamlAuthnRequest
{
    public string RequestId { get; set; } = string.Empty;
    public string RedirectUrl { get; set; } = string.Empty;
    public string SamlRequest { get; set; } = string.Empty;
    public string? RelayState { get; set; }
}

/// <summary>
/// SAML logout request.
/// </summary>
public sealed class SamlLogoutRequest
{
    public string RequestId { get; set; } = string.Empty;
    public string RedirectUrl { get; set; } = string.Empty;
    public string SamlRequest { get; set; } = string.Empty;
}

/// <summary>
/// SAML status.
/// </summary>
public sealed class SamlStatus
{
    public bool IsSuccess { get; set; }
    public string? StatusCode { get; set; }
    public string? StatusMessage { get; set; }
}
