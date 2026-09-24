<template>
  <PrintSheet :title="`Stock Count ${query.ref ?? ''}`" :subtitle="subtitle" :loading="loading" :error="error"
              :signatures="['Counted by', 'Approved by']">
    <template v-if="count">
      <table class="print-table" style="margin-bottom:10px; width:auto;">
        <tbody>
          <tr><th>Storage</th><td>{{ count.scope === 'Normal' ? 'Main store' : 'Racks & ovens' }} · {{ count.category || 'All types' }}</td></tr>
          <tr><th>Lines counted / adjusted</th><td>{{ count.linesCounted }} / {{ count.linesAdjusted }}</td></tr>
          <tr><th>Gain / loss / net (KG)</th><td>+{{ kg(count.gainKg) }} / −{{ kg(count.lossKg) }} / {{ kg(count.netKg) }}</td></tr>
          <tr><th>Adjustment Txn No.</th><td>{{ count.txnNo || '— (no differences)' }}<template v-if="count.isVoided"> · VOIDED</template></td></tr>
          <tr><th>Remarks</th><td>{{ count.remarks }}</td></tr>
        </tbody>
      </table>
      <table class="print-table">
        <thead>
          <tr>
            <th>Location</th>
            <th>Classification</th>
            <th>Brand</th>
            <th>Lot / Heat No.</th>
            <th class="num">Counted (KG)</th>
            <th class="num">Difference (KG)</th>
          </tr>
        </thead>
        <tbody>
          <tr v-if="!adjustments.length"><td colspan="6">No differences — the count matched the system.</td></tr>
          <tr v-for="a in adjustments" :key="a.id">
            <td>{{ a.toCompartment ?? a.fromCompartment ?? location(a) }}</td>
            <td>{{ a.diaSpec }}</td>
            <td>{{ a.brand }}</td>
            <td>{{ a.lotNumber }}</td>
            <td class="num">{{ kg(a.countedQtyKg) }}</td>
            <td class="num">{{ a.toStage ? `+${kg(a.quantityKg)}` : `−${kg(a.quantityKg)}` }}</td>
          </tr>
        </tbody>
      </table>
    </template>
  </PrintSheet>
</template>

<script setup>
  import { computed, onMounted, ref } from 'vue'
  import { useConsumableStore } from '@/store/consumableStore'
  import { errorText, fmtDate, fmtDateTime, kg } from '@/utils/consumables'
  import PrintSheet from '@/components/consumables/print/PrintSheet.vue'

  const props = defineProps({ query: { type: Object, default: () => ({}) } })

  const store = useConsumableStore()
  const count = ref(null)
  const adjustments = ref([])
  const loading = ref(true)
  const error = ref('')

  const subtitle = computed(() =>
    count.value ? `Count date ${fmtDate(count.value.countDate)} · posted by ${count.value.createdBy} on ${fmtDateTime(count.value.createdAt)}` : '')

  function location(a) {
    const stage = a.toStage ?? a.fromStage
    if (stage === 'Normal') return 'Main store'
    return a.category === 'Electrode Filler' ? 'Unassigned' : 'Rack'
  }

  onMounted(async () => {
    try {
      if (!props.query.ref) throw new Error('No stock count reference was given.')
      const detail = await store.loadStockCount(props.query.ref)
      count.value = detail.count
      adjustments.value = detail.adjustments
    } catch (e) {
      error.value = errorText(e, 'Could not load the stock count.')
    } finally {
      loading.value = false
    }
  })
</script>
