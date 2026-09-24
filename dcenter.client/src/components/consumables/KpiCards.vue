<template>
  <v-row>
    <v-col v-for="c in cards" :key="c.label" cols="12" sm="6" md="4" xl="2">
      <v-card border flat class="h-100" :color="c.color" :variant="c.color ? 'tonal' : undefined" @click="c.go">
        <v-card-text class="d-flex align-center ga-3">
          <v-icon :icon="c.icon" size="32" :color="c.iconColor" />
          <div>
            <div class="text-caption text-medium-emphasis text-uppercase">{{ c.label }}</div>
            <div class="text-h6 font-weight-bold">
              <v-skeleton-loader v-if="!kpis" type="text" width="100" />
              <template v-else>{{ c.value }}</template>
            </div>
            <div v-if="kpis && c.sub" class="text-caption text-medium-emphasis">{{ c.sub }}</div>
          </div>
        </v-card-text>
      </v-card>
    </v-col>
  </v-row>
</template>

<script setup>
  import { computed } from 'vue'
  import { useRouter } from 'vue-router'
  import { useConsumableStore } from '@/store/consumableStore'
  import { kg } from '@/utils/consumables'

  const store = useConsumableStore()
  const router = useRouter()
  const kpis = computed(() => store.dashboard?.kpis ?? null)

  function toHistory(type) {
    store.showHistory({ type, from: kpis.value.monthStart, to: kpis.value.today, category: store.dashboardCategory })
    router.push({ name: 'consumable-history' })
  }

  function toInventory(patch) {
    store.showInventory({ category: store.dashboardCategory, ...patch })
    router.push({ name: 'consumable-inventory' })
  }

  const cards = computed(() => {
    const k = kpis.value
    const month = k?.monthLabel ?? ''
    return [
      {
        label: 'Total inventory', value: `${kg(k?.totalKg)} kg`, sub: `incl. ${kg(k?.bakingKg)} kg in baking`,
        icon: 'mdi-warehouse', iconColor: 'primary', go: () => toInventory({}),
      },
      {
        label: 'Normal storage', value: `${kg(k?.normalKg)} kg`, sub: 'Main store',
        icon: 'mdi-package-variant-closed', iconColor: 'blue-grey', go: () => toInventory({}),
      },
      {
        label: 'Activated storage', value: `${kg(k?.activatedKg)} kg`, sub: `${k?.refillCount ?? 0} rack(s) below minimum`,
        icon: 'mdi-package-variant', iconColor: 'info', color: k?.refillCount ? 'info' : undefined,
        go: () => router.push({ name: 'consumable-transfer' }),
      },
      {
        label: 'Holding ovens', value: `${kg(k?.inHoldingKg)} kg`,
        sub: `${k?.occupiedCompartments ?? 0} of ${k?.totalCompartments ?? 0} compartments in use`,
        icon: 'mdi-view-grid-outline', iconColor: 'indigo', go: () => router.push({ name: 'consumable-ovens' }),
      },
      {
        label: 'Low stock items', value: k?.lowStockCount ?? 0, sub: 'At or below minimum — reorder',
        icon: 'mdi-alert', iconColor: 'warning', color: k?.lowStockCount ? 'warning' : undefined,
        go: () => toInventory({ lowOnly: true }),
      },
      {
        label: `Used ${month}`, value: `${kg(k?.netConsumedThisMonthKg)} kg`, sub: `Received ${kg(k?.receivedThisMonthKg)} kg this month`,
        icon: 'mdi-fire', iconColor: 'deep-orange', go: () => toHistory('Issue'),
      },
    ]
  })
</script>
