// Dashboard JavaScript
document.addEventListener("DOMContentLoaded", () => {
    // Inicializar funcionalidades del dashboard
    initializeDashboard()
    initializeSidebar()
    initializeSearch()
    initializeNotifications()
    // initializeSubmenus() // Comentado - Esta funcionalidad está en sidebar.js
})

function initializeDashboard() {
    console.log("Dashboard inicializado")
}

function initializeSidebar() {
    // Funcionalidad del sidebar
    const sidebar = document.querySelector(".sidebar")
    if (!sidebar) return
    
    console.log("Sidebar inicializado")
}

function initializeSearch() {
    const searchInput = document.querySelector(".search-input")
    if (!searchInput) return
    
    console.log("Búsqueda inicializada")
}

function initializeNotifications() {
    const notificationBell = document.querySelector(".notification-bell")
    if (!notificationBell) return
    
    console.log("Notificaciones inicializadas")
}
