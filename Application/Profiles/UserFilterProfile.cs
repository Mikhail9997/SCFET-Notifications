using Application.DTOs;
using AutoMapper;
using Core.Models;

namespace Application.Profiles;

public class UserFilterProfile:Profile
{
    public UserFilterProfile()
    {
        CreateMap<UserFilterDto, UserFilterEntity>()
            .ForMember(dest => dest.FirstName, opt => opt.MapFrom(src => 
                string.IsNullOrWhiteSpace(src.FirstName) ? null : src.FirstName))
            .ForMember(dest => dest.LastName, opt => opt.MapFrom(src => 
                string.IsNullOrWhiteSpace(src.LastName) ? null : src.LastName))
            .ForMember(dest => dest.Email, opt => opt.MapFrom(src => 
                string.IsNullOrWhiteSpace(src.Email) ? null : src.Email))
            .ForMember(dest => dest.GroupId, opt => opt.MapFrom(src => src.GroupId))
            .ForMember(dest => dest.IsActive, opt => opt.MapFrom(src => src.IsActive));

        CreateMap<UserFilterEntity, UserFilterDto>()
            .ForMember(dest => dest.FirstName, opt => opt.MapFrom(src => 
                src.FirstName ?? string.Empty))
            .ForMember(dest => dest.LastName, opt => opt.MapFrom(src => 
                src.LastName ?? string.Empty))
            .ForMember(dest => dest.Email, opt => opt.MapFrom(src => 
                src.Email ?? string.Empty))
            .ForMember(dest => dest.GroupId, opt => opt.MapFrom(src => src.GroupId))
            .ForMember(dest => dest.IsActive, opt => opt.MapFrom(src => src.IsActive));
    }
}