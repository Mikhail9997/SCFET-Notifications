using Application.DTOs;
using Application.Services;
using AutoMapper;
using Core.Models;

namespace Application.Profiles;

public class ProfileMapping : Profile
{
    public ProfileMapping()
    {
        CreateMap<User, ProfileDto>()
            .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.Id))
            .ForMember(dest => dest.Email, opt => opt.MapFrom(src => src.Email ?? string.Empty))
            .ForMember(dest => dest.FirstName, opt => opt.MapFrom(src => src.FirstName ?? string.Empty))
            .ForMember(dest => dest.LastName, opt => opt.MapFrom(src => src.LastName ?? string.Empty))
            .ForMember(dest => dest.PhoneNumber, opt => opt.MapFrom(src => src.PhoneNumber ?? string.Empty))
            .ForMember(dest => dest.Role, opt => opt.MapFrom(src => src.Role.ToString()))
            .ForMember(dest => dest.Group, opt => opt.MapFrom(src => src.Group != null ? src.Group.Name : string.Empty))
            .ForMember(dest => dest.GroupId, opt => opt.MapFrom(src => src.GroupId));
    }
}