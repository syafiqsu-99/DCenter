<template>
  <v-card ref="card" border flat class="d-flex flex-column" :style="{ height }">
    <v-card-item>
      <template #prepend><v-avatar color="info" variant="tonal" icon="mdi-tray-arrow-up" /></template>
      <v-card-title>Stock Out</v-card-title>
      <v-card-subtitle>Issue consumables from a lot to a requestor</v-card-subtitle>
    </v-card-item>
    <v-divider />

    <v-card-text class="flex-grow-1 overflow-y-auto">
      <div class="text-overline text-medium-emphasis">Issue details</div>
      <v-row dense align="start">
        <v-col cols="12" sm="6" md="3">
          <v-text-field v-model="form.txnDate" type="date" :label="COLUMN.date" :max="maxDate" v-bind="field" />
        </v-col>
        <v-col cols="12" sm="6" md="4">
          <LocationToggle v-model="form.location" :label="COLUMN.source" block density="default" />
        </v-col>
        <v-col cols="12" md="5">
          <RequestorField v-model="form.requestor" :label="COLUMN.requestor" required />
        </v-col>
      </v-row>

      <div class="text-overline text-medium-emphasis mt-4">Stock</div>
      <StockLotPicker ref="picker" v-model="lotId" :stock="store.stock" :location="form.location"
                      :loading="store.loadingStock" />
      <v-row dense class="mt-3" align="stretch">
        <v-col cols="12" md="8">
          <DetailGrid v-if="lot" :items="lotDetails" md="4" class="h-100" />
          <v-sheet v-else color="grey-lighten-4" rounded class="pa-3 h-100 d-flex align-center text-body-2 text-medium-emphasis">
            Search and select a lot above. The oldest lot of each consumable is marked “Use first”.
          </v-sheet>
        </v-col>
        <v-col cols="12" md="4">
          <AvailableBalancePanel :available="available" :quantity="qty" />
        </v-col>
      </v-row>

      <div class="text-overline text-medium-emphasis mt-4">Quantity</div>
      <v-row dense align="start">
        <v-col cols="12" sm="6" md="4">
          <v-text-field ref="qtyField" v-model.number="form.quantityKg" type="number" min="0.01" step="0.01"
                        :max="available ?? undefined" :label="COLUMN.take" suffix="kg" v-bind="field"
                        :disabled="!lot" :rules="[qtyRule]" @keydown.enter.prevent="save(false)" />
          <v-chip v-if="lot" size="small" variant="tonal" class="mt-1" @click="form.quantityKg = available">
            Issue all ({{ kg(available) }} kg)
          </v-chip>
        </v-col>
        <v-col cols="12" sm="6" md="8">
          <v-text-field v-model="form.remarks" label="Remarks" v-bind="field" />
        </v-col>
      </v-row>

      <v-alert v-if="error" type="error" variant="tonal" density="compact" class="mt-4">{{ error }}</v-alert>
    </v-card-text>

    <v-divider />
    <v-card-actions class="px-4 py-3">
      <span class="text-body-2 text-medium-emphasis">{{ summary }}</span>
      <v-spacer />
      <v-btn variant="text" :disabled="!canSave" :loading="saving" @click="save(true)">Issue &amp; close</v-btn>
      <v-btn color="primary" variant="flat" prepend-icon="mdi-content-save" :disabled="!canSave" :loading="saving"
             @click="save(false)">
        Issue &amp; next
      </v-btn>
    </v-card-actions>
  </v-card>

  <v-snackbar v-model="snackbar" color="success" timeout="4000">{{ snackbarText }}</v-snackbar>
</template>

<script setup>
  import { computed, nextTick, onMounted, reactive, ref, watch } from 'vue'
  import { useRoute, useRouter } from 'vue-router'
  import { useConsumableStore } from '@/store/consumableStore'
  import { useFillHeight } from '@/composables/useFillHeight'
  import { COLUMN, errorText, fmtDate, kg, todayIso } from '@/utils/consumables'
  import LocationToggle from '@/components/consumables/LocationToggle.vue'
  import RequestorField from '@/components/consumables/RequestorField.vue'
  import StockLotPicker from '@/components/consumables/StockLotPicker.vue'
  import AvailableBalancePanel from '@/components/consumables/AvailableBalancePanel.vue'
  import DetailGrid from '@/components/consumables/DetailGrid.vue'

  const store = useConsumableStore()
  const route = useRoute()
  const router = useRouter()
  const card = ref(null)
  const { height } = useFillHeight(card)
  const field = { variant: 'outlined', density: 'comfortable', hideDetails: 'auto' }
  const maxDate = todayIso()

  const presetLocation = store.catalog.locations.includes(route.query.location) ? route.query.location : null
  const presetLotId = Number(route.query.lotId) || null

  const form = reactive({
    txnDate: todayIso(),
    location: presetLocation ?? store.header.issueLocation,
    requestor: store.header.requestor,
    quantityKg: null,
    remarks: '',
  })

  const picker = ref(null)
  const qtyField = ref(null)
  const lotId = ref(null)
  const saving = ref(false)
  const error = ref('')
  const snackbar = ref(false)
  const snackbarText = ref('')

  const text = (v) => (v ?? '').toString().trim()
  const lot = computed(() => store.stock.find((s) => s.lotId === lotId.value) ?? null)
  const available = computed(() => (lot.value ? lot.value.balanceKg : null))
  const qty = computed(() => Number(form.quantityKg) || 0)
  const qtyRule = (v) => !v || available.value === null || Number(v) <= available.value
    || `Cannot exceed ${kg(available.value)} kg`
  const canSave = computed(() =>
    !!lot.value && !!text(form.requestor) && qty.value > 0 && qty.value <= (available.value ?? 0)
    && !!form.txnDate && form.txnDate <= maxDate && !saving.value)

  const lotDetails = computed(() => {
    const l = lot.value
    return [
      { label: COLUMN.type, value: l.consumableType },
      { label: COLUMN.brand, value: l.manufacturer },
      { label: COLUMN.diaSpec, value: l.diaSpec },
      { label: COLUMN.lot, value: l.lotNumber },
      { label: `First received`, value: fmtDate(l.firstReceivedOn) },
      { label: COLUMN.source, value: l.location },
    ]
  })

  const summary = computed(() => {
    if (!lot.value) return 'Select a lot to issue from.'
    if (!text(form.requestor)) return 'Enter the requestor.'
    if (!(qty.value > 0)) return 'Enter the Take/KG quantity.'
    if (qty.value > available.value) return `Only ${kg(available.value)} kg available.`
    return `Takes ${kg(qty.value)} kg for ${text(form.requestor)}, leaving ${kg(available.value - qty.value)} kg.`
  })

  async function refreshStock(keepLotId = null) {
    try {
      await store.loadStock(form.location)
    } catch {
      error.value = 'Could not load stock for this location.'
      return
    }
    lotId.value = keepLotId && store.stock.some((s) => s.lotId === keepLotId) ? keepLotId : null
  }

  watch(() => form.location, () => {
    form.quantityKg = null
    refreshStock()
  })

  watch(lotId, (id) => {
    if (!id) form.quantityKg = null
    else nextTick(() => qtyField.value?.focus())
  })

  async function save(close) {
    if (!canSave.value) return
    saving.value = true
    error.value = ''
    const selectedLotId = lotId.value
    try {
      const result = await store.issue({
        txnDate: form.txnDate,
        location: form.location,
        requestor: text(form.requestor),
        lotId: selectedLotId,
        quantityKg: qty.value,
        remarks: text(form.remarks) || null,
      })
      store.rememberHeader({ issueLocation: form.location, requestor: text(form.requestor) })
      const t = result.transaction
      snackbarText.value = `Issued ${kg(Math.abs(t.quantityKg))} kg of ${t.diaSpec} (lot ${t.lotNumber}) to ${t.requestor}. Balance ${kg(result.balanceKg)} kg.`
      snackbar.value = true
      if (close) {
        router.push({ name: 'consumable-inventory' })
        return
      }
      lotId.value = null
      form.remarks = ''
      nextTick(() => picker.value?.focus())
    } catch (e) {
      error.value = errorText(e, 'Could not save this stock out.')
      if (e?.response?.status === 409) await refreshStock(selectedLotId)
    } finally {
      saving.value = false
    }
  }

  onMounted(() => refreshStock(presetLotId))
</script>
