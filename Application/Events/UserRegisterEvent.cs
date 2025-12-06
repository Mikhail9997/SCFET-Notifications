namespace Application.Events;

public class UserRegisterEvent:UserIsActiveEvent
{
    public bool IsActive { get; set; }
}