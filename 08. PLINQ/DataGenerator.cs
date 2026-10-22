using System;
using System.Collections.Generic;

namespace PlinqQueries;

public class MarketData
{
    public int Id { get; set; }

    public string Symbol { get; set; } = "";

    public decimal Price { get; set; }

    public long Volume { get; set; }

    public DateTime Timestamp { get; set; }

    public decimal ChangePercent { get; set; }
}

public class DataGenerator
{
    private static readonly string[] Symbols = { "AAPL", "GOOGL", "MSFT", "TSLA", "AMZN" };

    public List<MarketData> GenerateMarketData(int count)
    {
        var random = new Random(42);
        var data = new List<MarketData>(count);
        var start = new DateTime(2024, 1, 1);
        for (int i = 0; i < count; i++)
        {
            data.Add(new MarketData
            {
                Id = i,
                Symbol = Symbols[random.Next(Symbols.Length)],
                Price = (decimal)(10 + random.NextDouble() * 990),
                Volume = random.Next(100, 100_000),
                Timestamp = start.AddSeconds(i),
                ChangePercent = (decimal)(-5 + random.NextDouble() * 10)
            });
        }

        return data;
    }
}
