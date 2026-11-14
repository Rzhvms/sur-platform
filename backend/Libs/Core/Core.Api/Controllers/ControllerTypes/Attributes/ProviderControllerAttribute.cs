using Core.Api.Controllers.ControllerTypes.Constants;
using Microsoft.AspNetCore.Authorization;

namespace Core.Api.Controllers.ControllerTypes.Attributes;

/// <summary>
/// Требует права администратора для доступа к контроллеру
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class ProviderControllerAttribute : AuthorizeAttribute
{
    ///
    public ProviderControllerAttribute()
    {
        Policy = ControllerPolicies.Provider;
    }
}