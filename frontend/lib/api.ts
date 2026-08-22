const API_URL = process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:8080/api/v1";

export class ApiError extends Error {
  constructor(
    public status: number,
    message: string,
  ) {
    super(message);
  }
}

/** Раздел 53: все self-service запросы идут с заголовком X-Dev-User (см. lib/session.ts) —
 * backend резолвит "свой" EmployeeId из него и жёстко фильтрует по нему в MeController. */
async function apiFetch<T>(path: string, devUserEmail: string, init?: RequestInit): Promise<T> {
  const res = await fetch(`${API_URL}${path}`, {
    ...init,
    cache: "no-store",
    headers: {
      "X-Dev-User": devUserEmail,
      ...(init?.body ? { "Content-Type": "application/json" } : {}),
      ...init?.headers,
    },
  });

  if (!res.ok) {
    const body = await res.text().catch(() => "");
    let message = body;
    try {
      const parsed = JSON.parse(body);
      message = parsed.error ?? parsed.message ?? parsed.title ?? body;
    } catch {
      // тело не JSON — оставляем как есть
    }
    throw new ApiError(res.status, message || `Ошибка API: ${res.status}`);
  }

  if (res.status === 204) return undefined as T;
  return res.json();
}

// --- Employees (HR Workspace) ---

export type Employee = {
  id: string;
  employeeCode: string;
  fullName: string;
  status: number;
  clinicId: string | null;
};

export async function fetchEmployees(devUserEmail: string, search?: string): Promise<Employee[]> {
  const query = search ? `?search=${encodeURIComponent(search)}` : "";
  return apiFetch<Employee[]>(`/employees${query}`, devUserEmail);
}

// --- Employee detail (HR Workspace, /employees/{id}) ---
// Внимание: EmployeesController.GetById сериализует Employee/EmploymentRecord/
// MedicalCredential через ToRestrictedDictionary (раздел 66) — это
// Dictionary<string, object?>, ключи которого НЕ проходят через camelCase
// JsonNamingPolicy (в отличие от обычных DTO), поэтому здесь PascalCase, как в
// самих C#-сущностях. Restricted-поля (Salary/BankAccount/NationalId) могут
// отсутствовать в объекте целиком, если у роли есть RestrictedFields — отсюда
// опциональность этих полей ниже.
export type EmployeeFull = {
  Id: string;
  EmployeeCode: string;
  FullName: string;
  PhotoUrl: string | null;
  Gender: number;
  DateOfBirth: string;
  Citizenship: string;
  Phone: string | null;
  PersonalEmail: string | null;
  CorporateEmail: string | null;
  Address: string | null;
  EmergencyContact: string | null;
  Status: number;
  ClinicId: string | null;
  OrganizationId: string;
  BankAccountEncrypted?: string | null;
  NationalIdEncrypted?: string | null;
};

export type EmploymentRecordFull = {
  Id: string;
  DepartmentId: string;
  PositionId: string;
  ManagerEmployeeId: string | null;
  DepartmentName: string | null;
  PositionTitle: string | null;
  ManagerFullName: string | null;
  Fte: number;
  EmploymentType: number;
  HireDate: string;
  ProbationEndDate: string | null;
  WorkSchedule: string | null;
  EffectiveFrom: string;
  EffectiveTo: string | null;
  IsCurrent: boolean;
  BaseSalary?: number | null;
};

export type MedicalCredentialFull = {
  Id: string;
  Type: number;
  Title: string;
  IssuingAuthority: string | null;
  IssueDate: string;
  ExpiryDate: string | null;
  Status: number;
};

export type EmployeeDetail = {
  employee: EmployeeFull;
  employmentRecords: EmploymentRecordFull[];
  medicalCredentials: MedicalCredentialFull[];
};

export const fetchEmployeeDetail = (email: string, id: string) => apiFetch<EmployeeDetail>(`/employees/${id}`, email);

// Документы конкретного сотрудника — обычный (camelCase) DTO из
// EmployeeDocumentsController.ToDto, в отличие от Employee/EmploymentRecord выше.
export type EmployeeDocumentFull = {
  id: string;
  employeeId: string;
  documentType: number;
  title: string;
  version: number;
  status: number;
  generatedAtUtc: string;
  approvedAtUtc: string | null;
  signedAtUtc: string | null;
  hasDocx: boolean;
  hasPdf: boolean;
};

export const fetchEmployeeDocuments = (email: string, employeeId: string) =>
  apiFetch<EmployeeDocumentFull[]>(`/employee-documents?employeeId=${employeeId}`, email);

// --- Employee Self Service (раздел 53) ---

export type MyProfile = {
  id: string;
  employeeCode: string;
  fullName: string;
  gender: number;
  dateOfBirth: string;
  citizenship: string;
  phone: string | null;
  personalEmail: string | null;
  corporateEmail: string | null;
  address: string | null;
  status: number;
};

export type MyEmployment = {
  id: string;
  departmentName: string | null;
  positionTitle: string | null;
  managerFullName: string | null;
  fte: number;
  employmentType: number;
  hireDate: string;
  probationEndDate: string | null;
  workSchedule: string | null;
  baseSalary: number | null;
};

export type LeaveBalance = {
  year: number;
  leaveType: string;
  entitlementDays: number;
  usedDays: number;
  pendingDays: number;
  remainingDays: number;
};

export type MyLeaveRequest = {
  id: string;
  leaveType: string;
  startDate: string;
  endDate: string;
  days: number;
  status: number;
  comment: string | null;
  createdAtUtc: string;
};

export type ConflictWarning = {
  hasConflict: boolean;
  message: string;
  category: string;
  totalPeers: number;
  absentCount: number;
  thresholdPercent: number;
  conflictingLeaves: { employeeId: string; fullName: string; startDate: string; endDate: string }[];
};

export type MyDocument = {
  id: string;
  documentType: number;
  title: string;
  status: number;
  generatedAtUtc: string;
};

export type MyTicket = {
  id: string;
  category: number;
  subject: string;
  status: number;
  createdAtUtc: string;
};

export type MyTicketDetail = MyTicket & {
  description: string;
  resolvedAtUtc: string | null;
  closedAtUtc: string | null;
  comments: { id: string; authorUserId: string; text: string; createdAtUtc: string }[];
};

export const fetchMyProfile = (email: string) => apiFetch<MyProfile>("/me/profile", email);
export const fetchMyEmployment = (email: string) => apiFetch<MyEmployment>("/me/employment", email);
export const fetchMyLeaveBalance = (email: string) => apiFetch<LeaveBalance>("/me/leave-balance", email);
export const fetchMyLeaveRequests = (email: string) => apiFetch<MyLeaveRequest[]>("/me/leave-requests", email);
export const fetchMyDocuments = (email: string) => apiFetch<MyDocument[]>("/me/documents", email);
export const fetchMyTickets = (email: string) => apiFetch<MyTicket[]>("/me/tickets", email);
export const fetchMyTicket = (email: string, id: string) => apiFetch<MyTicketDetail>(`/me/tickets/${id}`, email);

export function createMyLeaveRequest(
  email: string,
  body: { leaveType: string; startDate: string; endDate: string; days: number; comment?: string },
) {
  return apiFetch<{ leaveRequest: MyLeaveRequest; conflictWarning: ConflictWarning }>("/me/leave-requests", email, {
    method: "POST",
    body: JSON.stringify(body),
  });
}

export function createMyTicket(email: string, body: { category: number; subject: string; description: string }) {
  return apiFetch<MyTicket>("/me/tickets", email, { method: "POST", body: JSON.stringify(body) });
}

export function addMyTicketComment(email: string, ticketId: string, text: string) {
  return apiFetch(`/me/tickets/${ticketId}/comments`, email, { method: "POST", body: JSON.stringify({ text }) });
}
