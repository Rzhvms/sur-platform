import assert from 'node:assert/strict';
import test from 'node:test';
import { readFileSync } from 'node:fs';
const data = JSON.parse(readFileSync(new URL('../../backend/Apps/SurPlatform/Infrastructure/SeedData/BankingDemo.json', import.meta.url), 'utf8'));
const index = rows => new Map(rows.map(row => [row.id, row]));
const users = new Map(data.users.map(user => [user.profile.userId, user]));
const persons = index(data.persons), processes = index(data.processes), risks = index(data.risks), links = index(data.riskProcesses);

test('banking demo is opt-in, has unique fictional users and no administrator grants', () => {
  const config = JSON.parse(readFileSync(new URL('../../backend/Apps/SurPlatform/Api/appsettings.json', import.meta.url), 'utf8'));
  assert.equal(config.ActivateDemoDataSeed, false);
  assert.equal(config.DemoDataSeedPassword, '');
  assert.equal(new Set(data.users.map(user => user.profile.normalizedEmail)).size, data.users.length);
  for (const user of data.users) {
    assert.ok(user.profile.email.endsWith('@bank-demo.invalid'));
    assert.equal(user.profile.email, user.profile.normalizedEmail);
    assert.notEqual(user.roleCode, 'ADMIN');
  }
});
test('banking organization has eligible owners and unique responsibility slots', () => {
  assert.equal(new Set(data.persons.map(person => person.userId)).size, data.persons.length);
  for (const person of data.persons) { assert.ok(users.has(person.userId)); }
  for (const process of data.processes) {
    const owner = persons.get(process.ownerId);
    assert.equal(owner.directorateId, process.directorateId);
    assert.equal(users.get(owner.userId).roleCode, 'OWNER');
  }
  const slots = new Set();
  for (const assignment of data.responsibilities) {
    assert.equal(users.get(persons.get(assignment.personId).userId).roleCode, assignment.responsibilityType);
    assert.equal(assignment.directorateId === null, assignment.responsibilityType === 'CEO');
    const slot = assignment.responsibilityType + ':' + assignment.directorateId;
    assert.ok(!slots.has(slot)); slots.add(slot);
  }
});
test('banking risks fill six columns and distinguish missing, current and historical assessments', () => {
  for (const status of ['DRAFT', 'ACTIVE', 'IN_PROGRESS', 'AWAITING_REVIEW', 'ACCEPTED', 'CLOSED']) {
    assert.equal(data.risks.filter(risk => risk.status === status).length, 3);
  }
  for (const risk of data.risks) {
    assert.equal(data.riskProcesses.filter(link => link.riskId === risk.id && link.isPrimary).length, 1);
    assert.equal(data.assessments.filter(row => row.riskId === risk.id && row.isActive).length,
      ['ACTIVE', 'IN_PROGRESS', 'ACCEPTED'].includes(risk.status) ? 1 : 0);
    if (risk.status === 'DRAFT') { assert.equal(data.assessments.some(row => row.riskId === risk.id), false); }
    if (risk.status === 'CLOSED') { assert.ok(risk.closeReason); }
    assert.ok(index(data.hazards).has(risk.hazardId) && index(data.categories).has(risk.categoryId));
  }
  for (const link of data.riskProcesses) { assert.equal(link.ownerId, processes.get(link.processId).ownerId); }
  assert.ok(data.assessments.some(row => row.isActive && row.riskScore === 0));
});
test('banking assessment inputs reproduce initial scale scores and downtime losses', () => {
  for (const row of data.assessments) {
    const likelihood = Number(row.likelihoodLevelId.slice(-2)), impact = Number(row.impactLevelId.slice(-2));
    assert.equal(impact, data.scaleMinimums.filter(minimum => row.lossAmount >= minimum).length);
    assert.equal(row.riskScore, likelihood * impact);
    const assetId = risks.get(row.riskId).assetId;
    if (assetId) {
      const loss = Math.round(row.downtimeHours * index(data.assets).get(assetId).hourlyDowntimeCost * 100) / 100;
      assert.equal(loss, row.lossAmount);
    }
  }
});
test('banking treatment slots and executor directorates are compatible', () => {
  const occupied = new Set();
  for (const row of data.treatments) {
    assert.equal(links.get(row.riskProcessId).riskId, row.riskId);
    assert.equal(links.get(row.riskProcessId).processId, row.processId);
    if (['PENDING', 'APPROVED', 'WAITING', 'IN_PROGRESS'].includes(row.status)) {
      const slot = row.riskId + ':' + row.processId;
      assert.ok(!occupied.has(slot)); occupied.add(slot);
    }
  }
  for (const row of data.assignments) {
    const person = persons.get(row.assigneeId), treatment = index(data.treatments).get(row.treatmentId);
    assert.equal(person.directorateId, processes.get(treatment.processId).directorateId);
    assert.ok(['EMPLOYEE', 'OWNER'].includes(users.get(person.userId).roleCode));
  }
});
test('banking reviews have completed sources and inactive previous assessments', () => {
  assert.equal(data.reviews.length, 3);
  for (const row of data.reviews) {
    assert.equal(risks.get(row.riskId).status, 'AWAITING_REVIEW');
    assert.equal(row.status, 'WAITING');
    assert.equal(index(data.reviewRuns).get(row.reviewRunId).status, 'COMPLETED');
    assert.equal(index(data.assessments).get(row.previousAssessmentId).isActive, false);
  }
});
test('banking fixture uses valid unique IDs and coherent dates relative to first startup', () => {
  for (const rows of Object.values(data).filter(Array.isArray)) {
    if (!rows.length || typeof rows[0] !== 'object') { continue; }
    const ids = rows.map(row => row.id ?? row.profile.id);
    assert.equal(new Set(ids).size, ids.length);
    for (const source of rows) {
      const row = source.profile ?? source;
      assert.match(row.id, /^[a-f0-9]{8}-[a-f0-9]{4}-4[a-f0-9]{3}-8[a-f0-9]{3}-[a-f0-9]{12}$/);
      for (const key of ['createdAt', 'updatedAt', 'occurredAt', 'processedAt']) {
        if (row[key]) { assert.ok(Date.parse(row[key]) <= Date.parse(data.referenceDate), key); }
      }
      if (row.updatedAt && row.createdAt) { assert.ok(Date.parse(row.updatedAt) >= Date.parse(row.createdAt)); }
    }
  }
});
