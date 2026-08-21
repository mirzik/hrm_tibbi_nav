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
3. ~~**Onboarding/Preboarding checklist engine**~~ ✅ Сделано: чеклист по
   этапам Preboarding → Day1 → Week1 → Day30 → Day60 → Day90, шаблон выбирается
   по категории персонала/должности/клинике (`backend/src/TibbiNav.Application/Onboarding/`),
   запускается автоматически из `HireCandidateService` — см. раздел
   «Как проверить Onboarding локально» ниже.
4. **Workforce validation** при создании вакансии (проверка FTE/бюджета, раздел 15).
5. ~~**Workflow Engine**~~ ✅ Сделано: настраиваемые маршруты согласования
   (Trigger + Conditions + Steps + SLA/Escalation, целиком данные —
   `backend/src/TibbiNav.Application/Workflow/`), подключены к Vacancy (раздел 16:
   Manager → HR → Finance → ChiefDoctor → GeneralDirector) и LeaveRequest
   (раздел 37: руководитель подразделения), запускается автоматически при
   создании — см. раздел «Как проверить Workflow Engine локально» ниже.
6. ~~**Document Generator**~~ ✅ Сделано: трудовой договор, приказ о приёме, NDA,
   согласие на обработку ПДн — генерируются из шаблона в DOCX и PDF
   (`backend/src/TibbiNav.Application/Documents/`), стандартный пакет запускается
   автоматически из `HireCandidateService`, плюс ручная генерация и статусы
   Draft → Review → Approved → Signed → Archived — см. раздел
   «Как проверить Document Generator локально» ниже.
7. ~~**Attendance/Timesheet, Leave conflict engine**~~ ✅ Сделано: табель с
   учётом рабочих часов/выходных/отпуска/больничного/командировки/переработки,
   закрытие периода Department Manager → HR → Accounting → Locked через
   Workflow Engine, "отдельная процедура" правки после Locked с журналом
   (`backend/src/TibbiNav.Application/Attendance/`); Leave conflict engine —
   предупреждение при создании отпуска, если превышен настраиваемый по
   подразделению порог одновременного отсутствия коллег той же категории
   персонала — см. раздел «Как проверить Attendance/Timesheet и Leave conflict
   engine локально» ниже.
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

## Как проверить Onboarding локально (раздел 25, 31-32)

`DevSeedData` создаёт 3 шаблона чеклиста: общий (подходит всем) и два по
категории персонала — `Reception` и `Doctor` (более специфичные побеждают
общий, см. `OnboardingChecklistTemplateSelector`). Чеклист создаётся
**автоматически** внутри `HireCandidateService.HireAsync` — той же транзакцией,
что и сам найм (`POST /api/v1/vacancies/applications/{id}/hire`); ручной
`POST /onboarding/checklists/start` нужен только для backfill сотрудников,
нанятых до появления модуля.

```bash
# Шаблоны (видно, какой выберется для какой категории)
curl -H "X-Dev-User: admin@tibbinav.local" http://localhost:8080/api/v1/onboarding/templates

# После Hire — чеклист уже создан, с датами, посчитанными от HireDate
# (Preboarding = HireDate-7, Day1 = HireDate, Week1 = +7, Day30 = +30, Day60 = +60, Day90 = +90)
curl -H "X-Dev-User: admin@tibbinav.local" http://localhost:8080/api/v1/onboarding/checklists/by-employee/<employeeId>

# Отметить задачу выполненной — когда закрыты все, чеклист сам переходит в Completed
curl -X PATCH -H "X-Dev-User: admin@tibbinav.local" -H "Content-Type: application/json" \
  -d '{"status":2}' http://localhost:8080/api/v1/onboarding/checklists/tasks/<taskId>
```

## Как проверить Document Generator локально (раздел 29)

`DevSeedData` создаёт по одному активному шаблону на каждый из 4 типов
документа (`EmployeeDocumentType`): трудовой договор, приказ о приёме, NDA,
согласие на обработку ПДн — с плейсхолдерами вида `{{FullName}}`
(полный список — `DocumentPlaceholderResolver`). Весь пакет генерируется
**автоматически** внутри `HireCandidateService.HireAsync`, той же транзакцией,
что и сам найм — DOCX (DocumentFormat.OpenXml) и PDF (QuestPDF, с встроенным
Cyrillic-шрифтом Noto Sans — иначе PDF-рендер может молча терять кириллицу на
хостах без системных шрифтов) рендерятся из одной и той же модели блоков, так
что не могут разойтись по содержанию. Файлы лежат на локальном диске
(`backend/src/TibbiNav.Api/document-storage/`, см. `IDocumentFileStorage`) —
заглушка до появления S3-compatible хранилища (раздел 80).

```bash
# Шаблоны
curl -H "X-Dev-User: admin@tibbinav.local" http://localhost:8080/api/v1/document-templates

# После Hire — документы уже сгенерированы
curl -H "X-Dev-User: admin@tibbinav.local" "http://localhost:8080/api/v1/employee-documents?employeeId=<employeeId>"

# Скачать DOCX или PDF
curl -H "X-Dev-User: admin@tibbinav.local" -o doc.pdf "http://localhost:8080/api/v1/employee-documents/<id>/file?format=pdf"

# Статусы: Draft -> Review -> Approved -> Signed -> Archived (Cancelled — из любого нетерминального)
curl -X POST -H "X-Dev-User: admin@tibbinav.local" http://localhost:8080/api/v1/employee-documents/<id>/submit-for-review
curl -X POST -H "X-Dev-User: admin@tibbinav.local" http://localhost:8080/api/v1/employee-documents/<id>/approve   # Permission "Approve"
curl -X POST -H "X-Dev-User: admin@tibbinav.local" http://localhost:8080/api/v1/employee-documents/<id>/sign      # Permission "Approve"
curl -X POST -H "X-Dev-User: admin@tibbinav.local" http://localhost:8080/api/v1/employee-documents/<id>/archive

# Ручная генерация одного документа (напр. перевыпуск NDA)
curl -X POST -H "X-Dev-User: admin@tibbinav.local" -H "Content-Type: application/json" \
  -d '{"employeeId":"<employeeId>","documentType":2}' http://localhost:8080/api/v1/employee-documents/generate
```

## Как проверить Workflow Engine локально (раздел 68)

`DevSeedData` создаёт 2 маршрута: `Vacancy` (Manager → HR → Finance →
ChiefDoctor → GeneralDirector, 5 шагов) и `LeaveRequest` (1 шаг — руководитель
подразделения, `WorkflowApproverStrategy.DirectManager`). Каждый шаг
резолвится либо по роли+scope (`RoleInClinic`/`RoleInOrganization`), либо
через `EmploymentRecord.ManagerEmployeeId` заявителя. Approve/Reject
проверяет, что действующий пользователь реально входит в число согласующих
текущего шага (пересчитывается заново, не по снимку) — иначе 403.
Пользователи под каждую роль маршрута: `manager.dus@tibbinav.local`
(DepartmentManager), `hr.dus@tibbinav.local` (HRManager),
`finance@tibbinav.local` (Finance), `chiefdoctor.dus@tibbinav.local`
(ChiefDoctor), `director@tibbinav.local` (GeneralDirector).

```bash
# 1. Создание вакансии — запускает workflow, назначает первого approver-а
curl -H "X-Dev-User: admin@tibbinav.local" -H "Content-Type: application/json" \
  -d '{"organizationId":"<orgId>","clinicId":"<clinicId>","departmentId":"<deptId>","positionId":"<positionId>","headcountRequested":1,"reason":0,"priority":1}' \
  http://localhost:8080/api/v1/vacancies
# -> вакансия Status=PendingApproval

# 2. Смотрим instance — виден текущий шаг и назначенный approver
curl -H "X-Dev-User: admin@tibbinav.local" "http://localhost:8080/api/v1/workflow/instances/by-entity/Vacancy/<vacancyId>"

# 3. Approve по очереди каждым согласующим — сдвигает currentStepIndex дальше
curl -X POST -H "X-Dev-User: manager.dus@tibbinav.local" http://localhost:8080/api/v1/workflow/instances/<instanceId>/approve
curl -X POST -H "X-Dev-User: hr.dus@tibbinav.local" http://localhost:8080/api/v1/workflow/instances/<instanceId>/approve
curl -X POST -H "X-Dev-User: finance@tibbinav.local" http://localhost:8080/api/v1/workflow/instances/<instanceId>/approve
curl -X POST -H "X-Dev-User: chiefdoctor.dus@tibbinav.local" http://localhost:8080/api/v1/workflow/instances/<instanceId>/approve
curl -X POST -H "X-Dev-User: director@tibbinav.local" http://localhost:8080/api/v1/workflow/instances/<instanceId>/approve
# -> последний approve переводит instance в Approved и Vacancy.Status в Approved

# "Моя очередь на согласование"
curl -H "X-Dev-User: hr.dus@tibbinav.local" http://localhost:8080/api/v1/workflow/instances/my-pending

# Ручной прогон эскалации просроченных (SLA) шагов — в проде это фоновый
# WorkflowEscalationHostedService раз в 15 минут
curl -X POST -H "X-Dev-User: admin@tibbinav.local" http://localhost:8080/api/v1/workflow/instances/escalations/process

# Новый маршрут с условием (раздел 68: Conditions) — напр. fast-track для
# критичных вакансий в обход промежуточных шагов
curl -X POST -H "X-Dev-User: admin@tibbinav.local" -H "Content-Type: application/json" \
  -d '{"name":"VIP fast-track","entityType":"Vacancy","priority":10,"conditions":[{"fieldName":"Priority","operator":0,"value":"Critical"}],"steps":[{"orderIndex":0,"name":"GeneralDirector fast-track","approverStrategy":0,"approverRoleCode":"GeneralDirector","slaHours":24,"escalationAction":0}]}' \
  http://localhost:8080/api/v1/workflow/definitions
```

## Как проверить Attendance/Timesheet и Leave conflict engine локально (раздел 35-39)

Табель (`Timesheet`) — по сотруднику за период; блокировка правок определяется
не полем на самом табеле, а наличием `TimesheetClosure` подразделения+периода
со статусом `Locked` — единственным источником истины (см.
`TimesheetService`). Закрытие табеля переиспользует Workflow Engine
(`EntityType="TimesheetClosure"`, маршрут Department Manager → HR →
Accounting в DevSeedData) — Approve/Reject каждого шага идёт через уже
знакомый `POST /workflow/instances/{id}/approve|reject`.

```bash
# 1. Создать табель на период
curl -X POST -H "X-Dev-User: admin@tibbinav.local" -H "Content-Type: application/json" \
  -d '{"employeeId":"<employeeId>","periodStart":"2026-08-01","periodEnd":"2026-08-31"}' \
  http://localhost:8080/api/v1/timesheets

# 2. Проставить дни (DayType: 0=Working,1=Weekend,2=Holiday,3=Leave,4=Sick,5=BusinessTrip,6=Absence)
curl -X PUT -H "X-Dev-User: admin@tibbinav.local" -H "Content-Type: application/json" \
  -d '{"days":[{"date":"2026-08-03","dayType":0,"hours":8,"overtimeHours":2}]}' \
  http://localhost:8080/api/v1/timesheets/<id>/days

# 3. Закрыть табель подразделения — запускает маршрут согласования
curl -X POST -H "X-Dev-User: admin@tibbinav.local" -H "Content-Type: application/json" \
  -d '{"departmentId":"<deptId>","periodStart":"2026-08-01","periodEnd":"2026-08-31"}' \
  http://localhost:8080/api/v1/timesheet-closures

curl -X POST -H "X-Dev-User: manager.dus@tibbinav.local" http://localhost:8080/api/v1/workflow/instances/<instanceId>/approve
curl -X POST -H "X-Dev-User: hr.dus@tibbinav.local" http://localhost:8080/api/v1/workflow/instances/<instanceId>/approve
curl -X POST -H "X-Dev-User: accounting@tibbinav.local" http://localhost:8080/api/v1/workflow/instances/<instanceId>/approve
# -> TimesheetClosure.Status = Locked

# 4. Обычная правка после Locked — 400 с понятной ошибкой
curl -X PUT -H "X-Dev-User: admin@tibbinav.local" -H "Content-Type: application/json" \
  -d '{"days":[{"date":"2026-08-10","dayType":0,"hours":8,"overtimeHours":0}]}' \
  http://localhost:8080/api/v1/timesheets/<id>/days

# 5. "Отдельная процедура" — работает и при Locked, требует Permission "Approve" и причину, логируется
curl -X POST -H "X-Dev-User: admin@tibbinav.local" -H "Content-Type: application/json" \
  -d '{"dayType":0,"hours":9,"overtimeHours":1,"reason":"HR audit found missing overtime hour"}' \
  http://localhost:8080/api/v1/timesheets/<id>/days/2026-08-03/correct
curl -H "X-Dev-User: admin@tibbinav.local" http://localhost:8080/api/v1/timesheets/<id>/edit-log
```

Leave conflict engine: `DevSeedData` создаёт отдел "Хирургия" с 4 врачами и
порогом 50% (`DepartmentLeaveThreshold`; без настройки для подразделения
применяется `LeaveConflictChecker.DefaultThresholdPercent`). "Коллеги" — та же
`PersonnelCategory` в том же подразделении, не всё подразделение целиком.

```bash
# Один врач в отпуске — конфликта нет (1 из 4 = 25% < 50%)
curl -X POST -H "X-Dev-User: admin@tibbinav.local" -H "Content-Type: application/json" \
  -d '{"employeeId":"<doctor1Id>","leaveType":"Annual","startDate":"2026-09-01","endDate":"2026-09-14","days":14}' \
  http://localhost:8080/api/v1/leave-requests
# -> одобрить через workflow (см. выше), затем:

# Второй врач с пересекающимся периодом — hasConflict:true, "2 из 4 ... порог превышен"
curl -X POST -H "X-Dev-User: admin@tibbinav.local" -H "Content-Type: application/json" \
  -d '{"employeeId":"<doctor2Id>","leaveType":"Annual","startDate":"2026-09-05","endDate":"2026-09-12","days":8}' \
  http://localhost:8080/api/v1/leave-requests

# Настроить порог по подразделению (раздел 39: конфигурируемо, не хардкод)
curl -X PUT -H "X-Dev-User: admin@tibbinav.local" -H "Content-Type: application/json" \
  -d '{"maxConcurrentAbsencePercent":30}' http://localhost:8080/api/v1/leave-thresholds/<deptId>
```
