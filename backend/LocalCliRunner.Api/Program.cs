using LocalCliRunner.Api.Application;
using LocalCliRunner.Api.Infrastructure;
using LocalCliRunner.Api.Workspace;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

// 싱글턴: Job 레지스트리는 서버 수명 동안 유지
builder.Services.AddSingleton<JobRegistry>();
builder.Services.AddSingleton<PiiTokenizer>();
builder.Services.AddSingleton<JiraService>();
builder.Services.AddSingleton<SettingsService>();

builder.Services.AddHttpClient(); // IHttpClientFactory for ClaudeVertexRunner + GitHubService

// Runner 선택: Vertex:ProjectId가 있으면 Vertex, 없으면 로컬 ClaudeCliRunner
// Vertex:Provider == "gemini" → GeminiVertexRunner, 그 외 → ClaudeVertexRunner (기본)
var vertexProjectId = builder.Configuration["Vertex:ProjectId"];
var vertexProvider  = builder.Configuration["Vertex:Provider"] ?? "claude";

if (!string.IsNullOrWhiteSpace(vertexProjectId))
{
    if (vertexProvider == "gemini")
        builder.Services.AddScoped<ICliRunner, GeminiVertexRunner>();
    else
        builder.Services.AddScoped<ICliRunner, ClaudeVertexRunner>();
}
else
{
    builder.Services.AddScoped<ICliRunner, ClaudeCliRunner>();
}
builder.Services.AddScoped<GitHubService>();
builder.Services.AddScoped<RepoSearchService>();
builder.Services.AddScoped<PromptBuilder>();
builder.Services.AddScoped<WorkspaceManager>();
builder.Services.AddScoped<RunStageHandler>();

// CORS: React dev server 허용
builder.Services.AddCors(opt =>
    opt.AddPolicy("Dev", p => p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

var app = builder.Build();

app.UseCors("Dev");
app.MapControllers();

app.Run();
