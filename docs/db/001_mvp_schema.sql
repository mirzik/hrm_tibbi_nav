-- Tibbi Nav HRM — MVP schema (раздел 92 ТЗ)
-- Это справочная схема для ревью DBA/архитектора.
-- В реальном проекте таблицы создаются через EF Core Migrations
-- (dotnet ef migrations add InitialCreate), а не вручную.

CREATE TABLE organization_units (
    id UUID PRIMARY KEY,
    name TEXT NOT NULL,
    legal_name TEXT NOT NULL,
    tax_id TEXT,
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    created_at_utc TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE TABLE clinics (
    id UUID PRIMARY KEY,
    organization_id UUID NOT NULL REFERENCES organization_units(id),
    code TEXT NOT NULL,
    name TEXT NOT NULL,
    address TEXT NOT NULL,
    status SMALLINT NOT NULL DEFAULT 1, -- 0 Planning,1 Active,2 Suspended,3 Closed
    planned_opening_date DATE,
    created_at_utc TIMESTAMPTZ NOT NULL DEFAULT now(),
    UNIQUE (organization_id, code)
);

CREATE TABLE departments (
    id UUID PRIMARY KEY,
    organization_id UUID NOT NULL,
    clinic_id UUID REFERENCES clinics(id),
    parent_department_id UUID REFERENCES departments(id),
    name TEXT NOT NULL,
    cost_center_code TEXT,
    created_at_utc TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE TABLE positions (
    id UUID PRIMARY KEY,
    organization_id UUID NOT NULL,
    clinic_id UUID,
    department_id UUID NOT NULL REFERENCES departments(id),
    title TEXT NOT NULL,
    category SMALLINT NOT NULL,
    approved_fte NUMERIC(5,2) NOT NULL DEFAULT 1.0,
    salary_budget_monthly NUMERIC(14,2) NOT NULL DEFAULT 0,
    reports_to_position_id UUID,
    created_at_utc TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE TABLE employees (
    id UUID PRIMARY KEY,
    organization_id UUID NOT NULL,
    clinic_id UUID,
    employee_code TEXT NOT NULL UNIQUE, -- TN-DUS-000152
    full_name TEXT NOT NULL,
    photo_url TEXT,
    gender SMALLINT NOT NULL,
    date_of_birth DATE NOT NULL,
    citizenship TEXT NOT NULL,
    phone TEXT,
    personal_email TEXT,
    corporate_email TEXT,
    address TEXT,
    emergency_contact TEXT,
    national_id_encrypted TEXT,
    bank_account_encrypted TEXT,
    status SMALLINT NOT NULL DEFAULT 1,
    is_deleted BOOLEAN NOT NULL DEFAULT FALSE,
    created_at_utc TIMESTAMPTZ NOT NULL DEFAULT now(),
    modified_at_utc TIMESTAMPTZ
);

-- Effective Dating (раздел 10): история никогда не перезаписывается,
-- новая запись = новая строка с effective_from, старая закрывается effective_to.
CREATE TABLE employment_records (
    id UUID PRIMARY KEY,
    organization_id UUID NOT NULL,
    clinic_id UUID,
    employee_id UUID NOT NULL REFERENCES employees(id),
    department_id UUID NOT NULL REFERENCES departments(id),
    position_id UUID NOT NULL REFERENCES positions(id),
    manager_employee_id UUID,
    functional_manager_employee_id UUID,
    fte NUMERIC(5,2) NOT NULL DEFAULT 1.0,
    employment_type SMALLINT NOT NULL,
    hire_date DATE NOT NULL,
    probation_end_date DATE,
    work_schedule TEXT,
    cost_center_code TEXT,
    base_salary NUMERIC(14,2),
    change_reason TEXT,
    effective_from DATE NOT NULL,
    effective_to DATE,
    is_current BOOLEAN NOT NULL DEFAULT TRUE,
    created_at_utc TIMESTAMPTZ NOT NULL DEFAULT now()
);
CREATE INDEX idx_employment_records_current ON employment_records (employee_id, is_current);

CREATE TABLE medical_credentials (
    id UUID PRIMARY KEY,
    employee_id UUID NOT NULL REFERENCES employees(id),
    type SMALLINT NOT NULL,
    title TEXT NOT NULL,
    issuing_authority TEXT,
    issue_date DATE NOT NULL,
    expiry_date DATE,
    file_url TEXT,
    status SMALLINT NOT NULL DEFAULT 4, -- PendingVerification
    created_at_utc TIMESTAMPTZ NOT NULL DEFAULT now()
);
CREATE INDEX idx_medical_credentials_expiry ON medical_credentials (expiry_date) WHERE expiry_date IS NOT NULL;

CREATE TABLE vacancies (
    id UUID PRIMARY KEY,
    organization_id UUID NOT NULL,
    clinic_id UUID,
    department_id UUID NOT NULL REFERENCES departments(id),
    position_id UUID NOT NULL REFERENCES positions(id),
    headcount_requested INT NOT NULL DEFAULT 1,
    reason SMALLINT NOT NULL,
    replacing_employee_id UUID,
    desired_start_date DATE,
    budget_salary NUMERIC(14,2),
    requirements TEXT,
    priority SMALLINT NOT NULL DEFAULT 1,
    status SMALLINT NOT NULL DEFAULT 0,
    created_by_employee_id UUID,
    created_at_utc TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE TABLE candidates (
    id UUID PRIMARY KEY,
    full_name TEXT NOT NULL,
    phone TEXT,
    email TEXT,
    cv_url TEXT,
    source TEXT,
    specialization TEXT,
    experience_years INT,
    salary_expectation NUMERIC(14,2),
    talent_pool_status SMALLINT,
    created_at_utc TIMESTAMPTZ NOT NULL DEFAULT now()
);
CREATE INDEX idx_candidates_phone ON candidates (phone);
CREATE INDEX idx_candidates_email ON candidates (email);

CREATE TABLE candidate_applications (
    id UUID PRIMARY KEY,
    candidate_id UUID NOT NULL REFERENCES candidates(id),
    vacancy_id UUID NOT NULL REFERENCES vacancies(id),
    stage SMALLINT NOT NULL DEFAULT 0,
    rejection_reason TEXT,
    created_at_utc TIMESTAMPTZ NOT NULL DEFAULT now(),
    UNIQUE (candidate_id, vacancy_id)
);

CREATE TABLE interviews (
    id UUID PRIMARY KEY,
    candidate_application_id UUID NOT NULL REFERENCES candidate_applications(id),
    type SMALLINT NOT NULL,
    scheduled_at_utc TIMESTAMPTZ NOT NULL,
    interviewer_user_id UUID,
    result SMALLINT,
    scorecard_json JSONB,
    notes TEXT,
    created_at_utc TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE TABLE offers (
    id UUID PRIMARY KEY,
    candidate_application_id UUID NOT NULL UNIQUE REFERENCES candidate_applications(id),
    salary NUMERIC(14,2) NOT NULL,
    bonus NUMERIC(14,2),
    fte NUMERIC(5,2) NOT NULL DEFAULT 1.0,
    proposed_start_date DATE NOT NULL,
    probation_days INT NOT NULL DEFAULT 90,
    additional_terms TEXT,
    status SMALLINT NOT NULL DEFAULT 0,
    sent_at_utc TIMESTAMPTZ,
    responded_at_utc TIMESTAMPTZ,
    created_at_utc TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE TABLE users (
    id UUID PRIMARY KEY,
    email TEXT NOT NULL UNIQUE,
    phone TEXT,
    display_name TEXT NOT NULL,
    employee_id UUID,
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    mfa_enabled BOOLEAN NOT NULL DEFAULT FALSE,
    created_at_utc TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE TABLE roles (
    id UUID PRIMARY KEY,
    code TEXT NOT NULL UNIQUE, -- SuperAdmin, HRAdmin, ChiefDoctor, ...
    name TEXT NOT NULL,
    is_system_role BOOLEAN NOT NULL DEFAULT FALSE
);

CREATE TABLE role_permissions (
    id UUID PRIMARY KEY,
    role_id UUID NOT NULL REFERENCES roles(id),
    resource TEXT NOT NULL,
    action SMALLINT NOT NULL,       -- View/Create/Edit/Delete/Approve/Export
    scope SMALLINT NOT NULL,        -- Self/OwnEmployees/Department/Clinic/Organization
    restricted_fields TEXT
);

CREATE TABLE user_roles (
    id UUID PRIMARY KEY,
    user_id UUID NOT NULL REFERENCES users(id),
    role_id UUID NOT NULL REFERENCES roles(id),
    scope_clinic_id UUID,
    scope_department_id UUID
);

-- Раздел 67: Audit Log. Обычный administrator role НЕ получает UPDATE/DELETE grant на эту таблицу.
CREATE TABLE audit_log_entries (
    id UUID PRIMARY KEY,
    user_id UUID,
    timestamp_utc TIMESTAMPTZ NOT NULL DEFAULT now(),
    ip TEXT,
    action TEXT NOT NULL,
    entity TEXT NOT NULL,
    entity_id UUID,
    old_value_json JSONB,
    new_value_json JSONB
);
CREATE INDEX idx_audit_entity ON audit_log_entries (entity, entity_id);

-- REVOKE UPDATE, DELETE ON audit_log_entries FROM app_role; -- выполняется отдельным DBA-скриптом при деплое
