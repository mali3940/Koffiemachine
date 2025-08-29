using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Koffiemachine.Exchange;
using Koffiemachine.Services;

namespace Koffiemachine.Bot
{
    public class TelegramBotService
    {
        private readonly ITelegramBotClient _bot;
        private readonly AnalysisService _analysisService;
        private readonly ExchangeApi _exchange;
        private readonly List<string> _symbols = new() { "BTCUSDT", "LINKUSDT", "HYPEUSDT", "ETHUSDT" };
        private Timer _scanTimer;
        private readonly PortfolioService _portfolio;
        private readonly SignalScoringService _scoring;


        // Wordt dynamisch gevuld bij /start
        private long? _defaultChatId = null;

        public TelegramBotService(string token)
        {
            _bot = new TelegramBotClient(token);
            _exchange = new ExchangeApi();
            _portfolio = new PortfolioService(1000m, 0.005m);
            _scoring = new SignalScoringService(0.7); // default op 70%
            _analysisService = new AnalysisService(_exchange, _scoring);
        }

        public void Start()
        {
            using var cts = new CancellationTokenSource();

            var receiverOptions = new ReceiverOptions
            {
                AllowedUpdates = Array.Empty<UpdateType>()
            };

            _bot.StartReceiving(
                HandleUpdateAsync,
                HandleErrorAsync,
                receiverOptions,
                cts.Token
            );

            var me = _bot.GetMe().Result;
            Console.WriteLine($"🤖 Bot gestart: {me.Username}");

            // Start timer (elke minuut scannen)
            _scanTimer = new Timer(async _ => await AutoScan(cts.Token), null, TimeSpan.Zero, TimeSpan.FromMinutes(1));

            Console.ReadLine();
            cts.Cancel();
        }

        private async Task AutoScan(CancellationToken token)
        {
            if (_defaultChatId == null)
            {
                Console.WriteLine("⚠️ Geen chatId ingesteld (stuur eerst /start in Telegram).");
                return;
            }

            foreach (var symbol in _symbols)
            {
                try
                {
                    // 1️⃣ Analyse
                    var results = await _analysisService.AnalyzeMultiAsync(symbol);
                    var best = results.First().Signal; // gewoon 1 timeframe nemen voor price
                    var currentPrice = best.Entry;

                    // 2️⃣ Open trades checken
                    var closedTrades = _portfolio.CheckAndCloseTrades(symbol, currentPrice);
                    foreach (var t in closedTrades)
                    {
                        string closeMsg =
                            $"✅ Trade closed {t.Symbol}\n" +
                            $"➡️ {t.Direction}\n" +
                            $"Entry: {t.Entry:F2}\n" +
                            $"Exit: {t.ClosePrice:F2}\n" +
                            $"PnL: {t.ProfitLoss:F2} USDT\n" +
                            $"Balance: {_portfolio.Balance:F2} USDT";

                        await _bot.SendMessage(_defaultChatId.Value, closeMsg, cancellationToken: token);
                    }

                    // 3️⃣ Nieuw signaal zoeken
                    int longCount = results.Count(r => r.Signal.Direction == "LONG");
                    int shortCount = results.Count(r => r.Signal.Direction == "SHORT");

                    string overall = "NEUTRAL";
                    if (longCount >= 2) overall = "LONG";
                    else if (shortCount >= 2) overall = "SHORT";

                    if (overall != "NEUTRAL")
                    {
                        var bestSignal = results.First(r => r.Signal.Direction == overall).Signal;

                        // ✅ Nieuw: trade openen
                        var trade = _portfolio.OpenTrade(symbol, overall, bestSignal.Entry, bestSignal.StopLoss, bestSignal.TakeProfit);

                        string msg =
                            $"⚡ Auto-signal {symbol}\n" +
                            $"➡️ {overall}\n" +
                            $"Entry: {bestSignal.Entry:F2}\n" +
                            $"SL: {bestSignal.StopLoss:F2} | TP: {bestSignal.TakeProfit:F2}\n" +
                            $"Size: {trade.Size:F2}\n" +
                            $"Balance: {_portfolio.Balance:F2} USDT";

                        await _bot.SendMessage(_defaultChatId.Value, msg, cancellationToken: token);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Error scanning {symbol}: {ex.Message}");
                }
            }
        }


        private async Task HandleUpdateAsync(ITelegramBotClient bot, Update update, CancellationToken token)
        {
            if (update.Message is not { Text: { } messageText })
                return;

            var chatId = update.Message.Chat.Id;

            if (messageText.StartsWith("/start"))
            {
                _defaultChatId = chatId; // Dynamisch opslaan
                Console.WriteLine($"✅ ChatId ingesteld: {_defaultChatId}");
                await bot.SendMessage(chatId, "Welkom bij je trading bot 🚀", cancellationToken: token);
            }
            else if (messageText.StartsWith("/price"))
            {
                var parts = messageText.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2)
                {
                    await bot.SendMessage(chatId, "Gebruik: /price SYMBOL (bijv. /price BTCUSDT)", cancellationToken: token);
                    return;
                }

                var symbol = parts[1].ToUpper();
                try
                {
                    var price = await _exchange.GetPriceAsync(symbol);
                    await bot.SendMessage(chatId, $"📈 {symbol} = {price:F2} USDT", cancellationToken: token);
                }
                catch (Exception ex)
                {
                    await bot.SendMessage(chatId, $"❌ Error: {ex.Message}", cancellationToken: token);
                }
            }
            else if (messageText.StartsWith("/analyze"))
            {
                var parts = messageText.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2)
                {
                    await bot.SendMessage(chatId, "Gebruik: /analyze SYMBOL (bijv. /analyze BTCUSDT)", cancellationToken: token);
                    return;
                }

                var symbol = parts[1].ToUpper();
                try
                {
                    var results = await _analysisService.AnalyzeMultiAsync(symbol);
                    string msg = $"📊 Analyse {symbol}\n";

                    foreach (var result in results)
                    {
                        var s = result.Signal;
                        msg += $"\n⏱ {result.Timeframe}\n" +
                               $"➡️ Signal: {s.Direction}\n" +
                               $"Entry: {s.Entry:F2}\n";

                        if (s.StopLoss > 0)
                            msg += $"SL: {s.StopLoss:F2} | TP: {s.TakeProfit:F2}\n";

                        msg += $"RSI: {s.Rsi:F2} | EMA9: {s.EmaFast:F2} / EMA21: {s.EmaSlow:F2} | MACD: {s.Macd:F2}\n";
                    }

                    await bot.SendMessage(chatId, msg, cancellationToken: token);
                }
                catch (Exception ex)
                {
                    await bot.SendMessage(chatId, $"❌ Error: {ex.Message}", cancellationToken: token);
                }
            }
            else if (messageText.StartsWith("/info"))
            {
                var msg = _portfolio.GetPortfolioInfo();
                await bot.SendMessage(chatId, msg, cancellationToken: token);
            }
            else if (messageText.StartsWith("/history"))
            {
                var msg = _portfolio.GetTradeHistory();
                await bot.SendMessage(chatId, msg, cancellationToken: token);
            }
            else if (messageText.StartsWith("/open"))
            {
                var msg = _portfolio.GetOpenTrades();
                await bot.SendMessage(chatId, msg, cancellationToken: token);
            }
            else if (messageText.StartsWith("/reset"))
            {
                _portfolio.Reset(1000m, 0.005m);
                await bot.SendMessage(chatId, "🔄 Portfolio gereset naar 1000 USDT met 0.5% risk.", cancellationToken: token);
            }
            else if (messageText.StartsWith("/set_risk"))
            {
                var parts = messageText.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2)
                {
                    await bot.SendMessage(chatId, "Gebruik: /set_risk 0.01 (voor 1%)", cancellationToken: token);
                    return;
                }

                if (decimal.TryParse(parts[1], out decimal risk))
                {
                    try
                    {
                        _portfolio.SetRisk(risk);
                        await bot.SendMessage(chatId, $"✅ Risk per trade ingesteld op {risk:P}", cancellationToken: token);
                    }
                    catch (Exception ex)
                    {
                        await bot.SendMessage(chatId, $"❌ Ongeldige waarde: {ex.Message}", cancellationToken: token);
                    }
                }
                else
                {
                    await bot.SendMessage(chatId, "❌ Ongeldige input. Voorbeeld: /set_risk 0.01", cancellationToken: token);
                }
            }
            else if (messageText.StartsWith("/set_min_score"))
            {
                var parts = messageText.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2)
                {
                    await bot.SendMessage(chatId, "Gebruik: /set_min_score 0.7 (70%)", cancellationToken: token);
                    return;
                }

                if (double.TryParse(parts[1], out double score))
                {
                    try
                    {
                        _scoring.SetMinScore(score);
                        await bot.SendMessage(chatId, $"✅ Min score ingesteld op {score:P0}", cancellationToken: token);
                    }
                    catch (Exception ex)
                    {
                        await bot.SendMessage(chatId, $"❌ Ongeldige waarde: {ex.Message}", cancellationToken: token);
                    }
                }
                else
                {
                    await bot.SendMessage(chatId, "❌ Ongeldige input. Voorbeeld: /set_min_score 0.7", cancellationToken: token);
                }
            }
            else if (messageText.StartsWith("/help"))
            {
                string msg =
                    "📖 Beschikbare commando's:\n\n" +
                    "/start - Bot starten & chat koppelen\n" +
                    "/price SYMBOL - Huidige prijs ophalen (bijv. /price BTCUSDT)\n" +
                    "/analyze SYMBOL - Analyseer munt met multi-timeframes\n" +
                    "/info - Portfolio status\n" +
                    "/history - Laatste gesloten trades\n" +
                    "/open - Openstaande trades\n" +
                    "/reset - Portfolio resetten\n" +
                    "/set_risk X - Stel risk per trade in (bijv. 0.01 = 1%)\n" +
                    "/set_min_score X - Stel minimum score in (bijv. 0.7 = 70%)\n" +
                    "/help - Dit overzicht tonen\n"+
                    "/export - logging alle trades\n";

                await bot.SendMessage(chatId, msg, cancellationToken: token);
            }
            else if (messageText.StartsWith("/export"))
            {
                string filePath = _portfolio.ExportTradesToCsv();

                if (string.IsNullOrEmpty(filePath))
                {
                    await bot.SendMessage(chatId, "❌ Geen trades om te exporteren.", cancellationToken: token);
                    return;
                }

                using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                {
                    await _bot.SendDocument(
                        chatId: chatId,
                        document: new InputFileStream(stream, Path.GetFileName(filePath)),
                        caption: "📂 Trade export",
                        cancellationToken: token
                    );
                }
            }

            else
            {
                await bot.SendMessage(chatId, "Onbekend commando ❓", cancellationToken: token);
            }
        }

        private Task HandleErrorAsync(ITelegramBotClient bot, Exception exception, CancellationToken token)
        {
            Console.WriteLine($"❌ Error: {exception.Message}");
            return Task.CompletedTask;
        }
    }
}
