<template>
  <section class="modules" aria-label="Modules">
    <ModuleCard title="Weld Shop Job Report" icon="mdi-file-document-edit-outline" color="#1565C0" :index="0"
                description="Find a work order, fill in joints, materials and welders, then export to Excel or PDF."
                :loading="reportStore.loadingSaved && !reportStore.savedReports.length" @open="go('/report')">
      <v-chip size="small" variant="outlined" prepend-icon="mdi-pencil-outline">{{ drafts }} draft(s)</v-chip>
      <v-chip size="small" variant="outlined" prepend-icon="mdi-check-circle-outline">{{ completed }} completed</v-chip>
    </ModuleCard>

    <ModuleCard title="Welding Consumables" icon="mdi-package-variant-closed" color="#EF6C00" :index="1"
                :description="store.isSupervisor
                  ? 'Dashboard, receiving, stock, baking & holding, audit and master data.'
                  : 'Welder view: take and return consumables, bake electrodes and use the holding ovens.'"
                :loading="store.isSupervisor && loadingDashboard" @open="go(store.isSupervisor ? '/consumables/dashboard' : '/consumables/station')">
      <template v-if="store.isSupervisor && kpis">
        <v-chip size="small" variant="outlined" prepend-icon="mdi-alert">{{ kpis.lowStockCount }} low stock</v-chip>
        <v-chip size="small" variant="outlined" prepend-icon="mdi-view-grid-outline">
          {{ kpis.occupiedCompartments }}/{{ kpis.totalCompartments }} compartments
        </v-chip>
      </template>
      <v-chip v-else size="small" variant="outlined" prepend-icon="mdi-account-hard-hat">Open to welders</v-chip>
    </ModuleCard>

    <ModuleCard title="Settings" icon="mdi-cog-outline" color="#455A64" :index="2" :locked="!store.isSupervisor"
                description="Welders, WPS, materials, dropdown lists, MRN links and the supervisor password."
                @open="go('/settings')">
      <v-chip size="small" variant="outlined" :prepend-icon="store.isSupervisor ? 'mdi-shield-check' : 'mdi-lock'">
        {{ store.isSupervisor ? 'Supervisor access' : 'Supervisor only' }}
      </v-chip>
    </ModuleCard>
  </section>
</template>

<script setup>
  import { computed, onMounted, ref, watch } from 'vue'
  import { useRouter } from 'vue-router'
  import { useConsumableStore } from '@/store/consumableStore'
  import { useReportStore } from '@/store/reportStore'
  import ModuleCard from '@/components/home/ModuleCard.vue'

  const router = useRouter()
  const store = useConsumableStore()
  const reportStore = useReportStore()
  const loadingDashboard = ref(false)

  const drafts = computed(() => reportStore.savedReports.filter((r) => r.status !== 'Completed').length)
  const completed = computed(() => reportStore.savedReports.filter((r) => r.status === 'Completed').length)
  const kpis = computed(() => store.dashboard?.kpis ?? null)

  function go(path) {
    router.push(path)
  }

  async function loadDashboard() {
    if (!store.isSupervisor || store.dashboard) return
    loadingDashboard.value = true
    try {
      await store.loadCatalog()
      await store.loadDashboard()
    } catch {
      // the card still opens; the summary chips just stay hidden
    } finally {
      loadingDashboard.value = false
    }
  }

  watch(() => store.isSupervisor, loadDashboard)
  onMounted(() => {
    loadDashboard()
    if (!reportStore.savedReports.length) reportStore.loadSavedReports().catch(() => {})
  })
</script>

<style scoped>
  .modules {
    display: grid;
    grid-template-columns: repeat(3, minmax(0, 1fr));
    gap: 20px;
  }

  @media (max-width: 960px) {
    .modules {
      grid-template-columns: minmax(0, 1fr);
    }
  }
</style>
