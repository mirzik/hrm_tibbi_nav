namespace TibbiNav.Application.Authorization;

/// <summary>
/// Хранит <see cref="ScopeContext"/>, разрешённый ScopeFilterMiddleware (проект API)
/// для текущего HTTP-запроса — по аналогии с IHttpContextAccessor, но без
/// зависимости Application-слоя от ASP.NET Core. Регистрируется как Scoped
/// (Add-Scoped), поэтому живёт ровно один запрос.
/// </summary>
public interface IScopeContextAccessor
{
    ScopeContext? Current { get; set; }
}

public sealed class ScopeContextAccessor : IScopeContextAccessor
{
    public ScopeContext? Current { get; set; }
}
