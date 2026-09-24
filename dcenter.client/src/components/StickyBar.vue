<template>
  <div class="sticky-bar">
    <slot />
  </div>
</template>

<style scoped>
  .sticky-bar {
    position: sticky;
    top: 0;
    z-index: 5;
    padding-block: 8px;
    margin-block: -8px 4px;
    background: rgb(var(--v-theme-background));
  }
</style>
