using Koffiemachine.Models;
using Binance.Net.Clients;
using Binance.Net.Enums;

namespace Koffiemachine.Exchange
{
    public class ExchangeApi
    {
        private readonly BinanceRestClient _client;

        public ExchangeApi()
        {
            _client = new BinanceRestClient();
        }

        public async Task<List<Candle>> GetCandlesAsync(
            string symbol = "BTCUSDT",
            KlineInterval interval = KlineInterval.FiveMinutes,
            int limit = 200)
        {
            var result = await _client.SpotApi.ExchangeData.GetKlinesAsync(symbol, interval, limit: limit);

            if (!result.Success)
                throw new Exception($"Binance error: {result.Error}");

            return result.Data.Select(k => new Candle
            {
                OpenTime = k.OpenTime,
                Open = k.OpenPrice,
                High = k.HighPrice,
                Low = k.LowPrice,
                Close = k.ClosePrice,
                Volume = k.Volume
            }).ToList();
        }

        public async Task<decimal> GetPriceAsync(string symbol)
        {
            var result = await _client.SpotApi.ExchangeData.GetPriceAsync(symbol);
            if (!result.Success)
                throw new Exception($"Binance error: {result.Error}");

            return result.Data.Price;
        }

    }
}
