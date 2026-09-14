import { defineStore } from 'pinia';
import api from '@/utils/api';

function blankMaterial(col) {
  return { id: 0, columnNumber: col, process: '', size: '', type: '', manuf: '', heatLot: '' };
}

function jointFromRow(index, row) {
  return {
    id: 0,
    jointNumber: index + 1,
    partDescLeft: row?.itemDesc ?? '',
    partNoLeft: row?.assemblyItem ?? '',
    heatNumberLeft: '',
    partDescRight: row?.componentDesc ?? '',
    partNoRight: row?.childPart ?? '',
    heatNumberRight: '',
    wpsNo: '',
    rev: '',
    welderName: '',
    welderNo: '',
    materials: [blankMaterial(1), blankMaterial(2), blankMaterial(3)],
  };
}

function normalizeJoints(report) {
  for (const j of report.joints ?? []) {
    const byCol = new Map((j.materials ?? []).map((m) => [m.columnNumber, m]));
    j.materials = [1, 2, 3].map((c) => byCol.get(c) ?? blankMaterial(c));
  }
  return report;
}

function newReport(jobNumber, rows) {
  const first = rows[0] ?? {};
  const today = new Date().toISOString().slice(0, 10);
  return {
    id: 0,
    jobNumber,
    reportRequired: true,
    completedAt: null,
    rowVersion: null,
    dateWelded: today,
    workOrder: jobNumber,
    partNo: first.assemblyItem ?? '',
    description: first.itemDesc ?? '',
    materialSpec1: '', materialSpec2: '', materialSpec3: '',
    grade1: '', grade2: '', grade3: '',
    pNumber1: '', pNumber2: '', pNumber3: '',
    joints: rows.slice(0, 9).map((row, i) => jointFromRow(i, row)),
  };
}

const SEARCH_DEBOUNCE_MS = 200;
let searchTimer = null;

export const useReportStore = defineStore('report', {
  state: () => ({
    searchInput: '',
    searchQuery: '',
    allRows: [],
    loadingRows: false,
    confirmed: false,
    resolvedJob: '',
    report: null,
    loading: false,
    saving: false,
    deleting: false,
    conflict: false,
    error: '',
    savedReports: [],
    loadingSaved: false,
    history: [],
    loadingHistory: false,
  }),

  getters: {
    isComplete: (s) => !!s.report?.completedAt,
    hasDateWelded: (s) => !!s.report?.dateWelded,
    filteredRows: (s) => {
      const q = (s.searchQuery ?? '').trim().toLowerCase();
      if (!q) return s.allRows;
      return s.allRows.filter((r) => (r.jobNumber ?? '').toLowerCase().includes(q));
    },
    distinctJobs() {
      return [...new Set(this.filteredRows.map((r) => r.jobNumber))];
    },
    canSelect() {
      return this.distinctJobs.length === 1;
    },
    selectedRows() {
      return this.allRows.filter((r) => r.jobNumber === this.resolvedJob);
    },
    savedByJobNumber() {
      return new Map(this.savedReports.map((r) => [r.jobNumber, r]));
    },
    partDescOptions() {
      return [
        ...new Set(this.selectedRows.flatMap((r) => [r.itemDesc, r.componentDesc]).filter(Boolean)),
      ];
    },
    partNoOptions() {
      return [
        ...new Set(this.selectedRows.flatMap((r) => [r.assemblyItem, r.childPart]).filter(Boolean)),
      ];
    },
    leftParts() {
      const seen = new Map();
      for (const r of this.selectedRows) {
        if (r.assemblyItem && !seen.has(r.assemblyItem)) {
          seen.set(r.assemblyItem, { no: r.assemblyItem, desc: r.itemDesc ?? '' });
        }
      }
      return [...seen.values()];
    },
    rightParts() {
      const seen = new Map();
      for (const r of this.selectedRows) {
        if (r.childPart && !seen.has(r.childPart)) {
          seen.set(r.childPart, { no: r.childPart, desc: r.componentDesc ?? '' });
        }
      }
      return [...seen.values()];
    },
  },

  actions: {
    setSearchInput(value) {
      this.searchInput = value;
      clearTimeout(searchTimer);
      searchTimer = setTimeout(() => {
        this.searchQuery = value;
      }, SEARCH_DEBOUNCE_MS);
    },

    async loadRows() {
      this.loadingRows = true;
      this.error = '';
      try {
        const { data } = await api.get('/jobs/top');
        this.allRows = data.map((r, i) => ({ ...r, _index: i }));
      } catch (e) {
        this.error = 'Could not load the job list.';
        this.allRows = [];
        throw e;
      } finally {
        this.loadingRows = false;
      }
    },

    async loadSavedReports() {
      this.loadingSaved = true;
      try {
        const { data } = await api.get('/reports');
        this.savedReports = data;
      } catch (e) {
        this.error = 'Could not load saved reports.';
        this.savedReports = [];
        throw e;
      } finally {
        this.loadingSaved = false;
      }
    },

    async selectJob() {
      if (!this.canSelect) return;
      this.resolvedJob = this.distinctJobs[0];
      this.loading = true;
      this.error = '';
      try {
        const res = await api.get(`/reports/${encodeURIComponent(this.resolvedJob)}`);
        this.report =
          res.status === 204 || !res.data
            ? newReport(this.resolvedJob, this.selectedRows)
            : normalizeJoints(res.data);
        this.confirmed = true;
      } catch {
        this.error = 'Could not load the report draft.';
      } finally {
        this.loading = false;
      }
    },

    async openReport(jobNumber) {
      this.resolvedJob = jobNumber;
      this.loading = true;
      this.error = '';
      try {
        const res = await api.get(`/reports/${encodeURIComponent(jobNumber)}`);
        if (res.status === 204 || !res.data) {
          this.error = 'That report no longer exists.';
          return;
        }
        this.report = normalizeJoints(res.data);
        this.confirmed = true;
      } catch {
        this.error = 'Could not open the report.';
      } finally {
        this.loading = false;
      }
    },

    async reloadReport() {
      if (!this.report?.jobNumber) return false;
      return this.openReport(this.report.jobNumber);
    },

    async save() {
      if (!this.report?.dateWelded) {
        this.error = 'Date welded is required before saving.';
        return false;
      }
      this.saving = true;
      this.error = '';
      this.conflict = false;
      try {
        const { data } = await api.post('/reports', this.report);
        this.report = normalizeJoints(data);
        await this.loadSavedReports();
        return true;
      } catch (e) {
        this.conflict = e.response?.status === 409;
        this.error = e.response?.data ?? 'Save failed.';
        return false;
      } finally {
        this.saving = false;
      }
    },

    async setComplete(complete) {
      if (!this.report?.jobNumber) return false;
      this.error = '';
      try {
        await api.post(`/reports/${encodeURIComponent(this.report.jobNumber)}/complete`, complete);
        this.report.completedAt = complete ? new Date().toISOString() : null;
        await this.loadSavedReports();
        return true;
      } catch (e) {
        this.error = e.response?.data ?? 'Could not update status.';
        return false;
      }
    },

    async loadHistory() {
      if (!this.report?.jobNumber) return;
      this.loadingHistory = true;
      try {
        const { data } = await api.get(`/reports/${encodeURIComponent(this.report.jobNumber)}/history`);
        this.history = data;
      } catch {
        this.history = [];
      } finally {
        this.loadingHistory = false;
      }
    },

    async deleteDraft() {
      if (!this.report?.jobNumber) return false;
      this.deleting = true;
      this.error = '';
      try {
        await api.delete(`/reports/${encodeURIComponent(this.report.jobNumber)}`);
        await this.loadSavedReports();
        this.backToList();
        return true;
      } catch (e) {
        this.error = e.response?.data ?? 'Could not delete the draft.';
        return false;
      } finally {
        this.deleting = false;
      }
    },

    backToList() {
      this.confirmed = false;
      this.report = null;
      this.resolvedJob = '';
      this.history = [];
      this.conflict = false;
    },

    async deleteSaved(jobNumber) {
      this.error = '';
      try {
        await api.delete(`/reports/${encodeURIComponent(jobNumber)}`);
        await this.loadSavedReports();
        return true;
      } catch (e) {
        this.error = e.response?.data ?? 'Could not delete the draft.';
        return false;
      }
    },
  },
});
