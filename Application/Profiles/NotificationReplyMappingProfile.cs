using Application.DTOs;
using AutoMapper;
using Core.Models;

namespace Application.Profiles;

public class NotificationReplyMappingProfile:Profile
{
    public NotificationReplyMappingProfile()
    {
        CreateMap<NotificationReply, ReplyDto>()
            .ForMember(dest => dest.UserName, 
                opt => opt.MapFrom(src => $"{src.User.FirstName} {src.User.LastName}"))
            .ForMember(dest => dest.UserRole, 
                opt => opt.MapFrom(src => src.User.Role.ToString()));
        
        CreateMap<CreateReplyDto, NotificationReply>();
        CreateMap<UpdateNotificationReplyDto, NotificationReply>();
        CreateMap<FilterDto, FilterEntity>();
    }
}