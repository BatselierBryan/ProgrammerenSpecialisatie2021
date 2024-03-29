﻿using Stock.Domain;
using System;
using System.Collections.Generic;

namespace WpfApp.Providers
{
    /// <summary>
    /// This provider implements the IStockQuery interface using the Yahoo Finance API
    /// </summary>
    public class StockQueryProvider : Stock.Domain.Contracts.IStockQuery
    {
        public IReadOnlyList<Candle> GetHistoricalData(string tick, DateTime from, DateTime to, Period period = Period.Daily)
        {
            List<Candle> result = new();
            try
            {
                Stock.Infrastructure.YahooFinanceApi.Period yahooPeriod = (Stock.Infrastructure.YahooFinanceApi.Period)period;

                var historicalData = Stock.Infrastructure.YahooFinanceApi.Yahoo.GetHistoricalAsync(tick, from, to, yahooPeriod);
                foreach (var t in historicalData.Result)
                {
                    Candle c = new();
                    c.Open = t.Open;
                    c.Close = t.Close;
                    c.Low = t.Low;
                    c.High = t.High;
                    c.Volume = t.Volume;
                    c.DateTime = t.DateTime;
                    c.AdjustedClose = t.AdjustedClose;
                    result.Add(c);
                }
            }
            catch(Exception e)
            {
                System.Diagnostics.Debug.WriteLine(e);
            }
            return result;
        }

        public IReadOnlyList<Security> GetSecurities(string[] shares)
        {
            List<Security> result = new();
            var securities = Stock.Infrastructure.YahooFinanceApi.Yahoo
                .Symbols(shares)
                .Fields(Stock.Infrastructure.YahooFinanceApi.Field.Symbol, Stock.Infrastructure.YahooFinanceApi.Field.RegularMarketOpen, Stock.Infrastructure.YahooFinanceApi.Field.RegularMarketPrice, Stock.Infrastructure.YahooFinanceApi.Field.RegularMarketTime, Stock.Infrastructure.YahooFinanceApi.Field.Currency, Stock.Infrastructure.YahooFinanceApi.Field.LongName)
                .QueryAsync();

/*
var total = (decimal)0.0;
foreach (var tick in securities)
{
    if (_shares.ContainsKey(tick.Key))
    {
        var v = _shares[tick.Key].Count * ConvertCurrency(tick.Value.Currency, (decimal)tick.Value.RegularMarketPrice);
        total += v;
        var u = _shares[tick.Key].Count * ConvertCurrency(tick.Value.Currency, _shares[tick.Key].Value);
        System.Diagnostics.Debug.WriteLine(tick.Key + ": " + u + " -> " + v + " (delta: " + (v - u) + ")");
    }
}
*/
            foreach (var s in securities.Result)
            {
                Security security = new(s.Value.Fields);
                result.Add(security);
            }
            return result;
        }
    }
}
