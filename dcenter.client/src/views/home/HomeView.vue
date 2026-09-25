<template>
  <div class="home" :style="{ '--home-bg': `url(${background})` }">
    <div class="home__inner">
      <HomeHero />
      <HomeModules />
    </div>
  </div>
</template>

<script setup>
  import background from '@/assets/home-bg.jpg'
  import HomeHero from '@/components/home/HomeHero.vue'
  import HomeModules from '@/components/home/HomeModules.vue'
</script>

<style scoped>
  .home {
    position: relative;
    height: 100%;
    overflow-y: auto;
    color: #fff;
    background:
      linear-gradient(115deg, rgba(0, 40, 85, 0.85) 0%, rgba(0, 40, 85, 0.7) 50%, rgba(0, 40, 85, 0.55) 100%),
      var(--home-bg) center / cover no-repeat;
  }

  .home__inner {
    min-height: 100%;
    padding: 48px 24px;
    display: flex;
    flex-direction: column;
    align-items: center;
    justify-content: center;
    gap: 48px;
  }

  @media (max-width: 600px) {
    .home__inner {
      padding: 32px 16px;
      gap: 32px;
    }
  }
</style>
