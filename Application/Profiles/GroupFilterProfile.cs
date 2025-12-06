using Application.DTOs;
using AutoMapper;
using Core.Models;

namespace Application.Profiles;

public class GroupFilterProfile:Profile
{
    public GroupFilterProfile()
    {
        CreateMap<GroupFilterDto, GroupFilterEntity>()
            .ForMember(dest => dest.Name, opt => opt.MapFrom(src => 
                string.IsNullOrEmpty(src.Name) ? null : src.Name));
    }
}