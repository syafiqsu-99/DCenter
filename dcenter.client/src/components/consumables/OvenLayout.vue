<template>
  <div class="oven-layout" :style="gridStyle(oven.ovenType)" role="grid" :aria-label="`${oven.name} compartments`">
    <button v-for="c in oven.compartments" :key="c.id" type="button" class="oven-cell" :class="cellClass(c)"
            :style="{ gridArea: `c${c.number}` }" :disabled="isDisabled(c)" :aria-label="ariaLabel(c)" @click="emit('select', c)">
      <span class="oven-cell__number">{{ c.number }}</span>
      <template v-if="c.contents.length">
        <span class="oven-cell__spec">{{ c.contents[0].specification }}</span>
        <span class="oven-cell__meta">Ø {{ c.contents[0].diameter }} mm · {{ lotSummary(c.contents) }}</span>
        <span class="oven-cell__status">
          <v-icon size="x-small" class="me-1">{{ statusIcon(c) }}</v-icon>{{ statusText(c) }}
        </span>
      </template>
      <template v-else>
        <span class="oven-cell__empty">Empty</span>
        <span v-if="mode === 'pick' && !isDisabled(c)" class="oven-cell__status">Available</span>
      </template>
    </button>
  </div>
</template>

<script setup>
  import { elapsed, kg } from '@/utils/consumables'
  import { gridStyle, lotSummary } from '@/utils/ovenLayouts'

  const props = defineProps({
    oven: { type: Object, required: true },
    mode: { type: String, default: 'board' },
    itemId: { type: Number, default: null },
    selectedId: { type: Number, default: null },
    excludeId: { type: Number, default: null },
    isMatch: { type: Function, default: null },
  })
  const emit = defineEmits(['select'])

  const holdsOther = (c) => c.contents.some((x) => x.itemId !== props.itemId)

  function isDisabled(c) {
    if (props.mode !== 'pick') return false
    return c.id === props.excludeId || holdsOther(c)
  }

  function cellClass(c) {
    const occupied = c.contents.length > 0
    const matched = props.isMatch ? props.isMatch(c) : null
    return {
      'oven-cell--occupied': occupied,
      'oven-cell--empty': !occupied,
      'oven-cell--selected': props.mode === 'pick' && c.id === props.selectedId,
      'oven-cell--blocked': props.mode === 'pick' && holdsOther(c),
      'oven-cell--match': matched === true,
      'oven-cell--dim': matched === false,
    }
  }

  function statusText(c) {
    if (props.mode === 'pick') return holdsOther(c) ? 'Other consumable' : `Same item · ${kg(c.totalKg)} kg`
    const held = c.oldestSinceAt ? ` · ${elapsed(c.oldestSinceAt)}` : ''
    return `${kg(c.totalKg)} kg${held}`
  }

  function statusIcon(c) {
    if (props.mode === 'pick') return holdsOther(c) ? 'mdi-block-helper' : 'mdi-check'
    return 'mdi-fire'
  }

  function ariaLabel(c) {
    if (!c.contents.length) return `Compartment ${c.number}, empty`
    const x = c.contents[0]
    return `Compartment ${c.number}, ${x.specification} ${x.diameter} millimetre, lot ${x.lotNumber}, ${kg(c.totalKg)} kilograms`
  }
</script>

<style scoped>
  .oven-layout {
    --oven-line: #b0bec5;
    --oven-ink: #1f2933;
    --oven-occupied: #fff3e0;
    --oven-accent: #ef6c00;
    display: grid;
    grid-template-rows: repeat(3, minmax(76px, auto));
    gap: 2px;
    padding: 2px;
    background: var(--oven-line);
    border-radius: 4px;
  }
  .oven-cell {
    position: relative;
    display: flex;
    flex-direction: column;
    align-items: center;
    justify-content: center;
    gap: 1px;
    padding: 16px 6px 4px;
    min-width: 0;
    min-height: 0;
    overflow: hidden;
    text-align: center;
    background: #ffffff;
    color: var(--oven-ink);
    font: inherit;
    cursor: pointer;
    transition: background-color 0.15s, opacity 0.15s, box-shadow 0.15s;
  }
  .oven-cell:hover:not(:disabled) {
    filter: brightness(0.97);
  }
  .oven-cell:focus-visible {
    outline: 3px solid rgb(var(--v-theme-primary));
    outline-offset: -3px;
  }
  .oven-cell__number {
    position: absolute;
    top: 2px;
    left: 6px;
    font-size: 0.9rem;
    font-weight: 800;
  }
  .oven-cell__spec {
    font-size: 0.9rem;
    font-weight: 700;
    line-height: 1.1;
    max-width: 100%;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
  }
  .oven-cell__meta {
    font-size: 0.72rem;
    line-height: 1.15;
    max-width: 100%;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
  }
  .oven-cell__status {
    font-size: 0.68rem;
    font-weight: 600;
    white-space: nowrap;
  }
  .oven-cell__empty {
    font-size: 0.8rem;
    color: #78909c;
  }
  .oven-cell--occupied {
    background: var(--oven-occupied);
    box-shadow: inset 4px 0 0 var(--oven-accent);
  }
  .oven-cell--occupied .oven-cell__status {
    color: #bf360c;
  }
  .oven-cell--empty {
    background-image: repeating-linear-gradient(135deg, transparent 0 10px, rgba(0, 0, 0, 0.03) 10px 20px);
  }
  .oven-cell--selected {
    background: rgb(var(--v-theme-primary));
    color: rgb(var(--v-theme-on-primary));
    box-shadow: none;
  }
  .oven-cell--selected .oven-cell__empty,
  .oven-cell--selected .oven-cell__status {
    color: inherit;
  }
  .oven-cell--blocked,
  .oven-cell:disabled {
    cursor: not-allowed;
    opacity: 0.45;
  }
  .oven-cell--match {
    box-shadow: inset 0 0 0 4px rgb(var(--v-theme-warning));
  }
  .oven-cell--dim {
    opacity: 0.3;
  }
  .oven-layout--fill {
    grid-template-rows: repeat(3, minmax(0, 1fr));
  }
  .oven-layout--fill .oven-cell {
    container-type: size;
  }
  @container (max-height: 66px) {
    .oven-cell__meta {
      display: none;
    }
  }
  @container (max-height: 46px) {
    .oven-cell__status {
      display: none;
    }
  }
</style>
