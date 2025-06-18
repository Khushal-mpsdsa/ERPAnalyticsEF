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

// =================================================================
// MAIN SCHEDULE FUNCTIONS (Enhanced with fixes)
// =================================================================

function validateDuration(e) {
    if (e.target.value < 1) {
        e.target.value = 1;
    }
}

function addSchedule() {
    var scheduleName = document.getElementById('scheduleName').value;
    var timeValue = document.getElementById('startTime').value;
    var duration = parseInt(document.getElementById('duration').value, 10);
    var cameraId = parseInt(document.getElementById('cameraSelect').value, 10);

    if (!scheduleName || !timeValue || isNaN(duration) || isNaN(cameraId)) {
        showNotification('Please fill in all fields with valid values!', 'error');
        return;
    }

    var [hours, minutes, seconds] = timeValue.split(':');
    seconds = seconds || '00';

    var today = new Date();
    var localTime = new Date(today.getFullYear(), today.getMonth(), today.getDate(), parseInt(hours), parseInt(minutes), parseInt(seconds));
    const pad = (n) => n.toString().padStart(2, '0');
    var formattedLocalDate = `${localTime.getFullYear()}-${pad(localTime.getMonth() + 1)}-${pad(localTime.getDate())}T${pad(localTime.getHours())}:${pad(localTime.getMinutes())}:${pad(localTime.getSeconds())}`;

    const scheduleData = {
        cameraID: cameraId,
        scheduleName: scheduleName,
        startTime: formattedLocalDate,
        durationInSec: duration
    };

    // Show loading state
    const addButton = document.querySelector('button[onclick="addSchedule()"]');
    const originalText = addButton.innerHTML;
    addButton.disabled = true;
    addButton.innerHTML = '<i class="fas fa-spinner fa-spin mr-2"></i>Adding...';

    fetch('/Schedule?handler=AddSchedule', {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json',
            'RequestVerificationToken': document.querySelector('input[name="__RequestVerificationToken"]').value
        },
        body: JSON.stringify(scheduleData)
    })
    .then(response => response.json())
    .then(data => {
        if (data.success) {
            showNotification(data.message || "Schedule added successfully!", 'success');
            clearForm();
            loadSchedules(); // Reload schedules for the selected camera
            updateStatistics();
        } else {
            showNotification(data.message || "Failed to add schedule", 'error');
        }
    })
    .catch(error => {
        console.error('Error adding schedule:', error);
        showNotification("Error adding schedule: " + error.message, 'error');
    })
    .finally(() => {
        // Reset button state
        addButton.disabled = false;
        addButton.innerHTML = originalText;
    });
}

function clearForm() {
    document.getElementById('scheduleName').value = '';
    document.getElementById('startTime').value = '';
    document.getElementById('duration').value = '';
}

function formatDateTime(dateString) {
    const date = new Date(dateString);
    return date.toLocaleTimeString([], {
        hour: '2-digit',
        minute: '2-digit',
        hour12: true
    });
}

function getScheduleStatus(startTime, duration) {
    const now = new Date();
    const scheduleStart = new Date(startTime);
    const scheduleEnd = new Date(scheduleStart.getTime() + duration * 1000);
    
    if (now < scheduleStart) {
        return { status: 'upcoming', text: 'Upcoming' };
    } else if (now >= scheduleStart && now <= scheduleEnd) {
        return { status: 'active', text: 'Active' };
    } else {
        return { status: 'completed', text: 'Completed' };
    }
}

function loadSchedules() {
    console.log('loadSchedules() called');
    
    var cameraId = document.getElementById('cameraSelect').value;
    console.log('Selected camera ID:', cameraId);
    
    const scheduleTable = document.getElementById("scheduleTable");
    const noSchedulesMessage = document.getElementById("noSchedulesMessage");
    const tableBody = document.querySelector("#scheduleTable tbody");
    
    if (!cameraId || cameraId === '') {
        console.log('No camera selected, hiding table');
        scheduleTable.style.display = "none";
        noSchedulesMessage.style.display = "block";
        noSchedulesMessage.innerHTML = `
            <div class="w-16 h-16 rounded-full bg-gray-100 flex items-center justify-center mx-auto mb-4">
                <i class="fas fa-calendar-alt text-2xl text-gray-400"></i>
            </div>
            <h3 class="text-lg font-medium text-gray-900 mb-2">No camera selected</h3>
            <p class="text-gray-500 mb-4">Please select a camera to view its schedules</p>
        `;
        updateStatistics();
        return;
    }

    // Show loading state
    scheduleTable.style.display = "none";
    noSchedulesMessage.style.display = "block";
    noSchedulesMessage.innerHTML = `
        <div class="w-16 h-16 rounded-full bg-gray-100 flex items-center justify-center mx-auto mb-4">
            <i class="fas fa-spinner fa-spin text-2xl text-gray-400"></i>
        </div>
        <h3 class="text-lg font-medium text-gray-900 mb-2">Loading schedules...</h3>
        <p class="text-gray-500 mb-4">Please wait while we fetch the schedules</p>
    `;

    console.log('Fetching schedules for camera:', cameraId);
    
    fetch(`/Schedule?handler=GetSchedules&cameraId=${cameraId}`)
        .then(response => {
            console.log('Response status:', response.status);
            return response.json();
        })
        .then(data => {
            console.log('Received data:', data);
            
            // Clear the table body
            tableBody.innerHTML = "";
            
            if (data.success && data.data && data.data.length > 0) {
                console.log('Found', data.data.length, 'schedules');
                
                data.data.forEach(s => {
                    const statusInfo = getScheduleStatus(s.startTime, s.durationInSec);
                    const row = `
                        <tr class="hover:bg-gray-50 transition-colors">
                            <td class="px-6 py-4 whitespace-nowrap">
                                <div class="flex items-center">
                                    <div class="w-10 h-10 rounded-lg bg-primary-100 flex items-center justify-center text-primary-600 mr-4">
                                        <i class="fas fa-calendar"></i>
                                    </div>
                                    <div>
                                        <div class="text-sm font-medium text-gray-900">${s.scheduleName}</div>
                                        <div class="text-sm text-gray-500">ID: ${s.scheduleID}</div>
                                    </div>
                                </div>
                            </td>
                            <td class="px-6 py-4 whitespace-nowrap text-sm text-gray-900">
                                <div class="flex items-center">
                                    <i class="fas fa-video text-gray-400 mr-2"></i>
                                    Camera ${s.cameraID}
                                </div>
                            </td>
                            <td class="px-6 py-4 whitespace-nowrap text-sm text-gray-900">${formatDateTime(s.startTime)}</td>
                            <td class="px-6 py-4 whitespace-nowrap text-sm text-gray-900">
                                <span class="inline-flex items-center">
                                    <i class="fas fa-clock text-gray-400 mr-1"></i>
                                    ${Math.floor(s.durationInSec / 60)}m ${s.durationInSec % 60}s
                                </span>
                            </td>
                            <td class="px-6 py-4 whitespace-nowrap">
                                <span class="schedule-status ${statusInfo.status}">
                                    <i class="fas fa-circle mr-1 text-xs"></i>
                                    ${statusInfo.text}
                                </span>
                            </td>
                            <td class="px-6 py-4 whitespace-nowrap text-sm font-medium">
                                <div class="flex items-center gap-2">
                                    <button class="text-primary-600 hover:text-primary-900 transition-colors p-2 rounded-lg hover:bg-primary-50" title="Edit" onclick="editSchedule(${s.scheduleID})">
                                        <i class="fas fa-edit"></i>
                                    </button>
                                    <button class="text-green-600 hover:text-green-900 transition-colors p-2 rounded-lg hover:bg-green-50" title="View" onclick="viewSchedule(${s.scheduleID})">
                                        <i class="fas fa-eye"></i>
                                    </button>
                                    <button class="text-red-600 hover:text-red-900 transition-colors p-2 rounded-lg hover:bg-red-50" title="Delete" onclick="deleteSchedule(${s.scheduleID}, '${s.scheduleName}')">
                                        <i class="fas fa-trash"></i>
                                    </button>
                                </div>
                            </td>
                        </tr>
                    `;
                    tableBody.insertAdjacentHTML('beforeend', row);
                });
                
                scheduleTable.style.display = "table";
                noSchedulesMessage.style.display = "none";
            } else {
                console.log('No schedules found');
                scheduleTable.style.display = "none";
                noSchedulesMessage.style.display = "block";
                noSchedulesMessage.innerHTML = `
                    <div class="w-16 h-16 rounded-full bg-gray-100 flex items-center justify-center mx-auto mb-4">
                        <i class="fas fa-calendar-alt text-2xl text-gray-400"></i>
                    </div>
                    <h3 class="text-lg font-medium text-gray-900 mb-2">No schedules found</h3>
                    <p class="text-gray-500 mb-4">This camera doesn't have any schedules yet. Create one using the form on the left.</p>
                    <button onclick="focusOnForm()" class="px-4 py-2 bg-primary-600 hover:bg-primary-700 text-white rounded-lg transition-colors text-sm font-medium">
                        Create Schedule
                    </button>
                `;
            }
            
            updateStatistics();
        })
        .catch(error => {
            console.error("Failed to load schedules:", error);
            scheduleTable.style.display = "none";
            noSchedulesMessage.style.display = "block";
            noSchedulesMessage.innerHTML = `
                <div class="w-16 h-16 rounded-full bg-red-100 flex items-center justify-center mx-auto mb-4">
                    <i class="fas fa-exclamation-triangle text-2xl text-red-400"></i>
                </div>
                <h3 class="text-lg font-medium text-gray-900 mb-2">Error loading schedules</h3>
                <p class="text-gray-500 mb-4">Failed to load schedules. Please try again.</p>
                <button onclick="loadSchedules()" class="px-4 py-2 bg-primary-600 hover:bg-primary-700 text-white rounded-lg transition-colors text-sm font-medium">
                    Try Again
                </button>
            `;
            showNotification("Failed to load schedules: " + error.message, 'error');
        });
}

function updateStatistics() {
    // Update statistics cards
    const tbody = document.querySelector("#scheduleTable tbody");
    const scheduleCount = tbody ? tbody.children.length : 0;
    
    document.getElementById('totalSchedules').textContent = scheduleCount;
    
    // Calculate other statistics (this is a simplified version)
    // In a real application, you'd fetch this data from the server
    document.getElementById('activeToday').textContent = Math.floor(scheduleCount * 0.3);
    document.getElementById('avgDuration').innerHTML = '45<span class="text-sm text-gray-500">min</span>';
}

function focusOnForm() {
    const scheduleName = document.getElementById('scheduleName');
    if (scheduleName) {
        scheduleName.focus();
        scheduleName.scrollIntoView({ behavior: 'smooth', block: 'center' });
    }
}

// =================================================================
// SCHEDULE ACTION FUNCTIONS (Edit, View, Delete)
// =================================================================

/**
 * Delete a schedule with confirmation
 * @param {number} scheduleId - Schedule ID to delete
 * @param {string} scheduleName - Schedule name for confirmation
 */
function deleteSchedule(scheduleId, scheduleName) {
    // Simple confirmation dialog
    const confirmMessage = `Are you sure you want to delete the schedule "${scheduleName}"?\n\nThis action cannot be undone.`;
    
    if (confirm(confirmMessage)) {
        performScheduleDelete(scheduleId);
    }
}

/**
 * Perform the actual schedule deletion
 * @param {number} scheduleId - Schedule ID to delete
 */
async function performScheduleDelete(scheduleId) {
    try {
        // Get the delete button and show loading state
        const deleteButtons = document.querySelectorAll(`button[onclick*="deleteSchedule(${scheduleId}"]`);
        const deleteButton = deleteButtons[0];
        
        if (deleteButton) {
            const originalContent = deleteButton.innerHTML;
            deleteButton.disabled = true;
            deleteButton.innerHTML = '<i class="fas fa-spinner fa-spin"></i>';
        }

        const response = await fetch('/Schedule?handler=DeleteSchedule', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'RequestVerificationToken': document.querySelector('input[name="__RequestVerificationToken"]').value
            },
            body: JSON.stringify({ scheduleID: parseInt(scheduleId) })
        });

        const result = await response.json();

        if (result.success) {
            showNotification(result.message, 'success');
            
            // Remove the schedule row from table with animation
            const scheduleRows = document.querySelectorAll('#scheduleTable tbody tr');
            scheduleRows.forEach(row => {
                const deleteBtn = row.querySelector(`button[onclick*="deleteSchedule(${scheduleId}"]`);
                if (deleteBtn) {
                    row.style.opacity = '0';
                    row.style.transform = 'translateX(-20px)';
                    setTimeout(() => {
                        row.remove();
                        updateStatistics();
                        
                        // Check if table is now empty
                        const remainingRows = document.querySelectorAll('#scheduleTable tbody tr');
                        if (remainingRows.length === 0) {
                            const cameraId = document.getElementById('cameraSelect').value;
                            if (cameraId) {
                                document.getElementById("scheduleTable").style.display = "none";
                                document.getElementById("noSchedulesMessage").style.display = "block";
                                document.getElementById("noSchedulesMessage").innerHTML = `
                                    <div class="w-16 h-16 rounded-full bg-gray-100 flex items-center justify-center mx-auto mb-4">
                                        <i class="fas fa-calendar-alt text-2xl text-gray-400"></i>
                                    </div>
                                    <h3 class="text-lg font-medium text-gray-900 mb-2">No schedules found</h3>
                                    <p class="text-gray-500 mb-4">This camera doesn't have any schedules yet. Create one using the form on the left.</p>
                                    <button onclick="focusOnForm()" class="px-4 py-2 bg-primary-600 hover:bg-primary-700 text-white rounded-lg transition-colors text-sm font-medium">
                                        Create Schedule
                                    </button>
                                `;
                            }
                        }
                    }, 300);
                }
            });
        } else {
            showNotification(result.message, 'error');
            
            // Reset button state on error
            if (deleteButton) {
                deleteButton.disabled = false;
                deleteButton.innerHTML = '<i class="fas fa-trash"></i>';
            }
        }
    } catch (error) {
        console.error('Error deleting schedule:', error);
        showNotification('Failed to delete schedule. Please try again.', 'error');
        
        // Reset button state on error
        const deleteButtons = document.querySelectorAll(`button[onclick*="deleteSchedule(${scheduleId}"]`);
        const deleteButton = deleteButtons[0];
        if (deleteButton) {
            deleteButton.disabled = false;
            deleteButton.innerHTML = '<i class="fas fa-trash"></i>';
        }
    }
}

/**
 * Edit a schedule (placeholder function)
 * @param {number} scheduleId - Schedule ID to edit
 */
function editSchedule(scheduleId) {
    showNotification('Edit functionality coming soon', 'info');
    console.log('Edit schedule:', scheduleId);
}

/**
 * View schedule details (placeholder function)
 * @param {number} scheduleId - Schedule ID to view
 */
function viewSchedule(scheduleId) {
    showNotification('View functionality coming soon', 'info');
    console.log('View schedule:', scheduleId);
}

// Show notification function
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
    
    // Add icon based on type
    const icons = {
        success: 'fas fa-check-circle',
        error: 'fas fa-exclamation-circle',
        warning: 'fas fa-exclamation-triangle',
        info: 'fas fa-info-circle'
    };
    
    notification.innerHTML = `
        <div class="flex items-center">
            <i class="${icons[type]} mr-2"></i>
            <span>${message}</span>
        </div>
    `;
    
    // Add to page
    document.body.appendChild(notification);
    
    // Auto remove after 4 seconds
    setTimeout(() => {
        notification.style.opacity = '0';
        notification.style.transform = 'translateX(100%)';
        setTimeout(() => {
            if (notification.parentNode) {
                notification.parentNode.removeChild(notification);
            }
        }, 300);
    }, 4000);
}

// =================================================================
// BOTTOM SCRIPT SECTION (Original Duplicate/Alternative functions)
// =================================================================

// Note: Some of these functions are duplicates with slight variations from the above
// These were kept for backward compatibility and alternative implementations

function validateDurationBottom(e) {
    if (e.target.value < 1) {
        e.target.value = 1;
    }
}

function addScheduleBottom() {
    var scheduleName = document.getElementById('scheduleName').value;
    var timeValue = document.getElementById('startTime').value;
    var duration = parseInt(document.getElementById('duration').value, 10);
    var cameraId = parseInt(document.getElementById('cameraSelect').value, 10);

    if (!scheduleName || !timeValue || isNaN(duration) || isNaN(cameraId)) {
        alert('Please fill in all fields with valid values!');
        return;
    }

    var [hours, minutes, seconds] = timeValue.split(':');
    seconds = seconds || '00';

    var today = new Date();
    var localTime = new Date(today.getFullYear(), today.getMonth(), today.getDate(), parseInt(hours), parseInt(minutes), parseInt(seconds));
    const pad = (n) => n.toString().padStart(2, '0');
    var formattedLocalDate = `${localTime.getFullYear()}-${pad(localTime.getMonth() + 1)}-${pad(localTime.getDate())}T${pad(localTime.getHours())}:${pad(localTime.getMinutes())}:${pad(localTime.getSeconds())}`;

    const scheduleData = {
        cameraID: cameraId,
        scheduleName: scheduleName,
        startTime: formattedLocalDate,
        durationInSec: duration
    };

    fetch('/Schedule?handler=AddSchedule', {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json',
            'RequestVerificationToken': document.querySelector('input[name="__RequestVerificationToken"]').value
        },
        body: JSON.stringify(scheduleData)
    })
    .then(response => response.json())
    .then(data => {
        alert("Schedule added: " + data.count);
        loadSchedules(); // Refresh the schedule list
    })
    .catch(error => alert("Schedule error: " + error));
}

function formatDateTimeBottom(dateString) {
    const date = new Date(dateString);
    return date.toLocaleTimeString([], {
        hour: '2-digit',
        minute: '2-digit',
        hour12: true
    });
}

function loadSchedulesBottom() {
    var cameraId = document.getElementById('cameraSelect').value;
    if (!cameraId) {
        document.getElementById("scheduleTable").style.display = "none";
        return;
    }

    fetch(`/Schedule?handler=GetSchedules&cameraId=${cameraId}`)
        .then(response => response.json())
        .then(data => {
            const tbody = document.querySelector("#scheduleTable tbody");
            tbody.innerHTML = "";
            data.forEach(s => {
                const row = `<tr>
                                <td>${s.scheduleName}</td>
                                <td>${formatDateTime(s.startTime)}</td>
                                <td>${s.durationInSec}</td>
                             </tr>`;
                tbody.insertAdjacentHTML('beforeend', row);
            });
            document.getElementById("scheduleTable").style.display = data.length > 0 ? "table" : "none";
        })
        .catch(error => console.error("Failed to load schedules:", error));
}

// Optional: Auto-load schedules for the first camera on page load
window.onload = function () {
    if (document.getElementById('cameraSelect').value) {
        loadSchedules();
    }
};

// =================================================================
// ADDITIONAL UTILITY FUNCTIONS AND ENHANCEMENTS
// =================================================================

// Additional functions that could be useful for external JS file usage

function refreshSchedules() {
    loadSchedules();
}

function clearAllForms() {
    clearForm();
}

function getFormData() {
    return {
        scheduleName: document.getElementById('scheduleName').value,
        startTime: document.getElementById('startTime').value,
        duration: document.getElementById('duration').value,
        cameraId: document.getElementById('cameraSelect').value
    };
}

function setFormData(data) {
    if (data.scheduleName !== undefined) document.getElementById('scheduleName').value = data.scheduleName;
    if (data.startTime !== undefined) document.getElementById('startTime').value = data.startTime;
    if (data.duration !== undefined) document.getElementById('duration').value = data.duration;
    if (data.cameraId !== undefined) document.getElementById('cameraSelect').value = data.cameraId;
}

function showScheduleTable() {
    document.getElementById("scheduleTable").style.display = "table";
    document.getElementById("noSchedulesMessage").style.display = "none";
}

function hideScheduleTable() {
    document.getElementById("scheduleTable").style.display = "none";
    document.getElementById("noSchedulesMessage").style.display = "block";
}

function toggleScheduleTable() {
    const table = document.getElementById("scheduleTable");
    const message = document.getElementById("noSchedulesMessage");
    
    if (table.style.display === "none") {
        showScheduleTable();
    } else {
        hideScheduleTable();
    }
}

// Additional utility functions for schedule management
function duplicateSchedule(scheduleId) {
    // Function to duplicate an existing schedule
    const scheduleRows = document.querySelectorAll('#scheduleTable tbody tr');
    // Implementation would fetch schedule data and pre-populate form
    console.log('Duplicate schedule:', scheduleId);
    showNotification('Duplicate functionality coming soon', 'info');
}

function exportSchedules() {
    // Function to export schedules to CSV or other formats
    const schedules = [];
    const rows = document.querySelectorAll('#scheduleTable tbody tr');
    
    rows.forEach(row => {
        const cells = row.querySelectorAll('td');
        if (cells.length > 0) {
            schedules.push({
                name: cells[0].textContent.trim(),
                camera: cells[1].textContent.trim(),
                time: cells[2].textContent.trim(),
                duration: cells[3].textContent.trim(),
                status: cells[4].textContent.trim()
            });
        }
    });
    
    if (schedules.length === 0) {
        showNotification('No schedules to export', 'warning');
        return;
    }
    
    // Convert to CSV
    const csvContent = [
        'Schedule Name,Camera,Time,Duration,Status',
        ...schedules.map(s => `"${s.name}","${s.camera}","${s.time}","${s.duration}","${s.status}"`)
    ].join('\n');
    
    // Download CSV
    const blob = new Blob([csvContent], { type: 'text/csv' });
    const url = window.URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `schedules_${new Date().toISOString().split('T')[0]}.csv`;
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    window.URL.revokeObjectURL(url);
    
    showNotification('Schedules exported successfully', 'success');
}

function bulkDeleteSchedules() {
    // Function for bulk operations on schedules
    const cameraId = document.getElementById('cameraSelect').value;
    if (!cameraId) {
        showNotification('Please select a camera first', 'warning');
        return;
    }
    
    const scheduleRows = document.querySelectorAll('#scheduleTable tbody tr');
    if (scheduleRows.length === 0) {
        showNotification('No schedules to delete', 'warning');
        return;
    }
    
    const cameraName = document.getElementById('cameraSelect').selectedOptions[0].textContent;
    
    if (confirm(`Are you sure you want to delete ALL schedules for camera "${cameraName}"?\n\nThis will delete ${scheduleRows.length} schedule(s) and cannot be undone.`)) {
        // Extract all schedule IDs from the current table
        const scheduleIds = [];
        scheduleRows.forEach(row => {
            const deleteButton = row.querySelector('button[onclick*="deleteSchedule"]');
            if (deleteButton) {
                const onclickAttr = deleteButton.getAttribute('onclick');
                const match = onclickAttr.match(/deleteSchedule\((\d+)/);
                if (match) {
                    scheduleIds.push(parseInt(match[1]));
                }
            }
        });
        
        if (scheduleIds.length > 0) {
            bulkDeleteSchedulesByIds(scheduleIds);
        }
    }
}

async function bulkDeleteSchedulesByIds(scheduleIds) {
    let successCount = 0;
    let failCount = 0;
    
    showNotification(`Deleting ${scheduleIds.length} schedules...`, 'info');
    
    for (const scheduleId of scheduleIds) {
        try {
            const response = await fetch('/Schedule?handler=DeleteSchedule', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'RequestVerificationToken': document.querySelector('input[name="__RequestVerificationToken"]').value
                },
                body: JSON.stringify({ scheduleID: scheduleId })
            });

            const result = await response.json();
            if (result.success) {
                successCount++;
            } else {
                failCount++;
            }
        } catch (error) {
            failCount++;
            console.error('Error deleting schedule:', scheduleId, error);
        }
    }
    
    // Show result and refresh the table
    if (successCount > 0) {
        showNotification(`Successfully deleted ${successCount} schedule(s)${failCount > 0 ? `, ${failCount} failed` : ''}`, successCount === scheduleIds.length ? 'success' : 'warning');
        loadSchedules(); // Refresh the entire table
    } else {
        showNotification(`Failed to delete all schedules`, 'error');
    }
}

function validateScheduleTime(timeString) {
    // Validate time format and constraints
    if (!timeString) return false;
    
    const timeRegex = /^([01]?[0-9]|2[0-3]):[0-5][0-9](:[0-5][0-9])?$/;
    return timeRegex.test(timeString);
}

function formatScheduleDuration(seconds) {
    // Format duration in a human-readable way
    const hours = Math.floor(seconds / 3600);
    const minutes = Math.floor((seconds % 3600) / 60);
    const remainingSeconds = seconds % 60;
    
    if (hours > 0) {
        return `${hours}h ${minutes}m ${remainingSeconds}s`;
    } else if (minutes > 0) {
        return `${minutes}m ${remainingSeconds}s`;
    } else {
        return `${remainingSeconds}s`;
    }
}

function getScheduleConflicts(newStartTime, newDuration, cameraId) {
    // Check for schedule conflicts
    const conflicts = [];
    const rows = document.querySelectorAll('#scheduleTable tbody tr');
    
    // Implementation would check for overlapping schedules
    // This is a placeholder for conflict detection logic
    
    return conflicts;
}

// Export functions for module use if needed
if (typeof module !== 'undefined' && module.exports) {
    module.exports = {
        validateDuration,
        addSchedule,
        clearForm,
        formatDateTime,
        getScheduleStatus,
        loadSchedules,
        updateStatistics,
        refreshSchedules,
        clearAllForms,
        getFormData,
        setFormData,
        showScheduleTable,
        hideScheduleTable,
        toggleScheduleTable,
        duplicateSchedule,
        exportSchedules,
        bulkDeleteSchedules,
        validateScheduleTime,
        formatScheduleDuration,
        getScheduleConflicts
    };
}

// Initialize page
document.addEventListener('DOMContentLoaded', function() {
    console.log('DOM loaded, initializing schedule page');
    
    updateStatistics();
    
    // Set up event listener for camera selection
    const cameraSelect = document.getElementById('cameraSelect');
    if (cameraSelect) {
        console.log('Adding change event listener to camera select');
        cameraSelect.addEventListener('change', function() {
            console.log('Camera selection changed to:', this.value);
            loadSchedules();
        });
        
        // Auto-load schedules if a camera is pre-selected
        if (cameraSelect.value) {
            console.log('Camera pre-selected, loading schedules');
            loadSchedules();
        }
    } else {
        console.error('Could not find cameraSelect element');
    }
    
    // Set up input validation
    const durationInput = document.getElementById('duration');
    if (durationInput) {
        durationInput.addEventListener('input', validateDuration);
    }
    
    // Set up quick action buttons
    setupQuickActions();
});

function setupQuickActions() {
    // Set up event listeners for quick action buttons
    const quickActions = document.querySelectorAll('.quick-action-item');
    
    quickActions.forEach((action, index) => {
        action.addEventListener('click', function() {
            switch(index) {
                case 0: // Duplicate
                    duplicateSchedule();
                    break;
                case 1: // Export
                    exportSchedules();
                    break;
                case 2: // Bulk Operations
                    bulkDeleteSchedules();
                    break;
                default:
                    console.log('Quick action clicked:', index);
            }
        });
    });
}

// Export global functions for window access
window.ScheduleManager = {
    loadSchedules: loadSchedules,
    addSchedule: addSchedule,
    clearForm: clearForm,
    updateStatistics: updateStatistics,
    showNotification: showNotification,
    refreshSchedules: refreshSchedules,
    exportSchedules: exportSchedules,
    duplicateSchedule: duplicateSchedule,
    bulkDeleteSchedules: bulkDeleteSchedules,
    deleteSchedule: deleteSchedule,
    editSchedule: editSchedule,
    viewSchedule: viewSchedule
};