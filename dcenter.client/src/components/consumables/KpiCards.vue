<template>
  <v-row>
    <v-col v-for="c in cards" :key="c.label" cols="12" sm="6" lg="3">
      <v-card border flat class="h-100" :color="c.color" :variant="c.color ? 'tonal' : undefined" @click="c.go">
        <v-card-text class="d-flex align-center ga-4">
          <v-icon :icon="c.icon" size="36" :color="c.iconColor" />
          <div>
            <div class="text-caption text-medium-emphasis text-uppercase">{{ c.label }}</div>
            <div class="text-h5 font-weight-bold">
              <v-skeleton-loader v-if="!kpis" type="text" width="120" />
              <template v-else>{{ c.value }}</template>
            </div>
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
    store.showHistory({ type, from: kpis.value.monthStart, to: kpis.value.today, location: store.dashboardLocation })
    router.push({ name: 'consumable-history' })
  }

  function toInventory(patch) {
    store.showInventory({ location: store.dashboardLocation, ...patch })
    router.push({ name: 'consumable-inventory' })
  }

  const cards = computed(() => {
    const k = kpis.value
    const month = k?.monthLabel ?? ''
    return [
      { label: 'Total inventory', value: `${kg(k?.totalInventoryKg)} kg`, icon: 'mdi-warehouse', iconColor: 'primary', go: () => toInventory({}) },
      { label: `Received ${month}`, value: `${kg(k?.receivedThisMonthKg)} kg`, icon: 'mdi-tray-arrow-down', iconColor: 'success', go: () => toHistory('Receive') },
      { label: `Issued ${month}`, value: `${kg(k?.issuedThisMonthKg)} kg`, icon: 'mdi-tray-arrow-up', iconColor: 'info', go: () => toHistory('Issue') },
      {
        label: 'Low stock items',
        value: k?.lowStockCount ?? 0,
        icon: 'mdi-alert',
        iconColor: 'warning',
        color: k?.lowStockCount ? 'warning' : undefined,
        go: () => toInventory({ location: 'All', lowOnly: true, view: 'consumable' }),
      },
    ]
  })
</script>
