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
      linear-gradient(115deg, rgba(0, 40, 85, 0.88) 0%, rgba(0, 40, 85, 0.7) 45%, rgba(0, 40, 85, 0.45) 100%),
      var(--home-bg) center / cover no-repeat;
  }

  .home__inner {
    max-width: 1200px;
    margin: 0 auto;
    min-height: 100%;
    padding: 48px 24px;
    display: flex;
    flex-direction: column;
    justify-content: center;
    gap: 40px;
  }

  @media (max-width: 600px) {
    .home__inner {
      padding: 32px 16px;
      gap: 28px;
    }
  }
</style>
