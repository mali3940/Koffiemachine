namespace Koffiemachine.Models
{
    public class Trade
    {
        public string Symbol { get; set; }
        public string Direction { get; set; } // LONG / SHORT
        public decimal Entry { get; set; }
        public decimal StopLoss { get; set; }
        public decimal TakeProfit { get; set; }
        public decimal Size { get; set; } // hoeveel USDT ingelegd
        public DateTime OpenTime { get; set; }
        public DateTime? CloseTime { get; set; }
        public decimal? ClosePrice { get; set; }
        public decimal? ProfitLoss { get; set; } // in USDT
        public bool IsOpen => !CloseTime.HasValue;
    }
}
