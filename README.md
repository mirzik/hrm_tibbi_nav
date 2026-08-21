# Tibbi Nav HRM — стартовый каркас (MVP core)

Это фундамент платформы по ТЗ v1.0 (ООО «Тибби Нав» / MEDSI Tajikistan).
Архитектура: **Modular Monolith + API First** (раздел 76), как и требовалось —
без микросервисов на старте.

## Что уже реализовано в этом каркасе

- **Domain**: Organization/Clinic/Department/Position (multi-clinic, раздел 4),
  Employee + EmploymentRecord с **Effective Dating** (раздел 10),
  Medical Credentials с контролем expiry (раздел 26-27),
  Recruitment: Vacancy → Candidate → Pipeline → Interview → Offer (разделы 13-24),
  Identity/RBAC: Role + Permission (Action + Scope + Restricted Fields, раздел 65),
  Audit Log entity (раздел 67).
- **Application**: генератор Employee ID (`TN-DUS-000152`), `HireCandidateService`
  реализующий кнопку "Hire Candidate" (раздел 28) в одной транзакции.
- **Infrastructure**: EF Core `DbContext`, PostgreSQL, UTC timestamps.
- **API**: ASP.NET Core, `/api/v1/employees`, `/api/v1/vacancies`, JWT/OIDC-ready
  аутентификация, Swagger, CORS.
- **Frontend**: Next.js/React/TypeScript, страница списка сотрудников,
  типизированный API-клиент.
- **DB**: справочная SQL-схема для ревью (`docs/db/001_mvp_schema.sql`).
- **DevOps**: `docker-compose.yml` (postgres + redis + api + frontend), Dockerfile.

## Что нужно достроить до полноценного MVP (раздел 92)

Это каркас, не готовая система. Дальше поэтапно добавляются:

1. **EF Core Migrations** (`dotnet ef migrations add InitialCreate`) — SQL-файл
   выше справочный, реальные миграции генерируются из C#-моделей.
2. **RBAC enforcement** — сейчас Permission/Role описаны в Domain, но нет
   `ScopeFilterMiddleware`, который реально режет запросы по scope пользователя.
3. **Onboarding/Preboarding checklist engine** (разделы 25, 31-32).
4. **Workforce validation** при создании вакансии (проверка FTE/бюджета, раздел 15).
5. **Workflow Engine** для настраиваемых маршрутов согласования (раздел 68).
6. **Document Generator** (DOCX/PDF из шаблонов, раздел 29).
7. **Attendance/Timesheet, Leave conflict engine** (разделы 35-39).
8. **Bulk Import** существующих Excel-данных (раздел 63) — это отдельный
   и критичный по срокам блок, т.к. текущие данные компании нужно перенести.
9. **Notification Engine**, **Audit interceptor** (пишущий реально на каждый SaveChanges).
10. Аутентификация: поднять Keycloak (или аналог) и подключить Authority.

## Как продолжить

Это стартовая точка. Полноценная разработка (миграции, все 33 модуля,
тесты, CI/CD, деплой) — многонедельный процесс, который эффективнее вести
в **Claude Code**: там я работаю с этим репозиторием напрямую (git, терминал,
запуск тестов, миграции), итеративно, сессия за сессией, а не в одном чате.

Рекомендуемый порядок продолжения (соответствует Phase-делению ТЗ,
разделы 92-94):
1. Домиграции + RBAC enforcement + Bulk Import штатного расписания и базы сотрудников.
2. Onboarding + Document Generator + Workflow Engine.
3. Attendance/Leave/Timesheet → передача в 1С (раздел 50).
4. Phase 2: LMS, KPI, Performance, Assets, HR Service Desk.
5. Phase 3: Talent Management, Gamification, AI Layer.

## Запуск локально (после появления команды разработки)

```bash
docker compose up -d postgres redis
cd backend && dotnet ef database update --project src/TibbiNav.Infrastructure --startup-project src/TibbiNav.Api
dotnet run --project backend/src/TibbiNav.Api
cd ../frontend && npm install && npm run dev
```
