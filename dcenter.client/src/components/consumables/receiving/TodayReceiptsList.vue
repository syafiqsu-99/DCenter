<template>
  <v-card border flat class="fill-card">
    <v-card-title class="text-subtitle-1 d-flex align-center">
      Received today
      <v-spacer />
      <v-btn icon="mdi-refresh" variant="text" size="small" :loading="store.loadingToday" aria-label="Refresh" @click="load" />
    </v-card-title>
    <v-divider />
    <v-alert v-if="error" type="error" variant="tonal" density="compact" class="ma-3">{{ error }}</v-alert>
    <v-skeleton-loader v-else-if="store.loadingToday && !store.todayReceipts.length" type="list-item-two-line@5" />
    <v-list v-else density="compact" lines="two" class="fill overflow-y-auto">
      <v-list-item v-if="!store.todayReceipts.length" class="text-medium-emphasis">Nothing received today yet.</v-list-item>
      <v-list-item v-for="t in store.todayReceipts" :key="t.id" :class="{ 'text-disabled': t.isVoided }">
        <v-list-item-title>
          <span :class="{ 'text-decoration-line-through': t.isVoided }">{{ kg(t.quantityKg) }} kg · {{ t.diaSpec }}</span>
        </v-list-item-title>
        <v-list-item-subtitle>
          {{ t.txnNo }} · {{ t.brand }} · Lot {{ t.lotNumber }} · {{ t.source }} · {{ fmtTime(t.createdAt) }} · {{ t.createdBy }}
        </v-list-item-subtitle>
        <template #append>
          <v-btn v-if="!t.isVoided" size="small" variant="text" color="error" @click="askVoid(t)">Void</v-btn>
        </template>
      </v-list-item>
    </v-list>
  </v-card>

  <VoidDialog v-model="voidOpen" :transaction="voidTarget" @voided="load" />
</template>

<script setup>
  import { onMounted, ref } from 'vue'
  import { useConsumableStore } from '@/store/consumableStore'
  import { errorText, fmtTime, kg } from '@/utils/consumables'
  import VoidDialog from '@/components/consumables/shared/VoidDialog.vue'

  const store = useConsumableStore()
  const error = ref('')
  const voidOpen = ref(false)
  const voidTarget = ref(null)

  async function load() {
    error.value = ''
    try {
      await store.loadToday()
    } catch (e) {
      error.value = errorText(e, 'Could not load today’s receipts.')
    }
  }

  function askVoid(t) {
    voidTarget.value = t
    voidOpen.value = true
  }

  onMounted(load)
</script>
