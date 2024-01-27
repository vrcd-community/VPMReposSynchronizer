using AutoMapper;
using VPMReposSynchronizer.Core.Models.Entity;
using VPMReposSynchronizer.Core.Models.Types;

namespace VPMReposSynchronizer.Core.Models.Mappers;

public class VpmRepoProfile : Profile
{
    public VpmRepoProfile()
    {
        CreateMap<VpmRepo, VpmRepoEntity>()
            .ForMember(dest => dest.UpStreamUrl, opt => opt.MapFrom(src => src.Url));

        CreateMap<VpmRepoEntity, VpmRepo>()
            .ForMember(dest => dest.Url, opt => opt.MapFrom(src => src.UpStreamUrl));
    }
}