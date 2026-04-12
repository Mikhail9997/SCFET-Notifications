using Application.DTOs;
using AutoMapper;
using Core.Models;

namespace Application.Profiles;

public class ChannelMessageProfile : Profile
{
    public ChannelMessageProfile()
    {
        CreateMap<ChannelMessage, ChannelMessageDto>()
            .ForMember(dest => dest.SenderName, opt => opt.Ignore())
            .ForMember(dest => dest.SenderAvatar, opt => opt.Ignore())
            .ForMember(dest => dest.SenderRole, opt => opt.Ignore())
            .ForMember(dest => dest.SenderChannelRole, opt => opt.Ignore())
            .ForMember(dest => dest.ReplyToMessage, opt => opt.Ignore())
            .ForMember(dest => dest.CanEdit, opt => opt.Ignore())
            .ForMember(dest => dest.CanDelete, opt => opt.Ignore());
    }
}