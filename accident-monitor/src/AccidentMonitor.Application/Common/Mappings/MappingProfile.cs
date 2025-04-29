using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AccidentMonitor.Application.Accident.Dtos;
using AccidentMonitor.Domain.Entities.Accident;

namespace AccidentMonitor.Application.Common.Mappings;
public class MappingProfile : Profile
{
    public MappingProfile()
    {
        CreateMap<AccidentDto, AccidentDto>()
            .ForMember(dest => dest.Guid, opt => opt.MapFrom(src => src.Guid))
            .ForMember(dest => dest.Timestamp, opt => opt.MapFrom(src => src.Timestamp))
            .ForMember(dest => dest.Longitude, opt => opt.MapFrom(src => src.Longitude))
            .ForMember(dest => dest.Latitude, opt => opt.MapFrom(src => src.Latitude))
            .ForMember(dest => dest.Severity, opt => opt.MapFrom(src => src.Severity))
            .ForMember(dest => dest.ResolvedStatus, opt => opt.MapFrom(src => src.ResolvedStatus));
        CreateMap<AccidentDto, AccidentDto>()
            .ForMember(dest => dest.Guid, opt => opt.MapFrom(src => src.Guid))
            .ForMember(dest => dest.Timestamp, opt => opt.MapFrom(src => src.Timestamp))
            .ForMember(dest => dest.Longitude, opt => opt.MapFrom(src => src.Longitude))
            .ForMember(dest => dest.Latitude, opt => opt.MapFrom(src => src.Latitude))
            .ForMember(dest => dest.Severity, opt => opt.MapFrom(src => src.Severity))
            .ForMember(dest => dest.ResolvedStatus, opt => opt.MapFrom(src => src.ResolvedStatus));
    }
}
