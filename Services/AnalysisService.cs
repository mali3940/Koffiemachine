using Binance.Net.Enums;
using Koffiemachine.Exchange;
using Koffiemachine.Helpers;
using Koffiemachine.Models;

namespace Koffiemachine.Services
{
    public class AnalysisService
    {
        private readonly ExchangeApi _exchange;
        private readonly SignalScoringService _scoring;

        public AnalysisService(ExchangeApi exchange, SignalScoringService scoring)
        {
            _exchange = exchange;
            _scoring = scoring;
        }

        public async Task<List<MultiTimeframeResult>> AnalyzeMultiAsync(string symbol)
        {
            var results = new List<MultiTimeframeResult>();

            var timeframes = new Dictionary<string, KlineInterval>
            {
                { "5m", KlineInterval.FiveMinutes },
                { "15m", KlineInterval.FifteenMinutes },
                { "1h", KlineInterval.OneHour }
            };

            foreach (var tf in timeframes)
            {
                var signal = await AnalyzeSingleAsync(symbol, tf.Value);
                results.Add(new MultiTimeframeResult { Timeframe = tf.Key, Signal = signal });
            }

            return results;
        }

        private async Task<TradeSignal> AnalyzeSingleAsync(string symbol, KlineInterval interval)
        {
            var candles = await _exchange.GetCandlesAsync(symbol, interval, 150);
            var closes = candles.Select(c => c.Close).ToList();
            var last = candles.Last();

            double avg = closes.Average(v => (double)v);
            double std = closes.StdDev();
            var highs = candles.Select(c => c.High).ToList();
            var lows = candles.Select(c => c.Low).ToList();

            var (stochK, stochD) = TradingLogic.CalculateStochastic(highs, lows, closes);
            var adx = TradingLogic.CalculateAdx(highs, lows, closes);
            var atr = TradingLogic.CalculateAtr(highs, lows, closes);

            var ind = new IndicatorResult
            {
                Close = (double)last.Close,
                Rsi = TradingLogic.CalculateRsi(closes),
                EmaFast = (double)TradingLogic.CalculateEma(closes, 9),
                EmaSlow = (double)TradingLogic.CalculateEma(closes, 21),
                Macd = (double)(TradingLogic.CalculateEma(closes, 12) - TradingLogic.CalculateEma(closes, 26)),
                MacdSignal = (double)TradingLogic.CalculateEma(closes, 9),
                BbUpper = avg + 2 * std,
                BbLower = avg - 2 * std,
                StochK = stochK,
                StochD = stochD,
                Adx = adx,
                Atr = atr
            };

            var (direction, score) = _scoring.ScoreIndicators(ind);

            return new TradeSignal
            {
                Symbol = symbol,
                Direction = direction,
                Entry = last.Close,
                StopLoss = direction == "LONG" ? last.Close * 0.99m : last.Close * 1.01m,
                TakeProfit = direction == "LONG" ? last.Close * 1.02m : last.Close * 0.98m,
                Rsi = ind.Rsi,
                EmaFast = (decimal)ind.EmaFast,
                EmaSlow = (decimal)ind.EmaSlow,
                Macd = (decimal)ind.Macd
            };
        }
    }
}
