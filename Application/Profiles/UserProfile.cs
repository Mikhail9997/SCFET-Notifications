using Application.DTOs;
using AutoMapper;
using Core.Models;
namespace Application.Profiles;

public class UserProfile:Profile
{
    public UserProfile()
    {
        // Group -> GroupDto
        CreateMap<Group, GroupDto>()
            .ForMember(dest => dest.StudentCount,
                opt => opt.MapFrom(src => src.Students.Count));
        
        // User -> UserDto
        CreateMap<User, UserDto>()
            .ForMember(dest => dest.Role, 
                opt => opt.MapFrom(src => src.Role))
            .ForMember(dest => dest.Group, 
                opt => opt.MapFrom(src => src.Group))
            .ForMember(dest => dest.PasswordHash, 
                opt => opt.Ignore());
    }
}