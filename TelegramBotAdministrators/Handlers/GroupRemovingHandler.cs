using Application.Services;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using TelegramBotAdministrators.Models;
using TelegramBotAdministrators.Services;

namespace TelegramBotAdministrators.Handlers;

public class GroupRemovingHandler
{
    private readonly ILogger<GroupRemovingHandler> _logger;
    private readonly IApiService _apiService;
    private readonly RedisService _redis;
    public Func<long,string,ReplyMarkup?,Task> SendMessage;
    public TelegramBotClient? BotClient;
    
    public GroupRemovingHandler(ILogger<GroupRemovingHandler> logger, IApiService apiService, RedisService redis)
    {
        _logger = logger;
        _apiService = apiService;
        _redis = redis;
    }
    
    public async Task StartGroupRemoveProcess(long chatId)
    {
        await ShowGroupSelection(chatId);
    }

    private async Task ShowGroupSelection(long chatId)
    {
        var groups = await _apiService.GetGroupsAsync();

        if (groups == null || !groups.Any())
        {
            await SendMessage.Invoke(chatId, "❌ В системе пока нет доступных групп. Обратитесь к администратору.", null);
            return;
        }

        var keyboard = new List<List<InlineKeyboardButton>>();

        foreach (var group in groups)
        {
            keyboard.Add(new()
            {
                InlineKeyboardButton.WithCallbackData(
                    $"{group.Name} ({group.StudentCount} студентов)", 
                    $"group_remove_{group.Id}")
            });
        }

        var replyMarkup = new InlineKeyboardMarkup(keyboard);
        
        await SendMessage.Invoke(chatId, 
            "🎯 Выберите группу из списка ниже:",
            replyMarkup);
    }

    public async Task HandleRemoveGroup(CallbackQuery callbackQuery, string callbackData, long chatId, int messageId, string token)
    {
        try
        {
            string groupIdStr = callbackData.Substring("group_remove_".Length);

            if (Guid.TryParse(groupIdStr, out Guid groupId))
            {
                var result = await _apiService.RemoveGroup(token, groupId);

                if (result.Success)
                {
                    string removeResultAnswer = "✅ Группа успешно удалена!";
                    await SendMessage.Invoke(chatId, removeResultAnswer, null);
                    await BotClient?.AnswerCallbackQuery(callbackQuery.Id, "Группа успешно удалена");
                    return;
                }
                await SendMessage.Invoke(chatId, $"❌ {result.Message}", null);
            }
        }
        catch(Exception ex)
        {
            _logger.LogError(ex, "Error during group remove process for chat {ChatId}", chatId);
            await SendMessage.Invoke(chatId, "❌ Произошла ошибка при удалении группы. Попробуйте снова.", null);
        }
        await BotClient?.AnswerCallbackQuery(callbackQuery.Id, "Ошибка при удалении группы");
    }
}
