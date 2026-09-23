<template>
  <v-dialog :model-value="modelValue" max-width="820" @update:model-value="emit('update:modelValue', $event)">
    <v-card v-if="row" prepend-icon="mdi-history">
      <template #title>{{ row.diaSpec }} · Lot {{ row.lotNumber }}</template>
      <template #subtitle>{{ row.brand }} · {{ row.consumableType }} · {{ row.source }}</template>
      <v-divider />
      <v-alert v-if="error" type="error" variant="tonal" density="compact" class="ma-4">{{ error }}</v-alert>
      <v-data-table-virtual v-else :headers="headers" :items="items" :loading="loading" item-value="id" class="consumable-table"
                            density="compact" fixed-header height="420" no-data-text="No transactions.">
        <template #loading>
          <v-skeleton-loader type="table-row@5" />
        </template>
        <template #item.txnDate="{ item }">{{ fmtDate(item.txnDate) }}</template>
        <template #item.txnType="{ item }"><TxnTypeChip :type="item.txnType" :voided="item.isVoided" /></template>
        <template #item.quantityKg="{ item }">
          <span :class="{ 'text-disabled text-decoration-line-through': item.isVoided }">
            {{ item.quantityKg > 0 ? '+' : '' }}{{ kg(item.quantityKg) }}
          </span>
        </template>
        <template #item.requestor="{ item }">{{ item.requestor || '—' }}</template>
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
  import { COLUMN, errorText, fmtDate, kg } from '@/utils/consumables'
  import TxnTypeChip from '@/components/consumables/TxnTypeChip.vue'

  const props = defineProps({
    modelValue: { type: Boolean, default: false },
    row: { type: Object, default: null },
  })
  const emit = defineEmits(['update:modelValue'])

  const store = useConsumableStore()
  const items = ref([])
  const loading = ref(false)
  const error = ref('')

  const headers = [
    { title: COLUMN.date, key: 'txnDate', width: '13%' },
    { title: 'Type', key: 'txnType', width: '13%' },
    { title: 'KG', key: 'quantityKg', align: 'end', width: '11%' },
    { title: COLUMN.requestor, key: 'requestor', width: '20%' },
    { title: 'Reference', key: 'referenceNo', width: '13%' },
    { title: 'Remarks', key: 'remarks', width: '30%' },
  ]

  watch(() => props.modelValue, async (open) => {
    if (!open || !props.row) return
    loading.value = true
    error.value = ''
    items.value = []
    try {
      const page = await store.loadTransactions({ lotId: props.row.lotId, location: props.row.source, take: 200 })
      items.value = page.items
    } catch (e) {
      error.value = errorText(e, 'Could not load the lot history.')
    } finally {
      loading.value = false
    }
  })
</script>
