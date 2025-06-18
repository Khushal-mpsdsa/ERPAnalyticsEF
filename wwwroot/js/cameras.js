// Tailwind Config
tailwind.config = {
    darkMode: 'class',
    theme: {
        extend: {
            colors: {
                primary: {
                    50: '#f0f9ff',
                    100: '#e0f2fe',
                    200: '#bae6fd',
                    300: '#7dd3fc',
                    400: '#38bdf8',
                    500: '#0ea5e9',
                    600: '#0284c7',
                    700: '#0369a1',
                    800: '#075985',
                    900: '#0c4a6e',
                    950: '#082f49',
                }
            }
        }
    }
}

// Camera Management JavaScript Functions

/**
 * Initialize the camera management page
 */
function initializeCameraManagement() {
    console.log('Camera management page loaded');
    
    // Add event listeners for interactive elements
    setupEventListeners();
    
    // Initialize any additional functionality
    setupFormValidation();
    setupTableInteractions();
}

/**
 * Set up event listeners for various UI elements
 */
function setupEventListeners() {
    // Form submission handling (if needed for AJAX)
    const form = document.querySelector('form[method="post"]');
    if (form) {
        form.addEventListener('submit', handleFormSubmission);
    }
    
    // Action button event listeners
    setupActionButtons();
    
    // Search and filter functionality (if implemented)
    setupSearchAndFilter();
}

/**
 * Handle form submission with validation
 * @param {Event} event - Form submission event
 */
function handleFormSubmission(event) {
    // Basic client-side validation
    const cameraName = document.querySelector('input[name="NewCamera.CameraName"]');
    const refreshRate = document.querySelector('input[name="NewCamera.RefreshRateInSeconds"]');
    
    if (!cameraName || !cameraName.value.trim()) {
        event.preventDefault();
        showNotification('Please enter a camera name', 'error');
        return false;
    }
    
    if (refreshRate && refreshRate.value < 0) {
        event.preventDefault();
        showNotification('Refresh rate cannot be negative', 'error');
        return false;
    }
    
    // If validation passes, show loading state
    showLoadingState();
    
    // Let the form submit normally (server-side handling)
    return true;
}

/**
 * Set up form validation
 */
function setupFormValidation() {
    const inputs = document.querySelectorAll('.form-input');
    
    inputs.forEach(input => {
        input.addEventListener('blur', validateField);
        input.addEventListener('input', clearFieldError);
    });
}

/**
 * Validate individual form field
 * @param {Event} event - Input blur event
 */
function validateField(event) {
    const field = event.target;
    const value = field.value.trim();
    
    // Remove existing error styling
    field.classList.remove('border-red-500');
    
    // Basic validation rules
    if (field.hasAttribute('required') && !value) {
        showFieldError(field, 'This field is required');
        return false;
    }
    
    if (field.type === 'number' && value && parseFloat(value) < 0) {
        showFieldError(field, 'Value cannot be negative');
        return false;
    }
    
    if (field.type === 'url' && value && !isValidUrl(value)) {
        showFieldError(field, 'Please enter a valid URL');
        return false;
    }
    
    return true;
}

/**
 * Clear field error styling
 * @param {Event} event - Input event
 */
function clearFieldError(event) {
    const field = event.target;
    field.classList.remove('border-red-500');
    
    // Remove error message if exists
    const errorSpan = field.parentNode.querySelector('.text-red-500');
    if (errorSpan) {
        errorSpan.textContent = '';
    }
}

/**
 * Show field error
 * @param {HTMLElement} field - Input field element
 * @param {string} message - Error message
 */
function showFieldError(field, message) {
    field.classList.add('border-red-500');
    
    // Find or create error span
    let errorSpan = field.parentNode.querySelector('.text-red-500');
    if (errorSpan) {
        errorSpan.textContent = message;
    }
}

/**
 * Check if URL is valid
 * @param {string} url - URL to validate
 * @returns {boolean} - True if valid URL
 */
function isValidUrl(url) {
    try {
        new URL(url);
        return true;
    } catch {
        return false;
    }
}

/**
 * Set up action buttons (Edit, View, Delete)
 */
function setupActionButtons() {
    // Edit buttons
    document.querySelectorAll('.fa-edit').forEach(button => {
        button.closest('button').addEventListener('click', handleEditCamera);
    });
    
    // View buttons
    document.querySelectorAll('.fa-eye').forEach(button => {
        button.closest('button').addEventListener('click', handleViewCamera);
    });
    
    // Delete buttons
    document.querySelectorAll('.fa-trash').forEach(button => {
        button.closest('button').addEventListener('click', handleDeleteCamera);
    });
}

/**
 * Handle camera edit action
 * @param {Event} event - Click event
 */
function handleEditCamera(event) {
    event.preventDefault();
    
    // Get camera row data
    const row = event.target.closest('tr');
    const cameraData = extractCameraDataFromRow(row);
    
    // Populate edit form or show edit modal
    showEditCameraModal(cameraData);
}

/**
 * Handle camera view action
 * @param {Event} event - Click event
 */
function handleViewCamera(event) {
    event.preventDefault();
    
    // Get camera row data
    const row = event.target.closest('tr');
    const cameraData = extractCameraDataFromRow(row);
    
    // Show camera details modal
    showCameraDetailsModal(cameraData);
}

/**
 * Handle camera delete action
 * @param {Event} event - Click event
 */
function handleDeleteCamera(event) {
    event.preventDefault();
    
    // Get camera row data
    const row = event.target.closest('tr');
    const cameraData = extractCameraDataFromRow(row);
    
    // Show enhanced confirmation dialog
    showDeleteConfirmation(cameraData);
}

/**
 * Extract camera data from table row
 * @param {HTMLElement} row - Table row element
 * @returns {Object} - Camera data object
 */
function extractCameraDataFromRow(row) {
    const cells = row.querySelectorAll('td');
    
    return {
        id: row.getAttribute('data-camera-id') || cells[0].querySelector('.text-gray-500').textContent.replace('ID: ', ''),
        name: cells[0].querySelector('.text-gray-900').textContent,
        status: cells[1].querySelector('.status-badge').textContent.trim(),
        refreshRate: cells[2].textContent.trim(),
        lastUpdate: cells[3].textContent.trim()
    };
}

/**
 * Show edit camera modal
 * @param {Object} cameraData - Camera data to edit
 */
function showEditCameraModal(cameraData) {
    // Implementation would depend on whether you want to use a modal
    // For now, we'll just populate the form
    const nameInput = document.querySelector('input[name="NewCamera.CameraName"]');
    const refreshRateInput = document.querySelector('input[name="NewCamera.RefreshRateInSeconds"]');
    
    if (nameInput) nameInput.value = cameraData.name;
    if (refreshRateInput) {
        const rate = cameraData.refreshRate.replace(' sec', '').replace('Disabled', '0');
        refreshRateInput.value = rate === 'Disabled' ? '0' : rate;
    }
    
    // Scroll to form
    document.querySelector('form').scrollIntoView({ behavior: 'smooth' });
    nameInput?.focus();
    
    showNotification(`Editing camera: ${cameraData.name}`, 'info');
}

/**
 * Show camera details modal
 * @param {Object} cameraData - Camera data to display
 */
function showCameraDetailsModal(cameraData) {
    const modalContent = `
        <div class="camera-details">
            <h3>Camera Details</h3>
            <p><strong>ID:</strong> ${cameraData.id}</p>
            <p><strong>Name:</strong> ${cameraData.name}</p>
            <p><strong>Status:</strong> ${cameraData.status}</p>
            <p><strong>Refresh Rate:</strong> ${cameraData.refreshRate}</p>
            <p><strong>Last Update:</strong> ${cameraData.lastUpdate}</p>
        </div>
    `;
    
    // For now, use alert - in production, you'd use a proper modal
    alert(`Camera Details:\n\nID: ${cameraData.id}\nName: ${cameraData.name}\nStatus: ${cameraData.status}\nRefresh Rate: ${cameraData.refreshRate}\nLast Update: ${cameraData.lastUpdate}`);
}

/**
 * Delete camera by ID
 * @param {string} cameraId - Camera ID to delete
 */
async function deleteCameraById(cameraId) {
    try {
        showLoadingState();
        
        const response = await fetch('/Cameras?handler=DeleteCamera', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'RequestVerificationToken': document.querySelector('input[name="__RequestVerificationToken"]').value
            },
            body: JSON.stringify({ cameraId: parseInt(cameraId) })
        });
        
        const result = await response.json();
        
        if (result.success) {
            showNotification(result.message, 'success');
            
            // Remove the row from the table
            const row = document.querySelector(`tr[data-camera-id="${cameraId}"]`);
            if (row) {
                row.style.opacity = '0';
                row.style.transform = 'translateX(-20px)';
                setTimeout(() => {
                    row.remove();
                    refreshCameraStats();
                }, 300);
            } else {
                // If no data attribute, reload the page
                setTimeout(() => {
                    window.location.reload();
                }, 1500);
            }
        } else {
            showNotification(result.message, 'error');
        }
    } catch (error) {
        console.error('Error deleting camera:', error);
        showNotification('Failed to delete camera. Please try again.', 'error');
    } finally {
        resetLoadingState();
    }
}

/**
 * Show enhanced delete confirmation dialog
 * @param {Object} cameraData - Camera data to delete
 */
function showDeleteConfirmation(cameraData) {
    // First, check if camera is in use
    fetch(`/Cameras?handler=CameraDetails&cameraId=${cameraData.id}`)
        .then(response => response.json())
        .then(data => {
            if (data.success && data.camera) {
                const camera = data.camera;
                const isInUse = camera.isInUse;
                
                let warningMessage = `Are you sure you want to delete the camera "${cameraData.name}"?`;
                let canDelete = true;
                
                if (isInUse) {
                    warningMessage = `⚠️ WARNING: Camera "${cameraData.name}" is currently being used in active schedules.\n\nDeleting this camera will affect the following:\n• All schedules using this camera will stop working\n• Historical data will be preserved but disconnected\n\nYou should remove the schedules first before deleting the camera.\n\nDo you still want to proceed with deletion?`;
                    canDelete = false; // Don't allow deletion if in use
                }
                
                if (canDelete) {
                    if (confirm(warningMessage + '\n\nThis action cannot be undone.')) {
                        deleteCameraById(cameraData.id);
                    }
                } else {
                    alert(`Cannot delete camera "${cameraData.name}" because it is being used in active schedules.\n\nPlease remove the schedules first, then try deleting the camera again.`);
                }
            } else {
                // Fallback to simple confirmation
                if (confirm(`Are you sure you want to delete camera "${cameraData.name}"?\n\nThis action cannot be undone.`)) {
                    deleteCameraById(cameraData.id);
                }
            }
        })
        .catch(error => {
            console.error('Error checking camera details:', error);
            // Fallback to simple confirmation
            if (confirm(`Are you sure you want to delete camera "${cameraData.name}"?\n\nThis action cannot be undone.`)) {
                deleteCameraById(cameraData.id);
            }
        });
}

/**
 * Set up search and filter functionality
 */
function setupSearchAndFilter() {
    // Add search functionality if needed
    const searchInput = document.getElementById('cameraSearch');
    if (searchInput) {
        searchInput.addEventListener('input', filterCameras);
    }
    
    // Add filter dropdowns if needed
    const statusFilter = document.getElementById('statusFilter');
    if (statusFilter) {
        statusFilter.addEventListener('change', filterCameras);
    }
}

/**
 * Filter cameras based on search and filter criteria
 */
function filterCameras() {
    const searchTerm = document.getElementById('cameraSearch')?.value.toLowerCase() || '';
    const statusFilter = document.getElementById('statusFilter')?.value || '';
    
    const rows = document.querySelectorAll('tbody tr');
    
    rows.forEach(row => {
        const cameraName = row.querySelector('.text-gray-900')?.textContent.toLowerCase() || '';
        const cameraStatus = row.querySelector('.status-badge')?.textContent.toLowerCase() || '';
        
        const matchesSearch = !searchTerm || cameraName.includes(searchTerm);
        const matchesStatus = !statusFilter || cameraStatus.includes(statusFilter.toLowerCase());
        
        if (matchesSearch && matchesStatus) {
            row.style.display = '';
        } else {
            row.style.display = 'none';
        }
    });
}

/**
 * Set up table interactions
 */
function setupTableInteractions() {
    // Add table sorting if needed
    const tableHeaders = document.querySelectorAll('th');
    tableHeaders.forEach(header => {
        if (header.textContent.trim() && !header.textContent.includes('Actions')) {
            header.style.cursor = 'pointer';
            header.addEventListener('click', () => sortTable(header));
        }
    });
}

/**
 * Sort table by column
 * @param {HTMLElement} header - Table header element
 */
function sortTable(header) {
    const table = header.closest('table');
    const tbody = table.querySelector('tbody');
    const rows = Array.from(tbody.querySelectorAll('tr'));
    const columnIndex = Array.from(header.parentNode.children).indexOf(header);
    
    // Determine sort direction
    const currentOrder = header.dataset.sortOrder || 'asc';
    const newOrder = currentOrder === 'asc' ? 'desc' : 'asc';
    header.dataset.sortOrder = newOrder;
    
    // Sort rows
    rows.sort((a, b) => {
        const aValue = a.children[columnIndex].textContent.trim();
        const bValue = b.children[columnIndex].textContent.trim();
        
        if (newOrder === 'asc') {
            return aValue.localeCompare(bValue);
        } else {
            return bValue.localeCompare(aValue);
        }
    });
    
    // Re-append sorted rows
    rows.forEach(row => tbody.appendChild(row));
    
    // Update header styling
    const tableHeaders = document.querySelectorAll('th');
    tableHeaders.forEach(h => h.classList.remove('sorted-asc', 'sorted-desc'));
    header.classList.add(`sorted-${newOrder}`);
}

/**
 * Show loading state
 */
function showLoadingState() {
    const submitButton = document.querySelector('button[type="submit"]');
    if (submitButton) {
        submitButton.disabled = true;
        submitButton.innerHTML = '<i class="fas fa-spinner fa-spin mr-2"></i>Adding Camera...';
    }
}

/**
 * Reset loading state
 */
function resetLoadingState() {
    const submitButton = document.querySelector('button[type="submit"]');
    if (submitButton) {
        submitButton.disabled = false;
        submitButton.innerHTML = '<i class="fas fa-plus mr-2"></i>Add Camera';
    }
}

/**
 * Show notification message
 * @param {string} message - Notification message
 * @param {string} type - Notification type (success, error, info, warning)
 */
function showNotification(message, type = 'info') {
    // Create notification element
    const notification = document.createElement('div');
    notification.className = `notification notification-${type} fixed top-4 right-4 z-50 p-4 rounded-lg shadow-lg transition-all duration-300`;
    
    // Set background color based on type
    const bgColors = {
        success: 'bg-green-500 text-white',
        error: 'bg-red-500 text-white',
        warning: 'bg-yellow-500 text-black',
        info: 'bg-blue-500 text-white'
    };
    
    notification.className += ` ${bgColors[type]}`;
    notification.textContent = message;
    
    // Add to page
    document.body.appendChild(notification);
    
    // Auto remove after 3 seconds
    setTimeout(() => {
        notification.style.opacity = '0';
        notification.style.transform = 'translateX(100%)';
        setTimeout(() => {
            if (notification.parentNode) {
                notification.parentNode.removeChild(notification);
            }
        }, 300);
    }, 3000);
}

/**
 * Refresh camera statistics
 */
function refreshCameraStats() {
    // This would typically fetch updated data from the server
    // For now, we'll just update the display with current DOM data
    
    const totalCameras = document.querySelectorAll('tbody tr').length;
    const activeCameras = document.querySelectorAll('.status-badge.active').length;
    const inactiveCameras = totalCameras - activeCameras;
    
    // Update stats cards if they exist
    const totalCamerasCard = document.querySelector('.text-2xl.font-bold.text-gray-800');
    if (totalCamerasCard) {
        totalCamerasCard.textContent = totalCameras;
    }
    
    console.log(`Updated stats: ${totalCameras} total, ${activeCameras} active, ${inactiveCameras} inactive`);
}

/**
 * Initialize camera management when DOM is loaded
 */
document.addEventListener('DOMContentLoaded', function() {
    initializeCameraManagement();
    
    // Set up periodic stats refresh (optional)
    // setInterval(refreshCameraStats, 30000); // Every 30 seconds
});

/**
 * Handle page visibility changes
 */
document.addEventListener('visibilitychange', function() {
    if (document.visibilityState === 'visible') {
        // Refresh data when user returns to tab
        refreshCameraStats();
    }
});

/**
 * Export functions for potential external use
 */
window.CameraManagement = {
    refreshStats: refreshCameraStats,
    showNotification: showNotification,
    filterCameras: filterCameras
};