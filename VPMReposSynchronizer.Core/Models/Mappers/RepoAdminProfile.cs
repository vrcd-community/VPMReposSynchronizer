using AutoMapper;
using VPMReposSynchronizer.Core.Models.Entity;
using VPMReposSynchronizer.Core.Models.Types.RepoAdmin;

namespace VPMReposSynchronizer.Core.Models.Mappers;

public class RepoAdminProfile : Profile
{
    public RepoAdminProfile()
    {
        CreateMap<RepoAdminUpdateDto, VpmRepoEntity>()
            .ForMember(dest => dest.RepoId, opt => opt.MapFrom(src => src.ApiId));
    }
}