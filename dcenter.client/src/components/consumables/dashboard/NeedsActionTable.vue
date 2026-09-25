<template>
  <v-card border flat>
    <v-card-title class="text-subtitle-1 d-flex align-center flex-wrap ga-2">
      <v-icon color="warning" class="me-1">mdi-bell-alert-outline</v-icon>
      Needs action
      <span class="text-caption text-medium-emphasis">Reorder = total at or below minimum · Refill = Activated below its minimum</span>
      <v-spacer />
      <v-chip size="small" variant="tonal" color="warning">{{ reorderCount }} to reorder</v-chip>
      <v-chip size="small" variant="tonal" color="info">{{ refillCount }} to refill</v-chip>
    </v-card-title>
    <v-data-table-virtual :headers="headers" :items="rows" item-value="itemId" class="consumable-table" density="compact"
                          fixed-header height="320" :loading="!store.dashboard"
                          no-data-text="Nothing to reorder or refill — every consumable is above its minimum.">
      <template #loading><v-skeleton-loader type="table-row@6" /></template>
      <template #item.diaSpec="{ item }">
        <strong>{{ item.diaSpec }}</strong>
        <div class="text-caption text-medium-emphasis">{{ item.category }}</div>
      </template>
      <template #item.action="{ item }">
        <v-chip v-if="item.isLow" size="x-small" color="warning" variant="flat" class="me-1">Reorder</v-chip>
        <v-chip v-if="item.needsRefill" size="x-small" color="info" variant="flat">Refill Activated</v-chip>
      </template>
      <template #item.totalKg="{ item }">
        <span :class="{ 'text-warning font-weight-bold': item.isLow }">{{ kg(item.totalKg) }}</span>
        <div v-if="item.minStockKg > 0" class="text-caption text-medium-emphasis">min {{ kg(item.minStockKg) }}</div>
      </template>
      <template #item.shortfall="{ item }">{{ item.isLow ? kg(item.shortfall) : '—' }}</template>
      <template #item.activatedKg="{ item }">
        <span :class="{ 'text-info font-weight-bold': item.needsRefill }">{{ kg(item.activatedKg) }}</span>
        <div v-if="item.activatedMinKg > 0" class="text-caption text-medium-emphasis">min {{ kg(item.activatedMinKg) }}</div>
      </template>
      <template #item.normalKg="{ item }">
        <template v-if="item.needsRefill">
          <span v-if="item.normalKg > 0">{{ kg(item.normalKg) }}</span>
          <span v-else class="text-error text-caption">Normal empty</span>
        </template>
        <span v-else>{{ kg(item.normalKg) }}</span>
      </template>
      <template #item.lastIssuedOn="{ item }">{{ fmtDate(item.lastIssuedOn) || '—' }}</template>
      <template #item.go="{ item }">
        <v-btn size="small" variant="tonal" color="primary" append-icon="mdi-arrow-right" @click="open(item)">
          {{ item.needsRefill && item.normalKg > 0 ? 'Activate' : 'Open' }}
        </v-btn>
      </template>
    </v-data-table-virtual>
  </v-card>
</template>

<script setup>
  import '@/components/consumables/shared/consumableTables.css'
  import { computed } from 'vue'
  import { useRouter } from 'vue-router'
  import { useConsumableStore } from '@/store/consumableStore'
  import { ELECTRODE, fmtDate, kg } from '@/utils/consumables'

  const store = useConsumableStore()
  const router = useRouter()

  const rows = computed(() =>
    (store.dashboard?.balances ?? [])
      .map((r) => ({ ...r, needsRefill: r.needsRefill && r.category !== ELECTRODE, shortfall: Math.max(r.minStockKg - r.totalKg, 0) }))
      .filter((r) => r.isLow || r.needsRefill)
      .sort((a, b) => Number(b.isLow) - Number(a.isLow) || b.shortfall - a.shortfall || a.diaSpec.localeCompare(b.diaSpec)))

  const reorderCount = computed(() => rows.value.filter((r) => r.isLow).length)
  const refillCount = computed(() => rows.value.filter((r) => r.needsRefill).length)

  const headers = [
    { title: 'Consumable', key: 'diaSpec', width: '22%' },
    { title: 'Action', key: 'action', sortable: false, width: '16%' },
    { title: 'Total (kg)', key: 'totalKg', align: 'end', width: '11%' },
    { title: 'Shortfall', key: 'shortfall', align: 'end', width: '10%' },
    { title: 'Activated (kg)', key: 'activatedKg', align: 'end', width: '11%' },
    { title: 'In Normal', key: 'normalKg', align: 'end', width: '10%' },
    { title: 'Last pickup', key: 'lastIssuedOn', width: '10%' },
    { title: '', key: 'go', sortable: false, align: 'end', width: '10%' },
  ]

  function open(row) {
    store.showInventory({ search: row.diaSpec })
    router.push({ name: 'consumable-stock' })
  }
</script>
