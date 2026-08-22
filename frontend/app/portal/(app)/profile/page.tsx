import { ApiError, fetchMyEmployment, fetchMyProfile } from "@/lib/api";
import { getDevUserEmail } from "@/lib/session";
import { EMPLOYMENT_TYPE_LABELS, GENDER_LABELS, label } from "@/lib/labels";
import AccessDenied from "../AccessDenied";

export default async function ProfilePage() {
  const email = (await getDevUserEmail())!;

  let profile, employment;
  try {
    [profile, employment] = await Promise.all([fetchMyProfile(email), fetchMyEmployment(email).catch(() => null)]);
  } catch (err) {
    if (err instanceof ApiError && (err.status === 403 || err.status === 401)) return <AccessDenied email={email} />;
    throw err;
  }

  return (
    <div>
      <h1 style={{ marginTop: 0 }}>Мой профиль</h1>

      <section style={cardStyle}>
        <h2 style={h2Style}>{profile.fullName}</h2>
        <dl style={dlStyle}>
          <Row term="Табельный номер" value={profile.employeeCode} />
          <Row term="Пол" value={label(GENDER_LABELS, profile.gender)} />
          <Row term="Дата рождения" value={profile.dateOfBirth} />
          <Row term="Гражданство" value={profile.citizenship} />
          <Row term="Телефон" value={profile.phone ?? "—"} />
          <Row term="Личная почта" value={profile.personalEmail ?? "—"} />
          <Row term="Корпоративная почта" value={profile.corporateEmail ?? "—"} />
          <Row term="Адрес" value={profile.address ?? "—"} />
        </dl>
      </section>

      <section style={cardStyle}>
        <h2 style={h2Style}>Текущее трудоустройство</h2>
        {employment ? (
          <dl style={dlStyle}>
            <Row term="Подразделение" value={employment.departmentName ?? "—"} />
            <Row term="Должность" value={employment.positionTitle ?? "—"} />
            <Row term="Руководитель" value={employment.managerFullName ?? "—"} />
            <Row term="Ставка (FTE)" value={String(employment.fte)} />
            <Row term="Вид занятости" value={label(EMPLOYMENT_TYPE_LABELS, employment.employmentType)} />
            <Row term="Дата приёма" value={employment.hireDate} />
            <Row term="Оклад" value={employment.baseSalary != null ? String(employment.baseSalary) : "—"} />
          </dl>
        ) : (
          <p style={{ color: "#888" }}>Текущая запись трудоустройства не найдена.</p>
        )}
      </section>
    </div>
  );
}

function Row({ term, value }: { term: string; value: string }) {
  return (
    <>
      <dt style={dtStyle}>{term}</dt>
      <dd style={ddStyle}>{value}</dd>
    </>
  );
}

const cardStyle: React.CSSProperties = {
  background: "#fff",
  border: "1px solid #e5e7eb",
  borderRadius: 8,
  padding: 20,
  marginBottom: 20,
  maxWidth: 640,
};
const h2Style: React.CSSProperties = { fontSize: 16, marginTop: 0, marginBottom: 12 };
const dlStyle: React.CSSProperties = { display: "grid", gridTemplateColumns: "180px 1fr", rowGap: 8, columnGap: 12, margin: 0 };
const dtStyle: React.CSSProperties = { color: "#6b7280", fontSize: 13 };
const ddStyle: React.CSSProperties = { margin: 0, fontSize: 14 };
