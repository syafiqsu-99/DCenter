<template>
  <v-alert v-if="error" type="error" variant="tonal" density="compact">{{ error }}</v-alert>
  <v-table v-else density="compact" class="consumable-table bg-transparent">
    <thead>
      <tr>
        <th>{{ COLUMN.brand }}</th>
        <th>{{ COLUMN.lot }}</th>
        <th>First received</th>
        <th>{{ COLUMN.source }}</th>
        <th class="text-end">{{ COLUMN.normal }}</th>
        <th class="text-end">{{ COLUMN.baking }}</th>
        <th class="text-end">{{ COLUMN.activated }}</th>
        <th class="text-end">{{ COLUMN.total }}</th>
        <th class="text-end" />
      </tr>
    </thead>
    <tbody>
      <tr v-if="loading"><td colspan="9"><v-progress-linear indeterminate /></td></tr>
      <tr v-else-if="!lots.length"><td colspan="9" class="text-medium-emphasis">No lots with stock.</td></tr>
      <tr v-for="l in lots" :key="l.lotId">
        <td>{{ l.brand }}</td>
        <td>{{ l.lotNumber }}</td>
        <td>{{ fmtDate(l.firstReceivedOn) || '—' }}</td>
        <td>{{ l.source || '—' }}</td>
        <td class="text-end">{{ kg(l.normalKg) }}</td>
        <td class="text-end">{{ l.bakingKg ? kg(l.bakingKg) : '—' }}</td>
        <td class="text-end">
          {{ kg(l.activatedKg) }}
          <div v-if="l.locations?.length" class="d-flex flex-wrap justify-end ga-1 mt-1">
            <v-chip v-for="loc in l.locations" :key="loc.compartmentId ?? 'none'" size="x-small" label variant="tonal"
                    :color="loc.compartmentId ? 'indigo' : 'warning'">{{ loc.label }} · {{ kg(loc.kg) }}</v-chip>
          </div>
        </td>
        <td class="text-end font-weight-bold">{{ kg(l.totalKg) }}</td>
        <td class="text-end text-no-wrap">
          <v-btn icon="mdi-history" size="x-small" variant="text" :aria-label="`History of lot ${l.lotNumber}`"
                 @click="openHistory(l)" />
          <v-btn v-if="!isElectrode" size="x-small" variant="text" color="deep-orange" :disabled="l.activatedKg <= 0" @click="openFinish(l)">
            Finished
          </v-btn>
          <v-btn v-if="!isElectrode" size="x-small" variant="text" color="warning" @click="openAdjust(l)">Adjust</v-btn>
        </td>
      </tr>
    </tbody>
  </v-table>

  <LotHistoryDialog v-model="historyOpen" :lot="historyLot" />
  <FinishDialog v-model="finishOpen" :item="item" :lot="selected" @saved="onChanged" />
  <AdjustDialog v-model="adjustOpen" :item="item" :lot="selected" @saved="onChanged" />
</template>

<script setup>
  import { computed, onMounted, ref } from 'vue'
  import { useConsumableStore } from '@/store/consumableStore'
  import { COLUMN, ELECTRODE, errorText, fmtDate, kg } from '@/utils/consumables'
  import LotHistoryDialog from '@/components/consumables/LotHistoryDialog.vue'
  import FinishDialog from '@/components/consumables/FinishDialog.vue'
  import AdjustDialog from '@/components/consumables/AdjustDialog.vue'

  const props = defineProps({ item: { type: Object, required: true } })
  const emit = defineEmits(['changed'])

  const store = useConsumableStore()
  const lots = ref([])
  const loading = ref(false)
  const error = ref('')
  const selected = ref(null)
  const historyLot = ref(null)
  const historyOpen = ref(false)

  const isElectrode = computed(() => props.item.category === ELECTRODE)
  const finishOpen = ref(false)
  const adjustOpen = ref(false)

  async function load() {
    loading.value = true
    error.value = ''
    try {
      lots.value = await store.lotBalances(props.item.itemId)
    } catch (e) {
      error.value = errorText(e, 'Could not load the lots.')
    } finally {
      loading.value = false
    }
  }

  function openHistory(l) {
    historyLot.value = { ...l, diaSpec: props.item.diaSpec }
    historyOpen.value = true
  }

  function openFinish(l) {
    selected.value = l
    finishOpen.value = true
  }

  function openAdjust(l) {
    selected.value = l
    adjustOpen.value = true
  }

  function onChanged() {
    emit('changed')
  }

  onMounted(load)
</script>
