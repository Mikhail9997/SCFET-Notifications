using Application.DTOs;
using AutoMapper;
using Core.Models;

namespace Application.Profiles;

public class FilterProfile:Profile
{
    public FilterProfile()
    {
        CreateMap<FilterDto, FilterEntity>()
            .ForMember(dest => dest.Page, opt => opt.MapFrom(src => src.Page))
            .ForMember(dest => dest.PageSize, opt => opt.MapFrom(src => src.PageSize))
            .ForMember(dest => dest.StartDate, opt => opt.MapFrom(src => 
                src.StartDate))
            .ForMember(dest => dest.EndDate, opt => opt.MapFrom(src => 
                src.EndDate))
            .ForMember(dest => dest.SortOrder, opt => opt.MapFrom(src => src.SortOrder))
            .ForMember(dest => dest.SortBy, opt => opt.MapFrom(src => src.SortBy));

        CreateMap<FilterEntity, FilterDto>()
            .ForMember(dest => dest.Page, opt => opt.MapFrom(src => src.Page))
            .ForMember(dest => dest.PageSize, opt => opt.MapFrom(src => src.PageSize))
            .ForMember(dest => dest.StartDate, opt => opt.MapFrom(src => src.StartDate))
            .ForMember(dest => dest.EndDate, opt => opt.MapFrom(src => src.EndDate))
            .ForMember(dest => dest.SortOrder, opt => opt.MapFrom(src => src.SortOrder))
            .ForMember(dest => dest.SortBy, opt => opt.MapFrom(src => src.SortBy));
    }
}