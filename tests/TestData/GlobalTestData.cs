namespace OoplesFinance.StockIndicators.Tests.Unit.TestData;

public class GlobalTestData
{
    public static List<TickerData> StockTestData { get; } = GetCsvData<TickerData>("AAPL");
    public static List<TickerData> MarketTestData { get; } = GetCsvData<TickerData>("SP500");

    public static List<T> GetCsvData<T>(string fileName)
    {
        using var reader = new StreamReader($"TestData/{fileName}.csv");
        using var csvReader = new CsvReader(reader, CultureInfo.InvariantCulture);
        var result = new List<T>();
        csvReader.Read();
        csvReader.ReadHeader();
        if (typeof(T) == typeof(TickerData))
        {
            while (csvReader.Read())
            {
                var ticker = new TickerData
                {
                    Date = DateTime.ParseExact(csvReader.GetField<string>("Date"), "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None),
                    Open = double.Parse(csvReader.GetField<string>("Open"), CultureInfo.InvariantCulture),
                    High = double.Parse(csvReader.GetField<string>("High"), CultureInfo.InvariantCulture),
                    Low = double.Parse(csvReader.GetField<string>("Low"), CultureInfo.InvariantCulture),
                    Close = double.Parse(csvReader.GetField<string>("Close"), CultureInfo.InvariantCulture),
                    Volume = double.Parse(csvReader.GetField<string>("Volume"), CultureInfo.InvariantCulture),
                };
                result.Add((T)(object)ticker);
            }
        }
        else if (typeof(T) == typeof(double))
        {
            while (csvReader.Read())
            {
                var text = csvReader.GetField<string>("Output");
                double value;
                if (string.IsNullOrWhiteSpace(text))
                {
                    value = 0d;
                }
                else if (double.TryParse(text, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var parsed)
                    && !double.IsNaN(parsed) && !double.IsInfinity(parsed))
                {
                    value = Math.Round(parsed, 4);
                }
                else
                {
                    throw new FormatException($"Invalid numeric Output '{text}' in fixture 'TestData/{fileName}.csv' at row {result.Count + 1}.");
                }

                result.Add((T)(object)value);
            }
        }
        else
        {
            throw new NotSupportedException($"Unsupported CSV fixture record type {typeof(T).FullName}.");
        }

        return result;
    }
}
