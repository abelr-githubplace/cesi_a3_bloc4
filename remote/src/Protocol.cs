using System.Text.Json.Serialization;

namespace EasySave.Remote;

// Messages envoyés par le client WPF → serveur
public record ClientMsg
{
    [JsonPropertyName("type")]       public string  Type       { get; init; } = "";
    [JsonPropertyName("jobId")]      public string? JobId      { get; init; }
    [JsonPropertyName("name")]       public string? Name       { get; init; }
    [JsonPropertyName("sourcePath")] public string? SourcePath { get; init; }
    [JsonPropertyName("destPath")]   public string? DestPath   { get; init; }
    [JsonPropertyName("saveType")]   public string? SaveType   { get; init; }
}

// Messages envoyés par le serveur → tous les clients connectés
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

// Représentation d'un job transmise au client
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
