const API_URL = process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:8080/api/v1";

export type Employee = {
  id: string;
  employeeCode: string;
  fullName: string;
  status: string;
  clinicId: string | null;
};

export async function fetchEmployees(search?: string): Promise<Employee[]> {
  const url = new URL(`${API_URL}/employees`);
  if (search) url.searchParams.set("search", search);

  const res = await fetch(url, { cache: "no-store" });
  if (!res.ok) throw new Error(`API error: ${res.status}`);
  return res.json();
}
