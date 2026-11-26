/** Нижняя граница временного пароля по контракту Identity */
const minimumLength = 8;
/** Верхняя граница временного пароля по контракту Identity */
const maximumLength = 256;

/** Проверяет совпадение без изменения пробелов в исходном пароле */
export function validTemporaryPassword(password: string, confirmation: string): boolean {
  return !!password.trim() && password.length >= minimumLength && password.length <= maximumLength
    && password === confirmation;
}
