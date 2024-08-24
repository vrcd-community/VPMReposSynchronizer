using AutoMapper;
using VPMReposSynchronizer.Core.Models.Entity;
using VPMReposSynchronizer.Core.Models.Types.RepoBrowser;

namespace VPMReposSynchronizer.Core.Models.Mappers;

public class RepoBrowserProfile : Profile
{
    public RepoBrowserProfile()
    {
        CreateMap<VpmRepoEntity, BrowserRepo>()
            .ForMember(dest => dest.ApiId, opt => opt.MapFrom(src => src.Id))
            .ForMember(dest => dest.UpstreamId, opt => opt.MapFrom(src => src.OriginalRepoId));
    }
}