using System;
using System.Collections.Generic;
using System.Linq;

namespace Koffiemachine.Services
{
    public static class TradingLogic
    {
        // === RSI ===
        public static double CalculateRsi(List<decimal> closes, int period = 14)
        {
            if (closes.Count < period + 1) return 50;

            double gain = 0, loss = 0;
            for (int i = closes.Count - period; i < closes.Count; i++)
            {
                var change = (double)(closes[i] - closes[i - 1]);
                if (change >= 0) gain += change;
                else loss -= change;
            }

            if (loss == 0) return 100;
            double rs = gain / loss;
            return 100 - (100 / (1 + rs));
        }

        // === EMA ===
        public static decimal CalculateEma(List<decimal> closes, int period)
        {
            if (closes.Count < period) return 0;

            decimal k = 2m / (period + 1);
            decimal ema = closes.Take(period).Average();

            for (int i = period; i < closes.Count; i++)
                ema = closes[i] * k + ema * (1 - k);

            return ema;
        }

        // === ATR (Average True Range) ===
        public static double CalculateAtr(List<decimal> highs, List<decimal> lows, List<decimal> closes, int period = 14)
        {
            if (highs.Count < period || lows.Count < period || closes.Count < period) return 0;

            var trs = new List<double>();
            for (int i = 1; i < closes.Count; i++)
            {
                double high = (double)highs[i];
                double low = (double)lows[i];
                double prevClose = (double)closes[i - 1];

                double tr = Math.Max(high - low, Math.Max(Math.Abs(high - prevClose), Math.Abs(low - prevClose)));
                trs.Add(tr);
            }

            return trs.TakeLast(period).Average();
        }

        // === Stochastic Oscillator (K/D) ===
        public static (double K, double D) CalculateStochastic(List<decimal> highs, List<decimal> lows, List<decimal> closes, int period = 14, int smoothK = 3, int smoothD = 3)
        {
            if (closes.Count < period) return (50, 50);

            var stochK = new List<double>();

            for (int i = period - 1; i < closes.Count; i++)
            {
                var high = (double)highs.Skip(i - period + 1).Take(period).Max();
                var low = (double)lows.Skip(i - period + 1).Take(period).Min();
                var close = (double)closes[i];

                double k = 100 * (close - low) / (high - low);
                stochK.Add(k);
            }

            // Smooth %K
            var smoothedK = stochK.Skip(stochK.Count - smoothK).Take(smoothK).Average();

            // Smooth %D
            var stochD = stochK.Skip(stochK.Count - smoothD).Take(smoothD).Average();

            return (smoothedK, stochD);
        }

        // === ADX (Average Directional Index) ===
        public static double CalculateAdx(List<decimal> highs, List<decimal> lows, List<decimal> closes, int period = 14)
        {
            if (highs.Count < period + 1 || lows.Count < period + 1 || closes.Count < period + 1) return 0;

            var plusDM = new List<double>();
            var minusDM = new List<double>();
            var trs = new List<double>();

            for (int i = 1; i < highs.Count; i++)
            {
                double upMove = (double)(highs[i] - highs[i - 1]);
                double downMove = (double)(lows[i - 1] - lows[i]);

                plusDM.Add(upMove > downMove && upMove > 0 ? upMove : 0);
                minusDM.Add(downMove > upMove && downMove > 0 ? downMove : 0);

                double high = (double)highs[i];
                double low = (double)lows[i];
                double prevClose = (double)closes[i - 1];
                trs.Add(Math.Max(high - low, Math.Max(Math.Abs(high - prevClose), Math.Abs(low - prevClose))));
            }

            double atr = trs.TakeLast(period).Average();
            if (atr == 0) return 0;

            double plusDI = 100 * (plusDM.TakeLast(period).Sum() / atr);
            double minusDI = 100 * (minusDM.TakeLast(period).Sum() / atr);

            double dx = 100 * Math.Abs(plusDI - minusDI) / (plusDI + minusDI);
            return dx;
        }
    }
}
