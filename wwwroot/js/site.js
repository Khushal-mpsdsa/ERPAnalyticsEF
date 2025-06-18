// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.


// Simple Sidebar Toggle Function - Add to wwwroot/js/site.js
// Minimal JavaScript for mobile sidebar toggle only

function toggleSidebar() {
    const sidebar = document.getElementById('sidebar');
    if (sidebar) {
        sidebar.classList.toggle('show');
    }
}

// Close sidebar when clicking outside on mobile
document.addEventListener('click', function(event) {
    const sidebar = document.getElementById('sidebar');
    const toggleButton = event.target.closest('[onclick="toggleSidebar()"]');
    
    if (sidebar && !sidebar.contains(event.target) && !toggleButton && window.innerWidth < 1024) {
        sidebar.classList.remove('show');
    }
});

// Close sidebar on escape key
document.addEventListener('keydown', function(event) {
    if (event.key === 'Escape') {
        const sidebar = document.getElementById('sidebar');
        if (sidebar && window.innerWidth < 1024) {
            sidebar.classList.remove('show');
        }
    }
});