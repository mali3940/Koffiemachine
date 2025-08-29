using Koffiemachine.Bot;

class Program
{
    static void Main(string[] args)
    {
        var botService = new TelegramBotService("8368938140:AAG0GDt8D5hW4xj1IzYS-ibbLRdNnzlnm8o");
        botService.Start();
    }
}
