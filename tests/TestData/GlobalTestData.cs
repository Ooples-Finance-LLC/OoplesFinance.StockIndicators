namespace OoplesFinance.StockIndicators.Tests.Unit.TestData;

public class GlobalTestData
{
    public static List<TickerData> StockTestData { get; } = GetCsvData<TickerData>("AAPL");
    public static List<TickerData> MarketTestData { get; } = GetCsvData<TickerData>("SP500");

    public static List<T> GetCsvData<T>(string fileName)
    {
        using var csvReader = CsvReader.FromFile($"TestData/{fileName}.csv");
        var result = new List<T>();
        csvReader.ReadHeader();
        if (typeof(T) == typeof(TickerData))
        {
            while (csvReader.Read())
            {
                var ticker = new TickerData
                {
                    Date = DateTime.ParseExact(csvReader.GetString("Date"), "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None),
                    Open = double.Parse(csvReader.GetString("Open"), CultureInfo.InvariantCulture),
                    High = double.Parse(csvReader.GetString("High"), CultureInfo.InvariantCulture),
                    Low = double.Parse(csvReader.GetString("Low"), CultureInfo.InvariantCulture),
                    Close = double.Parse(csvReader.GetString("Close"), CultureInfo.InvariantCulture),
                    Volume = double.Parse(csvReader.GetString("Volume"), CultureInfo.InvariantCulture),
                };
                result.Add((T)(object)ticker);
            }
        }
        else if (typeof(T) == typeof(double))
        {
            while (csvReader.Read())
            {
                var text = csvReader.GetString("Output");
                var value = double.TryParse(text, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var parsed)
                    ? Math.Round(parsed, 4)
                    : 0d;
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
