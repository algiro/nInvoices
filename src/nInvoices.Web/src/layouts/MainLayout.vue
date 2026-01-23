<template>
  <div class="app-container">
    <nav class="sidebar">
      <div class="logo">
        <h1>nInvoices</h1>
      </div>
      <ul class="nav-menu">
        <li>
          <router-link to="/" class="nav-link">
            <span>📊</span> Dashboard
          </router-link>
        </li>
        <li>
          <router-link to="/customers" class="nav-link">
            <span>👥</span> Customers
          </router-link>
        </li>
        <li>
          <router-link to="/invoices" class="nav-link">
            <span>📄</span> Invoices
          </router-link>
        </li>
        <li>
          <router-link to="/settings" class="nav-link">
            <span>⚙️</span> Settings
          </router-link>
        </li>
      </ul>
    </nav>

    <main class="main-content">
      <router-view />
    </main>
  </div>
</template>

<style scoped>
.app-container {
  display: flex;
  min-height: 100vh;
}

.sidebar {
  width: 250px;
  background: #2c3e50;
  color: white;
  padding: 2rem 0;
}

.logo {
  padding: 0 2rem 2rem;
  border-bottom: 1px solid #34495e;
  margin-bottom: 2rem;
}

.logo h1 {
  margin: 0;
  font-size: 1.5rem;
  color: #3498db;
}

.nav-menu {
  list-style: none;
  padding: 0;
  margin: 0;
}

.nav-link {
  display: block;
  padding: 1rem 2rem;
  color: #ecf0f1;
  text-decoration: none;
  transition: all 0.3s;
}

.nav-link:hover {
  background: #34495e;
  padding-left: 2.5rem;
}

.nav-link.router-link-active {
  background: #3498db;
  border-left: 4px solid #2980b9;
}

.nav-link span {
  margin-right: 0.5rem;
}

.main-content {
  flex: 1;
  padding: 2rem;
  background: #ecf0f1;
  overflow-y: auto;
}
</style>
