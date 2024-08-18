using System.Text.Json;
using AutoMapper;
using VPMReposSynchronizer.Core.Models.Entity;
using VPMReposSynchronizer.Core.Models.Types;

namespace VPMReposSynchronizer.Core.Models.Mappers;

public class VpmPackageProfile : Profile
{
    public VpmPackageProfile()
    {
        CreateMap<VpmPackageEntity, VpmPackage>()
            .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.VpmId))
            // Legacy
            .ForMember(dest => dest.LegacyFiles,
                opt => opt.MapFrom(src => ConvertStringToDictionary(src.LegacyFiles ?? "{}")))
            .ForMember(dest => dest.LegacyFolders,
                opt => opt.MapFrom(src => ConvertStringToDictionary(src.LegacyFolders ?? "{}")))
            .ForMember(dest => dest.LegacyPackages,
                opt => opt.MapFrom(src => ConvertStringToArray(src.LegacyPackages ?? "{}")))
            // Dependencies
            .ForMember(dest => dest.Dependencies,
                opt => opt.MapFrom(src => ConvertStringToDictionary(src.Dependencies ?? "{}")))
            .ForMember(dest => dest.VpmDependencies,
                opt => opt.MapFrom(src => ConvertStringToDictionary(src.VpmDependencies ?? "{}")))
            .ForMember(dest => dest.GitDependencies,
                opt => opt.MapFrom(src => ConvertStringToDictionary(src.GitDependencies ?? "{}")))
            // Headers
            .ForMember(dest => dest.Headers,
                opt => opt.MapFrom(src => ConvertStringToDictionary(src.Headers ?? "{}")))
            // Author
            .ForMember(dest => dest.Author,
                opt => opt.MapFrom(src => src.AuthorName == null
                    ? null
                    : new VpmPackageAuthor
                    {
                        Name = src.AuthorName,
                        Url = src.AuthorUrl,
                        Email = src.AuthorEmail
                    }))
            // Keywords
            .ForMember(dest => dest.Keywords,
                opt => opt.MapFrom(src => ConvertStringToArray(src.Keywords ?? "[]")))
            // Samples
            .ForMember(dest => dest.Samples,
                opt => opt.MapFrom(src => ConvertFromJson<PackageSample[]>(src.Samples ?? "[]")));

        CreateMap<VpmPackage, VpmPackageEntity>()
            .ForMember(dest => dest.VpmId, opt => opt.MapFrom(src => src.Id))
            .ForMember(dest => dest.PackageId, opt => opt.MapFrom(src => src.Name + "@" + src.Version))
            // Legacy
            .ForMember(dest => dest.LegacyFiles,
                opt => opt.MapFrom(
                    src => ConvertDictionaryToString(src.LegacyFiles ?? new Dictionary<string, string>())))
            .ForMember(dest => dest.LegacyFolders,
                opt => opt.MapFrom(src =>
                    ConvertDictionaryToString(src.LegacyFolders ?? new Dictionary<string, string>())))
            .ForMember(dest => dest.LegacyPackages,
                opt => opt.MapFrom(src => ConvertArrayToString(src.LegacyPackages ?? Array.Empty<string>())))
            // Dependencies
            .ForMember(dest => dest.Dependencies,
                opt => opt.MapFrom(src =>
                    ConvertDictionaryToString(src.Dependencies ?? new Dictionary<string, string>())))
            .ForMember(dest => dest.VpmDependencies,
                opt => opt.MapFrom(src =>
                    ConvertDictionaryToString(src.VpmDependencies ?? new Dictionary<string, string>())))
            .ForMember(dest => dest.GitDependencies,
                opt => opt.MapFrom(src =>
                    ConvertDictionaryToString(src.GitDependencies ?? new Dictionary<string, string>())))
            // Headers
            .ForMember(dest => dest.Headers,
                opt => opt.MapFrom(src => ConvertDictionaryToString(src.Headers ?? new Dictionary<string, string>())))
            // Author
            .ForMember(dest => dest.AuthorName, opt => opt.MapFrom(src => src.Author == null ? null : src.Author.Name))
            .ForMember(dest => dest.AuthorEmail,
                opt => opt.MapFrom(src => src.Author == null ? null : src.Author.Email))
            .ForMember(dest => dest.AuthorUrl, opt => opt.MapFrom(src => src.Author == null ? null : src.Author.Url))
            // Keywords
            .ForMember(dest => dest.Keywords,
                opt => opt.MapFrom(src => ConvertArrayToString(src.Keywords ?? Array.Empty<string>())))
            // Samples
            .ForMember(dest => dest.Samples,
                opt => opt.MapFrom(src => ConvertToJson(src.Samples)));
    }

    private static Dictionary<string, string> ConvertStringToDictionary(string input)
    {
        return JsonSerializer.Deserialize<Dictionary<string, string>>(input) ?? new Dictionary<string, string>();
    }

    private static string ConvertDictionaryToString(Dictionary<string, string> input)
    {
        return JsonSerializer.Serialize(input);
    }

    private static string[] ConvertStringToArray(string input)
    {
        return JsonSerializer.Deserialize<string[]>(input) ?? Array.Empty<string>();
    }

    private static string ConvertArrayToString(string[] input)
    {
        return JsonSerializer.Serialize(input);
    }

    private static string ConvertToJson<T>(T input)
    {
        return JsonSerializer.Serialize(input);
    }

    private static T? ConvertFromJson<T>(string input)
    {
        return JsonSerializer.Deserialize<T>(input);
    }
}