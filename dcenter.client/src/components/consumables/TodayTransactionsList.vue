<template>
  <v-card ref="card" border flat class="d-flex flex-column" :style="{ height }">
    <v-card-title class="d-flex align-center text-subtitle-1">
      {{ title }}
      <v-chip size="small" variant="tonal" class="ms-2">{{ activeItems.length }}</v-chip>
      <v-spacer />
      <v-tooltip location="top" text="Refresh list">
        <template #activator="{ props: p }">
          <v-btn v-bind="p" icon="mdi-refresh" variant="text" size="small" aria-label="Refresh list"
                 :loading="store.loadingToday[type]" @click="store.loadToday(type)" />
        </template>
      </v-tooltip>
    </v-card-title>
    <v-card-subtitle class="pb-2">Total {{ kg(totalKg) }} kg entered today</v-card-subtitle>
    <v-divider />
    <div class="flex-grow-1 overflow-y-auto">
      <div v-if="!items.length" class="pa-6 text-center text-medium-emphasis text-body-2">
        <v-icon size="40" class="mb-2">mdi-clipboard-text-outline</v-icon>
        <div>Nothing entered today yet.</div>
      </div>
      <v-list v-else density="compact" lines="two" class="py-0">
        <template v-for="(t, i) in items" :key="t.id">
          <v-divider v-if="i" />
          <v-list-item :class="{ 'text-disabled': t.isVoided }">
            <v-list-item-title>
              <span :class="{ 'text-decoration-line-through': t.isVoided }">
                <strong>{{ kg(Math.abs(t.quantityKg)) }} kg</strong> · {{ t.diaSpec }}
              </span>
            </v-list-item-title>
            <v-list-item-subtitle>
              {{ fmtTime(t.createdAt) }} · Lot {{ t.lotNumber }} · {{ t.location }}<span v-if="t.requestor"> · {{ t.requestor }}</span>
            </v-list-item-subtitle>
            <template #append>
              <v-chip v-if="t.isVoided" size="x-small" color="error" variant="tonal">Voided</v-chip>
              <v-tooltip v-else location="top" text="Cancel this entry (kept in history with your reason)">
                <template #activator="{ props: p }">
                  <v-btn v-bind="p" size="small" variant="text" color="error" @click="askVoid(t)">Void</v-btn>
                </template>
              </v-tooltip>
            </template>
          </v-list-item>
        </template>
      </v-list>
    </div>
  </v-card>

  <VoidDialog v-model="voidOpen" :transaction="voidTarget" @voided="onVoided" />
  <v-snackbar v-model="snackbar" color="success" timeout="3000">{{ snackbarText }}</v-snackbar>
</template>

<script setup>
  import { computed, onMounted, ref } from 'vue'
  import { useConsumableStore } from '@/store/consumableStore'
  import { useFillHeight } from '@/composables/useFillHeight'
  import { fmtTime, kg } from '@/utils/consumables'
  import VoidDialog from '@/components/consumables/VoidDialog.vue'

  const props = defineProps({
    type: { type: String, required: true },
    title: { type: String, required: true },
  })

  const store = useConsumableStore()
  const card = ref(null)
  const { height } = useFillHeight(card)
  const items = computed(() => store.today[props.type] ?? [])
  const activeItems = computed(() => items.value.filter((t) => !t.isVoided))
  const totalKg = computed(() => activeItems.value.reduce((sum, t) => sum + Math.abs(t.quantityKg), 0))
  const voidOpen = ref(false)
  const voidTarget = ref(null)
  const snackbar = ref(false)
  const snackbarText = ref('')

  function askVoid(t) {
    voidTarget.value = t
    voidOpen.value = true
  }

  function onVoided(result) {
    snackbarText.value = `Entry voided. Lot balance is now ${kg(result.balanceKg)} kg.`
    snackbar.value = true
  }

  onMounted(() => store.loadToday(props.type).catch(() => {}))
</script>
