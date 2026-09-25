<template>
  <button type="button" class="module-card" :class="{ 'module-card--locked': locked }"
          :style="{ animationDelay: `${index * 80}ms` }" :aria-label="`${title}${locked ? ' (login required)' : ''}`"
          @click="$emit('open')">
    <v-icon v-if="locked" class="module-card__lock" size="18">mdi-lock</v-icon>
    <v-icon :icon="icon" size="40" />
    <span class="module-card__title">{{ title }}</span>
  </button>
</template>

<script setup>
  defineProps({
    title: { type: String, required: true },
    icon: { type: String, required: true },
    locked: { type: Boolean, default: false },
    index: { type: Number, default: 0 },
  })
  defineEmits(['open'])
</script>

<style scoped>
  .module-card {
    position: relative;
    display: flex;
    flex-direction: column;
    align-items: center;
    justify-content: center;
    gap: 12px;
    width: 160px;
    height: 150px;
    color: #fff;
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
    background: rgba(255, 255, 255, 0.18);
    border-color: rgba(255, 255, 255, 0.45);
  }

  .module-card:focus-visible {
    outline: 3px solid #fff;
    outline-offset: 3px;
  }

  .module-card--locked {
    opacity: 0.75;
  }

  .module-card__lock {
    position: absolute;
    top: 10px;
    right: 10px;
  }

  .module-card__title {
    font-size: 1.05rem;
    font-weight: 600;
  }

  @keyframes card-in {
    from { opacity: 0; transform: translateY(12px); }
    to { opacity: 1; transform: none; }
  }

  @media (prefers-reduced-motion: reduce) {
    .module-card { animation: none; transition: none; }
  }
</style>
