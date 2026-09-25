<template>
  <section class="hero">
    <div class="hero__logo"><v-img :src="logo" width="56" height="56" alt="" /></div>
    <h1 class="hero__title">DCenter Operations Hub</h1>

    <div class="d-flex flex-wrap justify-center align-center ga-3">
      <template v-if="store.isSupervisor">
        <span class="hero__user"><v-icon size="18" class="me-1">mdi-shield-account</v-icon>{{ store.supervisor.name }}</span>
        <v-btn variant="outlined" color="white" size="small" prepend-icon="mdi-logout" @click="store.lockSupervisor()">Log out</v-btn>
      </template>
      <v-btn v-else variant="outlined" color="white" prepend-icon="mdi-shield-lock-outline" @click="login">Login</v-btn>
    </div>
  </section>
</template>

<script setup>
  import { useRoute, useRouter } from 'vue-router'
  import { useConsumableStore } from '@/store/consumableStore'
  import logo from '@/assets/DCenter_No_bg.png'

  const store = useConsumableStore()
  const route = useRoute()
  const router = useRouter()

  function login() {
    router.replace({ path: route.path, query: { ...route.query, unlock: '1' } })
  }
</script>

<style scoped>
  .hero {
    display: flex;
    flex-direction: column;
    align-items: center;
    gap: 20px;
    text-align: center;
    animation: rise 0.4s ease both;
  }

  .hero__logo {
    background: #fff;
    border-radius: 16px;
    padding: 10px;
    box-shadow: 0 4px 16px rgba(0, 0, 0, 0.25);
  }

  .hero__title {
    font-size: clamp(1.75rem, 4vw, 2.75rem);
    font-weight: 600;
    letter-spacing: 0.01em;
    line-height: 1.15;
    margin: 0;
  }

  .hero__user {
    display: inline-flex;
    align-items: center;
    opacity: 0.9;
  }

  @keyframes rise {
    from { opacity: 0; transform: translateY(10px); }
    to { opacity: 1; transform: none; }
  }

  @media (prefers-reduced-motion: reduce) {
    .hero { animation: none; }
  }
</style>
