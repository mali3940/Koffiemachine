using Koffiemachine.Models;

namespace Koffiemachine.Services
{
    public class SignalScoringService
    {
        private readonly Dictionary<string, double> _weights = new()
        {
            { "ema_cross", 0.25 },
            { "macd_cross", 0.25 },
            { "rsi", 0.15 },
            { "stoch_cross", 0.15 },
            { "bb", 0.10 },
            { "adx", 0.10 },
            { "atr", 0.05 },
            { "pattern", 0.15 },    // TODO
            { "divergence", 0.05 }  // TODO
        };

        public double MinScore { get; private set; }

        public SignalScoringService(double minScore = 0.7)
        {
            MinScore = minScore;
        }

        public void SetMinScore(double minScore)
        {
            if (minScore < 0 || minScore > 1)
                throw new ArgumentException("Min score moet tussen 0 en 1 liggen (bv. 0.7 = 70%)");
            MinScore = minScore;
        }

        public (string Direction, double Score) ScoreIndicators(IndicatorResult ind)
        {
            double scoreLong = 0;
            double scoreShort = 0;

            // === EMA Cross ===
            if (ind.EmaFast > ind.EmaSlow) scoreLong += _weights["ema_cross"];
            else if (ind.EmaFast < ind.EmaSlow) scoreShort += _weights["ema_cross"];

            // === MACD Cross ===
            if (ind.Macd > ind.MacdSignal) scoreLong += _weights["macd_cross"];
            else if (ind.Macd < ind.MacdSignal) scoreShort += _weights["macd_cross"];

            // === RSI ===
            if (ind.Rsi < 30) scoreLong += _weights["rsi"];
            else if (ind.Rsi > 70) scoreShort += _weights["rsi"];

            // === Stochastic Cross ===
            if (ind.StochK > ind.StochD) scoreLong += _weights["stoch_cross"];
            else if (ind.StochK < ind.StochD) scoreShort += _weights["stoch_cross"];

            // === Bollinger Bands ===
            if (ind.Close <= ind.BbLower) scoreLong += _weights["bb"];
            else if (ind.Close >= ind.BbUpper) scoreShort += _weights["bb"];

            // === ADX (filter: trend sterk genoeg?) ===
            if (ind.Adx >= 25)
            {
                if (ind.EmaFast > ind.EmaSlow) scoreLong += _weights["adx"];
                else if (ind.EmaFast < ind.EmaSlow) scoreShort += _weights["adx"];
            }

            // === ATR (filter: genoeg volatiliteit?) ===
            if (ind.Atr > 0)
            {
                if (ind.EmaFast > ind.EmaSlow) scoreLong += _weights["atr"];
                else if (ind.EmaFast < ind.EmaSlow) scoreShort += _weights["atr"];
            }

            // TODO: patterns & divergence toevoegen

            double totalScore = Math.Max(scoreLong, scoreShort);

            string direction = "NEUTRAL";
            if (scoreLong > scoreShort && totalScore >= MinScore) direction = "LONG";
            else if (scoreShort > scoreLong && totalScore >= MinScore) direction = "SHORT";

            return (direction, totalScore);
        }
    }
}
