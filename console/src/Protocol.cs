using System.Text.Json.Serialization;

namespace EasySave.Console;

// Miroir exact du Protocol.cs serveur — même structure JSON des deux côtés.

public record ClientMsg
{
    [JsonPropertyName("type")]       public string  Type       { get; init; } = "";
    [JsonPropertyName("jobId")]      public string? JobId      { get; init; }
    [JsonPropertyName("name")]       public string? Name       { get; init; }
    [JsonPropertyName("sourcePath")] public string? SourcePath { get; init; }
    [JsonPropertyName("destPath")]   public string? DestPath   { get; init; }
    [JsonPropertyName("saveType")]   public string? SaveType   { get; init; }
}

public record ServerMsg
{
    [JsonPropertyName("type")]           public string?       Type           { get; init; }
    [JsonPropertyName("jobs")]           public List<JobDto>? Jobs           { get; init; }
    [JsonPropertyName("jobId")]          public string?       JobId          { get; init; }
    [JsonPropertyName("name")]           public string?       Name           { get; init; }
    [JsonPropertyName("status")]         public string?       Status         { get; init; }
    [JsonPropertyName("progress")]       public float?        Progress       { get; init; }
    [JsonPropertyName("filesRemaining")] public int?          FilesRemaining { get; init; }
    [JsonPropertyName("message")]        public string?       Message        { get; init; }
}

public record JobDto
{
    [JsonPropertyName("id")]         public string Id         { get; init; } = "";
    [JsonPropertyName("name")]       public string Name       { get; init; } = "";
    [JsonPropertyName("sourcePath")] public string SourcePath { get; init; } = "";
    [JsonPropertyName("destPath")]   public string DestPath   { get; init; } = "";
    [JsonPropertyName("saveType")]   public string SaveType   { get; init; } = "Complete";
    [JsonPropertyName("status")]     public string Status     { get; init; } = "Inactive";
    [JsonPropertyName("progress")]   public float  Progress   { get; init; }
}
