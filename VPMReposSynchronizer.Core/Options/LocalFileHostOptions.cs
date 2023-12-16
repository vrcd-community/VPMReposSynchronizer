﻿namespace VPMReposSynchronizer.Core.Options;

public class LocalFileHostOptions
{
    public Uri BaseUrl { get; set; } = new("http://example.com/");
    public string FilesPath { get; set; } = "files";
}