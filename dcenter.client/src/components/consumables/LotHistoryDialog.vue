<template>
  <v-dialog :model-value="modelValue" max-width="960" @update:model-value="emit('update:modelValue', $event)">
    <v-card v-if="lot" prepend-icon="mdi-history">
      <template #title>{{ lot.diaSpec }} · Lot {{ lot.lotNumber }}</template>
      <template #subtitle>{{ lot.brand }}</template>
      <v-divider />
      <v-alert v-if="error" type="error" variant="tonal" density="compact" class="ma-4">{{ error }}</v-alert>
      <v-data-table-virtual v-else :headers="headers" :items="items" :loading="loading" item-value="id"
                            class="consumable-table" density="compact" fixed-header height="440"
                            no-data-text="No transactions.">
        <template #loading><v-skeleton-loader type="table-row@5" /></template>
        <template #item.txnDate="{ item }">{{ fmtDate(item.txnDate) }}</template>
        <template #item.txnType="{ item }"><TxnTypeChip :type="item.txnType" :voided="item.isVoided" /></template>
        <template #item.flow="{ item }">{{ stageFlow(item) }}</template>
        <template #item.quantityKg="{ item }">
          <span :class="{ 'text-disabled text-decoration-line-through': item.isVoided }">{{ kg(item.quantityKg) }}</span>
        </template>
        <template #item.who="{ item }">{{ item.welderName || item.requestor || '—' }}</template>
      </v-data-table-virtual>
      <v-divider />
      <v-card-actions>
        <v-spacer />
        <v-btn variant="text" @click="emit('update:modelValue', false)">Close</v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>

<script setup>
  import '@/components/consumables/consumableTables.css'
  import { ref, watch } from 'vue'
  import { useConsumableStore } from '@/store/consumableStore'
  import { COLUMN, errorText, fmtDate, kg, stageFlow } from '@/utils/consumables'
  import TxnTypeChip from '@/components/consumables/TxnTypeChip.vue'

  const props = defineProps({
    modelValue: { type: Boolean, default: false },
    lot: { type: Object, default: null },
  })
  const emit = defineEmits(['update:modelValue'])

  const store = useConsumableStore()
  const items = ref([])
  const loading = ref(false)
  const error = ref('')

  const headers = [
    { title: 'Txn No.', key: 'txnNo', width: '12%' },
    { title: COLUMN.date, key: 'txnDate', width: '10%' },
    { title: 'Type', key: 'txnType', width: '11%' },
    { title: 'From → To', key: 'flow', width: '16%' },
    { title: 'KG', key: 'quantityKg', align: 'end', width: '9%' },
    { title: 'Welder / Requestor', key: 'who', width: '16%' },
    { title: 'Reason', key: 'reason', width: '11%' },
    { title: 'Remarks', key: 'remarks', width: '15%' },
  ]

  watch(() => props.modelValue, async (open) => {
    if (!open || !props.lot) return
    loading.value = true
    error.value = ''
    items.value = []
    try {
      const page = await store.loadTransactions({ lotId: props.lot.lotId, take: 200 })
      items.value = page.items
    } catch (e) {
      error.value = errorText(e, 'Could not load the lot history.')
    } finally {
      loading.value = false
    }
  })
</script>
