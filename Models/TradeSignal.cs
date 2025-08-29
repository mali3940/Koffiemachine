using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Koffiemachine.Models
{
    public class TradeSignal
    {
        public string? Symbol { get; set; }
        public string? Direction { get; set; } 
        public decimal Entry { get; set; }
        public decimal StopLoss { get; set; }
        public decimal TakeProfit { get; set; }
        public double Rsi { get; set; }
        public decimal EmaFast { get; set; }
        public decimal EmaSlow { get; set; }
        public decimal Macd { get; set; }
    }

}
