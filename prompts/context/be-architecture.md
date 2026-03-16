# Backend Architecture — clo3d-api (Clean Architecture + CQRS/MediatR)

## Layer Structure
Domain → Application → Infrastructure → API

## Application Layer
Path: `api/clo3d-admin/Application.Shared/Features/{Domain}/Commands|Queries/{UseCase}/`
- `{Verb}{Entity}Request.cs` — `public record {Verb}{Entity}Request : {Dto}, IRequest<{ReturnType}> { }`
- `{Verb}{Entity}RequestHandler.cs` — `internal class ... : IRequestHandler<TRequest, TResponse>`
- `{Verb}{Entity}Response.cs` — 응답 DTO (필요 시)
- Primary constructor 사용 (C# 12+)
- Namespace: `Application.{Product}.Features.{Domain}.Commands|Queries.{UseCase}`

## Domain Layer
Path: `Domain/Common/src/AggregatesModel/{Domain}Aggregate/`
- 엔티티: `Tbl{Entity}.cs` + `Tbl{Entity}Partial.cs`
- 인터페이스: `I{Entity}Repository.cs`
- 스펙: `Specification/{Entity}Specification.cs`
- 이벤트: `Events/{Entity}{Event}Event.cs`

## Infrastructure Layer
Path: `Infrastructure/Common/src/EntityConfigurations/`
- DB 모델: `ModelConfigurations/{Entity}EntityTypeConfiguration.cs`
- DbContext: `CLO3DContext.cs` 또는 `CLOVFContext.cs` (멀티 컨텍스트)

## API Layer
Path: `api/clo3d-admin/clo3d-admin-api/Controllers/{Domain}Controller.cs`
- `[Route("api/{domain}")]`, `[Authorize]`, `[ApiController]`
- `[ServiceFilter(typeof(ClientIPAddressFilterAttribute))]`
- 오류 응답: `{ "code": "ERROR_CODE", "message": "설명" }`
- 페이징: offset/limit 방식, API 버전: v1 경로 포함

## CQRS 데이터 접근 규칙
- **Query Handler**: `DbContext` 직접 주입 후 LINQ로 조회 — Repository 거치지 않음
- **Command Handler**: `I{Entity}Repository` + `{Entity}Specification` 사용 — 도메인 규칙은 Specification에 캡슐화

```csharp
// Query — DbContext 직접 LINQ
internal class GetUserQueryHandler(CLO3DContext db) : IRequestHandler<...>
{
    public async Task<...> Handle(...) =>
        await db.TblUsers.Where(u => u.IsActive == true).ToListAsync(ct);
}

// Command — Repository + Specification
internal class CreateUserCommandHandler(IUserRepository repo) : IRequestHandler<...>
{
    public async Task<...> Handle(...)
    {
        var spec = new UserSpecification(request.Email);
        var exists = await repo.AnyAsync(spec, ct);
        ...
    }
}
```

## Coding Rules
- DB 엔티티 prefix: `Tbl{Entity}` 유지
- using 정리: 불필요한 using 제거
- 외부 연동: AWS S3, CloudFront, Google BigQuery, CloSet, CloVise
- 페이징: 목록 조회 시 `IPageable` 인터페이스 사용 — 기존 코드베이스와 일관성 유지
- 부정 연산자 `!` 사용 금지 — 반드시 `== false` 또는 `!= true` 로 표현
