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

export const useReportStore = defineStore('report', {
  state: () => ({
    searchInput: '',
    rows: [],
    resolvedJob: '',
    rowCount: 0,
    confirmed: false,
    report: null,
    loading: false,
    saving: false,
    error: '',
  }),

  getters: {
    // Confirm button shows only when the search resolved to exactly one job number.
    canConfirm: (s) =>
      s.rows.length > 0 && new Set(s.rows.map((r) => r.jobNumber)).size === 1,
    // Autocomplete pools seeded from the job's returned rows.
    partDescOptions: (s) => [
      ...new Set(s.rows.flatMap((r) => [r.itemDesc, r.componentDesc]).filter(Boolean)),
    ],
    partNoOptions: (s) => [
      ...new Set(s.rows.flatMap((r) => [r.assemblyItem, r.childPart]).filter(Boolean)),
    ],
  },

  actions: {
    async search() {
      this.error = '';
      this.confirmed = false;
      this.report = null;
      this.loading = true;
      try {
        const { data } = await api.get('/jobs/search', {
          params: { jobNumber: this.searchInput.trim() },
        });
        this.rows = data.rows;
        this.resolvedJob = data.jobNumber;
        this.rowCount = data.rowCount;
      } catch {
        this.error = 'Search failed. Check the job number and try again.';
        this.rows = [];
      } finally {
        this.loading = false;
      }
    },

    // Load existing draft, or build one joint per returned row.
    async confirmSelection() {
      this.loading = true;
      this.error = '';
      try {
        const res = await api.get(`/reports/${encodeURIComponent(this.resolvedJob)}`);
        if (res.status === 204 || !res.data) {
          this.report = {
            id: 0,
            jobNumber: this.resolvedJob,
            reportRequired: true,
            dateWelded: null,
            workOrder: this.resolvedJob,
            partNo: '',
            description: '',
            materialSpec1: '', materialSpec2: '', materialSpec3: '',
            grade1: '', grade2: '', grade3: '',
            pNumber1: '', pNumber2: '', pNumber3: '',
            engineerSupervisor: '',
            qaInspector: '',
            joints: this.rows.map((row, i) => jointFromRow(i, row)),
          };
        } else {
          this.report = res.data;
        }
        this.confirmed = true;
      } catch {
        this.error = 'Could not load the report draft.';
      } finally {
        this.loading = false;
      }
    },

    async save() {
      this.saving = true;
      this.error = '';
      try {
        const { data } = await api.post('/reports', this.report);
        this.report = data;
        return true;
      } catch {
        this.error = 'Save failed.';
        return false;
      } finally {
        this.saving = false;
      }
    },

    reset() {
      this.$reset();
    },
  },
});
