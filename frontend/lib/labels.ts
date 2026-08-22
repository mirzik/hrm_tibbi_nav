// Подписи для enum-ов backend'а (сериализуются как числа, см. TibbiNav.Domain.*).

export const GENDER_LABELS: Record<number, string> = { 0: "Мужской", 1: "Женский" };

export const EMPLOYMENT_TYPE_LABELS: Record<number, string> = {
  0: "Полная занятость",
  1: "Частичная занятость",
  2: "Внутреннее совмещение",
  3: "Внешнее совмещение",
  4: "Временная",
  5: "Стажировка",
};

export const LEAVE_STATUS_LABELS: Record<number, string> = {
  0: "Заявлен",
  1: "Одобрен",
  2: "Отклонён",
  3: "Отменён",
};

export const DOCUMENT_TYPE_LABELS: Record<number, string> = {
  0: "Трудовой договор",
  1: "Приказ о приёме",
  2: "NDA",
  3: "Согласие на обработку ПДн",
};

export const DOCUMENT_STATUS_LABELS: Record<number, string> = {
  0: "Черновик",
  1: "На согласовании",
  2: "Одобрен",
  3: "Подписан",
  4: "В архиве",
  5: "Отменён",
};

export const TICKET_CATEGORY_LABELS: Record<number, string> = {
  0: "Кадровые документы",
  1: "Справки",
  2: "Отпуск",
  3: "Изменение данных",
  4: "Обучение",
  5: "Зарплатный вопрос",
  6: "Доступ",
  7: "Другое",
};

export const TICKET_STATUS_LABELS: Record<number, string> = {
  0: "Новый",
  1: "Назначен",
  2: "В работе",
  3: "Ожидание",
  4: "Решён",
  5: "Закрыт",
};

export const EMPLOYEE_STATUS_LABELS: Record<number, string> = {
  0: "Кандидат",
  1: "Активен",
  2: "В отпуске",
  3: "Испытательный срок",
  4: "Приостановлен",
  5: "Уволен",
  6: "Кадровый резерв",
};

export const CREDENTIAL_TYPE_LABELS: Record<number, string> = {
  0: "Диплом",
  1: "Интернатура",
  2: "Ординатура",
  3: "Специальность",
  4: "Сертификат",
  5: "Сертификат Минздрава",
  6: "Квалификационная категория",
  7: "Повышение квалификации",
  8: "Санитарная книжка",
  9: "Медосмотр",
  10: "Другое",
};

export const CREDENTIAL_STATUS_LABELS: Record<number, string> = {
  0: "Действителен",
  1: "Истекает",
  2: "Просрочен",
  3: "Отсутствует",
  4: "На проверке",
};

export function label(map: Record<number, string>, value: number): string {
  return map[value] ?? String(value);
}
