namespace KnowledgeHub.Application.Search;

public class PageIndexOptions
{
    public string CliPath { get; set; } = "src/pageindex-cli/pageindex_cli.py";
    public string PythonPath { get; set; } = "python3";
    public int TimeoutMinutes { get; set; } = 5;
    public string Model { get; set; } = "qwen-long";
}
