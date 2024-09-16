namespace VPMReposSynchronizer.Core.Models.Types;

public record PageResult<T>(IEnumerable<T> Items, int TotalCount);
