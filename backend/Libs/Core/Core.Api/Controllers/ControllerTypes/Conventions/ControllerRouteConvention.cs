using Core.Api.Controllers.ControllerTypes.Attributes;
using Core.Api.Controllers.ControllerTypes.Base;
using Core.Api.Controllers.ControllerTypes.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationModels;

namespace Core.Api.Controllers.ControllerTypes.Conventions;

/// <summary>
/// Добавляет версию API и префикс типа контроллера к маршрутам
/// </summary>
public sealed class ControllerRouteConvention : IApplicationModelConvention
{
    /// <inheritdoc/>
    public void Apply(ApplicationModel application)
    {
        foreach (var controller in application.Controllers)
        {
            if (!typeof(ApiControllerBase).IsAssignableFrom(controller.ControllerType))
            {
                continue;
            }

            if (typeof(BaseProviderController).IsAssignableFrom(controller.ControllerType) ||
                controller.Attributes.OfType<ProviderControllerAttribute>().Any())
            {
                if (controller.Attributes.OfType<IAllowAnonymous>().Any() ||
                    controller.Actions.Any(action => action.Attributes.OfType<IAllowAnonymous>().Any()))
                {
                    throw new InvalidOperationException($"Provider-контроллер {controller.ControllerName} не может разрешать анонимный доступ");
                }
            }

            foreach (var selector in controller.Selectors.Concat(controller.Actions.SelectMany(action => action.Selectors)))
            {
                var template = selector.AttributeRouteModel?.Template;

                if (template is not null && (template.StartsWith('/') || template.StartsWith("~/", StringComparison.Ordinal)))
                {
                    throw new InvalidOperationException($"Контроллер {controller.ControllerName} должен использовать относительные маршруты");
                }
            }

            var prefixAttribute = controller.Attributes.OfType<ControllerPrefixAttribute>().FirstOrDefault();
            var prefix = prefixAttribute is null ? ControllerPrefix.ApiVersionPrefix : $"{ControllerPrefix.ApiVersionPrefix}/{prefixAttribute.Prefix}";
            ApplyPrefix(controller, prefix);
        }
    }

    /// <summary>
    /// Добавляет префикс ко всем маршрутам контроллера
    /// </summary>
    /// <param name="controller">Модель контроллера MVC</param>
    /// <param name="prefix">Префикс маршрута</param>
    private static void ApplyPrefix(ControllerModel controller, string prefix)
    {
        var prefixRoute = new AttributeRouteModel(new RouteAttribute(prefix));

        foreach (var selector in controller.Selectors)
        {
            selector.AttributeRouteModel = selector.AttributeRouteModel is null
                ? prefixRoute
                : AttributeRouteModel.CombineAttributeRouteModel(prefixRoute, selector.AttributeRouteModel);
        }
    }
}
