import { createApp } from 'vue';
import { createPinia } from 'pinia';
import router from './router';
import './style.css';
import App from './App.vue';
import { useAuthStore } from './stores/auth';
import { initTheme } from './composables/useTheme';

initTheme();

const app = createApp(App);

const pinia = createPinia();
app.use(pinia);
app.use(router);

// Initialize authentication before mounting
const authStore = useAuthStore();
authStore.initialize().then(() => {
  app.mount('#app');
});

