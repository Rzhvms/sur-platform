export interface AssessmentFields {
  likelihoodLevelId: string;
  lossAmount: number | null;
  downtimeWeeks: number | null;
  downtimeDays: number | null;
  downtimeHours: number | null;
}

export type AssessmentInputs = Pick<AssessmentFields, 'likelihoodLevelId'> &
  ({ lossAmount: number } | Pick<AssessmentFields, 'downtimeWeeks' | 'downtimeDays' | 'downtimeHours'>);

/** Сохраняет ноль как значение и отправляет только поля, допустимые для риска с объектом или без него. */
export function assessmentInputs(fields: AssessmentFields, hasAsset: boolean): AssessmentInputs | null {
  const { likelihoodLevelId, lossAmount, downtimeWeeks, downtimeDays, downtimeHours } = fields;
  if (!likelihoodLevelId) {
    return null;
  }
  if (!hasAsset) {
    if (lossAmount === null || !Number.isFinite(lossAmount) || lossAmount < 0 ||
      !/^\d+(?:\.\d{1,2})?$/.test(String(lossAmount))) {
      return null;
    }
    return { likelihoodLevelId, lossAmount };
  }
  const validInteger = (value: number | null) => value === null ||
    Number.isInteger(value) && value >= 0 && value <= 2_147_483_647;
  if (!validInteger(downtimeWeeks) || !validInteger(downtimeDays) ||
    (downtimeHours !== null && (!Number.isFinite(downtimeHours) || downtimeHours < 0)) ||
    [downtimeWeeks, downtimeDays, downtimeHours].every(value => value === null)) {
    return null;
  }
  return { likelihoodLevelId, downtimeWeeks, downtimeDays, downtimeHours };
}

export interface AssessmentVersions {
  riskVersion: string;
  scaleVersionId: string;
  assetVersion: string | null;
}

export function assessmentSubmission(inputs: AssessmentInputs, preview: AssessmentVersions) {
  return { inputs, version: preview.riskVersion, scaleVersionId: preview.scaleVersionId, assetVersion: preview.assetVersion };
}

export function validSplit(processIds: readonly string[], primaryProcessId: string): boolean {
  return processIds.length >= 2 && new Set(processIds).size === processIds.length &&
    processIds.includes(primaryProcessId);
}
