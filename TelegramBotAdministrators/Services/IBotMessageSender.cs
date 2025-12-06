using Telegram.Bot.Types.ReplyMarkups;

namespace TelegramBotAdministrators.Services;

public interface IBotMessageSender
{
    Task SendMessage(long chatId, string message, ReplyMarkup? replyMarkup = null);
}