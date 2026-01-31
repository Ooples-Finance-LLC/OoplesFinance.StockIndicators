using System.Globalization;
using System.Text;
using System.Xml.Linq;

namespace OoplesFinance.StockIndicators.Builder.Compliance;

/// <summary>
/// Generates regulatory reports for compliance requirements.
/// Supports Form 13F, Schedule D, and other SEC filings.
/// </summary>
public sealed class RegulatoryReportingEngine
{
    private readonly RegulatoryReportingOptions _options;

    /// <summary>
    /// Initializes a new instance of the RegulatoryReportingEngine.
    /// </summary>
    public RegulatoryReportingEngine(RegulatoryReportingOptions? options = null)
    {
        _options = options ?? new RegulatoryReportingOptions();
    }

    /// <summary>
    /// Generates a Form 13F report for institutional investment managers.
    /// </summary>
    public Form13FReport GenerateForm13F(
        Form13FInput input,
        DateTime reportingPeriodEnd)
    {
        var report = new Form13FReport
        {
            ReportingPeriod = reportingPeriodEnd,
            FilerInfo = input.FilerInfo,
            GeneratedAt = DateTime.UtcNow
        };

        // Filter to only 13F securities
        var eligibleHoldings = input.Holdings
            .Where(h => h.Is13FSecurity)
            .OrderBy(h => h.IssuerName)
            .ToList();

        foreach (var holding in eligibleHoldings)
        {
            var entry = new Form13FEntry
            {
                IssuerName = holding.IssuerName,
                TitleOfClass = holding.TitleOfClass,
                Cusip = holding.Cusip,
                Value = (long)Math.Round(holding.MarketValue / 1000m), // Report in thousands
                SharesOrPrincipalAmount = holding.Shares,
                SharesOrPrincipalAmountType = holding.IsDebt ? "PRN" : "SH",
                InvestmentDiscretion = holding.InvestmentDiscretion,
                OtherManagers = holding.OtherManagers,
                VotingAuthoritySole = holding.VotingAuthoritySole,
                VotingAuthorityShared = holding.VotingAuthorityShared,
                VotingAuthorityNone = holding.VotingAuthorityNone
            };

            report.InfoTable.Add(entry);
            report.TotalValue += entry.Value;
            report.TotalHoldings++;
        }

        return report;
    }

    /// <summary>
    /// Generates Form 13F XML for electronic filing.
    /// </summary>
    public string GenerateForm13FXml(Form13FReport report)
    {
        var ns = XNamespace.Get("http://www.sec.gov/edgar/document/thirteenf/informationtable");

        var xml = new XDocument(
            new XDeclaration("1.0", "UTF-8", null),
            new XElement(ns + "informationTable",
                new XAttribute("xmlns", ns.NamespaceName),
                report.InfoTable.Select(entry =>
                    new XElement(ns + "infoTable",
                        new XElement(ns + "nameOfIssuer", entry.IssuerName),
                        new XElement(ns + "titleOfClass", entry.TitleOfClass),
                        new XElement(ns + "cusip", entry.Cusip),
                        new XElement(ns + "value", entry.Value),
                        new XElement(ns + "shrsOrPrnAmt",
                            new XElement(ns + "sshPrnamt", entry.SharesOrPrincipalAmount),
                            new XElement(ns + "sshPrnamtType", entry.SharesOrPrincipalAmountType)
                        ),
                        new XElement(ns + "investmentDiscretion", entry.InvestmentDiscretion),
                        entry.OtherManagers > 0
                            ? new XElement(ns + "otherManager", entry.OtherManagers)
                            : null,
                        new XElement(ns + "votingAuthority",
                            new XElement(ns + "Sole", entry.VotingAuthoritySole),
                            new XElement(ns + "Shared", entry.VotingAuthorityShared),
                            new XElement(ns + "None", entry.VotingAuthorityNone)
                        )
                    )
                )
            )
        );

        return xml.ToString();
    }

    /// <summary>
    /// Generates Schedule D (Form 1040) for capital gains/losses.
    /// </summary>
    public ScheduleDReport GenerateScheduleD(
        IReadOnlyList<TaxLotDisposition> dispositions,
        int taxYear)
    {
        var report = new ScheduleDReport
        {
            TaxYear = taxYear,
            GeneratedAt = DateTime.UtcNow
        };

        foreach (var disposition in dispositions)
        {
            var holdingPeriod = (disposition.SaleDate - disposition.AcquiredDate).TotalDays;
            var isShortTerm = holdingPeriod <= 365;

            var entry = new ScheduleDEntry
            {
                Description = disposition.Description,
                DateAcquired = disposition.AcquiredDate,
                DateSold = disposition.SaleDate,
                Proceeds = disposition.Proceeds,
                CostBasis = disposition.CostBasis,
                AdjustmentCode = disposition.WashSaleDisallowed > 0 ? "W" : null,
                AdjustmentAmount = disposition.WashSaleDisallowed,
                GainOrLoss = disposition.Proceeds - disposition.CostBasis - disposition.WashSaleDisallowed
            };

            if (isShortTerm)
            {
                report.ShortTermTransactions.Add(entry);
                if (entry.GainOrLoss >= 0)
                {
                    report.ShortTermGains += entry.GainOrLoss;
                }
                else
                {
                    report.ShortTermLosses += Math.Abs(entry.GainOrLoss);
                }
            }
            else
            {
                report.LongTermTransactions.Add(entry);
                if (entry.GainOrLoss >= 0)
                {
                    report.LongTermGains += entry.GainOrLoss;
                }
                else
                {
                    report.LongTermLosses += Math.Abs(entry.GainOrLoss);
                }
            }
        }

        report.NetShortTermGainLoss = report.ShortTermGains - report.ShortTermLosses;
        report.NetLongTermGainLoss = report.LongTermGains - report.LongTermLosses;
        report.TotalNetGainLoss = report.NetShortTermGainLoss + report.NetLongTermGainLoss;

        // Calculate loss carryover
        if (report.TotalNetGainLoss < 0)
        {
            var usableLoss = Math.Min(Math.Abs(report.TotalNetGainLoss), _options.AnnualLossLimit);
            report.LossCarryforward = Math.Abs(report.TotalNetGainLoss) - usableLoss;
        }

        return report;
    }

    /// <summary>
    /// Generates Form 8949 for sales and dispositions of capital assets.
    /// </summary>
    public Form8949Report GenerateForm8949(
        IReadOnlyList<TaxLotDisposition> dispositions,
        int taxYear)
    {
        var report = new Form8949Report
        {
            TaxYear = taxYear,
            GeneratedAt = DateTime.UtcNow
        };

        foreach (var disposition in dispositions)
        {
            var holdingPeriod = (disposition.SaleDate - disposition.AcquiredDate).TotalDays;
            var isShortTerm = holdingPeriod <= 365;

            // Determine box category based on reporting
            Form8949Box box;
            if (disposition.Basis1099Reported)
            {
                box = isShortTerm ? Form8949Box.A : Form8949Box.D;
            }
            else if (disposition.BasisReportedToIRS)
            {
                box = isShortTerm ? Form8949Box.B : Form8949Box.E;
            }
            else
            {
                box = isShortTerm ? Form8949Box.C : Form8949Box.F;
            }

            var entry = new Form8949Entry
            {
                Box = box,
                Description = disposition.Description,
                DateAcquired = disposition.AcquiredDate,
                DateSold = disposition.SaleDate,
                Proceeds = disposition.Proceeds,
                CostBasis = disposition.CostBasis,
                AdjustmentCode = disposition.WashSaleDisallowed > 0 ? "W" : null,
                AdjustmentAmount = disposition.WashSaleDisallowed,
                GainOrLoss = disposition.Proceeds - disposition.CostBasis - disposition.WashSaleDisallowed
            };

            switch (box)
            {
                case Form8949Box.A:
                    report.PartI_BoxA.Add(entry);
                    break;
                case Form8949Box.B:
                    report.PartI_BoxB.Add(entry);
                    break;
                case Form8949Box.C:
                    report.PartI_BoxC.Add(entry);
                    break;
                case Form8949Box.D:
                    report.PartII_BoxD.Add(entry);
                    break;
                case Form8949Box.E:
                    report.PartII_BoxE.Add(entry);
                    break;
                case Form8949Box.F:
                    report.PartII_BoxF.Add(entry);
                    break;
            }
        }

        // Calculate totals
        report.TotalPartI = report.PartI_BoxA.Concat(report.PartI_BoxB).Concat(report.PartI_BoxC)
            .Sum(e => e.GainOrLoss);
        report.TotalPartII = report.PartII_BoxD.Concat(report.PartII_BoxE).Concat(report.PartII_BoxF)
            .Sum(e => e.GainOrLoss);

        return report;
    }

    /// <summary>
    /// Generates a wash sale report.
    /// </summary>
    public WashSaleReport GenerateWashSaleReport(
        IReadOnlyList<TaxLotDisposition> dispositions,
        int taxYear)
    {
        var report = new WashSaleReport
        {
            TaxYear = taxYear,
            GeneratedAt = DateTime.UtcNow
        };

        var washSales = dispositions
            .Where(d => d.WashSaleDisallowed > 0)
            .OrderBy(d => d.SaleDate)
            .ToList();

        foreach (var sale in washSales)
        {
            report.WashSales.Add(new WashSaleEntry
            {
                Symbol = sale.Symbol,
                SaleDate = sale.SaleDate,
                SaleProceeds = sale.Proceeds,
                OriginalCostBasis = sale.OriginalCostBasis,
                DisallowedLoss = sale.WashSaleDisallowed,
                AdjustedCostBasis = sale.CostBasis,
                ReplacementPurchaseDate = sale.ReplacementPurchaseDate,
                ReplacementShares = sale.ReplacementShares
            });

            report.TotalDisallowedLoss += sale.WashSaleDisallowed;
        }

        report.TotalWashSales = washSales.Count;

        return report;
    }

    /// <summary>
    /// Generates an FBAR (FinCEN 114) report for foreign accounts.
    /// </summary>
    public FBARReport GenerateFBAR(
        IReadOnlyList<ForeignAccount> accounts,
        int reportingYear)
    {
        var report = new FBARReport
        {
            ReportingYear = reportingYear,
            GeneratedAt = DateTime.UtcNow
        };

        foreach (var account in accounts)
        {
            report.Accounts.Add(new FBARAccountEntry
            {
                AccountNumber = account.AccountNumber,
                FinancialInstitution = account.FinancialInstitution,
                Country = account.Country,
                AccountType = account.AccountType,
                MaximumValue = account.MaximumValueDuringYear,
                Currency = account.Currency,
                JointAccount = account.IsJointAccount,
                SignatureAuthority = account.HasSignatureAuthority
            });

            report.TotalMaxValue += account.MaximumValueDuringYear;
        }

        report.FilingRequired = report.TotalMaxValue >= _options.FBARThreshold;

        return report;
    }

    /// <summary>
    /// Generates a dividend summary report.
    /// </summary>
    public DividendSummaryReport GenerateDividendSummary(
        IReadOnlyList<DividendPayment> dividends,
        int taxYear)
    {
        var report = new DividendSummaryReport
        {
            TaxYear = taxYear,
            GeneratedAt = DateTime.UtcNow
        };

        var byPayer = dividends.GroupBy(d => d.PayerName);

        foreach (var group in byPayer)
        {
            var entry = new DividendPayerSummary
            {
                PayerName = group.Key,
                PayerTIN = group.First().PayerTIN
            };

            foreach (var dividend in group)
            {
                switch (dividend.Type)
                {
                    case DividendType.Ordinary:
                        entry.OrdinaryDividends += dividend.Amount;
                        break;
                    case DividendType.Qualified:
                        entry.QualifiedDividends += dividend.Amount;
                        entry.OrdinaryDividends += dividend.Amount; // Qualified included in ordinary
                        break;
                    case DividendType.CapitalGain:
                        entry.CapitalGainDistributions += dividend.Amount;
                        break;
                    case DividendType.NonTaxable:
                        entry.NonTaxableDistributions += dividend.Amount;
                        break;
                    case DividendType.Foreign:
                        entry.OrdinaryDividends += dividend.Amount;
                        entry.ForeignTaxPaid += dividend.ForeignTaxWithheld;
                        break;
                }

                entry.FederalTaxWithheld += dividend.FederalTaxWithheld;
            }

            report.PayerSummaries.Add(entry);

            report.TotalOrdinaryDividends += entry.OrdinaryDividends;
            report.TotalQualifiedDividends += entry.QualifiedDividends;
            report.TotalCapitalGainDistributions += entry.CapitalGainDistributions;
            report.TotalForeignTaxPaid += entry.ForeignTaxPaid;
            report.TotalFederalTaxWithheld += entry.FederalTaxWithheld;
        }

        return report;
    }

    /// <summary>
    /// Generates a trade blotter for regulatory audit.
    /// </summary>
    public TradeBlotterReport GenerateTradeBlotter(
        IReadOnlyList<ExecutedTrade> trades,
        DateTime startDate,
        DateTime endDate)
    {
        var report = new TradeBlotterReport
        {
            StartDate = startDate,
            EndDate = endDate,
            GeneratedAt = DateTime.UtcNow
        };

        var filteredTrades = trades
            .Where(t => t.ExecutionTime >= startDate && t.ExecutionTime <= endDate)
            .OrderBy(t => t.ExecutionTime)
            .ToList();

        foreach (var trade in filteredTrades)
        {
            report.Trades.Add(new TradeBlotterEntry
            {
                TradeId = trade.TradeId,
                ExecutionTime = trade.ExecutionTime,
                Symbol = trade.Symbol,
                Side = trade.Side,
                Quantity = trade.Quantity,
                Price = trade.Price,
                Notional = trade.Quantity * trade.Price,
                Commission = trade.Commission,
                Exchange = trade.Exchange,
                OrderId = trade.OrderId,
                AccountId = trade.AccountId,
                TraderId = trade.TraderId,
                SettlementDate = trade.SettlementDate,
                Status = trade.Status
            });

            report.TotalNotional += trade.Quantity * trade.Price;
            report.TotalCommissions += trade.Commission;
            report.TotalTrades++;
        }

        // Calculate statistics
        report.AverageTradeSize = report.TotalTrades > 0
            ? report.TotalNotional / report.TotalTrades
            : 0;

        report.TradesBySymbol = filteredTrades
            .GroupBy(t => t.Symbol)
            .ToDictionary(g => g.Key, g => g.Count());

        report.TradesByExchange = filteredTrades
            .GroupBy(t => t.Exchange)
            .ToDictionary(g => g.Key, g => g.Count());

        return report;
    }

    /// <summary>
    /// Exports report to CSV format.
    /// </summary>
    public string ExportToCsv<T>(IReadOnlyList<T> records) where T : class
    {
        var sb = new StringBuilder();
        var properties = typeof(T).GetProperties();

        // Header
        sb.AppendLine(string.Join(",", properties.Select(p => EscapeCsvField(p.Name))));

        // Rows
        foreach (var record in records)
        {
            var values = properties.Select(p =>
            {
                var value = p.GetValue(record);
                return EscapeCsvField(FormatValue(value));
            });
            sb.AppendLine(string.Join(",", values));
        }

        return sb.ToString();
    }

    private static string EscapeCsvField(string field)
    {
        if (string.IsNullOrEmpty(field)) return string.Empty;

        if (field.Contains(',') || field.Contains('"') || field.Contains('\n'))
        {
            return $"\"{field.Replace("\"", "\"\"")}\"";
        }

        return field;
    }

    private static string FormatValue(object? value)
    {
        return value switch
        {
            null => string.Empty,
            DateTime dt => dt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            decimal d => d.ToString("F2", CultureInfo.InvariantCulture),
            _ => value.ToString() ?? string.Empty
        };
    }
}

/// <summary>
/// Regulatory reporting options.
/// </summary>
public sealed class RegulatoryReportingOptions
{
    /// <summary>Annual capital loss limit.</summary>
    public decimal AnnualLossLimit { get; set; } = 3000m;

    /// <summary>FBAR filing threshold.</summary>
    public decimal FBARThreshold { get; set; } = 10000m;
}

/// <summary>
/// Form 13F input.
/// </summary>
public sealed class Form13FInput
{
    public FilerInfo FilerInfo { get; set; } = new();
    public List<Form13FHolding> Holdings { get; set; } = [];
}

/// <summary>
/// Filer information.
/// </summary>
public sealed class FilerInfo
{
    public string Name { get; set; } = string.Empty;
    public string CIK { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
}

/// <summary>
/// Form 13F holding.
/// </summary>
public sealed class Form13FHolding
{
    public string IssuerName { get; set; } = string.Empty;
    public string TitleOfClass { get; set; } = string.Empty;
    public string Cusip { get; set; } = string.Empty;
    public decimal MarketValue { get; set; }
    public long Shares { get; set; }
    public bool IsDebt { get; set; }
    public bool Is13FSecurity { get; set; } = true;
    public string InvestmentDiscretion { get; set; } = "SOLE";
    public int OtherManagers { get; set; }
    public long VotingAuthoritySole { get; set; }
    public long VotingAuthorityShared { get; set; }
    public long VotingAuthorityNone { get; set; }
}

/// <summary>
/// Form 13F report.
/// </summary>
public sealed class Form13FReport
{
    public DateTime ReportingPeriod { get; set; }
    public FilerInfo FilerInfo { get; set; } = new();
    public List<Form13FEntry> InfoTable { get; set; } = [];
    public long TotalValue { get; set; }
    public int TotalHoldings { get; set; }
    public DateTime GeneratedAt { get; set; }
}

/// <summary>
/// Form 13F entry.
/// </summary>
public sealed class Form13FEntry
{
    public string IssuerName { get; set; } = string.Empty;
    public string TitleOfClass { get; set; } = string.Empty;
    public string Cusip { get; set; } = string.Empty;
    public long Value { get; set; }
    public long SharesOrPrincipalAmount { get; set; }
    public string SharesOrPrincipalAmountType { get; set; } = "SH";
    public string InvestmentDiscretion { get; set; } = "SOLE";
    public int OtherManagers { get; set; }
    public long VotingAuthoritySole { get; set; }
    public long VotingAuthorityShared { get; set; }
    public long VotingAuthorityNone { get; set; }
}

/// <summary>
/// Tax lot disposition.
/// </summary>
public sealed class TaxLotDisposition
{
    public string Symbol { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime AcquiredDate { get; set; }
    public DateTime SaleDate { get; set; }
    public decimal Proceeds { get; set; }
    public decimal OriginalCostBasis { get; set; }
    public decimal CostBasis { get; set; }
    public decimal WashSaleDisallowed { get; set; }
    public bool Basis1099Reported { get; set; }
    public bool BasisReportedToIRS { get; set; }
    public DateTime? ReplacementPurchaseDate { get; set; }
    public long ReplacementShares { get; set; }
}

/// <summary>
/// Schedule D report.
/// </summary>
public sealed class ScheduleDReport
{
    public int TaxYear { get; set; }
    public List<ScheduleDEntry> ShortTermTransactions { get; set; } = [];
    public List<ScheduleDEntry> LongTermTransactions { get; set; } = [];
    public decimal ShortTermGains { get; set; }
    public decimal ShortTermLosses { get; set; }
    public decimal LongTermGains { get; set; }
    public decimal LongTermLosses { get; set; }
    public decimal NetShortTermGainLoss { get; set; }
    public decimal NetLongTermGainLoss { get; set; }
    public decimal TotalNetGainLoss { get; set; }
    public decimal LossCarryforward { get; set; }
    public DateTime GeneratedAt { get; set; }
}

/// <summary>
/// Schedule D entry.
/// </summary>
public sealed class ScheduleDEntry
{
    public string Description { get; set; } = string.Empty;
    public DateTime DateAcquired { get; set; }
    public DateTime DateSold { get; set; }
    public decimal Proceeds { get; set; }
    public decimal CostBasis { get; set; }
    public string? AdjustmentCode { get; set; }
    public decimal AdjustmentAmount { get; set; }
    public decimal GainOrLoss { get; set; }
}

/// <summary>
/// Form 8949 box.
/// </summary>
public enum Form8949Box
{
    A, B, C, D, E, F
}

/// <summary>
/// Form 8949 report.
/// </summary>
public sealed class Form8949Report
{
    public int TaxYear { get; set; }
    public List<Form8949Entry> PartI_BoxA { get; set; } = [];
    public List<Form8949Entry> PartI_BoxB { get; set; } = [];
    public List<Form8949Entry> PartI_BoxC { get; set; } = [];
    public List<Form8949Entry> PartII_BoxD { get; set; } = [];
    public List<Form8949Entry> PartII_BoxE { get; set; } = [];
    public List<Form8949Entry> PartII_BoxF { get; set; } = [];
    public decimal TotalPartI { get; set; }
    public decimal TotalPartII { get; set; }
    public DateTime GeneratedAt { get; set; }
}

/// <summary>
/// Form 8949 entry.
/// </summary>
public sealed class Form8949Entry
{
    public Form8949Box Box { get; set; }
    public string Description { get; set; } = string.Empty;
    public DateTime DateAcquired { get; set; }
    public DateTime DateSold { get; set; }
    public decimal Proceeds { get; set; }
    public decimal CostBasis { get; set; }
    public string? AdjustmentCode { get; set; }
    public decimal AdjustmentAmount { get; set; }
    public decimal GainOrLoss { get; set; }
}

/// <summary>
/// Wash sale report.
/// </summary>
public sealed class WashSaleReport
{
    public int TaxYear { get; set; }
    public List<WashSaleEntry> WashSales { get; set; } = [];
    public decimal TotalDisallowedLoss { get; set; }
    public int TotalWashSales { get; set; }
    public DateTime GeneratedAt { get; set; }
}

/// <summary>
/// Wash sale entry.
/// </summary>
public sealed class WashSaleEntry
{
    public string Symbol { get; set; } = string.Empty;
    public DateTime SaleDate { get; set; }
    public decimal SaleProceeds { get; set; }
    public decimal OriginalCostBasis { get; set; }
    public decimal DisallowedLoss { get; set; }
    public decimal AdjustedCostBasis { get; set; }
    public DateTime? ReplacementPurchaseDate { get; set; }
    public long ReplacementShares { get; set; }
}

/// <summary>
/// Foreign account.
/// </summary>
public sealed class ForeignAccount
{
    public string AccountNumber { get; set; } = string.Empty;
    public string FinancialInstitution { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public string AccountType { get; set; } = string.Empty;
    public decimal MaximumValueDuringYear { get; set; }
    public string Currency { get; set; } = "USD";
    public bool IsJointAccount { get; set; }
    public bool HasSignatureAuthority { get; set; }
}

/// <summary>
/// FBAR report.
/// </summary>
public sealed class FBARReport
{
    public int ReportingYear { get; set; }
    public List<FBARAccountEntry> Accounts { get; set; } = [];
    public decimal TotalMaxValue { get; set; }
    public bool FilingRequired { get; set; }
    public DateTime GeneratedAt { get; set; }
}

/// <summary>
/// FBAR account entry.
/// </summary>
public sealed class FBARAccountEntry
{
    public string AccountNumber { get; set; } = string.Empty;
    public string FinancialInstitution { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public string AccountType { get; set; } = string.Empty;
    public decimal MaximumValue { get; set; }
    public string Currency { get; set; } = string.Empty;
    public bool JointAccount { get; set; }
    public bool SignatureAuthority { get; set; }
}

/// <summary>
/// Dividend payment.
/// </summary>
public sealed class DividendPayment
{
    public string PayerName { get; set; } = string.Empty;
    public string PayerTIN { get; set; } = string.Empty;
    public DateTime PaymentDate { get; set; }
    public decimal Amount { get; set; }
    public DividendType Type { get; set; }
    public decimal FederalTaxWithheld { get; set; }
    public decimal ForeignTaxWithheld { get; set; }
}

/// <summary>
/// Dividend type.
/// </summary>
public enum DividendType
{
    Ordinary,
    Qualified,
    CapitalGain,
    NonTaxable,
    Foreign
}

/// <summary>
/// Dividend summary report.
/// </summary>
public sealed class DividendSummaryReport
{
    public int TaxYear { get; set; }
    public List<DividendPayerSummary> PayerSummaries { get; set; } = [];
    public decimal TotalOrdinaryDividends { get; set; }
    public decimal TotalQualifiedDividends { get; set; }
    public decimal TotalCapitalGainDistributions { get; set; }
    public decimal TotalForeignTaxPaid { get; set; }
    public decimal TotalFederalTaxWithheld { get; set; }
    public DateTime GeneratedAt { get; set; }
}

/// <summary>
/// Dividend payer summary.
/// </summary>
public sealed class DividendPayerSummary
{
    public string PayerName { get; set; } = string.Empty;
    public string PayerTIN { get; set; } = string.Empty;
    public decimal OrdinaryDividends { get; set; }
    public decimal QualifiedDividends { get; set; }
    public decimal CapitalGainDistributions { get; set; }
    public decimal NonTaxableDistributions { get; set; }
    public decimal ForeignTaxPaid { get; set; }
    public decimal FederalTaxWithheld { get; set; }
}

/// <summary>
/// Executed trade.
/// </summary>
public sealed class ExecutedTrade
{
    public string TradeId { get; set; } = string.Empty;
    public DateTime ExecutionTime { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public string Side { get; set; } = string.Empty;
    public long Quantity { get; set; }
    public decimal Price { get; set; }
    public decimal Commission { get; set; }
    public string Exchange { get; set; } = string.Empty;
    public string OrderId { get; set; } = string.Empty;
    public string AccountId { get; set; } = string.Empty;
    public string TraderId { get; set; } = string.Empty;
    public DateTime SettlementDate { get; set; }
    public string Status { get; set; } = string.Empty;
}

/// <summary>
/// Trade blotter report.
/// </summary>
public sealed class TradeBlotterReport
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public List<TradeBlotterEntry> Trades { get; set; } = [];
    public decimal TotalNotional { get; set; }
    public decimal TotalCommissions { get; set; }
    public int TotalTrades { get; set; }
    public decimal AverageTradeSize { get; set; }
    public Dictionary<string, int> TradesBySymbol { get; set; } = [];
    public Dictionary<string, int> TradesByExchange { get; set; } = [];
    public DateTime GeneratedAt { get; set; }
}

/// <summary>
/// Trade blotter entry.
/// </summary>
public sealed class TradeBlotterEntry
{
    public string TradeId { get; set; } = string.Empty;
    public DateTime ExecutionTime { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public string Side { get; set; } = string.Empty;
    public long Quantity { get; set; }
    public decimal Price { get; set; }
    public decimal Notional { get; set; }
    public decimal Commission { get; set; }
    public string Exchange { get; set; } = string.Empty;
    public string OrderId { get; set; } = string.Empty;
    public string AccountId { get; set; } = string.Empty;
    public string TraderId { get; set; } = string.Empty;
    public DateTime SettlementDate { get; set; }
    public string Status { get; set; } = string.Empty;
}
