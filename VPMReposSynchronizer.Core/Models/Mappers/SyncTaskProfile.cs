using AutoMapper;
using VPMReposSynchronizer.Core.Models.Entity;
using VPMReposSynchronizer.Core.Models.Types;

namespace VPMReposSynchronizer.Core.Models.Mappers;

public class SyncTaskProfile : Profile
{
    public SyncTaskProfile()
    {
        CreateMap<SyncTaskEntity, SyncTaskPublic>();
    }
}