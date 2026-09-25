<template>
  <button type="button" class="module-card" :class="{ 'module-card--locked': locked }"
          :style="{ animationDelay: `${index * 80}ms` }" :aria-label="`${title}${locked ? ' (supervisor login required)' : ''}`"
          @click="$emit('open')">
    <div class="d-flex align-center ga-3">
      <v-avatar :color="color" size="52"><v-icon :icon="icon" size="28" color="white" /></v-avatar>
      <v-spacer />
      <v-chip v-if="locked" size="small" color="white" variant="outlined" prepend-icon="mdi-lock">Login required</v-chip>
    </div>
    <div class="module-card__title">{{ title }}</div>
    <div class="module-card__text">{{ description }}</div>
    <div class="d-flex flex-wrap ga-2 mt-auto pt-4 module-card__stats">
      <template v-if="loading">
        <v-skeleton-loader v-for="n in 2" :key="n" type="chip" class="stat-skeleton" />
      </template>
      <slot v-else />
    </div>
    <div class="module-card__cta">
      {{ locked ? 'Log in to open' : 'Open' }} <v-icon size="small">mdi-arrow-right</v-icon>
    </div>
  </button>
</template>

<script setup>
  defineProps({
    title: { type: String, required: true },
    description: { type: String, required: true },
    icon: { type: String, required: true },
    color: { type: String, default: 'primary' },
    locked: { type: Boolean, default: false },
    loading: { type: Boolean, default: false },
    index: { type: Number, default: 0 },
  })
  defineEmits(['open'])
</script>

<style scoped>
  .module-card {
    display: flex;
    flex-direction: column;
    text-align: left;
    color: #fff;
    min-height: 240px;
    padding: 24px;
    border-radius: 16px;
    border: 1px solid rgba(255, 255, 255, 0.22);
    background: rgba(255, 255, 255, 0.1);
    backdrop-filter: blur(10px);
    cursor: pointer;
    transition: transform 0.18s ease, background 0.18s ease, border-color 0.18s ease;
    animation: card-in 0.4s ease both;
  }

  .module-card:hover {
    transform: translateY(-4px);
    background: rgba(255, 255, 255, 0.16);
    border-color: rgba(255, 255, 255, 0.4);
  }

  .module-card:focus-visible {
    outline: 3px solid #fff;
    outline-offset: 3px;
  }

  .module-card--locked {
    background: rgba(255, 255, 255, 0.06);
  }

  .module-card__title {
    font-size: 1.35rem;
    font-weight: 700;
    margin-top: 16px;
  }

  .module-card__text {
    opacity: 0.85;
    margin-top: 6px;
  }

  .module-card__stats :deep(.v-chip) {
    color: #fff;
  }

  .stat-skeleton {
    background: transparent;
  }

  .stat-skeleton :deep(.v-skeleton-loader__chip) {
    margin: 0;
    width: 110px;
    background: rgba(255, 255, 255, 0.18);
  }

  .module-card__cta {
    margin-top: 16px;
    font-weight: 600;
    display: flex;
    align-items: center;
    gap: 4px;
  }

  @keyframes card-in {
    from { opacity: 0; transform: translateY(12px); }
    to { opacity: 1; transform: none; }
  }

  @media (prefers-reduced-motion: reduce) {
    .module-card { animation: none; transition: none; }
  }
</style>
