using LocalCliRunner.Api.Application;
using LocalCliRunner.Api.Infrastructure;
using LocalCliRunner.Api.Workspace;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

// 싱글턴: Job 레지스트리는 서버 수명 동안 유지
builder.Services.AddSingleton<JobRegistry>();
builder.Services.AddSingleton<PiiTokenizer>();
builder.Services.AddSingleton<JiraService>();

// 스코프/트랜지언트 서비스
builder.Services.AddScoped<ICliRunner, ClaudeCliRunner>();
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
