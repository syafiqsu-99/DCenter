<template>
  <v-card border flat class="h-100">
    <v-card-title class="text-subtitle-1 d-flex align-center">
      {{ store.counterWelder ? `${store.counterWelder.welderName} — today` : 'Welder activity today' }}
    </v-card-title>
    <v-divider />
    <v-list density="compact" lines="two" class="overflow-y-auto" style="max-height:600px;">
      <v-list-item v-if="!store.counterWelder" class="text-medium-emphasis">Choose a welder to see today’s entries.</v-list-item>
      <v-list-item v-else-if="!store.welderToday.length" class="text-medium-emphasis">No pickups or returns today.</v-list-item>
      <v-list-item v-if="store.counterWelder && !store.isSupervisor && store.welderToday.length" class="text-caption text-medium-emphasis">
        Entered something wrong? Ask a supervisor to void it.
      </v-list-item>
      <v-list-item v-for="t in store.welderToday" :key="t.id" :class="{ 'text-disabled': t.isVoided }">
        <template #prepend><TxnTypeChip :type="t.txnType" :voided="t.isVoided" class="me-2" /></template>
        <v-list-item-title>
          <span :class="{ 'text-decoration-line-through': t.isVoided }">{{ kg(t.quantityKg) }} kg · {{ t.diaSpec }}</span>
        </v-list-item-title>
        <v-list-item-subtitle>{{ t.txnNo }} · Lot {{ t.lotNumber }} · {{ fmtTime(t.createdAt) }}</v-list-item-subtitle>
        <template #append>
          <v-btn v-if="!t.isVoided && store.isSupervisor" size="small" variant="text" color="error" @click="askVoid(t)">Void</v-btn>
        </template>
      </v-list-item>
    </v-list>
  </v-card>

  <VoidDialog v-model="voidOpen" :transaction="voidTarget" @voided="store.loadCounter().catch(() => {})" />
</template>

<script setup>
  import { ref } from 'vue'
  import { useConsumableStore } from '@/store/consumableStore'
  import { fmtTime, kg } from '@/utils/consumables'
  import TxnTypeChip from '@/components/consumables/shared/TxnTypeChip.vue'
  import VoidDialog from '@/components/consumables/shared/VoidDialog.vue'

  const store = useConsumableStore()
  const voidOpen = ref(false)
  const voidTarget = ref(null)

  function askVoid(t) {
    voidTarget.value = t
    voidOpen.value = true
  }
</script>
