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

// Guarantee each joint carries all 3 material slots (saved drafts omit empty columns).
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

export const useReportStore = defineStore('report', {
  state: () => ({
    searchInput: '',
    allRows: [],
    loadingRows: false,
    confirmed: false,
    resolvedJob: '',
    report: null,
    loading: false,
    saving: false,
    error: '',
    savedReports: [],
    loadingSaved: false,
  }),

  getters: {
    filteredRows: (s) => {
      const q = (s.searchInput ?? '').trim().toLowerCase();
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
    async loadRows() {
      this.loadingRows = true;
      this.error = '';
      try {
        const { data } = await api.get('/jobs/top');
        this.allRows = data.map((r, i) => ({ ...r, _index: i }));
      } catch {
        this.error = 'Could not load the job list.';
        this.allRows = [];
      } finally {
        this.loadingRows = false;
      }
    },

    async loadSavedReports() {
      this.loadingSaved = true;
      try {
        const { data } = await api.get('/reports');
        this.savedReports = data;
      } catch {
        this.error = 'Could not load saved reports.';
        this.savedReports = [];
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

    async save() {
      this.saving = true;
      this.error = '';
      try {
        const { data } = await api.post('/reports', this.report);
        this.report = normalizeJoints(data);
        await this.loadSavedReports();
        return true;
      } catch {
        this.error = 'Save failed.';
        return false;
      } finally {
        this.saving = false;
      }
    },

    async setComplete(complete) {
      if (!this.report?.jobNumber) return;
      try {
        await api.post(`/reports/${encodeURIComponent(this.report.jobNumber)}/complete`, complete, {
          headers: { 'Content-Type': 'application/json' },
        });
        await this.loadSavedReports();
      } catch {
        this.error = 'Could not update status.';
      }
    },

    backToList() {
      this.confirmed = false;
      this.report = null;
      this.resolvedJob = '';
    },
  },
});
