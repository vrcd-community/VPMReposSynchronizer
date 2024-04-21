using AutoMapper;
using VPMReposSynchronizer.Core.Models.Entity;
using VPMReposSynchronizer.Core.Models.Types.RepoAdmin;

namespace VPMReposSynchronizer.Core.Models.Mappers;

public class RepoAdminProfile : Profile
{
    public RepoAdminProfile()
    {
        CreateMap<VpmRepoEntity, RepoAdmin>();
        CreateMap<RepoAdmin, VpmRepoEntity>();
        CreateMap<RepoAdminUpdateDto, VpmRepoEntity>();
    }
}