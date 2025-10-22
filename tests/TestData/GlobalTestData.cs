namespace OoplesFinance.StockIndicators.Tests.Unit.TestData;

public class GlobalTestData
{
    public static List<TickerData> StockTestData { get; } = GetCsvData<TickerData>("AAPL");
    public static List<TickerData> MarketTestData { get; } = GetCsvData<TickerData>("SP500");

    public static List<T> GetCsvData<T>(string fileName)
    {
        using var reader = new StreamReader($"TestData/{fileName}.csv");
        using var csvReader = new CsvReader(reader, CultureInfo.InvariantCulture);

        List<T> result;
        if (typeof(T) == typeof(TickerData))
        {
            csvReader.Context.TypeConverterCache.AddConverter<DateTime>(new CustomDateConverter());
            result = new List<T>(csvReader.GetRecords<T>());
        }
        else
        {
            if (typeof(T) == typeof(double))
            {
                csvReader.Context.TypeConverterCache.AddConverter<double>(new CustomDoubleConverter());
            }
            
            result = new List<T>();
            var count = 0;
            while (csvReader.Read())
            {
                if (count == 0)
                {
                    csvReader.ReadHeader();
                }
                else
                {
                    result.Add(csvReader.GetField<T>("Output")!);
                }
                
                count++;
            }
        }

        return result;
    }
}

public class CustomDoubleConverter : DefaultTypeConverter
{
    public override object? ConvertFromString(string? text, IReaderRow row, MemberMapData memberMapData)
    {
        var isSuccess = double.TryParse(text, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var result);
        return isSuccess ? Math.Round(result, 4) : string.IsNullOrWhiteSpace(text) ? 0d : base.ConvertFromString(text, row, memberMapData);
    }
}

public class CustomDateConverter : DefaultTypeConverter
{
    public override object? ConvertFromString(string? text, IReaderRow row, MemberMapData memberMapData)
    {
        var isSuccess = DateTime.TryParseExact(text, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var result);
        return isSuccess ? result : base.ConvertFromString(text, row, memberMapData);
    }
}
