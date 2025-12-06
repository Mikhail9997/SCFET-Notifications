using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Types.ReplyMarkups;
using TelegramBotAdministrators.Models;
using TelegramBotAdministrators.Services;

namespace TelegramBotAdministrators.Handlers;

public class GroupCreationHandler
{
    private readonly ILogger<GroupCreationHandler> _logger;
    private readonly IApiService _apiService;
    private readonly RedisCache _redis;
    public Func<long,string,ReplyMarkup?,Task> SendMessage;

    public GroupCreationHandler(ILogger<GroupCreationHandler> logger, IApiService apiService, RedisCache redis)
    {
        _logger = logger;
        _apiService = apiService;
        _redis = redis;
    }

    public async Task StartGroupCreation(long chatId)
    {
        var userState = await _redis.GetAsync<BotUserState>(chatId.ToString());
        
        var groupState = new GroupState();
        groupState.State = GroupCreationState.WaitingForName;
        userState.GroupState = groupState;
        
        await _redis.SetAsync(chatId.ToString(), userState);
        
        await SendMessage.Invoke(chatId, "📧 Введите название группы:", null);
    }

    public async Task HandleTextMessageAsync(long chatId, string text, BotUserState state)
    {
        var groupState = state.GroupState?.State;
        switch (groupState)
        {
            case GroupCreationState.WaitingForName:
                await ProcessGroupNameAsync(chatId, text);
                break;
            case GroupCreationState.WaitingForDescription:
                await ProcessGroupDescriptionAsync(chatId, text);
                break;
            case GroupCreationState.Completed:
                await SendMessage.Invoke(chatId, "я тебя не понимаю", null);
                break;
            default:
                await SendMessage.Invoke(chatId, "Для создания группы используйте команду /createGroup", null);
                break;
        }
    }

    private async Task ProcessGroupNameAsync(long chatId, string name)
    {
        var userState = await _redis.GetAsync<BotUserState>(chatId.ToString());
        userState.GroupState.Name = name;
        userState.GroupState.State = GroupCreationState.WaitingForDescription;
        
        await _redis.SetAsync(chatId.ToString(), userState);
        await SendMessage.Invoke(chatId, "✅ Название группы принято!", null);
        await SendMessage.Invoke(chatId, "📧 Введите описание группы:", null);
    }
    
    private async Task ProcessGroupDescriptionAsync(long chatId, string description)
    {
        var userState = await _redis.GetAsync<BotUserState>(chatId.ToString());
        userState.GroupState.Description = description;
        userState.GroupState.State = GroupCreationState.Completed;
        
        await _redis.SetAsync(chatId.ToString(), userState);
        await SendMessage.Invoke(chatId, "✅ Описание группы принято!", null);
        await ProcessGroupCreationAsync(chatId, userState);
    }
    
    private async Task ProcessGroupCreationAsync(long chatId, BotUserState state)
    {
        try
        {
            var token = state.Token ?? "";
            var groupState = state.GroupState;
            var groupDto = new GroupDto {Name = groupState?.Name ?? "", Description = groupState?.Description ?? ""};
            
            // Выполняем запрос на сервер
            var response = await _apiService.CreateGroup(token, groupDto);

            if (response.Success)
            {
                await SendMessage.Invoke(chatId, "✅ Группа успешно создана!", null);
            }
            else
            {
                await SendMessage.Invoke(chatId, $"❌ {response.Message}", null);
            }
        }
        catch(Exception ex)
        {
            _logger.LogError(ex, "Error during group creation process for chat {ChatId}", chatId);
            await SendMessage.Invoke(chatId, "❌ Произошла ошибка при создании группы. Попробуйте снова.", null);
        }
        // очищаем группу и сохраняем в redis
        state.GroupState = null;
        await _redis.SetAsync(chatId.ToString(), state);
    }
}