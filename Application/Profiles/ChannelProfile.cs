using Application.DTOs;
using AutoMapper;
using Core.Models;

namespace Application.Profiles;

public class ChannelProfile: Profile
{
    public ChannelProfile()
    {
        CreateMap<Channel, ChannelDto>()
            .ForMember(dest => dest.OwnerName, 
                opt => opt.MapFrom(src => $"{src.Owner.LastName} {src.Owner.FirstName}".Trim()))
            .ForMember(dest => dest.MembersCount, 
                opt => opt.MapFrom(src => src.ChannelUsers.Count))
            .ForMember(dest => dest.IsOwner, 
                opt => opt.Ignore())
            .ForMember(dest => dest.IsMember, 
                opt => opt.Ignore())
            .ForMember(dest => dest.UserRole, 
                opt => opt.Ignore())
            .ForMember(dest => dest.OwnerAvatar, 
                opt => opt.Ignore());
        
        CreateMap<CreateChannelDto, Channel>()
            .ForMember(dest => dest.Id, opt => opt.Ignore())
            .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.OwnerId, opt => opt.Ignore())
            .ForMember(dest => dest.Owner, opt => opt.Ignore())
            .ForMember(dest => dest.ChannelUsers, opt => opt.Ignore())
            .ForMember(dest => dest.Invitations, opt => opt.Ignore());

        CreateMap<ChannelUser, ChannelMemberDto>()
            .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.Id))
            .ForMember(dest => dest.UserId, opt => opt.MapFrom(src => src.UserId))
            .ForMember(dest => dest.FirstName, opt => opt.MapFrom(src => src.User.FirstName))
            .ForMember(dest => dest.LastName, opt => opt.MapFrom(src => src.User.LastName))
            .ForMember(dest => dest.Email, opt => opt.MapFrom(src => src.User.Email))
            .ForMember(dest => dest.PhoneNumber, opt => opt.MapFrom(src => src.User.PhoneNumber))
            .ForMember(dest => dest.UserRole, opt => opt.MapFrom(src => src.User.Role))
            .ForMember(dest => dest.ChannelRole, opt => opt.MapFrom(src => src.Role))
            .ForMember(dest => dest.AvatarUrl, opt => opt.Ignore());
        
        CreateMap<ChannelInvitation, ChannelInvitationDto>()
            .ForMember(dest => dest.ChannelName, opt => opt.MapFrom(src => src.Channel.Name))
            .ForMember(dest => dest.ChannelDescription, opt => opt.MapFrom(src => src.Channel.Description))
            .ForMember(dest => dest.InviterName, 
                opt => opt.MapFrom(src => $"{src.Inviter.LastName} {src.Inviter.FirstName}".Trim()))
            .ForMember(dest => dest.InviteeName, 
                opt => opt.MapFrom(src => $"{src.Invitee.LastName} {src.Invitee.FirstName}".Trim()))
            .ForMember(dest => dest.InviterAvatar, opt => opt.Ignore())
            .ForMember(dest => dest.InviteeAvatar, opt => opt.Ignore());

        CreateMap<ChannelInvitationDto, ChannelInvitation>()
            .ForMember(dest => dest.Id, opt => opt.Ignore())
            .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.Channel, opt => opt.Ignore())
            .ForMember(dest => dest.Inviter, opt => opt.Ignore())
            .ForMember(dest => dest.Invitee, opt => opt.Ignore());

        CreateMap<User, AvailableUserDto>()
            .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.Id))
            .ForMember(dest => dest.FirstName, opt => opt.MapFrom(src => src.FirstName))
            .ForMember(dest => dest.LastName, opt => opt.MapFrom(src => src.LastName))
            .ForMember(dest => dest.Email, opt => opt.MapFrom(src => src.Email))
            .ForMember(dest => dest.Role, opt => opt.MapFrom(src => src.Role))
            .ForMember(dest => dest.AvatarUrl, opt => opt.Ignore());
    }
}