using Koffiemachine.Models;
using System.Text.Json;

namespace Koffiemachine.Services
{
    public class PortfolioService
    {
        private decimal _initialCapital;
        private decimal _balance;
        private decimal _riskPerTrade;
        private List<Trade> _trades = new();
        private readonly string _logFile = "trades_log.json";

        public PortfolioService(decimal initialCapital = 1000m, decimal riskPerTrade = 0.005m)
        {
            _initialCapital = initialCapital;
            _balance = initialCapital;
            _riskPerTrade = riskPerTrade;

            LoadTrades(); // ✅ bij start trades herladen
        }

        public decimal Balance => _balance;
        public decimal InitialCapital => _initialCapital;
        public IReadOnlyList<Trade> Trades => _trades.AsReadOnly();

        // === Logging ===
        private void SaveTrades()
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            File.WriteAllText(_logFile, JsonSerializer.Serialize(_trades, options));
        }

        private void LoadTrades()
        {
            if (File.Exists(_logFile))
            {
                var json = File.ReadAllText(_logFile);
                _trades = JsonSerializer.Deserialize<List<Trade>>(json) ?? new List<Trade>();
                _balance = _initialCapital + _trades.Where(t => t.ProfitLoss.HasValue).Sum(t => t.ProfitLoss.Value);
            }
        }

        // === Trade openen ===
        public Trade OpenTrade(string symbol, string direction, decimal entry, decimal stopLoss, decimal takeProfit)
        {
            decimal riskAmount = _balance * _riskPerTrade;
            decimal riskPerUnit = direction == "LONG"
                ? entry - stopLoss
                : stopLoss - entry;

            if (riskPerUnit <= 0) riskPerUnit = entry * 0.01m; // fallback

            decimal positionSize = riskAmount / riskPerUnit;

            var trade = new Trade
            {
                Symbol = symbol,
                Direction = direction,
                Entry = entry,
                StopLoss = stopLoss,
                TakeProfit = takeProfit,
                Size = positionSize,
                OpenTime = DateTime.UtcNow
            };

            _trades.Add(trade);
            SaveTrades();
            return trade;
        }

        // === Trade sluiten ===
        public void CloseTrade(Trade trade, decimal closePrice)
        {
            if (!trade.IsOpen) return;

            decimal pnl;
            if (trade.Direction == "LONG")
                pnl = (closePrice - trade.Entry) * trade.Size;
            else
                pnl = (trade.Entry - closePrice) * trade.Size;

            trade.ClosePrice = closePrice;
            trade.ProfitLoss = pnl;
            trade.CloseTime = DateTime.UtcNow;

            _balance += pnl;
            SaveTrades();
        }

        // === Auto-check op TP/SL ===
        public List<Trade> CheckAndCloseTrades(string symbol, decimal currentPrice)
        {
            var closed = new List<Trade>();

            foreach (var trade in _trades.Where(t => t.Symbol == symbol && t.IsOpen))
            {
                bool hitSL = (trade.Direction == "LONG" && currentPrice <= trade.StopLoss) ||
                             (trade.Direction == "SHORT" && currentPrice >= trade.StopLoss);

                bool hitTP = (trade.Direction == "LONG" && currentPrice >= trade.TakeProfit) ||
                             (trade.Direction == "SHORT" && currentPrice <= trade.TakeProfit);

                if (hitSL || hitTP)
                {
                    CloseTrade(trade, currentPrice);
                    closed.Add(trade);
                }
            }

            return closed;
        }

        // === Reset portfolio ===
        public void Reset(decimal initialCapital = 1000m, decimal riskPerTrade = 0.005m)
        {
            _initialCapital = initialCapital;
            _balance = initialCapital;
            _riskPerTrade = riskPerTrade;
            _trades.Clear();
            SaveTrades();
        }

        // === Risk aanpassen ===
        public void SetRisk(decimal riskPerTrade)
        {
            if (riskPerTrade <= 0 || riskPerTrade > 1)
                throw new ArgumentException("Risk per trade moet tussen 0 en 1 liggen (bv. 0.01 = 1%)");
            _riskPerTrade = riskPerTrade;
        }

        // === Telegram helpers ===
        public string GetPortfolioInfo()
        {
            int openTrades = _trades.Count(t => t.IsOpen);
            int closedTrades = _trades.Count(t => !t.IsOpen);
            decimal totalPnL = _trades.Where(t => t.ProfitLoss.HasValue).Sum(t => t.ProfitLoss.Value);

            return
                $"💰 Portfolio info\n\n" +
                $"Start: {_initialCapital:F2} USDT\n" +
                $"Balance: {_balance:F2} USDT\n" +
                $"PnL: {totalPnL:F2} USDT\n" +
                $"Open trades: {openTrades}\n" +
                $"Closed trades: {closedTrades}\n";
        }

        public string GetTradeHistory(int limit = 5)
        {
            var lastTrades = _trades.Where(t => !t.IsOpen).TakeLast(limit);

            if (!lastTrades.Any())
                return "📜 Geen afgesloten trades.";

            string msg = "📜 Laatste trades:\n";
            foreach (var t in lastTrades)
            {
                msg += $"{t.Symbol} {t.Direction} | Entry: {t.Entry:F2} | Exit: {t.ClosePrice:F2} | PnL: {t.ProfitLoss:F2}\n";
            }
            return msg;
        }

        public string GetOpenTrades()
        {
            var openTrades = _trades.Where(t => t.IsOpen).ToList();
            if (!openTrades.Any())
                return "📂 Geen open trades.";

            string msg = "📂 Open trades:\n";
            foreach (var t in openTrades)
            {
                msg += $"{t.Symbol} {t.Direction} | Entry: {t.Entry:F2} | SL: {t.StopLoss:F2} | TP: {t.TakeProfit:F2} | Size: {t.Size:F2}\n";
            }
            return msg;
        }

        public string ExportTradesToCsv(string filePath = "trades_export.csv")
        {
            if (!_trades.Any())
                return string.Empty;

            using (var writer = new StreamWriter(filePath))
            {
                writer.WriteLine("Symbol,Direction,Entry,StopLoss,TakeProfit,Size,OpenTime,CloseTime,ClosePrice,ProfitLoss");

                foreach (var t in _trades)
                {
                    writer.WriteLine(
                        $"{t.Symbol},{t.Direction},{t.Entry:F2},{t.StopLoss:F2},{t.TakeProfit:F2},{t.Size:F2}," +
                        $"{t.OpenTime:u},{t.CloseTime:u},{t.ClosePrice:F2},{t.ProfitLoss:F2}"
                    );
                }
            }

            return filePath;
        }

    }
}
