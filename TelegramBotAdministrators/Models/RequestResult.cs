namespace TelegramBotAdministrators.Models;

public class RequestResult<T>
{
    public T Data { get; set; }
    public int Code { get; set; }
}