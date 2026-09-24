<template>
  <div class="d-flex flex-column ga-3">
    <WelderIdentityBar />
    <WelderActionBar v-model="action" :disabled="!store.counterWelder" />
    <v-alert v-if="!store.counterWelder" type="info" variant="tonal" class="text-body-1">
      Choose your name above to start. Only welders set up for consumable stock are listed.
    </v-alert>
    <template v-else>
      <WelderUsePanel v-if="action === 'use' || action === 'return'" :key="action" :mode="action" />
      <template v-else-if="action === 'baking'">
        <WelderNormalStockPanel ref="normalStock" @sent="bakingKey++" />
        <BakingBoard :key="bakingKey" operator @changed="normalStock?.load()" />
      </template>
      <WelderHoldingPanel v-else />
    </template>
  </div>
</template>

<script setup>
  import { ref } from 'vue'
  import { useConsumableStore } from '@/store/consumableStore'
  import WelderIdentityBar from '@/components/consumables/welder/WelderIdentityBar.vue'
  import WelderActionBar from '@/components/consumables/welder/WelderActionBar.vue'
  import WelderUsePanel from '@/components/consumables/welder/WelderUsePanel.vue'
  import WelderHoldingPanel from '@/components/consumables/welder/WelderHoldingPanel.vue'
  import BakingBoard from '@/components/consumables/BakingBoard.vue'
  import WelderNormalStockPanel from '@/components/consumables/welder/WelderNormalStockPanel.vue'

  const store = useConsumableStore()
  const action = ref('use')
  const bakingKey = ref(0)
  const normalStock = ref(null)
</script>
