# Общие библиотеки Core

Core — общая папка для независимых проектов ClassLibrary на .NET 10. Корневого `Core.csproj` и отдельных решений библиотек нет. Проекты подключаются через `ProjectReference`; каждый можно отдельно упаковать в NuGet.

| Проект | Ответственность | Зависимости |
|---|---|---|
| Core.Abstractions | Общие исключения, контракт текущего пользователя, атрибут проверки значений | Только стандартная библиотека .NET |
| Core.Database | Средства Dapper, преобразования типов PostgreSQL, запуск FluentMigrator | Dapper, Npgsql, FluentMigrator, абстракции DI и ASP.NET Core для расширения запуска миграций |
| Core.Api | Контроллеры, фильтры, обработка HTTP-ошибок, извлечение пользователя из HTTP-контекста | ASP.NET Core и Core.Abstractions |

Название Core.Api отражает назначение компонентов HTTP API. Проект остаётся ClassLibrary, не запускает собственный веб-сервер и не содержит бизнес-логику сервисов.

В SurPlatform слой Api подключает Core.Api; Infrastructure — Core.Database и Core.Abstractions; Application — Core.Abstractions. Domain не зависит от этих библиотек. Репозитории, SQL-запросы и сами миграции SurPlatform остаются в Infrastructure приложения.

`ICurrentUserService` находится в `Core.Abstractions.User`, а его реализация через `IHttpContextAccessor` и регистрация `AddCoreUser` — в `Core.Api.User`. Обработка исключений использует общие типы `Core.Abstractions.Exceptions`.

Расширение `MigrateDatabase` в Core.Database сохраняет исходный контракт с `IApplicationBuilder`. SurPlatform подготавливает базы через `ClientDatabaseInitializer` и допускает запросы после проверки схемы конкретного клиента. Для этого расширения Core.Database содержит ссылку на платформу ASP.NET Core.

Для сборки контейнера SurPlatform контекстом Docker служит папка backend. Dockerfile копирует файлы проектов всех трёх библиотек до восстановления зависимостей.
