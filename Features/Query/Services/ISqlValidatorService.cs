namespace OpsPilotAI.Features.Query.Services;

public interface ISqlValidatorService
{
    (bool IsValid, string Message) Validate(string sql);
    string EnsureLimit(string sql, int limit = 100);
}
