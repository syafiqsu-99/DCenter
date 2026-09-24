<template>
  <PrintSheet title="Electrode Baking Record" :subtitle="subtitle" :loading="loading" :error="error"
              :signatures="['Person In Charge', 'Checked by']">
    <table class="print-table">
      <thead>
        <tr>
          <th>Baking No</th>
          <th>Baking Date</th>
          <th>Person In Charge</th>
          <th>Classification</th>
          <th>Brand</th>
          <th>Lot / Heat No.</th>
          <th class="num">Qty (KG)</th>
          <th>Start Time</th>
          <th>Stop Time</th>
          <th>Re-Baking Start</th>
          <th>Re-Baking Stop</th>
          <th>Status</th>
          <th>Remarks</th>
        </tr>
      </thead>
      <tbody>
        <tr v-if="!loading && !rows.length"><td colspan="13">No baking records for this selection.</td></tr>
        <tr v-for="r in rows" :key="r.id">
          <td>{{ r.bakingNo }}</td>
          <td>{{ fmtDate(r.bakingDate) }}</td>
          <td>{{ r.personInCharge }}</td>
          <td>{{ r.diaSpec }}</td>
          <td>{{ r.brand }}</td>
          <td>{{ r.lotNumber }}</td>
          <td class="num">{{ kg(r.quantityKg) }}</td>
          <td>{{ fmtDateTime(r.bakeStart) }}</td>
          <td>{{ fmtDateTime(r.bakeStop) }}</td>
          <td>{{ fmtDateTime(r.rebakeStart) }}</td>
          <td>{{ fmtDateTime(r.rebakeStop) }}</td>
          <td>{{ BAKING_LABELS[r.status] }}</td>
          <td>{{ r.remarks }}</td>
        </tr>
      </tbody>
    </table>
  </PrintSheet>
</template>

<script setup>
  import { computed, onMounted, ref } from 'vue'
  import { useConsumableStore } from '@/store/consumableStore'
  import { BAKING_LABELS, errorText, fmtDate, fmtDateTime, kg } from '@/utils/consumables'
  import { loadAllPages, rangeText } from '@/components/consumables/print/printData'
  import PrintSheet from '@/components/consumables/print/PrintSheet.vue'

  const props = defineProps({ query: { type: Object, default: () => ({}) } })

  const store = useConsumableStore()
  const rows = ref([])
  const loading = ref(true)
  const error = ref('')

  const subtitle = computed(() => {
    const parts = [rangeText(props.query.from, props.query.to)]
    if (props.query.status) parts.push(`Status: ${BAKING_LABELS[props.query.status] ?? props.query.status}`)
    if (props.query.q) parts.push(`Search: ${props.query.q}`)
    return parts.join(' · ')
  })

  onMounted(async () => {
    try {
      const items = await loadAllPages((skip, take) => store.loadBakingRecords({ ...props.query, skip, take }))
      rows.value = items.sort((a, b) => a.id - b.id)
    } catch (e) {
      error.value = errorText(e, 'Could not load baking records.')
    } finally {
      loading.value = false
    }
  })
</script>
