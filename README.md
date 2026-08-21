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

1. ~~**EF Core Migrations**~~ ✅ Сделано: `InitialCreate` сгенерирована из
   Domain-моделей (`backend/src/TibbiNav.Infrastructure/Migrations/`) и
   применена к локальной PostgreSQL — 20 доменных таблиц + `__EFMigrationsHistory`.
2. ~~**RBAC enforcement**~~ ✅ Сделано: `ScopeFilterMiddleware` (`backend/src/TibbiNav.Api/Middleware/`)
   реально режет запросы по Role+Action+Scope+RestrictedFields — см. раздел
   «Как проверить RBAC локально» ниже.
3. **Onboarding/Preboarding checklist engine** (разделы 25, 31-32).
4. **Workforce validation** при создании вакансии (проверка FTE/бюджета, раздел 15).
5. **Workflow Engine** для настраиваемых маршрутов согласования (раздел 68).
6. **Document Generator** (DOCX/PDF из шаблонов, раздел 29).
7. **Attendance/Timesheet, Leave conflict engine** (разделы 35-39).
8. ~~**Bulk Import**~~ ✅ Сделано: `POST /api/v1/bulk-import/{kind}/upload` +
   пайплайн Upload → Mapping → Preview → Validation → Import → Result для
   штатного расписания и базы сотрудников (`backend/src/TibbiNav.Application/BulkImport/`).
   Образец шаблона — `GET /api/v1/bulk-import/template`.
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

## Как проверить RBAC локально (ScopeFilterMiddleware)

Пока Keycloak/OIDC не поднят (пункт 10 выше), в `Development` вместо реального
JWT работает `DevHeaderAuthenticationHandler`: любой запрос с заголовком
`X-Dev-User: <email>` аутентифицируется как этот пользователь. При первом
запуске в Development `DevSeedData` создаёт демо-набор (идемпотентно, если
`Users` пуст) — Organization, 2 клиники (DUS/KHJ), 3 сотрудника, 3 роли и 3
пользователя, покрывающие все уровни Scope:

| Пользователь | Роль | Scope | Что видит |
|---|---|---|---|
| `admin@tibbinav.local` | SuperAdmin | Organization | всех сотрудников, все поля |
| `hr.dus@tibbinav.local` | HRManager | Clinic (DUS) | только сотрудников DUS; `BankAccount`/`NationalId` скрыты |
| `manager.dus@tibbinav.local` | DepartmentManager | OwnEmployees | только своих подчинённых; `Salary`/`BankAccount`/`NationalId` скрыты |

```bash
curl http://localhost:8080/api/v1/employees                                    # 401 — нет заголовка
curl -H "X-Dev-User: admin@tibbinav.local" http://localhost:8080/api/v1/employees      # все
curl -H "X-Dev-User: hr.dus@tibbinav.local" http://localhost:8080/api/v1/employees     # только DUS
curl -H "X-Dev-User: manager.dus@tibbinav.local" http://localhost:8080/api/v1/employees # только подчинённые
curl -H "X-Dev-User: nobody@tibbinav.local" http://localhost:8080/api/v1/employees     # 403 — нет роли
```

## Как проверить Bulk Import локально (раздел 63)

Пайплайн Upload → Mapping → Preview → Validation → Import → Result, по одному
endpoint-у на шаг (`api/v1/bulk-import/...`), два вида данных — `StaffingSchedule`
(штатное расписание: Department+Position) и `Employees` (Employee+EmploymentRecord).
Employees-импорт требует, чтобы Department/Position уже существовали — сначала
прогоните StaffingSchedule. У `admin@tibbinav.local` (SuperAdmin) есть права
View/Create/Approve на ресурс `BulkImport`; `Approve` нужен именно для
финального `/import` — это необратимый шаг.

```bash
# Образец файла (2 листа: «Штатное расписание», «Сотрудники» + «Справочник»)
curl -H "X-Dev-User: admin@tibbinav.local" -o template.xlsx http://localhost:8080/api/v1/bulk-import/template

# 1. Upload — парсит XLSX и сам пытается сматчить столбцы по шаблону
curl -H "X-Dev-User: admin@tibbinav.local" -F "file=@template.xlsx" \
  "http://localhost:8080/api/v1/bulk-import/StaffingSchedule/upload?organizationId=<orgId>"
# -> {"id": "<batchId>", "status": "Mapped", ...}

# 2. Mapping (не обязателен, если авто-маппинг покрыл все обязательные поля)
curl -X PUT -H "X-Dev-User: admin@tibbinav.local" -H "Content-Type: application/json" \
  -d '{"ClinicCode":"Код клиники *", "...": "..."}' \
  http://localhost:8080/api/v1/bulk-import/<batchId>/mapping

# 3. Preview — первые N строк, без записи в БД
curl -H "X-Dev-User: admin@tibbinav.local" "http://localhost:8080/api/v1/bulk-import/<batchId>/preview?take=10"

# 4. Validation — все строки, статус батча -> Validated/ValidationFailed
curl -X POST -H "X-Dev-User: admin@tibbinav.local" http://localhost:8080/api/v1/bulk-import/<batchId>/validate

# 5. Import — только из Validated, одной транзакцией (всё или ничего)
curl -X POST -H "X-Dev-User: admin@tibbinav.local" http://localhost:8080/api/v1/bulk-import/<batchId>/import

# 6. Result
curl -H "X-Dev-User: admin@tibbinav.local" http://localhost:8080/api/v1/bulk-import/<batchId>
```
